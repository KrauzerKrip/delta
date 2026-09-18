using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class TunnelCameraTransferTests
{
	[TestMethod]
	public void EnteringWaitsForCameraToSettle()
	{
		var state = new TunnelCameraTransferState();
		Assert.IsFalse( state.CanTransfer( true, false ) );
		Assert.IsTrue( state.CanTransfer( true, true ) );
	}

	[TestMethod]
	public void LeavingBeforeCameraSettlesCancelsTransfer()
	{
		var state = new TunnelCameraTransferState();
		Assert.IsFalse( state.CanTransfer( true, false ) );
		Assert.IsFalse( state.CanTransfer( false, true ) );
	}

	[TestMethod]
	public void ArrivalCannotBounceBackEvenWithSettledCamera()
	{
		var state = new TunnelCameraTransferState();
		state.MarkArrival();
		for ( var frame = 0; frame < 20; frame++ )
			Assert.IsFalse( state.CanTransfer( true, true ) );
	}

	[TestMethod]
	public void LeavingAndReenteringAllowsReturnTrip()
	{
		var state = new TunnelCameraTransferState();
		state.MarkArrival();
		Assert.IsFalse( state.CanTransfer( false, true ) );
		Assert.IsFalse( state.CanTransfer( true, false ) );
		Assert.IsTrue( state.CanTransfer( true, true ) );
	}

	[TestMethod]
	public void FollowingLagDoesNotBlockFixedTunnelShot()
	{
		Assert.IsTrue( TunnelCameraTransferState.IsShotSettled( 3f, 100f, 50f,
			false, true, true, 0f, 0f, 8f ) );
	}

	[TestMethod]
	public void UnsettledFixedAxisPreventsTransfer()
	{
		Assert.IsFalse( TunnelCameraTransferState.IsShotSettled( 20f, 0f, 0f,
			false, true, true, 0f, 0f, 8f ) );
	}

	[TestMethod]
	public void FullyFollowingShotStillRequiresPositionConvergence()
	{
		Assert.IsFalse( TunnelCameraTransferState.IsShotSettled( 20f, 0f, 0f,
			true, true, true, 0f, 0f, 8f ) );
	}

	[DataTestMethod]
	[DataRow( 3f, 0f )]
	[DataRow( 0f, 2f )]
	public void RotationAndFieldOfViewMustSettle( float rotation, float fieldOfView )
	{
		Assert.IsFalse( TunnelCameraTransferState.IsShotSettled( 0f, 0f, 0f,
			false, false, false, rotation, fieldOfView, 8f ) );
	}

}
