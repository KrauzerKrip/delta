using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Framework-independent state for a run timeline. Entries are fired in timestamp then authored order.
/// </summary>
internal sealed class RunTimelineState
{
	internal sealed class Entry
	{
		public float TriggerAtSeconds { get; init; }
		public int AuthoredOrder { get; init; }
		public System.Action Callback { get; init; }
	}

	private readonly List<Entry> entries = new();
	private int nextEntryIndex;

	public float ElapsedSeconds { get; private set; }
	public int RunNumber { get; private set; }
	public bool IsRunning { get; private set; }

	public void Configure( IEnumerable<Entry> configuredEntries )
	{
		entries.Clear();
		entries.AddRange( configuredEntries
			.Where( entry => entry is not null )
			.OrderBy( entry => System.MathF.Max( entry.TriggerAtSeconds, 0f ) )
			.ThenBy( entry => entry.AuthoredOrder ) );
		nextEntryIndex = 0;
	}

	public void StartRun()
	{
		ElapsedSeconds = 0f;
		nextEntryIndex = 0;
		RunNumber++;
		IsRunning = true;
		FireDueEntries();
	}

	public void Advance( float deltaSeconds )
	{
		if ( !IsRunning )
			return;

		ElapsedSeconds += System.MathF.Max( deltaSeconds, 0f );
		FireDueEntries();
	}

	private void FireDueEntries()
	{
		while ( nextEntryIndex < entries.Count )
		{
			var entry = entries[nextEntryIndex];
			if ( System.MathF.Max( entry.TriggerAtSeconds, 0f ) > ElapsedSeconds )
				return;

			nextEntryIndex++;
			entry.Callback?.Invoke();
		}
	}
}
