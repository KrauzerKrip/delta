namespace Sandbox;

/// <summary>
/// The player's s&amp;box slot inventory, with the project's compact HUD layered on top.
/// </summary>
public sealed class PlayerInventory : BaseInventoryComponent
{
	private readonly List<BaseInventoryItem> acceptedItems = new();
	private GameObject hudObject;
	private InventoryHud hud;
	private DollhouseCursor cursor;
	private InteractionController interaction;

	/// <summary>Raised after this inventory accepts an item.</summary>
	public event System.Action Changed;

	public PlayerInventory()
	{
		Behaviour = InventoryBehaviour.Hotbar;
		MaxSlots = 8;
		UsesLoadout = false;
		GiveOnStart = false;
		PickupMode = PickupBehaviour.None;
		AutoSwitchOnPickup = false;
		AutoSwitchOnEmpty = false;
	}

	protected override void OnStart()
	{
		base.OnStart();
		EnsureHudObject();
	}

	protected override void OnDestroy()
	{
		hudObject?.Destroy();
	}

	/// <summary>
	/// Creates an inventory item GameObject, configures it, and adds it to a base inventory slot.
	/// Returns null if the inventory has no suitable slot or refuses the item.
	/// </summary>
	public T GiveItem<T>( System.Action<T> configure = null, int slot = -1 )
		where T : BaseInventoryItem, new()
	{
		var itemObject = new GameObject( true, typeof( T ).Name );
		var item = itemObject.AddComponent<T>();
		configure?.Invoke( item );

		if ( Add( item, slot ) )
			return item;

		Log.Warning( $"[Inventory] Could not add '{item.DisplayName}' to '{GameObject.Name}'." );
		itemObject.Destroy();
		return null;
	}

	/// <summary>Returns whether a world item can currently be picked up through interaction.</summary>
	public bool CanInteractivelyPickup( BaseInventoryItem item )
	{
		return CanPickupWorldItem( item );
	}

	/// <summary>Requests pickup using the inventory's normal host-authoritative world-item path.</summary>
	public bool TryInteractivePickup( BaseInventoryItem item )
	{
		if ( !CanInteractivelyPickup( item ) )
			return false;

		PickupWorldItem( item );
		return true;
	}

	/// <summary>Formats engine inventory items for the project's inventory HUD.</summary>
	public string GetHudDisplayName( BaseInventoryItem item ) => item.DisplayName;

	/// <summary>
	/// Returns the synchronized base items. During the short engine hierarchy delay after Add,
	/// accepted item references keep the local HUD from temporarily losing the new entries.
	/// </summary>
	public IEnumerable<BaseInventoryItem> GetHudItems()
	{
		var syncedItems = Items.ToArray();
		return syncedItems.Length > 0
			? syncedItems
			: acceptedItems.Where( item => item is not null );
	}

	protected override void OnItemAdded( BaseInventoryItem item )
	{
		base.OnItemAdded( item );
		if ( !acceptedItems.Contains( item ) )
			acceptedItems.Add( item );

		Changed?.Invoke();
		Log.Info( $"[Inventory] Added '{GetHudDisplayName( item )}' to '{GameObject.Name}' in slot {item.Slot}. Base Items currently reports {Items.Count()} item(s)." );
	}

	internal GameObject EnsureHudObject()
	{
		if ( hudObject is null )
		{
			hudObject = new GameObject( true, $"{GameObject.Name} HUD" );
			hudObject.NetworkMode = NetworkMode.Never;

			var screen = hudObject.AddComponent<ScreenPanel>();
			screen.ZIndex = 100;
			Log.Info( $"[Inventory] Created local HUD object '{hudObject.Name}'." );
		}

		if ( hud is null )
		{
			hud = hudObject.Components.Get<InventoryHud>() ?? hudObject.AddComponent<InventoryHud>();
			hud.Inventory = this;
			hud.Refresh();
			Log.Info( $"[Inventory] Inventory HUD ready on local UI object '{hudObject.Name}'." );
		}

		interaction ??= Components.Get<InteractionController>() ?? GameObject.AddComponent<InteractionController>();
		cursor ??= hudObject.Components.Get<DollhouseCursor>() ?? hudObject.AddComponent<DollhouseCursor>();
		cursor.Interaction = interaction;

		return hudObject;
	}
}
