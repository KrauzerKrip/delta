/// <summary>
/// Holds the local player's world-interaction settings.
/// </summary>
public sealed class InteractionController : Component
{
	[Property, Range( 1f, 2048f ), Group( "Interaction" )]
	public float InteractionDistance { get; set; } = 160f;
}
