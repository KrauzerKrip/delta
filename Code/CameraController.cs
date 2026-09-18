/// <summary>
/// Drives the main camera from the highest-priority <see cref="CameraZone"/>
/// currently occupied by the local player.
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

	[Property, Group( "Following" )]
	public Vector3 DeadZoneSize { get; set; } = new Vector3( 0f, 128f, 96f );

	private readonly Dictionary<CameraZone, long> activeZones = new();
	private long enterSequence;
	private CameraZone currentZone;
	private Vector3 zoneFocusPosition;
	private bool focusInitialized;
	private Vector3 lastTargetPosition;
	private Rotation lastTargetRotation;
	private float lastTargetFieldOfView;
	private bool hasRenderedTarget;

	/// <summary>The zone currently controlling the camera, or null for the default shot.</summary>
	public CameraZone CurrentZone => currentZone;

	protected override void OnPreRender()
	{
		if ( GameObject.IsProxy || Scene.Camera is null )
			return;

		var selectedZone = SelectZone();
		if ( !focusInitialized || selectedZone != currentZone )
		{
			currentZone = selectedZone;
			zoneFocusPosition = currentZone is not null
				? currentZone.WorldPosition
				: Controller.WorldPosition;
			focusInitialized = true;
		}

		Vector3 targetPosition;
		Rotation targetRotation;
		float targetFieldOfView;
		float blendSpeed;

		if ( currentZone is not null )
		{
			var deadZoneSize = currentZone.OverrideDeadZone
				? currentZone.DeadZoneSize
				: DeadZoneSize;

			UpdateFocusPosition( Controller.WorldPosition, deadZoneSize );
			targetPosition = currentZone.GetCameraPosition( zoneFocusPosition );
			targetRotation = currentZone.CameraAnchor.WorldRotation;
			targetFieldOfView = currentZone.FieldOfView;
			blendSpeed = currentZone.BlendSpeed;
		}
		else
		{
			if ( DefaultCameraAnchor is null )
				UpdateFocusPosition( Controller.WorldPosition, DeadZoneSize );

			targetPosition = DefaultCameraAnchor is not null
				? DefaultCameraAnchor.WorldPosition
				: zoneFocusPosition + DefaultCameraOffset;
			targetRotation = DefaultCameraAnchor is not null
				? DefaultCameraAnchor.WorldRotation
				: DefaultCameraAngles.ToRotation();
			targetFieldOfView = DefaultFieldOfView;
			blendSpeed = DefaultBlendSpeed;
		}

		lastTargetPosition = targetPosition;
		lastTargetRotation = targetRotation;
		lastTargetFieldOfView = targetFieldOfView;
		hasRenderedTarget = true;

		var blend = 1f - System.MathF.Exp( -System.MathF.Max( blendSpeed, 0.01f ) * Time.Delta );
		Scene.Camera.WorldPosition = Vector3.Lerp( Scene.Camera.WorldPosition, targetPosition, blend );
		Scene.Camera.WorldRotation = Rotation.Slerp( Scene.Camera.WorldRotation, targetRotation, blend );
		Scene.Camera.FieldOfView = MathX.Lerp( Scene.Camera.FieldOfView, targetFieldOfView, blend );
	}

	/// <summary>Whether the authored close shot has reached its fixed axes, rotation and FOV.</summary>
	public bool IsZoneSettled( CameraZone zone, float positionTolerance = 8f )
	{
		if ( !hasRenderedTarget || !zone.IsValid() || currentZone != zone || SelectZone() != zone || Scene.Camera is null )
			return false;
		var error = Scene.Camera.WorldPosition - lastTargetPosition;
		// Following axes may continually move while the player walks. Their ordinary
		// tracking lag must not prevent arrival at the fixed tunnel shot.
		return TunnelCameraTransferState.IsShotSettled( error.x, error.y, error.z,
			zone.FollowPlayerX, zone.FollowPlayerY, zone.FollowPlayerZ,
			Scene.Camera.WorldRotation.Distance( lastTargetRotation ),
			Scene.Camera.FieldOfView - lastTargetFieldOfView, positionTolerance );
	}

	private void UpdateFocusPosition( Vector3 playerPosition, Vector3 deadZoneSize )
	{
		zoneFocusPosition.x = AdvanceFocus( zoneFocusPosition.x, playerPosition.x, deadZoneSize.x );
		zoneFocusPosition.y = AdvanceFocus( zoneFocusPosition.y, playerPosition.y, deadZoneSize.y );
		zoneFocusPosition.z = AdvanceFocus( zoneFocusPosition.z, playerPosition.z, deadZoneSize.z );
	}

	private static float AdvanceFocus( float focus, float player, float deadZoneSize )
	{
		if ( deadZoneSize <= 0f )
			return player;

		var halfSize = deadZoneSize * 0.5f;
		var distance = player - focus;

		if ( distance > halfSize )
			return player - halfSize;

		if ( distance < -halfSize )
			return player + halfSize;

		return focus;
	}

	/// <summary>Translates the current shot and hands it to the destination tunnel.</summary>
	public void TransferToTunnel( Vector3 translation, CameraZone destinationZone )
	{
		if ( GameObject.IsProxy )
			return;

		if ( Scene.Camera is not null )
		{
			Scene.Camera.WorldPosition += translation;
			// Preserve the shifted pose on the next render instead of interpolating
			// back toward the pre-teleport camera and replaying the approach.
			Scene.Camera.Transform.ClearInterpolation();
		}
		zoneFocusPosition = focusInitialized ? zoneFocusPosition + translation : Controller.WorldPosition;
		focusInitialized = true;
		lastTargetPosition += translation;
		activeZones.Clear();
		foreach ( var zone in Scene.GetAllComponents<CameraZone>().ToArray() )
			zone.ReconcileAfterTransfer( this );

		// Enter last to win equal-priority overlaps without resetting the translated focus.
		EnterZone( destinationZone );
		currentZone = destinationZone;
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
			if ( !zone.IsValid() || !zone.Active || !zone.CameraAnchor.IsValid() || !zone.CameraAnchor.Active )
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



