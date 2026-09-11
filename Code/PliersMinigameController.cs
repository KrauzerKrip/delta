/// <summary>
/// Provides an inertial, screen-plane control prototype for the electrical repair pliers.
/// </summary>
public sealed class PliersMinigameController : Component
{
	[Property, Group( "Setup" )]
	public bool TestControlsEnabled { get; set; }

	[Property, Group( "Setup" )]
	public GameObject Pliers { get; set; }

	[Property, Group( "Setup" )]
	public GameObject MovementLowerLeft { get; set; }

	[Property, Group( "Setup" )]
	public GameObject MovementUpperRight { get; set; }

	[Property, Group( "Setup" )]
	public GameObject CameraAnchor { get; set; }

	[Property, Group( "Setup" )]
	public PlayerController PlayerController { get; set; }

	[Property, Group( "Setup" )]
	public MarieMovementController MarieMovementController { get; set; }

	[Property, Group( "Movement" ), Range( 1f, 500f )]
	public float MaximumSpeed { get; set; } = 110f;

	[Property, Group( "Movement" ), Range( 1f, 2000f )]
	public float Acceleration { get; set; } = 350f;

	[Property, Group( "Movement" ), Range( 0f, 20f )]
	public float ReleaseDamping { get; set; } = 3.5f;

	[Property, Group( "Rotation" ), Range( 1f, 720f )]
	public float MaximumRotationSpeed { get; set; } = 90f;

	[Property, Group( "Rotation" ), Range( 1f, 2000f )]
	public float RotationAcceleration { get; set; } = 360f;

	[Property, Group( "Rotation" ), Range( 0f, 20f )]
	public float RotationReleaseDamping { get; set; } = 4f;

	[Property, Group( "Wobble" ), Range( 0f, 20f )]
	public float MaximumLeanDegrees { get; set; } = 6f;

	[Property, Group( "Wobble" ), Range( 0f, 100f )]
	public float LeanSpring { get; set; } = 32f;

	[Property, Group( "Wobble" ), Range( 0f, 30f )]
	public float LeanDamping { get; set; } = 6f;

	[Property, Group( "Tremor" ), Range( 0f, 5f )]
	public float TremorPositionAmplitude { get; set; } = 0.75f;

	[Property, Group( "Tremor" ), Range( 0f, 5f )]
	public float TremorRotationAmplitude { get; set; } = 0.8f;

	[Property, Group( "Tremor" ), Range( 0f, 10f )]
	public float TremorFrequency { get; set; } = 1.7f;

	private Vector3 authoredPosition;
	private Rotation authoredRotation;
	private Vector3 horizontalAxis;
	private Vector3 verticalAxis;
	private Vector3 planeNormal;
	private Vector3 logicalOffset;
	private Vector3 movementVelocity;
	private float rotationAngle;
	private float rotationVelocity;
	private float leanAngle;
	private float leanVelocity;
	private float tremorTime;
	private bool poseInitialized;
	private bool testWasEnabled;
	private bool playerControlsSuppressed;
	private bool previousPlayerInputEnabled;
	private bool previousMarieInputEnabled;

	protected override void OnStart()
	{
		if ( Pliers is null )
		{
			Log.Warning( $"[Pliers Minigame] '{GameObject.Name}' has no pliers object assigned." );
			return;
		}

		authoredPosition = Pliers.WorldPosition;
		authoredRotation = Pliers.WorldRotation;
		horizontalAxis = WorldRotation.Right;
		verticalAxis = WorldRotation.Up;
		planeNormal = WorldRotation.Forward;
		poseInitialized = true;

		SynchronizeTestState();
	}

	protected override void OnFixedUpdate()
	{
		if ( GameObject.IsProxy || !poseInitialized )
			return;

		SynchronizeTestState();
		if ( !TestControlsEnabled )
			return;

		UpdateRotation( Time.Delta );
		UpdateMovement( Time.Delta );
		UpdateWobble( Time.Delta );
		ApplyPose();
	}

	protected override void OnDisabled()
	{
		RestorePlayerControls();
		testWasEnabled = false;
	}

	private void SynchronizeTestState()
	{
		if ( TestControlsEnabled == testWasEnabled )
			return;

		testWasEnabled = TestControlsEnabled;
		if ( TestControlsEnabled )
		{
			ResetPliers();
			SuppressPlayerControls();
			SetupCamera();
			return;
		}

		RestorePlayerControls();
		ReleaseCamera();
	}

	private void UpdateMovement( float deltaTime )
	{
		var horizontalInput = GetInputAxis( "Right", "Left" );
		var verticalInput = GetInputAxis( "Forward", "Backward" );
		var unrotatedDirection = horizontalAxis * horizontalInput + verticalAxis * verticalInput;
		var inputRotation = Rotation.FromAxis( planeNormal, rotationAngle );
		var rotatedDirection = inputRotation * unrotatedDirection;
		var input = new Vector3(
			Vector3.Dot( rotatedDirection, horizontalAxis ),
			Vector3.Dot( rotatedDirection, verticalAxis ),
			0f
		);

		if ( input.LengthSquared > 1f )
			input = input.Normal;

		if ( input.LengthSquared > 0f )
		{
			movementVelocity += input * Acceleration * deltaTime;
		}
		else
		{
			var damping = System.MathF.Exp( -System.MathF.Max( ReleaseDamping, 0f ) * deltaTime );
			movementVelocity *= damping;
		}

		var maximumSpeed = System.MathF.Max( MaximumSpeed, 0f );
		if ( maximumSpeed <= 0f )
		{
			movementVelocity = Vector3.Zero;
		}
		else if ( movementVelocity.LengthSquared > maximumSpeed * maximumSpeed )
		{
			movementVelocity = movementVelocity.Normal * maximumSpeed;
		}

		logicalOffset += movementVelocity * deltaTime;
		ClampToMovementBounds();
	}

	private void UpdateRotation( float deltaTime )
	{
		var rotationInput = GetInputAxis(
			"RotatePliersClockwise",
			"RotatePliersCounterClockwise"
		);

		if ( rotationInput != 0f )
		{
			rotationVelocity += rotationInput * RotationAcceleration * deltaTime;
		}
		else
		{
			var damping = System.MathF.Exp(
				-System.MathF.Max( RotationReleaseDamping, 0f ) * deltaTime
			);
			rotationVelocity *= damping;
		}

		var maximumSpeed = System.MathF.Max( MaximumRotationSpeed, 0f );
		rotationVelocity = rotationVelocity.Clamp( -maximumSpeed, maximumSpeed );
		rotationAngle += rotationVelocity * deltaTime;

		if ( System.MathF.Abs( rotationAngle ) >= 360f )
			rotationAngle %= 360f;
	}

	private void UpdateWobble( float deltaTime )
	{
		var safeMaximumSpeed = System.MathF.Max( MaximumSpeed, 1f );
		var velocityLean = -movementVelocity.y / safeMaximumSpeed;
		var sidewaysLag = -movementVelocity.x / safeMaximumSpeed * 0.35f;
		var targetLean = ((velocityLean * 0.75f) + sidewaysLag) * MaximumLeanDegrees;
		targetLean = targetLean.Clamp( -MaximumLeanDegrees, MaximumLeanDegrees );

		leanVelocity += (targetLean - leanAngle) * LeanSpring * deltaTime;
		leanVelocity *= System.MathF.Exp( -System.MathF.Max( LeanDamping, 0f ) * deltaTime );
		leanAngle += leanVelocity * deltaTime;
		leanAngle = leanAngle.Clamp( -MaximumLeanDegrees, MaximumLeanDegrees );
		tremorTime += deltaTime;
	}

	private void ClampToMovementBounds()
	{
		if ( MovementLowerLeft is null || MovementUpperRight is null )
			return;

		var lowerOffset = MovementLowerLeft.WorldPosition - authoredPosition;
		var upperOffset = MovementUpperRight.WorldPosition - authoredPosition;
		var lowerHorizontal = Vector3.Dot( lowerOffset, horizontalAxis );
		var upperHorizontal = Vector3.Dot( upperOffset, horizontalAxis );
		var lowerVertical = Vector3.Dot( lowerOffset, verticalAxis );
		var upperVertical = Vector3.Dot( upperOffset, verticalAxis );

		var minimumHorizontal = System.MathF.Min( lowerHorizontal, upperHorizontal );
		var maximumHorizontal = System.MathF.Max( lowerHorizontal, upperHorizontal );
		var minimumVertical = System.MathF.Min( lowerVertical, upperVertical );
		var maximumVertical = System.MathF.Max( lowerVertical, upperVertical );

		ClampAxis(
			logicalOffset.x,
			movementVelocity.x,
			minimumHorizontal,
			maximumHorizontal,
			out var horizontalPosition,
			out var horizontalVelocity
		);
		ClampAxis(
			logicalOffset.y,
			movementVelocity.y,
			minimumVertical,
			maximumVertical,
			out var verticalPosition,
			out var verticalVelocity
		);

		logicalOffset.x = horizontalPosition;
		logicalOffset.y = verticalPosition;
		movementVelocity.x = horizontalVelocity;
		movementVelocity.y = verticalVelocity;
	}

	private void ApplyPose()
	{
		var angularFrequency = TremorFrequency * System.MathF.PI * 2f;
		var horizontalTremor = System.MathF.Sin( tremorTime * angularFrequency );
		var verticalTremor = System.MathF.Sin( tremorTime * angularFrequency * 1.37f + 1.1f );
		var rotationalTremor = System.MathF.Sin( tremorTime * angularFrequency * 0.83f + 2.4f );

		var movementPosition = authoredPosition
			+ horizontalAxis * logicalOffset.x
			+ verticalAxis * logicalOffset.y;
		var tremorPosition = (horizontalAxis * horizontalTremor + verticalAxis * verticalTremor)
			* TremorPositionAmplitude;
		var wobbleAngle = (leanAngle + rotationalTremor * TremorRotationAmplitude)
			.Clamp( -MaximumLeanDegrees, MaximumLeanDegrees );
		var totalRotation = rotationAngle + wobbleAngle;

		Pliers.WorldPosition = movementPosition + tremorPosition;
		Pliers.WorldRotation = Rotation.FromAxis( planeNormal, totalRotation ) * authoredRotation;
	}

	private void ResetPliers()
	{
		logicalOffset = Vector3.Zero;
		movementVelocity = Vector3.Zero;
		rotationAngle = 0f;
		rotationVelocity = 0f;
		leanAngle = 0f;
		leanVelocity = 0f;
		tremorTime = 0f;
		Pliers.WorldPosition = authoredPosition;
		Pliers.WorldRotation = authoredRotation;
	}

	private void SetupCamera()
	{
		PlayerController.GetComponent<CameraController>( includeDisabled: true ).Enabled = false;
		Scene.Camera.WorldPosition = CameraAnchor.WorldPosition;
		Scene.Camera.WorldRotation = CameraAnchor.WorldRotation;
		Scene.Camera.Orthographic = true;
		Scene.Camera.OrthographicHeight = 128;
	}

	private void ReleaseCamera()
	{
		PlayerController.GetComponent<CameraController>( includeDisabled: true ).Enabled = true;
		Scene.Camera.Orthographic = false;
	}

	private void SuppressPlayerControls()
	{
		if ( playerControlsSuppressed )
			return;

		if ( PlayerController is not null )
		{
			previousPlayerInputEnabled = PlayerController.UseInputControls;
			PlayerController.UseInputControls = false;
			PlayerController.WishVelocity = Vector3.Zero;
		}

		if ( MarieMovementController is not null )
		{
			previousMarieInputEnabled = MarieMovementController.MovementInputEnabled;
			MarieMovementController.MovementInputEnabled = false;
		}

		playerControlsSuppressed = true;
	}

	private void RestorePlayerControls()
	{
		if ( !playerControlsSuppressed )
			return;

		if ( PlayerController is not null )
		{
			PlayerController.UseInputControls = previousPlayerInputEnabled;
			PlayerController.WishVelocity = Vector3.Zero;
		}

		if ( MarieMovementController is not null )
			MarieMovementController.MovementInputEnabled = previousMarieInputEnabled;

		playerControlsSuppressed = false;
	}

	private static float GetInputAxis( string positiveAction, string negativeAction )
	{
		return (Input.Down( positiveAction ) ? 1f : 0f)
			- (Input.Down( negativeAction ) ? 1f : 0f);
	}

	private static void ClampAxis(
		float position,
		float velocity,
		float minimum,
		float maximum,
		out float clampedPosition,
		out float clampedVelocity )
	{
		clampedPosition = position;
		clampedVelocity = velocity;

		if ( position < minimum )
		{
			clampedPosition = minimum;
			if ( velocity < 0f )
				clampedVelocity = 0f;
		}
		else if ( position > maximum )
		{
			clampedPosition = maximum;
			if ( velocity > 0f )
				clampedVelocity = 0f;
		}
	}
}
