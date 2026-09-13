/// <summary>
/// Activates a timed minigame button when the controlled pliers enter its trigger.
/// Attach this component beside the trigger collider inside the moving button part.
/// </summary>
public sealed class PliersButtonGoal : Component, Component.ITriggerListener
{
	[Property, Group( "Setup" )]
	public PliersMinigameController Minigame { get; set; }

	[Property, Group( "Setup" )]
	public GameObject MovingPart { get; set; }

	[Property, Group( "Setup" )]
	public PointLight Indicator { get; set; }

	[Property, Group( "Button" ), Range( 0f, 32f )]
	public float PressDistance { get; set; } = 2.120f;

	[Property, Group( "Button" ), Range( 0.01f, 2f )]
	public float MoveDuration { get; set; } = 0.12f;

	[Property, Group( "Button" ), Range( 0f, 60f )]
	public float ActiveDuration { get; set; } = 3f;

	public bool IsActivated { get; private set; }

	private Vector3 releasedPosition;
	private Vector3 activatedPosition;
	private Vector3 moveStartPosition;
	private Vector3 moveTargetPosition;
	private float moveElapsed;
	private float activeTimeRemaining;
	private bool isMoving;
	private bool initialized;

	protected override void OnStart()
	{
		var collider = Components.Get<Collider>();
		if ( collider is null )
			Log.Warning( $"[Pliers Button] Trigger '{GameObject.Name}' has no Collider component." );
		else if ( !collider.IsTrigger )
			Log.Warning( $"[Pliers Button] Collider on '{GameObject.Name}' is not marked as a trigger." );

		if ( Minigame is null )
			Log.Warning( $"[Pliers Button] Trigger '{GameObject.Name}' has no minigame assigned." );

		if ( MovingPart is null )
		{
			Log.Warning( $"[Pliers Button] Trigger '{GameObject.Name}' has no moving part assigned." );
			return;
		}

		if ( Indicator is null )
			Log.Warning( $"[Pliers Button] Trigger '{GameObject.Name}' has no indicator light assigned." );

		releasedPosition = MovingPart.LocalPosition;
		activatedPosition = releasedPosition + Vector3.Up * System.MathF.Max( PressDistance, 0f );
		MovingPart.LocalPosition = releasedPosition;
		SetIndicatorColor( Color.Red );
		initialized = true;
	}

	protected override void OnUpdate()
	{
		if ( !initialized )
			return;

		UpdateMovement();

		if ( !IsActivated )
			return;

		activeTimeRemaining = System.MathF.Max( activeTimeRemaining - Time.Delta, 0f );
		if ( activeTimeRemaining <= 0f )
			Deactivate();
	}

	protected override void OnDisabled()
	{
		if ( !initialized || MovingPart is null )
			return;

		IsActivated = false;
		isMoving = false;
		MovingPart.LocalPosition = releasedPosition;
		SetIndicatorColor( Color.Red );
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( !initialized || IsActivated || other is null )
			return;

		if ( Minigame is null || !Minigame.IsPliersCollider( other ) )
			return;

		Activate();
	}

	public void OnTriggerExit( Collider other )
	{
	}

	private void Activate()
	{
		IsActivated = true;
		activeTimeRemaining = System.MathF.Max( ActiveDuration, 0f );
		BeginMove( activatedPosition );
		SetIndicatorColor( Color.Green );
		Minigame?.NotifyButtonEngaged();
	}

	private void Deactivate()
	{
		IsActivated = false;
		BeginMove( releasedPosition );
		SetIndicatorColor( Color.Red );
		Minigame?.NotifyButtonBlownOff();
	}

	private void BeginMove( Vector3 targetPosition )
	{
		moveStartPosition = MovingPart.LocalPosition;
		moveTargetPosition = targetPosition;
		moveElapsed = 0f;
		isMoving = true;
	}

	private void UpdateMovement()
	{
		if ( !isMoving || MovingPart is null )
			return;

		var duration = System.MathF.Max( MoveDuration, 0.001f );
		moveElapsed = System.MathF.Min( moveElapsed + Time.Delta, duration );
		var progress = moveElapsed / duration;
		var easedProgress = progress * progress * (3f - 2f * progress);
		MovingPart.LocalPosition = Vector3.Lerp( moveStartPosition, moveTargetPosition, easedProgress );

		if ( moveElapsed >= duration )
			isMoving = false;
	}

	private void SetIndicatorColor( Color color )
	{
		if ( Indicator is not null )
			Indicator.LightColor = color;
	}
}
