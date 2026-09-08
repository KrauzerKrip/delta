namespace Sandbox;

/// <summary>A work order with no gameplay behavior yet.</summary>
public sealed class WorkOrder : BaseInventoryItem
{
	public WorkOrder()
	{
		DisplayName = "WorkOrder";
		PreferredSlot = -1;
	}
}
