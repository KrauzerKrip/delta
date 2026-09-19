/// <summary>An editor-authored callback that fires at a specific offset into every run.</summary>
public sealed class TimedRunEvent : Component
{
	[Property, Group( "Timing" ), Range( 0f, 86400f )]
	public float TriggerAtSeconds { get; set; }

	[Property, Group( "Event" )]
	public Doo OnTriggered { get; set; }

	[Property, Group( "Optional GameObject Action" )]
	public GameObject Target { get; set; }

	private bool warnedAboutEmptyCallback;
	private bool warnedAboutMissingTarget;

	internal void Trigger()
	{
		if ( OnTriggered is null || OnTriggered.IsEmpty() )
		{
			if ( !warnedAboutEmptyCallback )
			{
				Log.Warning( $"[Run Timeline] Event '{GameObject.Name}' at T+{TriggerAtSeconds:0.###} has no callback." );
				warnedAboutEmptyCallback = true;
			}
			return;
		}

		RunDoo( OnTriggered, _ => { } );
	}

	/// <summary>Convenience action for enabling this event's optional target.</summary>
	public void EnableTarget()
	{
		if ( !TryGetTarget( out var target ) )
			return;

		target.Enabled = true;
	}

	/// <summary>Convenience action for disabling this event's optional target.</summary>
	public void DisableTarget()
	{
		if ( !TryGetTarget( out var target ) )
			return;

		target.Enabled = false;
	}

	private bool TryGetTarget( out GameObject target )
	{
		target = Target;
		if ( target is not null )
			return true;

		if ( !warnedAboutMissingTarget )
		{
			Log.Warning( $"[Run Timeline] Event '{GameObject.Name}' has no target assigned." );
			warnedAboutMissingTarget = true;
		}

		return false;
	}
}
