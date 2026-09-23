/// <summary>
/// Disables the box collider on an assigned game object while a player occupies
/// this trigger, then enables it again after the player leaves.
/// </summary>
public sealed class GlassPortalTrigger : Component, Component.ITriggerListener
{
	[Property]
	public GameObject Target { get; set; }

	private readonly Dictionary<Collider, PlayerController> contacts = new();

	protected override void OnStart()
	{
		if ( !GetComponents<Collider>().Any( collider => collider.IsTrigger ) )
			Log.Warning( $"[{nameof( GlassPortalTrigger )}] '{GameObject.Name}' needs a collider with Is Trigger enabled on the same object." );

		if ( Target is null )
			Log.Warning( $"[{nameof( GlassPortalTrigger )}] '{GameObject.Name}' has no target assigned." );
		else if ( GetTargetCollider() is null )
			Log.Warning( $"[{nameof( GlassPortalTrigger )}] Target '{Target.Name}' has no BoxCollider component." );
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( other is null || contacts.ContainsKey( other ) )
			return;

		var player = other.Components.Get<PlayerController>( FindMode.InAncestors );
		if ( player is null )
			return;

		var wasEmpty = contacts.Count == 0;
		contacts.Add( other, player );

		if ( wasEmpty )
			SetTargetColliderEnabled( false );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( other is null || !contacts.Remove( other ) )
			return;

		if ( contacts.Count == 0 )
			SetTargetColliderEnabled( true );
	}

	protected override void OnDisabled()
	{
		if ( contacts.Count > 0 )
			SetTargetColliderEnabled( true );

		contacts.Clear();
	}

	private BoxCollider GetTargetCollider()
	{
		return Target?.GetComponent<BoxCollider>( includeDisabled: true );
	}

	private void SetTargetColliderEnabled( bool enabled )
	{
		var targetCollider = GetTargetCollider();
		if ( targetCollider is not null )
			targetCollider.Enabled = enabled;
	}
}
