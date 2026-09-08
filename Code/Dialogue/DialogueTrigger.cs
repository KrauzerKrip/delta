namespace Sandbox;

/// <summary>
/// Starts an assigned NPC dialogue when a player enters this trigger volume.
/// Add this beside a collider whose Is Trigger property is enabled.
/// </summary>
public sealed class DialogueTrigger : Component, Component.ITriggerListener
{
	[Property]
	public NpcDialogue Dialogue { get; set; }

	[Property]
	public bool DebugLogging { get; set; } = true;

	private readonly Dictionary<Collider, PlayerController> contacts = new();

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
		{
			Log.Warning( $"[Dialogue] Trigger '{GameObject.Name}' has no Collider component." );
		}
		else if ( !collider.IsTrigger )
		{
			Log.Warning( $"[Dialogue] Collider on '{GameObject.Name}' is not marked as a trigger." );
		}

		if ( Dialogue is null )
		{
			Log.Warning( $"[Dialogue] Trigger '{GameObject.Name}' has no dialogue assigned." );
		}
		else if ( DebugLogging )
		{
			Log.Info( $"[Dialogue] Trigger '{GameObject.Name}' ready for '{Dialogue.GameObject.Name}'." );
		}
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( DebugLogging )
			Log.Info( $"[Dialogue] '{GameObject.Name}' entered by '{other?.GameObject.Name ?? "null"}'." );

		if ( Dialogue is null )
		{
			Log.Warning( $"[Dialogue] '{GameObject.Name}' cannot start: no dialogue is assigned." );
			return;
		}

		if ( other is null || contacts.ContainsKey( other ) )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is null )
		{
			if ( DebugLogging )
				Log.Info( $"[Dialogue] Ignoring '{other.GameObject.Name}': no PlayerController in its ancestors." );
			return;
		}

		var wasOutside = !contacts.Values.Contains( player );
		contacts.Add( other, player );

		if ( wasOutside )
		{
			var started = Dialogue.TryStart( player.GameObject );
			if ( DebugLogging )
				Log.Info( $"[Dialogue] Start request for '{Dialogue.SpeakerName}' returned {started}." );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( other is not null )
		{
			contacts.Remove( other );
			if ( DebugLogging )
				Log.Info( $"[Dialogue] '{other.GameObject.Name}' exited '{GameObject.Name}'." );
		}
	}

	protected override void OnDisabled()
	{
		contacts.Clear();
	}
}
