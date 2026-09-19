/// <summary>Provides the single death entry point for Marie and an optional debug shortcut.</summary>
public sealed class HeroineDeathController : Component
{
	[Property, Group( "Death" )]
	public RunTimeline RunTimeline { get; set; }

	[Property, Group( "Respawn" )]
	public GameObject HeroineSpawn { get; set; }

	[Property, Group( "Debug" )]
	public bool DebugDeathEnabled { get; set; } = true;

	[Property, Group( "Debug" )]
	public string DebugDeathAction { get; set; } = "DebugDeath";

	public bool IsDead { get; private set; }
	public bool HasInfiniteLives => RunTimeline?.IsRunning == true;

	private bool warnedAboutMissingTimeline;
	private bool warnedAboutMissingSpawn;

	protected override void OnStart()
	{
		ResolveTimeline();
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy || !DebugDeathEnabled || IsDead )
			return;

		if ( !string.IsNullOrWhiteSpace( DebugDeathAction ) && Input.Pressed( DebugDeathAction ) )
			Die();
	}

	/// <summary>Marks the electrocution as Marie's loop anchor and starts the first run at T+0.</summary>
	public bool AcquireInfiniteLives()
	{
		if ( HasInfiniteLives )
			return false;

		if ( !TryResolveTimeline() )
			return false;

		RunTimeline.StartRun();
		Log.Info( $"[Heroine Death] '{GameObject.Name}' acquired infinite lives; the loop is anchored to the electrocution." );
		return true;
	}

	/// <summary>Kills Marie and rewinds her to the electrocution that began the loop.</summary>
	public bool Die()
	{
		if ( IsDead )
			return false;

		if ( !TryResolveTimeline() )
			return false;

		if ( !HasInfiniteLives )
		{
			Log.Warning( $"[Heroine Death] '{GameObject.Name}' cannot rewind before acquiring infinite lives." );
			return false;
		}

		if ( !RunTimeline.RestartRun() )
			return false;

		IsDead = true;
		Log.Info( $"[Heroine Death] '{GameObject.Name}' died; resetting the run." );
		return true;
	}

	/// <summary>Places Marie at the loop spawn and restores the items she had at the anchor point.</summary>
	internal bool PrepareLoopRespawn()
	{
		if ( HeroineSpawn is null )
		{
			if ( !warnedAboutMissingSpawn )
			{
				Log.Warning( $"[Heroine Death] '{GameObject.Name}' has no HeroineSpawn assigned; using the authored player transform." );
				warnedAboutMissingSpawn = true;
			}
			return false;
		}

		GameObject.WorldPosition = HeroineSpawn.WorldPosition;
		GameObject.WorldRotation = HeroineSpawn.WorldRotation;

		var playerController = Components.Get<PlayerController>();
		if ( playerController is not null )
		{
			playerController.EyeAngles = HeroineSpawn.WorldRotation;
			playerController.WishVelocity = Vector3.Zero;
		}

		var rigidbody = Components.Get<Rigidbody>();
		if ( rigidbody is not null )
		{
			rigidbody.Velocity = Vector3.Zero;
			rigidbody.AngularVelocity = Vector3.Zero;
		}

		EnsureRespawnInventory();
		return true;
	}

	private void ResolveTimeline()
	{
		RunTimeline ??= Scene.GetAllComponents<RunTimeline>().FirstOrDefault();
	}

	private bool TryResolveTimeline()
	{
		ResolveTimeline();
		if ( RunTimeline is not null )
			return true;

		if ( !warnedAboutMissingTimeline )
		{
			Log.Warning( $"[Heroine Death] '{GameObject.Name}' has no RunTimeline available." );
			warnedAboutMissingTimeline = true;
		}

		return false;
	}

	private void EnsureRespawnInventory()
	{
		var inventory = Components.Get<PlayerInventory>();
		if ( inventory is null )
		{
			Log.Warning( $"[Heroine Death] '{GameObject.Name}' has no PlayerInventory for respawn items." );
			return;
		}

		var items = inventory.GetHudItems().ToArray();
		if ( !items.OfType<KeyCard>().Any( card => card.AccessLevel == AccessLevel.Maintenance ) )
		{
			inventory.GiveItem<KeyCard>(
				card => card.AccessLevel = AccessLevel.Maintenance,
				slot: 0 );
		}

		if ( !items.OfType<WorkOrder>().Any() )
			inventory.GiveItem<WorkOrder>( slot: 1 );
	}
}
