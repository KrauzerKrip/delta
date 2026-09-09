/// <summary>
/// Handles Marie's top-down movement and drives her animation graph.
/// </summary>
public sealed class MarieMovementController : Component
{
	private const string AnimationGraphPath = "animgraphs/marie.vanmgrph";
	private const string GroundMovementParameter = "ground_movement";
	private const string GroundedParameter = "grounded";
	private const string CrouchingParameter = "crouching";

	private enum GroundMovement
	{
		Idle,
		Walking,
		Running
	}

	[RequireComponent]
	public PlayerController Controller { get; set; }

	[Property, Group( "Animation" )]
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Group( "Animation" )]
	public AnimationGraph AnimationGraph { get; set; }

	[Property, Group( "Movement" ), Range( 0f, 100f )]
	public float AnimationIdleThreshold { get; set; } = 5f;

	[Property, Group( "Movement" ), Range( 0f, 30f )]
	public float TurnSpeed { get; set; } = 10f;

	protected override void OnStart()
	{
		Renderer ??= Controller.Renderer;

		if ( Renderer is null )
		{
			Log.Warning( $"{nameof( MarieMovementController )} requires a skinned model renderer." );
			return;
		}

		AnimationGraph ??= AnimationGraph.Load( AnimationGraphPath );
		Renderer.AnimationGraph = AnimationGraph;
		Renderer.UseAnimGraph = true;

		// The built-in animator targets Citizen parameters. Marie's graph is driven below.
		Controller.UseAnimatorControls = false;
		UpdateAnimationParameters();
	}

	protected override void OnFixedUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		if ( Components.Get<DialogueController>()?.IsOpen == true )
		{
			Controller.WishVelocity = Vector3.Zero;
			return;
		}

		var speed = Input.Down( "Run" ) ? Controller.RunSpeed : Controller.WalkSpeed;
		Controller.WishVelocity = Input.AnalogMove * speed;

		var movementDirection = new Vector3( Controller.WishVelocity.x, Controller.WishVelocity.y, 0f );
		if ( movementDirection.LengthSquared <= 0f )
			return;

		var targetRotation = Rotation.LookAt( movementDirection.Normal, Vector3.Up );
		var turnAmount = 1f - System.MathF.Exp( -TurnSpeed * Time.Delta );

		WorldRotation = Rotation.Slerp( WorldRotation, targetRotation, turnAmount );
		Controller.EyeAngles = Rotation.Slerp( Controller.EyeAngles, targetRotation, turnAmount );
	}

	protected override void OnUpdate()
	{
		UpdateAnimationParameters();
	}

	private void UpdateAnimationParameters()
	{
		if ( Renderer is null )
			return;

		Renderer.Set( GroundMovementParameter, (int)GetGroundMovement() );
		Renderer.Set( GroundedParameter, !Controller.IsAirborne );
		Renderer.Set( CrouchingParameter, Controller.IsDucking );
	}

	private GroundMovement GetGroundMovement()
	{
		var relativeVelocity = Controller.Velocity - Controller.GroundVelocity;
		var planarSpeed = new Vector3( relativeVelocity.x, relativeVelocity.y, 0f ).Length;

		if ( planarSpeed < AnimationIdleThreshold )
			return GroundMovement.Idle;

		var runThreshold = (Controller.WalkSpeed + Controller.RunSpeed) * 0.5f;
		return planarSpeed >= runThreshold
			? GroundMovement.Running
			: GroundMovement.Walking;
	}
}
