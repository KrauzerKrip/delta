/// <summary>
/// Exposes progress for interactions that complete while Use is held.
/// </summary>
public interface IHoldProgressProvider
{
	float HoldProgress { get; }
}
