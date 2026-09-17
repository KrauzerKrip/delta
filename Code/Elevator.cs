/// <summary>
/// Moves a cargo platform between two stationary endpoint objects.
/// Attach to the platform root with a Rigidbody and solid colliders, assign PointA
/// and PointB, then call Toggle() from code or an editor action graph.
/// Endpoint world positions are captured at startup; only translation is changed.
/// </summary>
public sealed class Elevator : Component
{
	[RequireComponent]
	public Rigidbody Rigidbody { get; set; }

	[Property]
	public GameObject PointA { get; set; }

	[Property]
	public GameObject PointB { get; set; }

	/// <summary>Travel speed in world units per second. Nonpositive values hold position.</summary>
	[Property]
	public float Speed { get; set; } = 100f;

	public bool IsMoving { get; private set; }

	private Vector3 positionA;
	private Vector3 positionB;
	private bool initialized;
	private bool endpointsReady;
	private bool hasDestination;
	private bool targetB;

	protected override void OnStart()
	{
		Initialize();
		if ( !GameObject.IsProxy )
			StopBody();
	}

	/// <summary>Starts travel or reverses the destination immediately during travel.</summary>
	public void Toggle()
	{
		if ( !Enabled || GameObject.IsProxy )
			return;

		Initialize();
		if ( !ValidateEndpoints() )
			return;

		targetB = hasDestination
			? !targetB
			: (positionB - WorldPosition).LengthSquared >= (positionA - WorldPosition).LengthSquared;
		hasDestination = true;
		IsMoving = true;
	}

	protected override void OnFixedUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		if ( !IsMoving || !ValidateEndpoints() || !float.IsFinite( Speed ) || Speed <= 0f )
		{
			StopBody();
			return;
		}

		var body = Rigidbody.IsValid() ? Rigidbody.PhysicsBody : null;
		if ( body is null || Time.Delta <= 0f )
			return;

		body.BodyType = PhysicsBodyType.Keyframed;
		var destination = targetB ? positionB : positionA;
		var offset = destination - WorldPosition;
		var distance = offset.Length;
		var step = Speed * Time.Delta;
		var arrived = distance <= step;
		var transform = WorldTransform;
		transform.Position = arrived ? destination : WorldPosition + offset / distance * step;
		body.Move( transform, Time.Delta );
		IsMoving = !arrived;
	}

	protected override void OnDisabled()
	{
		IsMoving = false;
		if ( !GameObject.IsProxy )
			StopBody();
	}

	private void Initialize()
	{
		if ( initialized )
			return;

		// needed to make enclosure colliders appear
		GetComponent<Rigidbody>(includeDisabled: true).Enabled = false;

		initialized = true;
		endpointsReady = PointA.IsValid() && PointB.IsValid();
		if ( !endpointsReady )
		{
			Log.Warning( $"[Elevator] '{GameObject.Name}' requires valid PointA and PointB endpoint objects." );
			return;
		}

		positionA = PointA.WorldPosition;
		positionB = PointB.WorldPosition;

		// needed to make enclosure colliders appear
		GetComponent<Rigidbody>( includeDisabled: true ).Enabled = true;
	}

	private bool ValidateEndpoints()
	{
		if ( endpointsReady && (!PointA.IsValid() || !PointB.IsValid()) )
		{
			endpointsReady = false;
			Log.Warning( $"[Elevator] '{GameObject.Name}' lost an endpoint and has stopped." );
		}

		if ( !endpointsReady )
			IsMoving = false;

		return endpointsReady;
	}

	private void StopBody()
	{
		var body = Rigidbody.IsValid() ? Rigidbody.PhysicsBody : null;
		if ( body is null )
			return;

		body.BodyType = PhysicsBodyType.Keyframed;
		body.Velocity = Vector3.Zero;
		body.AngularVelocity = Vector3.Zero;
	}
}
