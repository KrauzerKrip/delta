/// <summary>
/// Makes selected mesh/model objects translucent while the local player occupies
/// this trigger. Collision stays enabled and original tints are restored on exit.
/// </summary>
public sealed class PlayerTranslucencyTrigger : Component, Component.ITriggerListener
{
	private const string TranslucentTag = "translucent";

	[Property]
	public List<GameObject> Targets { get; set; } = new();

	[Property]
	public bool IncludeChildren { get; set; } = true;

	[Property, Range( 0f, 1f )]
	public float Opacity { get; set; } = 0.15f;

	private readonly Dictionary<Collider, PlayerController> contacts = new();
	private readonly HashSet<Component> affectedTargets = new();
	private readonly HashSet<GameObject> affectedObjects = new();

	// Shared walls remain faded until the final occupied trigger releases them.
	private static readonly Dictionary<Component, TargetState> targetStates = new();
	private static readonly Dictionary<GameObject, ObjectTagState> objectTagStates = new();

	private sealed class TargetState
	{
		public Color OriginalTint;
		public readonly Dictionary<PlayerTranslucencyTrigger, float> Requests = new();
	}

	private sealed class ObjectTagState
	{
		public bool HadTranslucentTag;
		public readonly HashSet<PlayerTranslucencyTrigger> Requests = new();
	}

	protected override void OnStart()
	{
		if ( !GetComponents<Collider>().Any( collider => collider.IsTrigger ) )
			Log.Warning( $"[{nameof( PlayerTranslucencyTrigger )}] '{GameObject.Name}' needs a collider with Is Trigger enabled on the same object." );
	}

	public void OnTriggerEnter( Collider other )
	{
		var player = GetLocalPlayer( other );
		if ( player is null )
			return;
		contacts[other] = player;
		UpdateTargets();
	}

	public void OnTriggerExit( Collider other )
	{
		contacts.Remove( other );
		UpdateTargets();
	}

	protected override void OnUpdate()
	{
		// Contact lists also handle enabling while already occupied, destroyed
		// colliders, and departures by teleport rather than ordinary walking.
		contacts.Clear();
		foreach ( var trigger in GetComponents<Collider>().Where( collider => collider.Active && collider.IsTrigger ) )
		{
			foreach ( var other in trigger.Touching )
			{
				var player = GetLocalPlayer( other );
				if ( player is not null )
					contacts[other] = player;
			}
		}
		UpdateTargets();
	}

	private static PlayerController GetLocalPlayer( Collider other )
	{
		if ( !other.IsValid() || !other.Active )
			return null;
		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		return player.IsValid() && player.Active && !player.GameObject.IsProxy ? player : null;
	}

	private void UpdateTargets()
	{
		var desired = new HashSet<Component>();
		var desiredObjects = new HashSet<GameObject>();
		if ( contacts.Count > 0 && Targets is not null )
		{
			foreach ( var target in Targets.Where( target => target.IsValid() ) )
			{
				desiredObjects.Add( target );
				var components = target.GetComponents<Component>( includeDisabled: true );
				if ( IncludeChildren )
					components = components.Concat( target.GetComponentsInChildren<Component>( includeDisabled: true ) );
				foreach ( var component in components )
				{
					desiredObjects.Add( component.GameObject );
					if ( component is MeshComponent || component is ModelRenderer )
						desired.Add( component );
				}
			}
		}

		foreach ( var target in affectedTargets.Where( target => !desired.Contains( target ) ).ToArray() )
			Release( target );
		foreach ( var targetObject in affectedObjects.Where( target => !desiredObjects.Contains( target ) ).ToArray() )
			ReleaseTag( targetObject );

		foreach ( var target in desired )
		{
			if ( !targetStates.TryGetValue( target, out var state ) )
			{
				state = new TargetState { OriginalTint = GetTint( target ) };
				targetStates.Add( target, state );
			}
			state.Requests[this] = float.IsFinite( Opacity ) ? System.Math.Clamp( Opacity, 0f, 1f ) : 0.15f;
			affectedTargets.Add( target );
			Apply( target, state );
		}

		foreach ( var targetObject in desiredObjects )
			ApplyTag( targetObject );
	}

	private void ApplyTag( GameObject target )
	{
		if ( !objectTagStates.TryGetValue( target, out var state ) )
		{
			state = new ObjectTagState { HadTranslucentTag = target.Tags.Has( TranslucentTag ) };
			objectTagStates.Add( target, state );
		}

		state.Requests.Add( this );
		affectedObjects.Add( target );
		target.Tags.Add( TranslucentTag );
	}

	private static Color GetTint( Component target ) => target switch
	{
		MeshComponent mesh => mesh.Color,
		ModelRenderer model => model.Tint,
		_ => Color.White
	};

	private static void SetTint( Component target, Color tint )
	{
		if ( target is MeshComponent mesh ) mesh.Color = tint;
		else if ( target is ModelRenderer model ) model.Tint = tint;
	}

	private static void Apply( Component target, TargetState state )
	{
		if ( !target.IsValid() )
			return;
		var opacity = state.Requests.Count > 0 ? state.Requests.Values.Min() : 1f;
		SetTint( target, state.OriginalTint.WithAlphaMultiplied( opacity ) );
	}

	private void Release( Component target )
	{
		affectedTargets.Remove( target );
		if ( !targetStates.TryGetValue( target, out var state ) )
			return;
		state.Requests.Remove( this );
		Apply( target, state );
		if ( state.Requests.Count == 0 )
			targetStates.Remove( target );
	}

	private void ReleaseTag( GameObject target )
	{
		affectedObjects.Remove( target );
		if ( !objectTagStates.TryGetValue( target, out var state ) )
			return;

		state.Requests.Remove( this );
		if ( state.Requests.Count > 0 )
			return;

		if ( target.IsValid() && !state.HadTranslucentTag )
			target.Tags.Remove( TranslucentTag );
		objectTagStates.Remove( target );
	}

	private void RestoreTargets()
	{
		foreach ( var target in affectedTargets.ToArray() )
			Release( target );
		foreach ( var targetObject in affectedObjects.ToArray() )
			ReleaseTag( targetObject );
		contacts.Clear();
	}

	protected override void OnDisabled() => RestoreTargets();
	protected override void OnDestroy() => RestoreTargets();
}
