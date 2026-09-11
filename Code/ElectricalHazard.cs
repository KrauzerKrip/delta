/// <summary>
/// Reacts when a controlled pliers collider touches an electrical obstacle.
/// Place this component beside the obstacle collider or trigger.
/// </summary>
public sealed class ElectricalHazard : Component, Component.ITriggerListener, Component.ICollisionListener
{
	[Property, Group( "Effects" )]
	public ParticleEffect SparkEffect { get; set; }

	[Property, Group( "Effects" ), Range( 0.01f, 1f )]
	public float SparkVisibleDuration { get; set; } = 0.1f;

	[Property, Group( "Effects" )]
	public Angles SparkRotationOffset { get; set; }

	[Property, Group( "Gizmo" )]
	public bool ShowSparkDirectionGizmo { get; set; }

	[Property, Group( "Gizmo" ), Range( 8f, 256f )]
	public float SparkDirectionGizmoLength { get; set; } = 48f;

	[Property, Group( "Gizmo" )]
	public bool ShowContactPointGizmo { get; set; }

	[Property, Group( "Gizmo" ), Range( 0.5f, 16f )]
	public float ContactPointGizmoRadius { get; set; } = 3f;

	[Property, Group( "Reaction" ), Range( 0f, 64f )]
	public float RecoilDistance { get; set; } = 12f;

	[Property, Group( "Reaction" ), Range( 0f, 500f )]
	public float RecoilSpeed { get; set; } = 160f;

	[Property, Group( "Reaction" ), Range( 0f, 1f )]
	public float ControlLockDuration { get; set; } = 0.18f;

	[Property, Group( "Reaction" ), Range( 0f, 3f )]
	public float TremorDuration { get; set; } = 0.75f;

	[Property, Group( "Reaction" ), Range( 1f, 12f )]
	public float TremorMultiplier { get; set; } = 4f;

	[Property, Group( "Reaction" ), Range( 0f, 2f )]
	public float ContactCooldown { get; set; } = 0.35f;

	private float cooldownRemaining;
	private float sparkTimeRemaining;
	private Vector3 lastContactPoint;
	private bool hasContactPoint;

	protected override void DrawGizmos()
	{
		if ( (!ShowSparkDirectionGizmo || SparkEffect is null) && (!ShowContactPointGizmo || !hasContactPoint) )
			return;

		using ( Gizmo.Scope() )
		{
			// Draw in world space so the markers remain visible even while the effect is disabled.
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.LineThickness = 2f;

			if ( ShowSparkDirectionGizmo && SparkEffect is not null )
			{
				var origin = SparkEffect.WorldPosition;
				var length = System.MathF.Max( SparkDirectionGizmoLength, 8f );
				var direction = SparkEffect.WorldRotation.Forward;
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.Arrow( origin, origin + direction * length, length * 0.25f, length * 0.08f );
			}

			if ( ShowContactPointGizmo && hasContactPoint )
			{
				Gizmo.Draw.Color = Color.Red;
				Gizmo.Draw.LineSphere( lastContactPoint, System.MathF.Max( ContactPointGizmoRadius, 0.5f ) );
			}
		}
	}

	protected override void OnStart()
	{
		if ( SparkEffect is null )
			Log.Warning( $"[Electrical Hazard] '{GameObject.Name}' has no spark particle effect assigned." );
		else
			SparkEffect.Enabled = false;
	}

	protected override void OnUpdate()
	{
		cooldownRemaining = System.MathF.Max( cooldownRemaining - Time.Delta, 0f );

		if ( sparkTimeRemaining <= 0f )
			return;

		sparkTimeRemaining = System.MathF.Max( sparkTimeRemaining - Time.Delta, 0f );
		if ( sparkTimeRemaining <= 0f && SparkEffect is not null )
			SparkEffect.Enabled = false;
	}


	protected override void OnDisabled()
	{
		sparkTimeRemaining = 0f;
		if ( SparkEffect is not null )
			SparkEffect.Enabled = false;
	}

	public void OnCollisionStart( Collision collision )
	{
		Log.Info( "CAT" );
		var outwardDirection = EstimateOutwardDirection( collision.Contact.Point, collision.Contact.Normal );
		TryReact( collision.Other.Collider, collision.Contact.Point, outwardDirection );
	}

	public void OnCollisionUpdate( Collision collision )
	{
	}

	public void OnCollisionStop( CollisionStop collision )
	{
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( other is null )
			return;

		var obstacleCollider = Components.Get<Collider>();
		if ( obstacleCollider is null )
			return;

		var contactPoint = EstimateTriggerContactPoint( obstacleCollider, other );
	
		var outwardDirection = EstimateOutwardDirection( contactPoint, other.WorldPosition - contactPoint );
		TryReact( other, contactPoint, outwardDirection );
	}

	public void OnTriggerExit( Collider other )
	{
	}

	private Vector3 EstimateTriggerContactPoint( Collider obstacleCollider, Collider other )
	{
		var hazardCenter = obstacleCollider.GetWorldBounds().Center;
		var radialDirection = other.WorldPosition - hazardCenter;
		if ( radialDirection.LengthSquared <= 0.0001f )
			return obstacleCollider.FindClosestPoint( other.WorldPosition );

		var traceDistance = obstacleCollider.GetWorldBounds().Size.Length + radialDirection.Length + 16f;
		var traceStart = hazardCenter + radialDirection.Normal * traceDistance;
		var hits = Scene.Trace
			.Ray( traceStart, hazardCenter )
			.HitTriggers()
			.UseHitPosition()
			.RunAll();

		foreach ( var hit in hits )
		{
			if ( hit.Collider == obstacleCollider )
				return hit.HitPosition;
		}

		// Query from outside the trigger so FindClosestPoint cannot return an interior point.
		return obstacleCollider.FindClosestPoint( traceStart );
	}

	private Vector3 EstimateOutwardDirection( Vector3 contactPoint, Vector3 fallbackDirection )
	{
		var obstacleCollider = Components.Get<Collider>();
		var hazardCenter = obstacleCollider is not null
			? obstacleCollider.GetWorldBounds().Center
			: GameObject.WorldPosition;
		var outwardDirection = contactPoint - hazardCenter;
		return outwardDirection.LengthSquared > 0.0001f
			? outwardDirection
			: fallbackDirection;
	}

	private void TryReact( Collider other, Vector3 contactPoint, Vector3 outwardDirection )
	{
		if ( cooldownRemaining > 0f || other is null )
			return;

		var controller = other.Components.Get<PliersMinigameController>( FindMode.InAncestors );
		if ( controller is null || !controller.IsPliersCollider( other ) )
			return;

		lastContactPoint = contactPoint;
		hasContactPoint = true;
		cooldownRemaining = System.MathF.Max( ContactCooldown, 0f );
		EmitSparks( contactPoint, outwardDirection );
		controller.RecoilFromDanger(
			contactPoint,
			RecoilDistance,
			RecoilSpeed,
			ControlLockDuration,
			TremorDuration,
			TremorMultiplier,
			Components.Get<Collider>()
		);
	}

	private void EmitSparks( Vector3 position, Vector3 outwardDirection )
	{
		if ( SparkEffect is null )
			return;

		SparkEffect.GameObject.Enabled = true;
		SparkEffect.Enabled = true;
		SparkEffect.WorldPosition = position;

		if ( outwardDirection.LengthSquared > 0.0001f )
		{
			var direction = outwardDirection.Normal;
			var up = System.MathF.Abs( Vector3.Dot( direction, Vector3.Up ) ) > 0.98f
				? Vector3.Right
				: Vector3.Up;
			SparkEffect.WorldRotation = Rotation.LookAt( direction, up )
				* SparkRotationOffset.ToRotation();
		}

		SparkEffect.ResetEmitters();
		sparkTimeRemaining = System.MathF.Max( SparkVisibleDuration, 0.01f );
	}
}
