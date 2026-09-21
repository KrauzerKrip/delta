/// <summary>
/// Drives cursor-based world interaction for the local player.
/// </summary>
public sealed class InteractionController : Component
{
	private const float TooltipHoverDelay = 1f;

	[Property, Range( 1f, 2048f ), Group( "Interaction" )]
	public float InteractionDistance { get; set; } = 256f;

	[Property, Group( "Outline" )]
	public Color OutOfRangeOutlineColor { get; set; } = new( 0.494f, 0.792f, 0.886f, 0.08f );

	[Property, Group( "Outline" )]
	public Color InRangeOutlineColor { get; set; } = new( 0.494f, 0.792f, 0.886f, 0.50f );

	[Property, Group( "Outline" )]
	public Color HoveredOutlineColor { get; set; } = new( 0.494f, 0.792f, 0.886f, 0.85f );

	[Property, Group( "Outline" )]
	public Color ObscuredOutlineColor { get; set; } = Color.Transparent;

	[Property, Group( "Outline" )]
	public Color InsideOutlineColor { get; set; } = Color.Transparent;

	[Property, Group( "Outline" )]
	public Color InsideObscuredOutlineColor { get; set; } = Color.Transparent;

	[Property, Range( 0f, 16f ), Group( "Outline" )]
	public float OutOfRangeOutlineWidth { get; set; } = 0.75f;

	[Property, Range( 0f, 16f ), Group( "Outline" )]
	public float InRangeOutlineWidth { get; set; } = 1f;

	[Property, Range( 0f, 16f ), Group( "Outline" )]
	public float HoveredOutlineWidth { get; set; } = 3f;

	[Property, Range( 0f, 30f ), Group( "Outline" )]
	public float OutlineTransitionSpeed { get; set; } = 12f;

	public Component CursorPressable { get; private set; }
	public bool CursorPressableIsInRange { get; private set; }
	public bool ShouldShowUseGlyph { get; private set; }
	public float HoldProgress => (playerController?.Pressed as IHoldProgressProvider)?.HoldProgress ?? 0f;
	public string UseButton => playerController?.UseButton ?? "use";

	private PlayerController playerController;
	private Component previousCursorPressable;
	private float cursorHoverTime;
	private bool restoreLookControls;

	protected override void OnStart()
	{
		playerController = Components.Get<PlayerController>();
		if ( playerController is null )
		{
			Log.Warning( $"{nameof( InteractionController )} requires a {nameof( PlayerController )}." );
			return;
		}

		restoreLookControls = playerController.UseLookControls;
		playerController.UseLookControls = false;
		UpdatePlayerInteraction();
	}

	protected override void OnUpdate()
	{
		UpdatePlayerInteraction();
	}

	public bool IsInRange( Component pressable )
	{
		if ( playerController is null || pressable is null )
			return false;

		var playerPosition = playerController.EyePosition;
		var closestDistance = float.MaxValue;
		foreach ( var collider in pressable.GameObject.GetComponentsInChildren<Collider>( false, true ) )
		{
			var closestPoint = collider.GetWorldBounds().ClosestPoint( playerPosition );
			closestDistance = System.MathF.Min(
				closestDistance,
				(closestPoint - playerPosition).Length
			);
		}

		if ( closestDistance == float.MaxValue )
			closestDistance = (pressable.WorldPosition - playerPosition).Length;

		return closestDistance <= InteractionDistance;
	}

	private void UpdatePlayerInteraction()
	{
		playerController ??= Components.Get<PlayerController>();
		if ( playerController is null )
			return;

		playerController.ReachLength = InteractionDistance;

		if ( !TryGetCursorHit( out var hitPosition, out var hitObject ) )
		{
			SetCursorPressable( null, false );
			playerController.Hovered = null;
			playerController.Tooltips.Clear();

			if ( playerController.Pressed is not null )
				playerController.UpdateLookAt();

			return;
		}

		var cursorPressable = FindPressable( hitObject );
		var isInRange = cursorPressable is not null
			&& (hitPosition - playerController.EyePosition).Length <= InteractionDistance;
		SetCursorPressable( cursorPressable, isInRange );

		var lookDirection = hitPosition - playerController.EyePosition;
		if ( lookDirection.LengthSquared <= 0.001f )
			return;

		playerController.EyeAngles = Rotation.LookAt( lookDirection.Normal, Vector3.Up );
		playerController.UpdateLookAt();
	}

	private void SetCursorPressable( Component pressable, bool isInRange )
	{
		CursorPressable = pressable;
		CursorPressableIsInRange = isInRange;

		if ( pressable is null || !isInRange || pressable != previousCursorPressable )
			cursorHoverTime = 0f;
		else
			cursorHoverTime += Time.Delta;

		previousCursorPressable = pressable;
		ShouldShowUseGlyph = pressable is not null
			&& isInRange
			&& cursorHoverTime >= TooltipHoverDelay;
	}

	private Component FindPressable( GameObject hitObject )
	{
		if ( hitObject is null )
			return null;

		foreach ( var pressable in hitObject.GetComponentsInParent<Component.IPressable>( false, true ) )
		{
			if ( pressable is Component component
				&& component.Active
				&& pressable.CanPress( new Component.IPressable.Event( playerController, null ) ) )
				return component;
		}

		return null;
	}

	private bool TryGetCursorHit( out Vector3 hitPosition, out GameObject hitObject )
	{
		hitPosition = default;
		hitObject = null;

		if ( Scene.Camera is null )
			return false;

		var cursorRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var trace = Scene.Trace
			.Ray( cursorRay, 100000f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "glass" )
			.Run();

		if ( !trace.Hit )
			return false;

		hitPosition = trace.HitPosition;
		hitObject = trace.GameObject;
		return true;
	}

	protected override void OnDisabled()
	{
		if ( playerController is not null )
			playerController.UseLookControls = restoreLookControls;
	}
}
