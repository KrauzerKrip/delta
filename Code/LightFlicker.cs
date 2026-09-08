namespace Sandbox;

/// <summary>
/// Briefly pulses a light before leaving it permanently enabled.
/// The controller should live on an enabled GameObject so it can drive a light
/// whose GameObject starts disabled.
/// </summary>
public sealed class LightFlicker : Component
{
	[Property, Group( "Light" )]
	public PointLight Light { get; set; }

	[Property, Range( 1, 10 ), Group( "Flicker" )]
	public int FlickerCount { get; set; } = 4;

	[Property, Range( 0.01f, 1f ), Group( "Flicker" )]
	public float OnDuration { get; set; } = 0.12f;

	[Property, Range( 0.01f, 1f ), Group( "Flicker" )]
	public float OffDuration { get; set; } = 0.18f;

	private int remainingFlickers;
	private float timeUntilToggle;
	private bool isFlickering;

	/// <summary>Starts the flicker sequence, restarting it if already active.</summary>
	public void Activate()
	{
		if ( Light is null )
		{
			Log.Warning( $"[Light Flicker] '{GameObject.Name}' has no light assigned." );
			return;
		}

		Light.GameObject.Enabled = true;
		Light.Enabled = true;
		remainingFlickers = FlickerCount;
		timeUntilToggle = OnDuration;
		isFlickering = true;
	}

	protected override void OnUpdate()
	{
		if ( !isFlickering || Light is null )
			return;

		timeUntilToggle -= Time.Delta;
		if ( timeUntilToggle > 0f )
			return;

		if ( Light.Enabled )
		{
			Light.Enabled = false;
			timeUntilToggle = OffDuration;
			return;
		}

		Light.Enabled = true;
		remainingFlickers--;
		if ( remainingFlickers <= 0 )
		{
			isFlickering = false;
			return;
		}

		timeUntilToggle = OnDuration;
	}
}
