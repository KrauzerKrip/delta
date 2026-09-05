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

		var wasOutside = !contacts.Values.Contains( director );
		contacts.Add( other, director );

		if ( wasOutside )
			director.EnterZone( this );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !contacts.Remove( other, out var director ) )
			return;

		if ( !contacts.Values.Contains( director ) )
			director.ExitZone( this );
	}

	protected override void OnDisabled()
	{
		foreach ( var director in contacts.Values.Distinct().ToArray() )
			director?.ExitZone( this );

		contacts.Clear();
	}

	private static float ClampFollow( float value, float limit )
	{
		return limit > 0f ? value.Clamp( -limit, limit ) : value;
	}
}
