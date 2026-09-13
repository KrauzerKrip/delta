/// <summary>
/// Provides an inertial, screen-plane control prototype for the electrical repair pliers.
/// </summary>
public sealed class PliersMinigameController : Component
{
	private const string InsertFuseLine = "I need to insert the fuse into the slot";
	private const string EngageButtonLine = "I need to push the engagement button and we are done";
	private const string FinalCheckLine = "Nice! Final check...";
	private const string RetryButtonLine = "It happens. Just push it again.";

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
	public Vignette BreathVignette { get; set; }

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
	public float TremorFrequency { get; set; } = 1.2f;

	[Property, Group( "Breathing" ), Range( 1f, 30f )]
	public float MaximumBreathHoldDuration { get; set; } = 7f;

	[Property, Group( "Breathing" ), Range( 0f, 1f )]
	public float HeldBreathTremorMultiplier { get; set; } = 0.12f;

	[Property, Group( "Breathing" ), Range( 0.1f, 5f )]
	public float BreathRecoveryDuration { get; set; } = 1.4f;

	[Property, Group( "Breathing" ), Range( 0f, 2f )]
	public float MinimumBreathRecoveryDuration { get; set; } = 0.25f;

	[Property, Group( "Breathing" ), Range( 1f, 12f )]
	public float BreathRecoveryTremorMultiplier { get; set; } = 3f;

	[Property, Group( "Breathing" ), Range( 1f, 5f )]
	public float BreathRecoveryFrequencyMultiplier { get; set; } = 1.35f;

	[Property, Group( "Breathing" ), Range( 0.05f, 3f )]
	public float VignetteFadeOutDuration { get; set; } = 0.6f;

	[Property, Group( "Breathing" ), Range( 0f, 1f )]
	public float MaximumVignetteIntensity { get; set; } = 1f;

	[Property, Group( "Danger Return" ), Range( 0f, 32f )]
	public float ReturnObstacleClearance { get; set; } = 6f;

	[Property, Group( "Danger Return" ), Range( 0.01f, 4f )]
	public float ReturnArrivalDistance { get; set; } = 0.25f;

	[Property, Group( "Danger Return" ), Range( 1f, 720f )]
	public float ReturnRotationSpeed { get; set; } = 240f;

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
	private float breathHoldTime;
	private float breathRecoveryTime;
	private float breathRecoveryTotalDuration;
	private float breathRecoverySeverity;
	private float dangerReactionTime;
	private float dangerReactionDuration;
	private float dangerTremorMultiplier = 1f;
	private float recoilControlLockTime;
	private readonly System.Collections.Generic.List<Vector3> dangerReturnWaypoints = new();
	private int dangerReturnWaypointIndex;
	private int dangerReturnRotationWaypointIndex;
	private float dangerReturnSpeed;
	private bool dangerReturnActive;
	private bool dangerReturnRotationComplete;
	private float vignetteIntensity;
	private bool isHoldingBreath;
	private bool waitForBreathKeyRelease;
	private bool poseInitialized;
	private bool testWasEnabled;
	private bool playerControlsSuppressed;
	private bool previousPlayerInputEnabled;
	private bool previousMarieInputEnabled;
	private MinigameDialogueHud dialogueHud;
	private MinigameDialogueState dialogueState = MinigameDialogueState.InsertFuse;

	public bool IsDialogueVisible => TestControlsEnabled;
	public string CurrentDialogueLine => dialogueState switch
	{
		MinigameDialogueState.EngageButton => EngageButtonLine,
		MinigameDialogueState.FinalCheck => FinalCheckLine,
		MinigameDialogueState.RetryButton => RetryButtonLine,
		_ => InsertFuseLine
	};

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
		SetVignetteIntensity( 0f );

		SynchronizeTestState();
	}

	protected override void OnFixedUpdate()
	{
		if ( GameObject.IsProxy || !poseInitialized )
			return;

		SynchronizeTestState();
		if ( !TestControlsEnabled )
			return;

		UpdateBreathing( Time.Delta );
		UpdateDangerReaction( Time.Delta );
		UpdateRotation( Time.Delta );
		UpdateMovement( Time.Delta );
		UpdateWobble( Time.Delta );
		ApplyPose();
	}

	protected override void OnDisabled()
	{
		ResetBreathing();
		RestorePlayerControls();
		dialogueState = MinigameDialogueState.InsertFuse;
		testWasEnabled = false;
	}

	/// <summary>Advances Marie's instruction after the fuse reaches its slot.</summary>
	public void NotifyFusePlaced()
	{
		if ( dialogueState == MinigameDialogueState.InsertFuse )
			dialogueState = MinigameDialogueState.EngageButton;
	}

	/// <summary>Advances Marie's instruction while the engagement button is held.</summary>
	public void NotifyButtonEngaged()
	{
		dialogueState = MinigameDialogueState.FinalCheck;
	}

	/// <summary>Asks the player to retry after the engagement button releases.</summary>
	public void NotifyButtonBlownOff()
	{
		dialogueState = MinigameDialogueState.RetryButton;
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
			EnsureDialogueHud();
			SetupCamera();
			return;
		}

		RestorePlayerControls();
		ReleaseCamera();
		ResetBreathing();
	}

	private void EnsureDialogueHud()
	{
		if ( dialogueHud is not null || PlayerController is null || PlayerController.GameObject.IsProxy )
			return;

		var inventory = PlayerController.Components.Get<PlayerInventory>()
			?? PlayerController.GameObject.AddComponent<PlayerInventory>();
		var hudObject = inventory.EnsureHudObject();
		dialogueHud = hudObject.Components.Get<MinigameDialogueHud>()
			?? hudObject.AddComponent<MinigameDialogueHud>();
		dialogueHud.Minigame = this;
	}

	private void UpdateBreathing( float deltaTime )
	{
		var breathKeyDown = Input.Down( "Run" );
		if ( waitForBreathKeyRelease && !breathKeyDown )
			waitForBreathKeyRelease = false;

		if ( breathRecoveryTime > 0f )
		{
			breathRecoveryTime = System.MathF.Max( breathRecoveryTime - deltaTime, 0f );
			FadeOutVignette( deltaTime );
			return;
		}

		if ( breathKeyDown && !waitForBreathKeyRelease )
		{
			isHoldingBreath = true;
			var maximumDuration = System.MathF.Max( MaximumBreathHoldDuration, 0.01f );
			breathHoldTime = System.MathF.Min( breathHoldTime + deltaTime, maximumDuration );
			var progress = (breathHoldTime / maximumDuration).Clamp( 0f, 1f );
			var easedProgress = progress * progress * (3f - 2f * progress);
			SetVignetteIntensity( easedProgress * MaximumVignetteIntensity );

			if ( breathHoldTime >= maximumDuration )
				BeginBreathRecovery( requireKeyRelease: true );

			return;
		}

		if ( isHoldingBreath )
			BeginBreathRecovery( requireKeyRelease: false );

		FadeOutVignette( deltaTime );
	}

	private void BeginBreathRecovery( bool requireKeyRelease )
	{
		var maximumHoldDuration = System.MathF.Max( MaximumBreathHoldDuration, 0.01f );
		breathRecoverySeverity = (breathHoldTime / maximumHoldDuration).Clamp( 0f, 1f );
		var maximumRecoveryDuration = System.MathF.Max( BreathRecoveryDuration, 0f );
		var minimumRecoveryDuration = MinimumBreathRecoveryDuration.Clamp(
			0f,
			maximumRecoveryDuration
		);
		breathRecoveryTotalDuration = minimumRecoveryDuration
			+ (maximumRecoveryDuration - minimumRecoveryDuration) * breathRecoverySeverity;

		isHoldingBreath = false;
		breathHoldTime = 0f;
		breathRecoveryTime = breathRecoveryTotalDuration;
		waitForBreathKeyRelease = requireKeyRelease;
	}

	private void FadeOutVignette( float deltaTime )
	{
		var fadeDuration = System.MathF.Max( VignetteFadeOutDuration, 0.01f );
		var fadeSpeed = System.MathF.Max( MaximumVignetteIntensity, 0f ) / fadeDuration;
		SetVignetteIntensity( System.MathF.Max( vignetteIntensity - fadeSpeed * deltaTime, 0f ) );
	}

	/// <summary>Returns true when a collider belongs to the controlled pliers hierarchy.</summary>
	public bool IsPliersCollider( Collider collider )
	{
		if ( collider is null || Pliers is null )
			return false;

		for ( var current = collider.GameObject; current is not null; current = current.Parent )
		{
			if ( current == Pliers )
				return true;
		}

		return false;
	}

	/// <summary>Immediately jerks the pliers away from a dangerous world-space contact.</summary>
	public void RecoilFromDanger(
		Vector3 dangerPosition,
		float recoilDistance,
		float recoilSpeed,
		float controlLockDuration,
		float tremorDuration,
		float tremorMultiplier,
		Collider dangerCollider = null )
	{
		if ( !TestControlsEnabled || !poseInitialized )
			return;

		var currentPosition = authoredPosition
			+ horizontalAxis * logicalOffset.x
			+ verticalAxis * logicalOffset.y;
		var away = currentPosition - dangerPosition;
		var planarAway = new Vector3(
			Vector3.Dot( away, horizontalAxis ),
			Vector3.Dot( away, verticalAxis ),
			0f
		);

		if ( planarAway.LengthSquared <= 0.0001f )
		{
			var facing = Rotation.FromAxis( planeNormal, rotationAngle ) * horizontalAxis;
			planarAway = new Vector3(
				-Vector3.Dot( facing, horizontalAxis ),
				-Vector3.Dot( facing, verticalAxis ),
				0f
			);
		}

		planarAway = planarAway.Normal;
		logicalOffset += planarAway * System.MathF.Max( recoilDistance, 0f );
		movementVelocity = planarAway * System.MathF.Max( recoilSpeed, 0f );
		ClampToMovementBounds();
		dangerReturnSpeed = System.MathF.Max( recoilSpeed, 1f );
		BuildDangerReturnPath( dangerCollider, System.MathF.Max( ReturnObstacleClearance, 0f ) );
		rotationAngle = NormalizeAngle( rotationAngle );
		dangerReturnRotationComplete = System.MathF.Abs( rotationAngle ) < 0.05f;
		dangerReturnActive = true;

		recoilControlLockTime = System.MathF.Max(
			recoilControlLockTime,
			System.MathF.Max( controlLockDuration, 0f )
		);
		dangerReactionDuration = System.MathF.Max( tremorDuration, 0f );
		dangerReactionTime = dangerReactionDuration;
		dangerTremorMultiplier = System.MathF.Max( tremorMultiplier, 1f );
	}

	private void UpdateDangerReaction( float deltaTime )
	{
		dangerReactionTime = System.MathF.Max( dangerReactionTime - deltaTime, 0f );
		recoilControlLockTime = System.MathF.Max( recoilControlLockTime - deltaTime, 0f );
	}

	private void UpdateMovement( float deltaTime )
	{
		if ( dangerReturnActive )
		{
			UpdateDangerReturnMovement( deltaTime );
			return;
		}

		var acceptInput = recoilControlLockTime <= 0f;
		var horizontalInput = acceptInput ? GetInputAxis( "Right", "Left" ) : 0f;
		var verticalInput = acceptInput ? GetInputAxis( "Forward", "Backward" ) : 0f;
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
		if ( dangerReturnActive )
		{
			rotationVelocity = 0f;
			if ( dangerReturnRotationComplete
				|| dangerReturnWaypointIndex < dangerReturnRotationWaypointIndex )
				return;

			rotationAngle = NormalizeAngle( rotationAngle );
			var rotationStep = System.MathF.Max( ReturnRotationSpeed, 1f ) * deltaTime;
			if ( System.MathF.Abs( rotationAngle ) <= rotationStep )
			{
				rotationAngle = 0f;
				dangerReturnRotationComplete = true;
			}
			else
				rotationAngle -= System.MathF.Sign( rotationAngle ) * rotationStep;
			return;
		}

		var rotationInput = recoilControlLockTime <= 0f
			? GetInputAxis( "RotatePliersClockwise", "RotatePliersCounterClockwise" )
			: 0f;

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
		tremorTime += deltaTime * GetBreathFrequencyScale();
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

	private void UpdateDangerReturnMovement( float deltaTime )
	{
		if ( !dangerReturnRotationComplete
			&& dangerReturnWaypointIndex >= dangerReturnRotationWaypointIndex )
		{
			movementVelocity = Vector3.Zero;
			return;
		}

		if ( dangerReturnWaypointIndex >= dangerReturnWaypoints.Count )
		{
			CompleteDangerReturn();
			return;
		}

		var target = dangerReturnWaypoints[dangerReturnWaypointIndex];
		var toTarget = target - logicalOffset;
		var arrivalDistance = System.MathF.Max( ReturnArrivalDistance, 0.01f );
		var stepDistance = dangerReturnSpeed * deltaTime;

		if ( toTarget.Length <= System.MathF.Max( arrivalDistance, stepDistance ) )
		{
			logicalOffset = target;
			dangerReturnWaypointIndex++;
			if ( !dangerReturnRotationComplete
				&& dangerReturnWaypointIndex >= dangerReturnRotationWaypointIndex )
			{
				movementVelocity = Vector3.Zero;
				return;
			}

			if ( dangerReturnWaypointIndex >= dangerReturnWaypoints.Count )
			{
				CompleteDangerReturn();
				return;
			}

			toTarget = dangerReturnWaypoints[dangerReturnWaypointIndex] - logicalOffset;
		}

		movementVelocity = toTarget.LengthSquared > 0.0001f
			? toTarget.Normal * dangerReturnSpeed
			: Vector3.Zero;
		logicalOffset += movementVelocity * deltaTime;
		ClampToMovementBounds();
	}

	private void CompleteDangerReturn()
	{
		dangerReturnActive = false;
		dangerReturnWaypoints.Clear();
		dangerReturnWaypointIndex = 0;
		dangerReturnRotationWaypointIndex = 0;
		dangerReturnRotationComplete = true;
		logicalOffset = Vector3.Zero;
		movementVelocity = Vector3.Zero;
		rotationAngle = 0f;
		rotationVelocity = 0f;
		leanAngle = 0f;
		leanVelocity = 0f;
	}

	private void BuildDangerReturnPath( Collider dangerCollider, float clearance )
	{
		dangerReturnWaypoints.Clear();
		dangerReturnWaypointIndex = 0;
		dangerReturnRotationWaypointIndex = 0;

		var target = Vector3.Zero;
		if ( dangerCollider is null )
		{
			dangerReturnWaypoints.Add( target );
			return;
		}

		var bounds = dangerCollider.GetWorldBounds();
		var minimumHorizontal = float.MaxValue;
		var maximumHorizontal = float.MinValue;
		var minimumVertical = float.MaxValue;
		var maximumVertical = float.MinValue;

		foreach ( var corner in bounds.Corners )
		{
			var fromStart = corner - authoredPosition;
			var horizontal = Vector3.Dot( fromStart, horizontalAxis );
			var vertical = Vector3.Dot( fromStart, verticalAxis );
			minimumHorizontal = System.MathF.Min( minimumHorizontal, horizontal );
			maximumHorizontal = System.MathF.Max( maximumHorizontal, horizontal );
			minimumVertical = System.MathF.Min( minimumVertical, vertical );
			maximumVertical = System.MathF.Max( maximumVertical, vertical );
		}

		var totalClearance = clearance + GetPliersSweepRadius();
		minimumHorizontal -= totalClearance;
		maximumHorizontal += totalClearance;
		minimumVertical -= totalClearance;
		maximumVertical += totalClearance;

		var routeStart = logicalOffset;
		if ( PointIsInsideRectangle(
			routeStart,
			minimumHorizontal,
			maximumHorizontal,
			minimumVertical,
			maximumVertical ) )
		{
			const float escapePadding = 0.1f;
			var escapeOrigin = routeStart;
			var escapePoints = new[]
			{
				new Vector3( minimumHorizontal - escapePadding, routeStart.y.Clamp( minimumVertical, maximumVertical ), 0f ),
				new Vector3( maximumHorizontal + escapePadding, routeStart.y.Clamp( minimumVertical, maximumVertical ), 0f ),
				new Vector3( routeStart.x.Clamp( minimumHorizontal, maximumHorizontal ), minimumVertical - escapePadding, 0f ),
				new Vector3( routeStart.x.Clamp( minimumHorizontal, maximumHorizontal ), maximumVertical + escapePadding, 0f )
			};
			var closestEscapeDistance = float.MaxValue;
			foreach ( var escapePoint in escapePoints )
			{
				if ( !IsInsideMovementBounds( escapePoint ) )
					continue;

				var escapeDistance = (escapePoint - escapeOrigin).Length;
				if ( escapeDistance >= closestEscapeDistance )
					continue;

				closestEscapeDistance = escapeDistance;
				routeStart = escapePoint;
			}

			if ( closestEscapeDistance < float.MaxValue )
			{
				dangerReturnWaypoints.Add( routeStart );
				dangerReturnRotationWaypointIndex = 1;
			}
		}

		if ( !SegmentCrossesRectangle(
			routeStart,
			target,
			minimumHorizontal,
			maximumHorizontal,
			minimumVertical,
			maximumVertical ) )
		{
			dangerReturnWaypoints.Add( target );
			return;
		}

		var corners = new[]
		{
			new Vector3( minimumHorizontal, minimumVertical, 0f ),
			new Vector3( maximumHorizontal, minimumVertical, 0f ),
			new Vector3( maximumHorizontal, maximumVertical, 0f ),
			new Vector3( minimumHorizontal, maximumVertical, 0f )
		};
		var bestDistance = float.MaxValue;
		var bestFirst = -1;
		var bestSecond = -1;

		for ( var first = 0; first < corners.Length; first++ )
		{
			if ( !IsInsideMovementBounds( corners[first] )
				|| SegmentCrossesRectangle( routeStart, corners[first], minimumHorizontal, maximumHorizontal, minimumVertical, maximumVertical ) )
				continue;

			if ( !SegmentCrossesRectangle( corners[first], target, minimumHorizontal, maximumHorizontal, minimumVertical, maximumVertical ) )
			{
				var distance = (corners[first] - routeStart).Length + (target - corners[first]).Length;
				if ( distance < bestDistance )
				{
					bestDistance = distance;
					bestFirst = first;
					bestSecond = -1;
				}
			}

			for ( var second = 0; second < corners.Length; second++ )
			{
				if ( second == first || !IsInsideMovementBounds( corners[second] ) )
					continue;
				if ( SegmentCrossesRectangle( corners[first], corners[second], minimumHorizontal, maximumHorizontal, minimumVertical, maximumVertical )
					|| SegmentCrossesRectangle( corners[second], target, minimumHorizontal, maximumHorizontal, minimumVertical, maximumVertical ) )
					continue;

				var distance = (corners[first] - routeStart).Length
					+ (corners[second] - corners[first]).Length
					+ (target - corners[second]).Length;
				if ( distance < bestDistance )
				{
					bestDistance = distance;
					bestFirst = first;
					bestSecond = second;
				}
			}
		}

		if ( bestFirst >= 0 )
		{
			dangerReturnWaypoints.Add( corners[bestFirst] );
			if ( bestSecond >= 0 )
				dangerReturnWaypoints.Add( corners[bestSecond] );
		}

		// If no route fits inside the movement bounds, returning home still takes priority.
		dangerReturnWaypoints.Add( target );
		// Hold the current angle until the first clearance point, then rotate before continuing.
		if ( dangerReturnRotationWaypointIndex == 0 )
			dangerReturnRotationWaypointIndex = 1;
	}

	private float GetPliersSweepRadius()
	{
		if ( Pliers is null )
			return 0f;

		var radius = 0f;
		foreach ( var collider in Pliers.GetComponentsInChildren<Collider>( includeDisabled: false, includeSelf: true ) )
		{
			foreach ( var corner in collider.GetWorldBounds().Corners )
			{
				var fromPivot = corner - Pliers.WorldPosition;
				var horizontal = Vector3.Dot( fromPivot, horizontalAxis );
				var vertical = Vector3.Dot( fromPivot, verticalAxis );
				var planarDistance = System.MathF.Sqrt(
					horizontal * horizontal + vertical * vertical
				);
				radius = System.MathF.Max( radius, planarDistance );
			}
		}

		return radius;
	}

	private bool IsInsideMovementBounds( Vector3 point )
	{
		if ( MovementLowerLeft is null || MovementUpperRight is null )
			return true;

		var lowerOffset = MovementLowerLeft.WorldPosition - authoredPosition;
		var upperOffset = MovementUpperRight.WorldPosition - authoredPosition;
		var minimumHorizontal = System.MathF.Min(
			Vector3.Dot( lowerOffset, horizontalAxis ),
			Vector3.Dot( upperOffset, horizontalAxis )
		);
		var maximumHorizontal = System.MathF.Max(
			Vector3.Dot( lowerOffset, horizontalAxis ),
			Vector3.Dot( upperOffset, horizontalAxis )
		);
		var minimumVertical = System.MathF.Min(
			Vector3.Dot( lowerOffset, verticalAxis ),
			Vector3.Dot( upperOffset, verticalAxis )
		);
		var maximumVertical = System.MathF.Max(
			Vector3.Dot( lowerOffset, verticalAxis ),
			Vector3.Dot( upperOffset, verticalAxis )
		);

		return point.x >= minimumHorizontal && point.x <= maximumHorizontal
			&& point.y >= minimumVertical && point.y <= maximumVertical;
	}

	private static bool SegmentCrossesRectangle(
		Vector3 start,
		Vector3 end,
		float minimumHorizontal,
		float maximumHorizontal,
		float minimumVertical,
		float maximumVertical )
	{
		const float edgeEpsilon = 0.01f;
		minimumHorizontal += edgeEpsilon;
		maximumHorizontal -= edgeEpsilon;
		minimumVertical += edgeEpsilon;
		maximumVertical -= edgeEpsilon;

		var minimumTime = 0f;
		var maximumTime = 1f;
		var delta = end - start;
		if ( !ClipSegmentAxis( start.x, delta.x, minimumHorizontal, maximumHorizontal, ref minimumTime, ref maximumTime )
			|| !ClipSegmentAxis( start.y, delta.y, minimumVertical, maximumVertical, ref minimumTime, ref maximumTime ) )
			return false;

		return maximumTime > edgeEpsilon && minimumTime < 1f - edgeEpsilon;
	}

	private static bool PointIsInsideRectangle(
		Vector3 point,
		float minimumHorizontal,
		float maximumHorizontal,
		float minimumVertical,
		float maximumVertical )
	{
		return point.x > minimumHorizontal && point.x < maximumHorizontal
			&& point.y > minimumVertical && point.y < maximumVertical;
	}

	private static bool ClipSegmentAxis(
		float start,
		float delta,
		float minimum,
		float maximum,
		ref float minimumTime,
		ref float maximumTime )
	{
		if ( System.MathF.Abs( delta ) <= 0.0001f )
			return start >= minimum && start <= maximum;

		var first = (minimum - start) / delta;
		var second = (maximum - start) / delta;
		if ( first > second )
			(first, second) = (second, first);

		minimumTime = System.MathF.Max( minimumTime, first );
		maximumTime = System.MathF.Min( maximumTime, second );
		return minimumTime <= maximumTime;
	}

	private void ApplyPose()
	{
		var breathTremorScale = GetBreathTremorScale();
		var dangerTremorScale = dangerReactionTime > 0f
			? GetDangerTremorScale()
			: 0f;
		var tremorScale = System.MathF.Max( breathTremorScale, dangerTremorScale );
		var angularFrequency = TremorFrequency * System.MathF.PI * 2f;
		var horizontalTremor = System.MathF.Sin( tremorTime * angularFrequency );
		var verticalTremor = System.MathF.Sin( tremorTime * angularFrequency * 1.37f + 1.1f );
		var rotationalTremor = System.MathF.Sin( tremorTime * angularFrequency * 0.83f + 2.4f );

		var movementPosition = authoredPosition
			+ horizontalAxis * logicalOffset.x
			+ verticalAxis * logicalOffset.y;
		var tremorPosition = (horizontalAxis * horizontalTremor + verticalAxis * verticalTremor)
			* TremorPositionAmplitude
			* tremorScale;
		var wobbleAngle = leanAngle
			+ rotationalTremor * TremorRotationAmplitude * tremorScale;
		var maximumWobble = MaximumLeanDegrees
			+ TremorRotationAmplitude * System.MathF.Max( tremorScale, 1f );
		wobbleAngle = wobbleAngle.Clamp( -maximumWobble, maximumWobble );
		var totalRotation = rotationAngle + wobbleAngle;

		Pliers.WorldPosition = movementPosition + tremorPosition;
		Pliers.WorldRotation = Rotation.FromAxis( planeNormal, totalRotation ) * authoredRotation;
	}

	private void ResetPliers()
	{
		dangerReturnActive = false;
		dangerReturnWaypoints.Clear();
		dangerReturnWaypointIndex = 0;
		dangerReturnRotationWaypointIndex = 0;
		dangerReturnRotationComplete = true;
		logicalOffset = Vector3.Zero;
		movementVelocity = Vector3.Zero;
		rotationAngle = 0f;
		rotationVelocity = 0f;
		leanAngle = 0f;
		leanVelocity = 0f;
		tremorTime = 0f;
		ResetBreathing();
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

	private float GetBreathTremorScale()
	{
		if ( isHoldingBreath )
			return HeldBreathTremorMultiplier.Clamp( 0f, 1f );

		if ( breathRecoveryTime <= 0f )
			return 1f;

		var duration = System.MathF.Max( breathRecoveryTotalDuration, 0.01f );
		var recoveryStrength = (breathRecoveryTime / duration).Clamp( 0f, 1f );
		var peakMultiplier = 1f
			+ (System.MathF.Max( BreathRecoveryTremorMultiplier, 1f ) - 1f)
			* breathRecoverySeverity;
		return 1f + (peakMultiplier - 1f)
			* recoveryStrength * recoveryStrength;
	}

	private float GetBreathFrequencyScale()
	{
		if ( breathRecoveryTime <= 0f )
			return 1f;

		var duration = System.MathF.Max( breathRecoveryTotalDuration, 0.01f );
		var recoveryStrength = (breathRecoveryTime / duration).Clamp( 0f, 1f );
		var peakMultiplier = 1f
			+ (System.MathF.Max( BreathRecoveryFrequencyMultiplier, 1f ) - 1f)
			* breathRecoverySeverity;
		return 1f + (peakMultiplier - 1f)
			* recoveryStrength;
	}

	private void ResetBreathing()
	{
		breathHoldTime = 0f;
		breathRecoveryTime = 0f;
		breathRecoveryTotalDuration = 0f;
		breathRecoverySeverity = 0f;
		dangerReactionTime = 0f;
		dangerReactionDuration = 0f;
		dangerTremorMultiplier = 1f;
		recoilControlLockTime = 0f;
		isHoldingBreath = false;
		waitForBreathKeyRelease = false;
		SetVignetteIntensity( 0f );
	}

	private void SetVignetteIntensity( float intensity )
	{
		vignetteIntensity = intensity.Clamp( 0f, 1f );
		if ( BreathVignette is not null )
			BreathVignette.Intensity = vignetteIntensity;
	}

	private float GetDangerTremorScale()
	{
		if ( dangerReactionTime <= 0f )
			return 1f;

		var duration = System.MathF.Max( dangerReactionDuration, 0.01f );
		var strength = (dangerReactionTime / duration).Clamp( 0f, 1f );
		return 1f + (dangerTremorMultiplier - 1f) * strength * strength;
	}

	private static float GetInputAxis( string positiveAction, string negativeAction )
	{
		return (Input.Down( positiveAction ) ? 1f : 0f)
			- (Input.Down( negativeAction ) ? 1f : 0f);
	}

	private static float NormalizeAngle( float angle )
	{
		angle %= 360f;
		if ( angle > 180f )
			angle -= 360f;
		else if ( angle < -180f )
			angle += 360f;

		return angle;
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

	private enum MinigameDialogueState
	{
		InsertFuse,
		EngageButton,
		FinalCheck,
		RetryButton
	}
}
