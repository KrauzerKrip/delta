using Sandbox.Mapping;

/// <summary>
/// Opens an assigned door while a player is inside this trigger volume.
/// Add this beside a collider whose Is Trigger property is enabled.
/// </summary>
public sealed class DoorTrigger : Component, Component.ITriggerListener
{
	[Property]
	public Door Door { get; set; }

	private readonly Dictionary<Collider, PlayerController> contacts = new();

	public void OnTriggerEnter( Collider other )
	{
		if ( Door is null || contacts.ContainsKey( other ) )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is null )
			return;

		var wasEmpty = contacts.Count == 0;
		contacts.Add( other, player );

		if ( wasEmpty )
			Door.Open( player.GameObject );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !contacts.Remove( other ) )
			return;

		if ( contacts.Count == 0 )
			Door?.Close();
	}

	protected override void OnDisabled()
	{
		if ( contacts.Count > 0 )
			Door?.Close();

		contacts.Clear();
	}
}
