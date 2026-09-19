using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

[TestClass]
public sealed class RunTimelineTests
{
	[TestMethod]
	public void StartRunFiresZeroSecondEventsImmediatelyInAuthoredOrder()
	{
		var fired = new List<int>();
		var timeline = CreateTimeline(
			(0f, 1, () => fired.Add( 1 )),
			(0f, 0, () => fired.Add( 0 )) );

		timeline.StartRun();

		CollectionAssert.AreEqual( new[] { 0, 1 }, fired );
	}

	[TestMethod]
	public void EventsFireChronologicallyAndOnlyOnce()
	{
		var fired = new List<int>();
		var timeline = CreateTimeline(
			(2f, 0, () => fired.Add( 2 )),
			(1f, 0, () => fired.Add( 1 )) );
		timeline.StartRun();

		timeline.Advance( 1f );
		timeline.Advance( 1f );
		timeline.Advance( 10f );

		CollectionAssert.AreEqual( new[] { 1, 2 }, fired );
	}

	[TestMethod]
	public void LargeFrameFiresEveryCrossedEvent()
	{
		var fired = new List<int>();
		var timeline = CreateTimeline(
			(1f, 0, () => fired.Add( 1 )),
			(2f, 1, () => fired.Add( 2 )),
			(3f, 2, () => fired.Add( 3 )) );
		timeline.StartRun();

		timeline.Advance( 3f );

		CollectionAssert.AreEqual( new[] { 1, 2, 3 }, fired );
	}

	[TestMethod]
	public void StartingAnotherRunRearmsAllEvents()
	{
		var fireCount = 0;
		var timeline = CreateTimeline( (0f, 0, () => fireCount++) );

		timeline.StartRun();
		timeline.StartRun();

		Assert.AreEqual( 2, fireCount );
		Assert.AreEqual( 2, timeline.RunNumber );
	}

	[TestMethod]
	public void ZeroDeltaDoesNotAdvanceScaledTimeline()
	{
		var fired = false;
		var timeline = CreateTimeline( (1f, 0, () => fired = true) );
		timeline.StartRun();

		timeline.Advance( 0f );

		Assert.AreEqual( 0f, timeline.ElapsedSeconds );
		Assert.IsFalse( fired );
	}

	[TestMethod]
	public void TimelineRemainsIdleUntilRunStarts()
	{
		var fired = false;
		var timeline = CreateTimeline( (0f, 0, () => fired = true) );

		timeline.Advance( 30f );

		Assert.IsFalse( timeline.IsRunning );
		Assert.IsFalse( fired );
	}

	private static RunTimelineState CreateTimeline(
		params (float Time, int Order, System.Action Callback)[] entries )
	{
		var timeline = new RunTimelineState();
		timeline.Configure( entries.Select( entry => new RunTimelineState.Entry
		{
			TriggerAtSeconds = entry.Time,
			AuthoredOrder = entry.Order,
			Callback = entry.Callback
		} ) );
		return timeline;
	}
}
