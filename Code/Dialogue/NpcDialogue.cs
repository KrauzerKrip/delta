namespace Sandbox;

/// <summary>
/// Reusable NPC conversation component. A trigger can call
/// <see cref="TryStart(GameObject)"/> with the entering object.
/// </summary>
public class NpcDialogue : Component
{
	[Property, Group( "Dialogue" )]
	public virtual string SpeakerName { get; set; } = "NPC";

	[Property, Group( "Dialogue" ), TextArea]
	public virtual List<string> Lines { get; set; } = new();

	[Property, Group( "Dialogue" )]
	public bool Once { get; set; } = true;

	/// <summary>Whether this dialogue has reached its end at least once.</summary>
	public bool Completed { get; private set; }

	/// <summary>
	/// Starts this dialogue for the player owning <paramref name="interactor"/>.
	/// Returns false when the object is not a local player or dialogue cannot start.
	/// </summary>
	public bool TryStart( GameObject interactor )
	{
		if ( interactor is null )
		{
			Log.Warning( $"[Dialogue] '{SpeakerName}' received a null interactor." );
			return false;
		}

		if ( Once && Completed )
		{
			Log.Info( $"[Dialogue] '{SpeakerName}' is already completed and cannot replay." );
			return false;
		}

		var player = interactor.Components.Get<PlayerController>()
			?? interactor.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is null )
		{
			Log.Warning( $"[Dialogue] '{SpeakerName}' could not find a PlayerController from '{interactor.Name}'." );
			return false;
		}

		if ( player.GameObject.IsProxy )
		{
			Log.Info( $"[Dialogue] '{SpeakerName}' ignored proxy player '{player.GameObject.Name}'." );
			return false;
		}

		var dialogueLines = Lines;
		if ( dialogueLines is null || dialogueLines.Count == 0 )
		{
			Log.Warning( $"[Dialogue] '{SpeakerName}' has no dialogue lines." );
			return false;
		}

		var inventory = player.Components.Get<PlayerInventory>()
			?? player.GameObject.AddComponent<PlayerInventory>();
		var controller = player.Components.Get<DialogueController>()
			?? player.GameObject.AddComponent<DialogueController>();

		var started = controller.TryBegin( this, SpeakerName, dialogueLines, inventory );
		Log.Info( $"[Dialogue] '{SpeakerName}' begin result: {started}. Lines: {dialogueLines.Count}." );
		return started;
	}

	protected virtual void OnDialogueCompleted( PlayerInventory inventory )
	{
	}

	internal void Complete( PlayerInventory inventory )
	{
		if ( Once && Completed )
			return;

		Completed = true;
		OnDialogueCompleted( inventory );
		Log.Info( $"[Dialogue] '{SpeakerName}' completed." );
	}
}
