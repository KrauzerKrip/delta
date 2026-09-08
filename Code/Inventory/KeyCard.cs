namespace Sandbox;

/// <summary>The clearance levels supported by facility key cards.</summary>
public enum AccessLevel
{
	None,
	Level1,
	Level2,
	Level3
}

/// <summary>An inventory item whose clearance can be configured per instance.</summary>
public sealed class KeyCard : BaseInventoryItem
{
	[Property]
	public AccessLevel AccessLevel { get; set; }

	public KeyCard()
	{
		DisplayName = "KeyCard";
		PreferredSlot = -1;
	}
}
