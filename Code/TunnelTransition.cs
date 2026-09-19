/// <summary>
/// Transfers a local player once the close tunnel camera shot settles.
/// Seam anchors are matching reference points used only to calculate translation.
/// </summary>
public sealed class TunnelTransition : Component
{
	[RequireComponent]
	public BoxCollider Trigger { get; set; }

	[Property]
	public TunnelTransition PairedTransition { get; set; }

	[Property]
	public GameObject SeamAnchor { get; set; }

	[Property, Range( 0.01f, 64f )]
	public float CameraPositionTolerance { get; set; } = 8f;

	[Property]
	public CameraZone CloseCameraZone { get; set; }

	private readonly Dictionary<PlayerController, TunnelCameraTransferState> arrivals = new();
	private readonly Dictionary<PlayerController, Vector3> arrivalDirections = new();
	private readonly Dictionary<PlayerController, Vector3> approachDirections = new();
	private string lastWarning;

	protected override void OnFixedUpdate()
	{
		if ( !ValidatePair() )
		{
			ClearAttempts();
			return;
		}

		foreach ( var pair in arrivals.ToArray() )
		{
			var player = pair.Key;
			var state = pair.Value;
			var playerValid = player.IsValid();
			var validPendingArea = TunnelCameraTransferState.ShouldKeepAttempt(
				playerValid, playerValid && player.GameObject.IsProxy,
				playerValid && Contains( player.WorldPosition ), state.IsArrivalBlocked,
				playerValid && CloseCameraZone.ContainsPosition( player.WorldPosition ) );
			if ( !validPendingArea )
			{
				arrivals.Remove( player );
				arrivalDirections.Remove( player );
				approachDirections.Remove( player );
			}
		}

		foreach ( var player in Scene.GetAllComponents<PlayerController>().ToArray() )
		{
			if ( !player.Active || player.GameObject.IsProxy )
				continue;
			var insideTrigger = Contains( player.WorldPosition );
			if ( !arrivals.TryGetValue( player, out var arrival ) )
			{
				if ( !insideTrigger )
					continue;
				arrivals[player] = arrival = new TunnelCameraTransferState();
			}
			var eligible = insideTrigger ||
				!arrival.IsArrivalBlocked;
			if ( !eligible )
				continue;
			var currentDirection = GetTravelDirection( player );
			if ( !arrivalDirections.ContainsKey( player ) && currentDirection.LengthSquared > 0f )
				approachDirections[player] = currentDirection;
			var director = player.Components.Get<CameraController>();
			if ( !director.IsValid() || !director.Active )
			{
				Warn( "The player requires an active CameraController." );
				continue;
			}
			if ( !arrival.IsArrivalBlocked )
				director.PinZone( CloseCameraZone );
			var reversing = arrivalDirections.TryGetValue( player, out var arrivalDirection ) &&
				IsReversing( player, arrivalDirection );
			if ( !arrival.CanTransfer( eligible,
				director.IsZoneSettled( CloseCameraZone, CameraPositionTolerance ), reversing ) )
				continue;
			arrivalDirections.Remove( player );
			var travelDirection = currentDirection.LengthSquared > 0f
				? currentDirection
				: approachDirections.GetValueOrDefault( player );
			approachDirections.Remove( player );
			var translation = PairedTransition.SeamAnchor.WorldPosition - SeamAnchor.WorldPosition;
			var destination = player.WorldPosition + translation;
			player.WorldPosition = destination;
			// Teleports must discard the previous physics/render transform history.
			// Otherwise the rendered player travels back through the old tunnel.
			player.Transform.ClearInterpolation();
			arrivals.Remove( player );
			var destinationArrival = new TunnelCameraTransferState();
			destinationArrival.MarkArrival();
			PairedTransition.arrivals[player] = destinationArrival;
			if ( travelDirection.LengthSquared > 0f )
				PairedTransition.arrivalDirections[player] = travelDirection;
			director.TransferToTunnel( translation, PairedTransition.CloseCameraZone );
		}
	}

	private static Vector3 GetTravelDirection( PlayerController player )
	{
		var velocity = player.WishVelocity.LengthSquared > 1f ? player.WishVelocity : player.Velocity;
		return velocity.LengthSquared > 1f ? velocity.Normal : Vector3.Zero;
	}

	private static bool IsReversing( PlayerController player, Vector3 arrivalDirection )
	{
		var direction = GetTravelDirection( player );
		return direction.LengthSquared > 0f && Vector3.Dot( direction, arrivalDirection ) < -0.5f;
	}

	private bool Contains( Vector3 position ) => Trigger.IsValid() && Trigger.Active && Trigger.IsTrigger &&
		Trigger.LocalBounds.Contains( Trigger.WorldTransform.PointToLocal( position ) );

	private bool ValidatePair()
	{
		var pair = PairedTransition;
		string error = null;
		if ( !pair.IsValid() || pair == this || !pair.Active || pair.Scene != Scene || pair.PairedTransition != this )
			error = "Assign an active, reciprocal endpoint in the same scene.";
		else if ( !SeamAnchor.IsValid() || !pair.SeamAnchor.IsValid() ||
			!SeamAnchor.Active || !pair.SeamAnchor.Active || SeamAnchor.Scene != Scene || pair.SeamAnchor.Scene != Scene )
			error = "Both endpoints require enabled seam anchors.";
		else if ( !Trigger.IsValid() || !Trigger.Active || !Trigger.IsTrigger ||
			!pair.Trigger.IsValid() || !pair.Trigger.Active || !pair.Trigger.IsTrigger )
			error = "Both endpoints require active BoxColliders with Is Trigger enabled.";
		else if ( !CloseCameraZone.IsValid() || !CloseCameraZone.Active || !CloseCameraZone.CameraAnchor.IsValid() ||
			!CloseCameraZone.CameraAnchor.Active || CloseCameraZone.Scene != Scene ||
			!pair.CloseCameraZone.IsValid() || !pair.CloseCameraZone.Active || !pair.CloseCameraZone.CameraAnchor.IsValid() ||
			!pair.CloseCameraZone.CameraAnchor.Active || pair.CloseCameraZone.Scene != Scene )
			error = "Both endpoints require active close camera zones with camera anchors.";

		if ( error is not null )
		{
			Warn( error );
			return false;
		}
		return true;
	}

	private void Warn( string message )
	{
		if ( lastWarning == message )
			return;
		lastWarning = message;
		Log.Warning( $"[TunnelTransition] '{GameObject.Name}': {message}" );
	}

	private void ClearAttempts()
	{
		foreach ( var player in arrivals.Keys.ToArray() )
			player?.Components.Get<CameraController>()?.UnpinZone( CloseCameraZone );
		arrivals.Clear();
		arrivalDirections.Clear();
		approachDirections.Clear();
	}

	protected override void OnDisabled() => ClearAttempts();
}
