using Sandbox.Mapping;

namespace Sandbox;

/// <summary>
/// Opens an assigned door after the local player enters the configured PIN.
/// Add this beside a collider whose Is Trigger property is enabled.
/// </summary>
public sealed class DoorKeypad : Component, Component.ITriggerListener
{
	[Property]
	public Door Door { get; set; }

	[Property]
	public string PinCode { get; set; } = "1234";

	[Property, Range( 0.1f, 60f )]
	public float AutoCloseDelay { get; set; } = 5f;

	[Property, Group( "Indicator" )]
	public List<PointLight> Indicators { get; set; } = new();

	[Property, Group( "Indicator" )]
	public List<ModelRenderer> IndicatorSpheres { get; set; } = new();

	[Property, Group( "Indicator" ), Range( 0.1f, 60f )]
	public float IndicatorDuration { get; set; } = 2f;

	private readonly Dictionary<Collider, PlayerController> contacts = new();
	private readonly Dictionary<ModelRenderer, Color> originalSphereTints = new();
	private float timeUntilClose;
	private float indicatorTimeRemaining;
	private bool waitingToClose;
	private bool indicatorIsLit;

	internal bool HasValidPin => !string.IsNullOrEmpty( PinCode )
		&& PinCode.All( digit => digit is >= '0' and <= '9' );

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
		{
			Log.Warning( $"[Door Keypad] Trigger '{GameObject.Name}' has no Collider component." );
		}
		else if ( !collider.IsTrigger )
		{
			Log.Warning( $"[Door Keypad] Collider on '{GameObject.Name}' is not marked as a trigger." );
		}

		if ( Door is null )
			Log.Warning( $"[Door Keypad] Trigger '{GameObject.Name}' has no door assigned." );

		if ( !HasValidPin )
			Log.Warning( $"[Door Keypad] '{GameObject.Name}' requires a non-empty, digit-only PIN." );

		TurnOffIndicators();
	}

	protected override void OnUpdate()
	{
		UpdateAutoClose();
		UpdateIndicators();

		if ( Door is null || !Input.Pressed( "Use" ) )
			return;

		var player = contacts.Values.FirstOrDefault( candidate => !candidate.GameObject.IsProxy );
		if ( player is null )
			return;

		if ( !HasValidPin )
		{
			Log.Warning( $"[Door Keypad] '{GameObject.Name}' refused to open because its PIN is invalid." );
			ShowAccessResult( false );
			return;
		}

		var session = player.Components.Get<KeypadSessionController>()
			?? player.GameObject.AddComponent<KeypadSessionController>();
		session.TryBegin( this, player );
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
		if ( other is null || !contacts.Remove( other, out var player ) )
			return;

		if ( contacts.Values.Contains( player ) )
			return;

		player.Components.Get<KeypadSessionController>()?.Cancel( this );
	}

	protected override void OnDisabled()
	{
		foreach ( var player in contacts.Values.Distinct() )
			player.Components.Get<KeypadSessionController>()?.Cancel( this );

		contacts.Clear();
		waitingToClose = false;
		TurnOffIndicators();
	}

	internal void GrantAccess( PlayerController player )
	{
		if ( Door is null )
			return;

		ShowAccessResult( true );
		Door.Open( player?.GameObject );
		timeUntilClose = AutoCloseDelay;
		waitingToClose = true;
	}

	internal void DenyAccess()
	{
		ShowAccessResult( false );
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

	private void ShowAccessResult( bool accessGranted )
	{
		var color = accessGranted ? Color.Green : Color.Red;
		foreach ( var indicator in Indicators )
		{
			if ( indicator is null )
				continue;

			indicator.GameObject.Enabled = true;
			indicator.LightColor = color;
			indicator.Enabled = true;
		}

		foreach ( var sphere in IndicatorSpheres )
		{
			if ( sphere is null )
				continue;

			originalSphereTints.TryAdd( sphere, sphere.Tint );
			sphere.Tint = color;
		}

		indicatorTimeRemaining = IndicatorDuration;
		indicatorIsLit = true;
	}

	private void UpdateIndicators()
	{
		if ( !indicatorIsLit )
			return;

		indicatorTimeRemaining -= Time.Delta;
		if ( indicatorTimeRemaining <= 0f )
			TurnOffIndicators();
	}

	private void TurnOffIndicators()
	{
		foreach ( var indicator in Indicators )
		{
			if ( indicator is not null )
				indicator.Enabled = false;
		}

		foreach ( var sphere in IndicatorSpheres )
		{
			if ( sphere is not null && originalSphereTints.TryGetValue( sphere, out var tint ) )
				sphere.Tint = tint;
		}

		indicatorTimeRemaining = 0f;
		indicatorIsLit = false;
	}
}
