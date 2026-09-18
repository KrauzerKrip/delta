/// <summary>Blocks an arrival from immediately transferring back before it leaves the entry volume.</summary>
internal sealed class TunnelCameraTransferState
{
	private bool arrivalBlocked;

	public void MarkArrival() => arrivalBlocked = true;

	public bool CanTransfer( bool insideTrigger, bool cameraSettled )
	{
		if ( !insideTrigger )
			arrivalBlocked = false;
		return insideTrigger && cameraSettled && !arrivalBlocked;
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
