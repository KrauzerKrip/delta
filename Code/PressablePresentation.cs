/// <summary>
/// Provides opt-in outline feedback for a pressable object.
/// </summary>
public sealed class PressablePresentation : Component
{
	[Property, Group( "Setup" )]
	public Component Pressable { get; set; }

	[Property, Group( "Setup" )]
	public List<Renderer> OutlineTargets { get; set; } = new();

	private HighlightOutline outline;
	private InteractionController interaction;
	private Color currentColor;
	private float currentWidth;

	protected override void OnStart()
	{
		Pressable ??= Components.GetAll().FirstOrDefault( component => component is Component.IPressable );
		if ( Pressable is not Component.IPressable )
			Log.Warning( $"{nameof( PressablePresentation )} on '{GameObject.Name}' requires a pressable component." );

		outline = Components.Get<HighlightOutline>() ?? GameObject.AddComponent<HighlightOutline>();
		outline.OverrideTargets = true;
		outline.Targets = OutlineTargets;
		EnsureCameraHighlight();
	}

	protected override void OnUpdate()
	{
		EnsureCameraHighlight();
		interaction ??= Scene.GetAllComponents<InteractionController>().FirstOrDefault();

		if ( outline is null || interaction is null || !IsAvailable() )
		{
			if ( outline is not null )
				outline.Enabled = false;

			return;
		}

		outline.Enabled = true;
		var isInRange = interaction.IsInRange( Pressable );
		var isHovered = interaction.CursorPressable == Pressable;
		var targetColor = isInRange
			? (isHovered ? interaction.HoveredOutlineColor : interaction.InRangeOutlineColor)
			: interaction.OutOfRangeOutlineColor;
		var targetWidth = isHovered
			? interaction.HoveredOutlineWidth
			: (isInRange ? interaction.InRangeOutlineWidth : interaction.OutOfRangeOutlineWidth);
		var blend = 1f - System.MathF.Exp( -interaction.OutlineTransitionSpeed * Time.Delta );

		currentColor = Color.Lerp( currentColor, targetColor, blend );
		currentWidth += (targetWidth - currentWidth) * blend;
		ApplyOutline();
	}

	private bool IsAvailable()
	{
		if ( Pressable is null || !Pressable.Active )
			return false;

		if ( Pressable is HoldPressable holdPressable )
			return holdPressable.IsAvailable;

		return Pressable is Component.IPressable pressable
			&& pressable.CanPress( new Component.IPressable.Event( interaction, null ) );
	}

	private void ApplyOutline()
	{
		outline.Color = currentColor;
		outline.Width = currentWidth;
		outline.ObscuredColor = interaction.ObscuredOutlineColor;
		outline.InsideColor = interaction.InsideOutlineColor;
		outline.InsideObscuredColor = interaction.InsideObscuredOutlineColor;
	}

	private void EnsureCameraHighlight()
	{
		if ( Scene.Camera is not null )
			_ = Scene.Camera.Components.Get<Highlight>()
				?? Scene.Camera.GameObject.AddComponent<Highlight>();
	}

	protected override void OnDisabled()
	{
		if ( outline is not null )
			outline.Enabled = false;
	}
}
