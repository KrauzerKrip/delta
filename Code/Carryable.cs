/// <summary>
/// A physical world object that can be picked up by a <see cref="Carrier"/> after holding Use.
/// </summary>
public sealed class Carryable : Component, Component.IPressable, IHoldProgressProvider
{
	[Property, Group( "Setup" )]
	public GameObject RightAnchor { get; set; }

	[Property, Group( "Setup" )]
	public GameObject LeftAnchor { get; set; }

	[Property, Group( "Setup" )]
	public Rigidbody Rigidbody { get; set; }

	[Property, Group( "Setup" )]
	public List<Collider> Colliders { get; set; } = new();

	[Property, Range( 0.1f, 30f ), Group( "Interaction" )]
	public float HoldDuration { get; set; } = 1f;

	public bool IsCarried => Carrier is not null;
	public Carrier Carrier { get; private set; }
	public float HoldProgress => IsCarried
		? 0f
		: (holdTime / System.MathF.Max( HoldDuration, 0.01f )).Clamp( 0f, 1f );

	private readonly Dictionary<Collider, bool> colliderEnabledStates = new();
	private GameObject originalParent;
	private Carrier pendingCarrier;
	private float holdTime;
	private bool isHolding;
	private bool rigidbodyWasEnabled;
	private bool rigidbodyGravity;
	private bool rigidbodyMotionEnabled;

	protected override void OnStart()
	{
		Rigidbody ??= Components.Get<Rigidbody>();

		if ( Colliders.Count == 0 )
			Colliders = GameObject.GetComponentsInChildren<Collider>( true, true ).ToList();

		if ( RightAnchor is null )
			Log.Warning( $"{nameof( Carryable )} on '{GameObject.Name}' has no RightAnchor assigned." );

		if ( LeftAnchor is null )
			Log.Warning( $"{nameof( Carryable )} on '{GameObject.Name}' has no LeftAnchor assigned." );
	}

	void Component.IPressable.Hover( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Look( Component.IPressable.Event e )
	{
	}

	void Component.IPressable.Blur( Component.IPressable.Event e )
	{
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e )
	{
		return TryGetCarrier( e.Source, out var carrier ) && carrier.CanCarry( this );
	}

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e ) => null;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		if ( !TryGetCarrier( e.Source, out var carrier ) || !carrier.CanCarry( this ) )
			return false;

		pendingCarrier = carrier;
		holdTime = 0f;
		isHolding = true;
		return true;
	}

	bool Component.IPressable.Pressing( Component.IPressable.Event e )
	{
		if ( !isHolding || pendingCarrier is null || !pendingCarrier.CanCarry( this ) )
		{
			ResetHold();
			return false;
		}

		holdTime = System.MathF.Min( holdTime + Time.Delta, System.MathF.Max( HoldDuration, 0f ) );
		if ( holdTime < HoldDuration )
			return true;

		var carrier = pendingCarrier;
		ResetHold();
		carrier.TryCarry( this );
		return false;
	}

	void Component.IPressable.Release( Component.IPressable.Event e )
	{
		ResetHold();
	}

	internal bool BeginCarry( Carrier carrier )
	{
		if ( carrier is null || !carrier.CanCarry( this ) )
			return false;

		Rigidbody ??= Components.Get<Rigidbody>();
		originalParent = GameObject.Parent;
		colliderEnabledStates.Clear();

		foreach ( var collider in Colliders.Where( collider => collider is not null ) )
		{
			colliderEnabledStates[collider] = collider.Enabled;
			collider.Enabled = false;
		}

		if ( Rigidbody is not null )
		{
			rigidbodyWasEnabled = Rigidbody.Enabled;
			rigidbodyGravity = Rigidbody.Gravity;
			rigidbodyMotionEnabled = Rigidbody.MotionEnabled;
			Rigidbody.Velocity = Vector3.Zero;
			Rigidbody.AngularVelocity = Vector3.Zero;
			Rigidbody.Enabled = false;
		}

		Carrier = carrier;
		if ( !carrier.TryGetCarryPose( this, out var attachment, out var anchor ) )
		{
			EndCarry( carrier );
			return false;
		}

		AttachTo( attachment, anchor );
		return true;
	}

	internal void AttachTo( GameObject attachment, GameObject anchor )
	{
		if ( anchor is null || attachment is null )
			return;

		if ( GameObject.Parent != attachment )
			GameObject.SetParent( attachment, true );

		var rotationDelta = attachment.WorldRotation * anchor.WorldRotation.Inverse;
		GameObject.WorldRotation = rotationDelta * GameObject.WorldRotation;
		GameObject.WorldPosition += attachment.WorldPosition - anchor.WorldPosition;
	}

	internal void EndCarry( Carrier carrier )
	{
		if ( Carrier != carrier )
			return;

		var parent = originalParent.IsValid() ? originalParent : null;
		GameObject.SetParent( parent, true );

		foreach ( var (collider, wasEnabled) in colliderEnabledStates )
		{
			if ( collider is not null )
				collider.Enabled = wasEnabled;
		}

		if ( Rigidbody is not null )
		{
			Rigidbody.Gravity = rigidbodyGravity;
			Rigidbody.MotionEnabled = rigidbodyMotionEnabled;
			Rigidbody.Enabled = rigidbodyWasEnabled;
			Rigidbody.Velocity = Vector3.Zero;
			Rigidbody.AngularVelocity = Vector3.Zero;
		}

		Carrier = null;
		originalParent = null;
		colliderEnabledStates.Clear();
	}

	protected override void OnDisabled()
	{
		ResetHold();
		Carrier?.Drop();
	}

	protected override void OnDestroy()
	{
		Carrier?.NotifyCarryableDestroyed( this );
	}

	private static bool TryGetCarrier( Component source, out Carrier carrier )
	{
		carrier = source?.Components.Get<Carrier>();
		return carrier is not null;
	}

	private void ResetHold()
	{
		holdTime = 0f;
		isHolding = false;
		pendingCarrier = null;
	}
}
