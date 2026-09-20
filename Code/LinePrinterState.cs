using System.Collections.Generic;

/// <summary>
/// Framework-independent queue and character feed for the line-printer projector.
/// </summary>
internal sealed class LinePrinterState
{
	private const int TabWidth = 4;

	private readonly Queue<string> queuedRows = new();
	private readonly List<string> completedRows = new();
	private string activeSource;
	private int activeCharacterCount;
	private float characterBudget;

	public IReadOnlyList<string> CompletedRows => completedRows;
	public string ActiveRow { get; private set; } = string.Empty;
	public bool HasActiveRow => activeSource is not null;
	public int QueuedRowCount => queuedRows.Count;
	public int Revision { get; private set; }
	public int FeedRevision { get; private set; }

	/// <summary>Queues a log after normalizing line endings, tabs, and fixed-width rows.</summary>
	public bool EnqueueLog( string text, int columnWidth )
	{
		if ( text is null )
			return false;

		var width = System.Math.Max( columnWidth, 1 );
		var normalized = text.Replace( "\r\n", "\n" ).Replace( '\r', '\n' );
		foreach ( var logicalLine in normalized.Split( '\n' ) )
		{
			var expanded = ExpandTabs( logicalLine );
			if ( expanded.Length == 0 )
			{
				queuedRows.Enqueue( string.Empty );
				continue;
			}

			for ( var offset = 0; offset < expanded.Length; offset += width )
			{
				var length = System.Math.Min( width, expanded.Length - offset );
				queuedRows.Enqueue( expanded.Substring( offset, length ) );
			}
		}

		Revision++;
		return true;
	}

	/// <summary>
	/// Advances the print head and returns how many physical rows completed during this step.
	/// </summary>
	public int Advance( float deltaSeconds, float charactersPerSecond, int maxVisibleRows )
	{
		var changed = false;
		var completedCount = 0;
		characterBudget += System.MathF.Max( deltaSeconds, 0f )
			* System.MathF.Max( charactersPerSecond, 1f );

		while ( true )
		{
			if ( activeSource is null )
			{
				if ( queuedRows.Count == 0 )
					break;

				activeSource = queuedRows.Dequeue();
				activeCharacterCount = 0;
				ActiveRow = string.Empty;
				changed = true;

				if ( activeSource.Length == 0 )
				{
					CompleteActiveRow( maxVisibleRows );
					completedCount++;
					continue;
				}

				TrimCompletedRows( System.Math.Max( maxVisibleRows - 1, 0 ) );
			}

			var availableCharacters = (int)characterBudget;
			if ( availableCharacters <= 0 )
				break;

			var remainingCharacters = activeSource.Length - activeCharacterCount;
			var printedCharacters = System.Math.Min( availableCharacters, remainingCharacters );
			activeCharacterCount += printedCharacters;
			characterBudget -= printedCharacters;
			ActiveRow = activeSource[..activeCharacterCount];
			changed = true;

			if ( activeCharacterCount < activeSource.Length )
				break;

			CompleteActiveRow( maxVisibleRows );
			completedCount++;
		}

		if ( activeSource is null && queuedRows.Count == 0 )
			characterBudget = 0f;

		if ( changed )
			Revision++;

		return completedCount;
	}

	private void CompleteActiveRow( int maxVisibleRows )
	{
		completedRows.Add( activeSource );
		TrimCompletedRows( System.Math.Max( maxVisibleRows, 1 ) );

		activeSource = null;
		activeCharacterCount = 0;
		ActiveRow = string.Empty;
		FeedRevision++;
	}

	private void TrimCompletedRows( int maximumRows )
	{
		while ( completedRows.Count > maximumRows )
			completedRows.RemoveAt( 0 );
	}

	private static string ExpandTabs( string line )
	{
		if ( !line.Contains( '\t' ) )
			return line;

		var expanded = new System.Text.StringBuilder( line.Length );
		foreach ( var character in line )
		{
			if ( character != '\t' )
			{
				expanded.Append( character );
				continue;
			}

			var spaces = TabWidth - expanded.Length % TabWidth;
			expanded.Append( ' ', spaces );
		}

		return expanded.ToString();
	}
}
