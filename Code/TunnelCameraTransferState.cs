/// <summary>Blocks an arrival from immediately transferring back before it leaves the entry volume.</summary>
internal sealed class TunnelCameraTransferState
{
	private bool arrivalBlocked;
	public bool IsArrivalBlocked => arrivalBlocked;

	public void MarkArrival() => arrivalBlocked = true;

	public bool CanTransfer( bool eligible, bool cameraSettled, bool reversing = false )
	{
		if ( !eligible )
			arrivalBlocked = false;
		else if ( reversing )
			arrivalBlocked = false;
		return eligible && cameraSettled && !arrivalBlocked;
	}

	internal static bool ShouldKeepAttempt( bool playerValid, bool isProxy, bool insideTrigger,
		bool arrivalBlocked, bool insideCloseCameraZone )
	{
		// Entering commits a normal attempt; it remains alive while the camera flies.
		// A destination arrival remains only while still inside its trigger.
		return playerValid && !isProxy && (!arrivalBlocked || insideTrigger);
	}

	internal static bool IsShotSettled( float x, float y, float z, bool followX, bool followY, bool followZ,
		float rotationError, float fieldOfViewError, float tolerance )
	{
		if ( !(followX && followY && followZ) )
		{
			if ( followX ) x = 0f;
			if ( followY ) y = 0f;
			if ( followZ ) z = 0f;
		}
		return System.MathF.Sqrt( x * x + y * y + z * z ) <= System.MathF.Max( tolerance, 0.01f ) &&
			rotationError <= 2f && System.MathF.Abs( fieldOfViewError ) <= 1f;
	}

}
