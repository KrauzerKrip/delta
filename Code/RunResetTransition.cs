/// <summary>Keeps the electrocution flash alive while the authored run scene is reloaded.</summary>
public sealed class RunResetTransition : Component
{
	private static bool resetActive;

	private enum TransitionPhase
	{
		Loading,
		Holding,
		Fading
	}

	public string ScenePath { get; set; }
	public float HoldDuration { get; set; }
	public float FadeDuration { get; set; }
	public float MarieYaw { get; set; }
	public float OverlayOpacity { get; private set; } = 1f;

	private TransitionPhase phase;
	private float holdElapsed;
	private float fadeElapsed;
	private bool warnedAboutTimeline;
	private PlayerController playerController;
	private MarieMovementController movementController;
	private bool previousPlayerInputEnabled;
	private bool previousMovementInputEnabled;
	private bool controlsSuppressed;

	public static bool Begin( string scenePath, float holdDuration, float fadeDuration, float marieYaw )
	{
		if ( resetActive )
		{
			Log.Warning( "[Run Timeline] Ignored a duplicate restart while a scene reset is already in progress." );
			return false;
		}

		resetActive = true;
		var transitionObject = new GameObject( true, "Run Reset Transition" )
		{
			Flags = GameObjectFlags.DontDestroyOnLoad,
			NetworkMode = NetworkMode.Never
		};
		var transition = transitionObject.AddComponent<RunResetTransition>();
		transition.ScenePath = scenePath;
		transition.HoldDuration = System.MathF.Max( holdDuration, 0f );
		transition.FadeDuration = System.MathF.Max( fadeDuration, 0f );
		transition.MarieYaw = marieYaw;
		return true;
	}

	protected override void OnStart()
	{
		var screen = GameObject.AddComponent<ScreenPanel>();
		screen.ZIndex = 10000;
		var hud = GameObject.AddComponent<FlashbangHud>();
		hud.ResetTransition = this;
		SuppressControls();

		var options = new SceneLoadOptions
		{
			ShowLoadingScreen = false
		};
		if ( !options.SetScene( ScenePath ) || !Game.ActiveScene.Load( options ) )
		{
			Log.Warning( $"[Run Timeline] Failed to begin loading reset scene '{ScenePath}'." );
			GameObject.Destroy();
		}
	}

	protected override void OnUpdate()
	{
		if ( phase == TransitionPhase.Loading )
		{
			OverlayOpacity = 1f;
			if ( Game.ActiveScene.IsLoading )
				return;

			var timelines = Game.ActiveScene.GetAllComponents<RunTimeline>().ToArray();
			if ( timelines.Length == 0 )
			{
				if ( !warnedAboutTimeline )
				{
					Log.Warning( $"[Run Timeline] Reset scene '{ScenePath}' contains no RunTimeline component." );
					warnedAboutTimeline = true;
				}
			}
			else if ( timelines.Length > 1 )
				Log.Warning( $"[Run Timeline] Reset scene '{ScenePath}' contains multiple timelines; the first will be started." );

			SuppressControls();
			var deathController = Game.ActiveScene.GetAllComponents<HeroineDeathController>()
				.FirstOrDefault( controller => !controller.GameObject.IsProxy );
			if ( deathController?.PrepareLoopRespawn() != true )
				movementController?.FaceYaw( MarieYaw );
			movementController?.SetLying( true );
			Sound.Play( "sounds/electric_shock.sound" );
			timelines.FirstOrDefault()?.StartRun();
			holdElapsed = 0f;
			phase = TransitionPhase.Holding;
		}

		if ( phase == TransitionPhase.Holding )
		{
			OverlayOpacity = 1f;
			holdElapsed += Time.Delta;
			if ( holdElapsed < HoldDuration )
				return;

			movementController?.SetLying( false );
			RestoreControls();
			phase = TransitionPhase.Fading;
			fadeElapsed = 0f;
		}

		if ( FadeDuration <= 0f )
		{
			Complete();
			return;
		}

		fadeElapsed += Time.Delta;
		OverlayOpacity = (1f - fadeElapsed / FadeDuration).Clamp( 0f, 1f );
		if ( fadeElapsed >= FadeDuration )
			Complete();
	}

	protected override void OnDestroy()
	{
		movementController?.SetLying( false );
		RestoreControls();
		resetActive = false;
	}

	private void SuppressControls()
	{
		playerController = Game.ActiveScene.GetAllComponents<PlayerController>()
			.FirstOrDefault( controller => !controller.GameObject.IsProxy );
		movementController = playerController?.Components.Get<MarieMovementController>();

		if ( playerController is not null )
		{
			previousPlayerInputEnabled = playerController.UseInputControls;
			playerController.UseInputControls = false;
			playerController.WishVelocity = Vector3.Zero;
		}

		if ( movementController is not null )
		{
			previousMovementInputEnabled = movementController.MovementInputEnabled;
			movementController.MovementInputEnabled = false;
		}

		controlsSuppressed = true;
	}

	private void RestoreControls()
	{
		if ( !controlsSuppressed )
			return;

		if ( playerController is not null )
		{
			playerController.UseInputControls = previousPlayerInputEnabled;
			playerController.WishVelocity = Vector3.Zero;
		}

		if ( movementController is not null )
			movementController.MovementInputEnabled = previousMovementInputEnabled;

		controlsSuppressed = false;
	}

	private void Complete()
	{
		RestoreControls();
		OverlayOpacity = 0f;
		GameObject.Destroy();
	}
}
