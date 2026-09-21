namespace Sandbox;

/// <summary>Owns one local player's active keypad session and screen UI.</summary>
public sealed class KeypadSessionController : Component
{
	private const float DeniedMessageDuration = 1.2f;

	private readonly KeypadEntryState entry = new();
	private DoorKeypad source;
	private PlayerController player;
	private KeypadHud hud;
	private bool previousUseInputControls;
	private bool waitForUseRelease;
	private float deniedTimeRemaining;

	public bool IsOpen { get; private set; }
	public bool IsDenied => deniedTimeRemaining > 0f;
	public bool CanEdit => IsOpen && !IsDenied;
	public string StatusText => IsDenied ? "ACCESS DENIED" : "ENTER ACCESS CODE";
	public int PinLength => entry.PinLength;
	public string DisplayText => string.Concat(
		Enumerable.Repeat( "●", entry.EnteredCode.Length )
			.Concat( Enumerable.Repeat( "—", entry.PinLength - entry.EnteredCode.Length ) ) );

	protected override void OnUpdate()
	{
		if ( !IsOpen )
			return;

		if ( source is null || !source.Active )
		{
			Close();
			return;
		}

		player.WishVelocity = Vector3.Zero;
		Mouse.Visibility = MouseVisibility.Visible;

		if ( Input.EscapePressed )
		{
			Input.EscapePressed = false;
			Close();
			return;
		}

		if ( deniedTimeRemaining > 0f )
		{
			deniedTimeRemaining -= Time.Delta;
			return;
		}

		if ( waitForUseRelease )
		{
			waitForUseRelease = Input.Down( "Use" );
			if ( waitForUseRelease )
				return;
		}

		for ( var digit = 0; digit <= 9; digit++ )
		{
			if ( Input.Keyboard.Pressed( digit.ToString() ) || Input.Keyboard.Pressed( $"KP_{digit}" ) )
				EnterDigit( (char)('0' + digit) );
		}

		if ( Input.Keyboard.Pressed( "BACKSPACE" ) || Input.Keyboard.Pressed( "DEL" ) || Input.Keyboard.Pressed( "KP_DEL" ) )
			Backspace();

		if ( Input.Keyboard.Pressed( "ENTER" ) || Input.Keyboard.Pressed( "KP_ENTER" ) )
			Submit();
	}

	protected override void OnDisabled()
	{
		Close();
	}

	internal bool TryBegin( DoorKeypad keypad, PlayerController keypadPlayer )
	{
		if ( IsOpen || keypad is null || keypadPlayer is null || !keypad.HasValidPin )
			return false;

		var inventory = keypadPlayer.Components.Get<PlayerInventory>();
		if ( inventory is null )
		{
			Log.Warning( $"[Door Keypad] Player '{keypadPlayer.GameObject.Name}' has no PlayerInventory for keypad UI." );
			return false;
		}

		source = keypad;
		player = keypadPlayer;
		entry.Configure( keypad.PinCode );
		deniedTimeRemaining = 0f;
		waitForUseRelease = Input.Down( "Use" );
		previousUseInputControls = player.UseInputControls;
		player.UseInputControls = false;
		player.WishVelocity = Vector3.Zero;
		IsOpen = true;

		var hudObject = inventory.EnsureHudObject();
		hud = hudObject.Components.Get<KeypadHud>() ?? hudObject.AddComponent<KeypadHud>();
		hud.Session = this;
		Mouse.Visibility = MouseVisibility.Visible;
		return true;
	}

	public void EnterDigit( char digit )
	{
		if ( CanEdit )
			entry.AppendDigit( digit );
	}

	public void Backspace()
	{
		if ( CanEdit )
			entry.Backspace();
	}

	public void ClearEntry()
	{
		if ( CanEdit )
			entry.Clear();
	}

	public void Submit()
	{
		if ( !CanEdit )
			return;

		if ( entry.Submit() )
		{
			var grantedBy = source;
			var grantedTo = player;
			Close();
			grantedBy?.GrantAccess( grantedTo );
			return;
		}

		source?.DenyAccess();
		deniedTimeRemaining = DeniedMessageDuration;
	}

	public void Cancel()
	{
		Close();
	}

	internal void Cancel( DoorKeypad keypad )
	{
		if ( source == keypad )
			Close();
	}

	private void Close()
	{
		if ( !IsOpen )
			return;

		if ( player is not null )
		{
			player.WishVelocity = Vector3.Zero;
			player.UseInputControls = previousUseInputControls;
		}

		IsOpen = false;
		source = null;
		player = null;
		entry.Configure( string.Empty );
		waitForUseRelease = false;
		deniedTimeRemaining = 0f;
	}
}
