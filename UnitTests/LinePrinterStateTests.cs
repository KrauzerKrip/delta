using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

[TestClass]
public sealed class LinePrinterStateTests
{
	[TestMethod]
	public void QueuedLogsPrintInSubmissionOrder()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "FIRST", 80 );
		printer.EnqueueLog( "SECOND", 80 );

		printer.Advance( 1f, 100f, 24 );

		CollectionAssert.AreEqual( new[] { "FIRST", "SECOND" }, printer.CompletedRows.ToArray() );
	}

	[TestMethod]
	public void CharacterFeedUsesConfiguredRate()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "ABCD", 80 );

		printer.Advance( 0.5f, 4f, 24 );

		Assert.IsTrue( printer.HasActiveRow );
		Assert.AreEqual( "AB", printer.ActiveRow );
		Assert.AreEqual( 0, printer.CompletedRows.Count );
	}

	[TestMethod]
	public void NormalizesLineEndingsAndPreservesBlankRows()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "A\r\n\rB\n", 80 );

		printer.Advance( 1f, 100f, 24 );

		CollectionAssert.AreEqual( new[] { "A", "", "B", "" }, printer.CompletedRows.ToArray() );
	}

	[TestMethod]
	public void ExpandsTabsToFourColumnStops()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "A\tB\tC", 80 );

		printer.Advance( 1f, 100f, 24 );

		Assert.AreEqual( "A   B   C", printer.CompletedRows.Single() );
	}

	[TestMethod]
	public void HardWrapsAtConfiguredColumnWidth()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "ABCDEFGHI", 4 );

		printer.Advance( 1f, 100f, 24 );

		CollectionAssert.AreEqual( new[] { "ABCD", "EFGH", "I" }, printer.CompletedRows.ToArray() );
	}

	[TestMethod]
	public void EvictsOldestRowsPastVisibleLimit()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "ONE\nTWO\nTHREE", 80 );

		printer.Advance( 1f, 100f, 2 );

		CollectionAssert.AreEqual( new[] { "TWO", "THREE" }, printer.CompletedRows.ToArray() );
	}

	[TestMethod]
	public void ActiveRowCountsTowardVisibleLimit()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "ONE\nTWO", 80 );
		printer.Advance( 1f, 100f, 2 );
		printer.EnqueueLog( "THREE", 80 );

		printer.Advance( 0.1f, 10f, 2 );

		CollectionAssert.AreEqual( new[] { "TWO" }, printer.CompletedRows.ToArray() );
		Assert.AreEqual( "T", printer.ActiveRow );
	}

	[TestMethod]
	public void BurstDoesNotReplacePartiallyPrintedRow()
	{
		var printer = new LinePrinterState();
		printer.EnqueueLog( "FIRST", 80 );
		printer.Advance( 0.2f, 10f, 24 );
		printer.EnqueueLog( "SECOND", 80 );

		printer.Advance( 0.2f, 10f, 24 );

		Assert.AreEqual( "FIRS", printer.ActiveRow );
		Assert.AreEqual( 1, printer.QueuedRowCount );
	}
}
