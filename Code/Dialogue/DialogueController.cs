namespace Sandbox;

/// <summary>Runs one local player's active dialogue and owns its screen UI.</summary>
public sealed class DialogueController : Component
{
	private NpcDialogue source;
	private PlayerInventory inventory;
	private IReadOnlyList<string> lines;
	private PlayerController player;
	private DialogueHud hud;
	private bool previousUseInputControls;
	private bool waitForUseRelease;

	public bool IsOpen { get; private set; }
	public string SpeakerName { get; private set; } = string.Empty;
	public int LineIndex { get; private set; }
	public int LineCount => lines?.Count ?? 0;
	public string CurrentLine => IsOpen && LineIndex >= 0 && LineIndex < LineCount
		? lines[LineIndex]
		: string.Empty;

	protected override void OnUpdate()
	{
		if ( !IsOpen )
			return;

		if ( waitForUseRelease )
		{
			waitForUseRelease = Input.Down( "Use" );
			return;
		}

		if ( Input.Pressed( "Use" ) )
			Advance();
	}

	protected override void OnDisabled()
	{
		Cancel();
	}

	internal bool TryBegin(
		NpcDialogue dialogueSource,
		string speakerName,
		IReadOnlyList<string> dialogueLines,
		PlayerInventory playerInventory )
	{
		if ( IsOpen || dialogueSource is null || playerInventory is null ||
			dialogueLines is null || dialogueLines.Count == 0 )
		{
			Log.Warning( "[Dialogue] DialogueController rejected an invalid or overlapping conversation." );
			return false;
		}

		player = Components.Get<PlayerController>();
		if ( player is null )
		{
			Log.Warning( $"[Dialogue] DialogueController on '{GameObject.Name}' has no PlayerController." );
			return false;
		}

		source = dialogueSource;
		inventory = playerInventory;
		EnsureHud();
		if ( hud is null )
		{
			Log.Warning( "[Dialogue] DialogueController could not create its HUD." );
			return false;
		}

		lines = dialogueLines;
		SpeakerName = speakerName ?? string.Empty;
		LineIndex = 0;
		IsOpen = true;
		waitForUseRelease = Input.Down( "Use" );

		previousUseInputControls = player.UseInputControls;
		player.UseInputControls = false;
		player.WishVelocity = Vector3.Zero;
		Log.Info( $"[Dialogue] Opened '{SpeakerName}' for '{player.GameObject.Name}'." );
		return true;
	}

	private void Advance()
	{
		if ( LineIndex + 1 < LineCount )
		{
			LineIndex++;
			Log.Info( $"[Dialogue] Advanced '{SpeakerName}' to line {LineIndex + 1}/{LineCount}." );
			return;
		}

		var completedSource = source;
		var completedInventory = inventory;
		Close();
		completedSource?.Complete( completedInventory );
	}

	private void Cancel()
	{
		if ( !IsOpen )
			return;

		Close();
	}

	private void Close()
	{
		if ( player is not null )
		{
			player.WishVelocity = Vector3.Zero;
			player.UseInputControls = previousUseInputControls;
		}

		IsOpen = false;
		source = null;
		inventory = null;
		lines = null;
		SpeakerName = string.Empty;
		LineIndex = 0;
		waitForUseRelease = false;
		player = null;
		Log.Info( "[Dialogue] Dialogue UI closed and player input restored." );
	}

	private void EnsureHud()
	{
		if ( hud is not null )
			return;

		if ( inventory is null )
			return;

		var hudObject = inventory.EnsureHudObject();
		hud = hudObject.Components.Get<DialogueHud>() ?? hudObject.AddComponent<DialogueHud>();
		hud.Dialogue = this;
		Log.Info( $"[Dialogue] Dialogue HUD ready on local UI object '{hudObject.Name}'." );
	}
}
