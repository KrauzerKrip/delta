namespace Sandbox;

/// <summary>
/// An inventory item that can be picked up through the player's normal interaction system.
/// </summary>
public abstract class InteractableInventoryItem : BaseInventoryItem, Component.IPressable
{
	void Component.IPressable.Hover( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Look( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Blur( Component.IPressable.Event e )
	{
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e )
	{
		var inventory = GetInventory( e.Source );
		return inventory is not null && inventory.CanInteractivelyPickup( this );
	}

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e ) => null;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		var inventory = GetInventory( e.Source );
		if ( inventory is null || !inventory.TryInteractivePickup( this ) )
			return false;

		return true;
	}

	bool Component.IPressable.Pressing( Component.IPressable.Event e ) => false;

	void Component.IPressable.Release( Component.IPressable.Event e )
	{
	}

	private static PlayerInventory GetInventory( Component source )
	{
		return source?.Components.Get<PlayerInventory>();
	}
}
