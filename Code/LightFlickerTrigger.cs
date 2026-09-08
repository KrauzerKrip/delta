namespace Sandbox;

/// <summary>
/// Starts a light flicker once when the local player enters after the required
/// dialogue has been completed.
/// </summary>
public sealed class LightFlickerTrigger : Component, Component.ITriggerListener
{
	[Property, Group( "Requirements" )]
	public NpcDialogue RequiredDialogue { get; set; }

	[Property, Group( "Effects" )]
	public LightFlicker Flicker { get; set; }

	public bool HasTriggered { get; private set; }

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
		{
			Log.Warning( $"[Light Flicker] Trigger '{GameObject.Name}' has no Collider component." );
		}
		else if ( !collider.IsTrigger )
		{
			Log.Warning( $"[Light Flicker] Collider on '{GameObject.Name}' is not marked as a trigger." );
		}

		if ( RequiredDialogue is null )
			Log.Warning( $"[Light Flicker] Trigger '{GameObject.Name}' has no required dialogue assigned." );

		if ( Flicker is null )
			Log.Warning( $"[Light Flicker] Trigger '{GameObject.Name}' has no flicker effect assigned." );
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( HasTriggered || RequiredDialogue?.Completed != true || Flicker is null || other is null )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is null || player.GameObject.IsProxy )
			return;

		HasTriggered = true;
		Flicker.Activate();
	}

	public void OnTriggerExit( Collider other )
	{
	}
}
