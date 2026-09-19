/// <summary>
/// Owns the clock and ordered callbacks for one gameplay run, and initiates full run resets.
/// </summary>
public sealed class RunTimeline : Component
{
	[Property, Group( "Reset" )]
	public string ResetScenePath { get; set; } = "scenes/main.scene";

	[Property, Group( "Reset" ), Range( 0f, 60f )]
	public float ResetFlashHoldDuration { get; set; } = 7f;

	[Property, Group( "Reset" ), Range( 0f, 60f )]
	public float ResetFlashFadeDuration { get; set; } = 7f;

	[Property, Group( "Reset" ), Range( -180f, 180f )]
	public float ResetMarieYaw { get; set; } = 90f;

	[Property, Group( "Schedule" )]
	public List<TimedRunEvent> Events { get; set; } = new();

	private readonly RunTimelineState state = new();
	private bool resetInProgress;

	public float ElapsedSeconds => state.ElapsedSeconds;
	public int RunNumber => state.RunNumber;
	public bool IsRunning => state.IsRunning;

	protected override void OnStart()
	{
		ConfigureState();
	}

	protected override void OnUpdate()
	{
		state.Advance( Time.Delta );
	}

	/// <summary>Starts this scene's authored timeline at T+0 without reloading the scene.</summary>
	public void StartRun()
	{
		ConfigureState();
		state.StartRun();
		Log.Info( $"[Run Timeline] Run {RunNumber} started with {Events.Count( timedEvent => timedEvent is not null )} event(s)." );
	}

	/// <summary>Reloads the configured scene beneath the run-reset flash.</summary>
	public bool RestartRun()
	{
		return RestartRun( ResetFlashHoldDuration, ResetFlashFadeDuration );
	}

	/// <summary>Reloads the configured scene using the supplied flash timings.</summary>
	public bool RestartRun( float holdDuration, float fadeDuration )
	{
		if ( resetInProgress )
			return false;

		if ( string.IsNullOrWhiteSpace( ResetScenePath ) )
		{
			Log.Warning( $"[Run Timeline] '{GameObject.Name}' cannot restart because no reset scene is configured." );
			return false;
		}

		resetInProgress = true;
		if ( !RunResetTransition.Begin( ResetScenePath, holdDuration, fadeDuration, ResetMarieYaw ) )
		{
			resetInProgress = false;
			return false;
		}

		return true;
	}

	private void ConfigureState()
	{
		state.Configure( Events.Select( (timedEvent, index) => new RunTimelineState.Entry
		{
			TriggerAtSeconds = timedEvent?.TriggerAtSeconds ?? float.MaxValue,
			AuthoredOrder = index,
			Callback = timedEvent is null ? null : timedEvent.Trigger
		} ) );
	}
}
