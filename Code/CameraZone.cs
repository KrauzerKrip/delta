/// <summary>
/// A trigger volume containing an authored dollhouse camera shot.
/// Add this beside a collider whose Is Trigger property is enabled.
/// </summary>
public sealed class CameraZone : Component, Component.ITriggerListener
{
	[Property, Group( "Shot" )]
	public GameObject CameraAnchor { get; set; }

	[Property, Group( "Shot" )]
	public int Priority { get; set; }

	[Property, Group( "Shot" ), Range( 1f, 179f )]
	public float FieldOfView { get; set; } = 60f;

	[Property, Group( "Shot" ), Range( 0.01f, 30f )]
	public float BlendSpeed { get; set; } = 5f;

	[Property, Group( "Following" )]
	public bool FollowPlayerX { get; set; }

	[Property, Group( "Following" )]
	public bool FollowPlayerY { get; set; }

	[Property, Group( "Following" )]
	public bool FollowPlayerZ { get; set; }

	[Property, Group( "Following" )]
	public Vector3 FollowLimits { get; set; }

	[Property, Group( "Following" )]
	public bool OverrideDeadZone { get; set; }

	[Property, Group( "Following" )]
	public Vector3 DeadZoneSize { get; set; } = new Vector3( 0f, 128f, 96f );

	private readonly Dictionary<Collider, CameraController> contacts = new();
	private readonly HashSet<CameraController> seededDirectors = new();
	private readonly HashSet<CameraController> departedDirectors = new();

	internal bool ContainsPosition( Vector3 position ) => Active && GetComponents<BoxCollider>()
		.Any( box => box.Active && box.IsTrigger && box.LocalBounds.Contains( box.WorldTransform.PointToLocal( position ) ) );

	internal void ReconcileAfterTransfer( CameraController director )
	{
		var departed = contacts.Values.Contains( director ) || seededDirectors.Contains( director );
		foreach ( var contact in contacts.Where( x => x.Value == director ).ToArray() )
			contacts.Remove( contact.Key );
		seededDirectors.Remove( director );
		if ( departed )
			departedDirectors.Add( director );
		if ( ContainsPosition( director.Controller.WorldPosition ) )
		{
			departedDirectors.Remove( director );
			seededDirectors.Add( director );
			director.EnterZone( this );
		}
	}

	protected override void OnFixedUpdate()
	{
		foreach ( var director in seededDirectors.ToArray() )
		{
			if ( !director.IsValid() )
			{
				seededDirectors.Remove( director );
				continue;
			}
			if ( !ContainsPosition( director.Controller.WorldPosition ) )
			{
				seededDirectors.Remove( director );
				if ( !contacts.Values.Contains( director ) )
					director.ExitZone( this );
			}
		}
	}

	/// <summary>
	/// Returns the authored anchor position with optional, bounded player following.
	/// The controller supplies a focus position after applying its dead-zone rules.
	/// A follow limit of zero leaves that axis unbounded.
	/// </summary>
	public Vector3 GetCameraPosition( Vector3 focusPosition )
	{
		if ( CameraAnchor is null )
			return WorldPosition;

		var position = CameraAnchor.WorldPosition;

		if ( FollowPlayerX )
			position.x += ClampFollow( focusPosition.x - WorldPosition.x, FollowLimits.x );

		if ( FollowPlayerY )
			position.y += ClampFollow( focusPosition.y - WorldPosition.y, FollowLimits.y );

		if ( FollowPlayerZ )
			position.z += ClampFollow( focusPosition.z - WorldPosition.z, FollowLimits.z );

		return position;
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( contacts.ContainsKey( other ) )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		var director = player?.Components.Get<CameraController>();

		if ( director is null || director.GameObject.IsProxy )
			return;

		// Only a player that just teleported needs protection from stale source callbacks.
		// Ordinary camera zones trust the engine's trigger events as before.
		if ( departedDirectors.Contains( director ) )
		{
			if ( !ContainsPosition( director.Controller.WorldPosition ) )
				return;
			departedDirectors.Remove( director );
		}
		var wasOutside = !contacts.Values.Contains( director ) && !seededDirectors.Contains( director );
		contacts.Add( other, director );
		seededDirectors.Remove( director );

		if ( wasOutside )
			director.EnterZone( this );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !contacts.Remove( other, out var director ) )
			return;

		if ( !contacts.Values.Contains( director ) && !seededDirectors.Contains( director ) )
			director.ExitZone( this );
	}

	protected override void OnDisabled()
	{
		foreach ( var director in contacts.Values.Concat( seededDirectors ).Distinct().ToArray() )
			director?.ExitZone( this );

		contacts.Clear();
		seededDirectors.Clear();
		departedDirectors.Clear();
	}

	private static float ClampFollow( float value, float limit )
	{
		return limit > 0f ? value.Clamp( -limit, limit ) : value;
	}
}
