/// <summary>
/// Moves the local player and drives the main camera from the highest-priority
/// <see cref="CameraZone"/> currently occupied by that player.
/// </summary>
public sealed class CameraController : Component
{
	[RequireComponent]
	public PlayerController Controller { get; set; }

	[Property, Group( "Default Shot" )]
	public GameObject DefaultCameraAnchor { get; set; }

	[Property, Group( "Default Shot" )]
	public Vector3 DefaultCameraOffset { get; set; } = Vector3.Backward * 1024f + Vector3.Up * 256f;

	[Property, Group( "Default Shot" )]
	public Angles DefaultCameraAngles { get; set; } = new Angles( 15f, 0f, 0f );

	[Property, Group( "Default Shot" ), Range( 1f, 179f )]
	public float DefaultFieldOfView { get; set; } = 60f;

	[Property, Group( "Default Shot" ), Range( 0.01f, 30f )]
	public float DefaultBlendSpeed { get; set; } = 5f;

	private readonly Dictionary<CameraZone, long> activeZones = new();
	private long enterSequence;
	private CameraZone currentZone;

	/// <summary>The zone currently controlling the camera, or null for the default shot.</summary>
	public CameraZone CurrentZone => currentZone;

	protected override void OnFixedUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		var speed = Input.Down( "Run" ) ? Controller.RunSpeed : Controller.WalkSpeed;
		Controller.WishVelocity = Input.AnalogMove * speed;

		if ( Controller.WishVelocity.LengthSquared > 0f )
		{
			var targetAngle = Controller.WishVelocity.EulerAngles;
			Controller.EyeAngles = Rotation.Slerp( Controller.EyeAngles, targetAngle, Time.Delta * 10f );
		}
	}

	protected override void OnPreRender()
	{
		if ( GameObject.IsProxy || Scene.Camera is null )
			return;

		currentZone = SelectZone();

		Vector3 targetPosition;
		Rotation targetRotation;
		float targetFieldOfView;
		float blendSpeed;

		if ( currentZone is not null )
		{
			targetPosition = currentZone.GetCameraPosition( Controller.WorldPosition );
			targetRotation = currentZone.CameraAnchor.WorldRotation;
			targetFieldOfView = currentZone.FieldOfView;
			blendSpeed = currentZone.BlendSpeed;
		}
		else
		{
			targetPosition = DefaultCameraAnchor is not null
				? DefaultCameraAnchor.WorldPosition
				: Controller.WorldPosition + DefaultCameraOffset;
			targetRotation = DefaultCameraAnchor is not null
				? DefaultCameraAnchor.WorldRotation
				: DefaultCameraAngles.ToRotation();
			targetFieldOfView = DefaultFieldOfView;
			blendSpeed = DefaultBlendSpeed;
		}

		var blend = 1f - System.MathF.Exp( -System.MathF.Max( blendSpeed, 0.01f ) * Time.Delta );
		Scene.Camera.WorldPosition = Vector3.Lerp( Scene.Camera.WorldPosition, targetPosition, blend );
		Scene.Camera.WorldRotation = Rotation.Slerp( Scene.Camera.WorldRotation, targetRotation, blend );
		Scene.Camera.FieldOfView = MathX.Lerp( Scene.Camera.FieldOfView, targetFieldOfView, blend );
	}

	internal void EnterZone( CameraZone zone )
	{
		if ( zone is null )
			return;

		activeZones[zone] = ++enterSequence;
	}

	internal void ExitZone( CameraZone zone )
	{
		if ( zone is null )
			return;

		activeZones.Remove( zone );
	}

	private CameraZone SelectZone()
	{
		CameraZone best = null;
		long bestSequence = long.MinValue;

		foreach ( var pair in activeZones.ToArray() )
		{
			var zone = pair.Key;
			if ( zone is null || !zone.Enabled || zone.CameraAnchor is null )
			{
				activeZones.Remove( zone );
				continue;
			}

			if ( best is null || zone.Priority > best.Priority ||
				(zone.Priority == best.Priority && pair.Value > bestSequence) )
			{
				best = zone;
				bestSequence = pair.Value;
			}
		}

		return best;
	}
}



