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
	private string lastWarning;

	protected override void OnFixedUpdate()
	{
		if ( !ValidatePair() )
		{
			arrivals.Clear();
			return;
		}

		foreach ( var player in arrivals.Keys.ToArray() )
			if ( !player.IsValid() || player.GameObject.IsProxy || !Contains( player.WorldPosition ) )
				arrivals.Remove( player );

		foreach ( var player in Scene.GetAllComponents<PlayerController>().ToArray() )
		{
			if ( !player.Active || player.GameObject.IsProxy || !Contains( player.WorldPosition ) )
				continue;
			if ( !arrivals.TryGetValue( player, out var arrival ) )
				arrivals[player] = arrival = new TunnelCameraTransferState();
			var director = player.Components.Get<CameraController>();
			if ( !director.IsValid() || !director.Active )
			{
				Warn( "The player requires an active CameraController." );
				continue;
			}
			if ( !arrival.CanTransfer( true, director.IsZoneSettled( CloseCameraZone, CameraPositionTolerance ) ) )
				continue;
			var translation = PairedTransition.SeamAnchor.WorldPosition - SeamAnchor.WorldPosition;
			var destination = player.WorldPosition + translation;
			if ( !PairedTransition.Contains( destination ) )
			{
				Warn( "The corresponding position is outside the destination trigger. Match the trigger volumes around the seams." );
				continue;
			}
			if ( !PairedTransition.CloseCameraZone.ContainsPosition( destination ) )
			{
				Warn( "The destination close camera zone must cover the player's corresponding position at the seam." );
				continue;
			}

			player.WorldPosition = destination;
			// Teleports must discard the previous physics/render transform history.
			// Otherwise the rendered player travels back through the old tunnel.
			player.Transform.ClearInterpolation();
			arrivals.Remove( player );
			var destinationArrival = new TunnelCameraTransferState();
			destinationArrival.MarkArrival();
			PairedTransition.arrivals[player] = destinationArrival;
			director.TransferToTunnel( translation, PairedTransition.CloseCameraZone );
		}
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

	protected override void OnDisabled() => arrivals.Clear();
}
