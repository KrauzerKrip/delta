using Sandbox.Mapping;

/// <summary>
/// Opens an assigned door when the local player uses this trigger while carrying
/// a key card with sufficient clearance.
/// </summary>
public sealed class CardReader : Component, Component.ITriggerListener
{
	[Property]
	public Door Door { get; set; }

	[Property]
	public AccessLevel RequiredAccessLevel { get; set; } = AccessLevel.Maintenance;

	[Property, Range( 0.1f, 60f )]
	public float AutoCloseDelay { get; set; } = 5f;

	private readonly Dictionary<Collider, PlayerController> contacts = new();
	private float timeUntilClose;
	private bool waitingToClose;

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
		{
			Log.Warning( $"[Card Reader] Trigger '{GameObject.Name}' has no Collider component." );
		}
		else if ( !collider.IsTrigger )
		{
			Log.Warning( $"[Card Reader] Collider on '{GameObject.Name}' is not marked as a trigger." );
		}

		if ( Door is null )
			Log.Warning( $"[Card Reader] Trigger '{GameObject.Name}' has no door assigned." );
	}

	protected override void OnUpdate()
	{
		UpdateAutoClose();

		if ( Door is null || !Input.Pressed( "Use" ) )
			return;

		var player = contacts.Values.FirstOrDefault( player => !player.GameObject.IsProxy );
		if ( player is null )
			return;

		var inventory = player.Components.Get<PlayerInventory>();
		if ( HasRequiredAccess( inventory ) )
		{
			Door.Open( player.GameObject );
			timeUntilClose = AutoCloseDelay;
			waitingToClose = true;
		}
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( other is null || contacts.ContainsKey( other ) )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is not null )
			contacts.Add( other, player );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( other is not null )
			contacts.Remove( other );
	}

	protected override void OnDisabled()
	{
		contacts.Clear();
		waitingToClose = false;
	}

	private void UpdateAutoClose()
	{
		if ( !waitingToClose || Door is null )
			return;

		timeUntilClose -= Time.Delta;
		if ( timeUntilClose > 0f )
			return;

		waitingToClose = false;
		Door.Close();
	}

	private bool HasRequiredAccess( PlayerInventory inventory )
	{
		if ( RequiredAccessLevel == AccessLevel.None )
			return true;

		return inventory is not null && inventory.GetHudItems()
			.OfType<KeyCard>()
			.Any( card => card.AccessLevel >= RequiredAccessLevel );
	}
}
