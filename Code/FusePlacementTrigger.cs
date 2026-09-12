/// <summary>
/// Keeps the minigame fuse attached to the pliers until it enters this trigger,
/// then snaps it into the circuit attachment and closes the pliers.
/// </summary>
public sealed class FusePlacementTrigger : Component, Component.ITriggerListener
{
	private const string PliersOpenParameter = "is_open";

	[Property, Group( "Setup" )]
	public GameObject Fuse { get; set; }

	[Property, Group( "Setup" )]
	public GameObject PliersFuseAttachment { get; set; }

	[Property, Group( "Setup" )]
	public GameObject CircuitFuseAttachment { get; set; }

	[Property, Group( "Setup" )]
	public PliersMinigameController Minigame { get; set; }

	[Property, Group( "Completion" ), Range( 0f, 5f )]
	public float PliersCloseDelay { get; set; } = 0.75f;

	public bool HasPlacedFuse { get; private set; }

	private SkinnedModelRenderer pliersRenderer;
	private float closeDelayRemaining;
	private bool pliersClosed;

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
			Log.Warning( $"[Fuse Placement] Trigger '{GameObject.Name}' has no Collider component." );
		else if ( !collider.IsTrigger )
			Log.Warning( $"[Fuse Placement] Collider on '{GameObject.Name}' is not marked as a trigger." );

		if ( Fuse is null )
			Log.Warning( $"[Fuse Placement] Trigger '{GameObject.Name}' has no fuse assigned." );

		if ( PliersFuseAttachment is null )
			Log.Warning( $"[Fuse Placement] Trigger '{GameObject.Name}' has no pliers fuse attachment assigned." );

		if ( CircuitFuseAttachment is null )
			Log.Warning( $"[Fuse Placement] Trigger '{GameObject.Name}' has no circuit fuse attachment assigned." );

		pliersRenderer = Minigame?.Pliers?.Components.Get<SkinnedModelRenderer>();
		if ( pliersRenderer is null )
			Log.Warning( $"[Fuse Placement] Trigger '{GameObject.Name}' could not find the pliers model renderer." );
		else
			pliersRenderer.Set( PliersOpenParameter, true );

		SnapFuseTo( PliersFuseAttachment );
	}

	protected override void OnUpdate()
	{
		if ( !HasPlacedFuse )
		{
			SnapFuseTo( PliersFuseAttachment );
			return;
		}

		if ( pliersClosed )
			return;

		closeDelayRemaining = System.MathF.Max( closeDelayRemaining - Time.Delta, 0f );
		if ( closeDelayRemaining > 0f )
			return;

		pliersClosed = true;
		pliersRenderer?.Set( PliersOpenParameter, false );
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( HasPlacedFuse || other is null || !BelongsToFuse( other.GameObject ) )
			return;

		HasPlacedFuse = true;
		SnapFuseTo( CircuitFuseAttachment );
		closeDelayRemaining = System.MathF.Max( PliersCloseDelay, 0f );
	}

	public void OnTriggerExit( Collider other )
	{
	}

	private bool BelongsToFuse( GameObject candidate )
	{
		for ( var current = candidate; current is not null; current = current.Parent )
		{
			if ( current == Fuse )
				return true;
		}

		return false;
	}

	private void SnapFuseTo( GameObject attachment )
	{
		if ( Fuse is null || attachment is null )
			return;

		Fuse.WorldPosition = attachment.WorldPosition;
		Fuse.WorldRotation = attachment.WorldRotation;
	}
}
