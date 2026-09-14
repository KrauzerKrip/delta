/// <summary>
/// A pressable interaction that completes only after Use is held continuously.
/// </summary>
public sealed class HoldPressable : Component, Component.IPressable
{
	[Property, Range( 0.1f, 30f ), Group( "Interaction" )]
	public float HoldDuration { get; set; } = 3f;

	[Property, Group( "Interaction" )]
	public bool OneShot { get; set; } = true;

	[Property, Group( "Events" )]
	public Doo OnCompleted { get; set; }

	public bool IsCompleted { get; private set; }
	public bool IsAvailable => !OneShot || !IsCompleted;
	public float HoldProgress => IsCompleted
		? 1f
		: (holdTime / System.MathF.Max( HoldDuration, 0.01f )).Clamp( 0f, 1f );

	private float holdTime;
	private bool isHolding;

	void Component.IPressable.Hover( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Look( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Blur( Component.IPressable.Event e )
	{
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => IsAvailable;

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e ) => null;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		if ( !IsAvailable )
			return false;

		holdTime = 0f;
		isHolding = true;
		return true;
	}

	bool Component.IPressable.Pressing( Component.IPressable.Event e )
	{
		if ( !isHolding || !IsAvailable )
			return false;

		holdTime = System.MathF.Min( holdTime + Time.Delta, System.MathF.Max( HoldDuration, 0f ) );
		if ( holdTime < HoldDuration )
			return true;

		isHolding = false;
		IsCompleted = true;
		RunDoo( OnCompleted, configuration =>
			configuration.SetArgument( "user", e.Source?.GameObject ) );
		return false;
	}

	void Component.IPressable.Release( Component.IPressable.Event e )
	{
		isHolding = false;

		if ( !OneShot || !IsCompleted )
		{
			holdTime = 0f;
			IsCompleted = false;
		}
	}

	protected override void OnDisabled()
	{
		isHolding = false;
		if ( !IsCompleted )
			holdTime = 0f;
	}
}
