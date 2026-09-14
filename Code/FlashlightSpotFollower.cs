/// <summary>
/// Adds a subtle handheld tremble to a spotlight while loosely aiming it at a target.
/// The component deliberately changes rotation only, leaving the authored position intact.
/// </summary>
public sealed class FlashlightSpotFollower : Component
{
	[Property, Group( "Following" )]
	public GameObject Target { get; set; }

	[Property, Group( "Following" ), Range( 0f, 1f )]
	public float FollowInfluence { get; set; } = 0.7f;

	[Property, Group( "Following" ), Range( 0.01f, 20f )]
	public float FollowSpeed { get; set; } = 2.5f;

	[Property, Group( "Handheld Motion" ), Range( 0f, 5f )]
	public float SwayAmplitude { get; set; } = 0.8f;

	[Property, Group( "Handheld Motion" ), Range( 0f, 5f )]
	public float TrembleAmplitude { get; set; } = 0.45f;

	[Property, Group( "Handheld Motion" ), Range( 0.1f, 10f )]
	public float TrembleFrequency { get; set; } = 2.2f;

	[Property, Group( "Shock Reaction" ), Range( 0f, 20f )]
	public float ShockTrembleAmplitude { get; set; } = 5.5f;

	[Property, Group( "Shock Reaction" ), Range( 0.1f, 10f )]
	public float ShockTrembleFrequency { get; set; } = 0.2f;

	private Rotation authoredRotation;
	private Rotation followedRotation;
	private PliersMinigameController minigame;
	private float motionTime;
	private bool initialized;

	protected override void OnStart()
	{
		authoredRotation = WorldRotation;
		followedRotation = authoredRotation;
		minigame = Target?.Components.Get<PliersMinigameController>( FindMode.InAncestors );
		initialized = true;
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy || !initialized )
			return;

		var desiredRotation = authoredRotation;
		if ( Target is not null )
		{
			var targetDirection = Target.WorldPosition - WorldPosition;
			if ( targetDirection.LengthSquared > 0.001f )
			{
				var targetRotation = Rotation.LookAt( targetDirection.Normal, authoredRotation.Up );
				desiredRotation = Rotation.Slerp(
					authoredRotation,
					targetRotation,
					FollowInfluence.Clamp( 0f, 1f )
				);
			}
		}

		var followBlend = 1f - System.MathF.Exp( -System.MathF.Max( FollowSpeed, 0.01f ) * Time.Delta );
		followedRotation = Rotation.Slerp( followedRotation, desiredRotation, followBlend );

		motionTime += Time.Delta;
		var shockStrength = minigame?.DangerTremorStrength ?? 0f;
		Log.Info( minigame?.DangerTremorStrength );
		var normalFrequency = System.MathF.Max( TrembleFrequency, 0.1f );
		var shockFrequency = System.MathF.Max( ShockTrembleFrequency, 0.1f );
		var frequency = normalFrequency;
		if (shockStrength > 0f)
		{
		 frequency = shockFrequency;
		}
		var trembleAmplitude = TrembleAmplitude + ShockTrembleAmplitude * shockStrength;
		var swayPitch = System.MathF.Sin( motionTime * 0.71f ) * SwayAmplitude;
		var swayYaw = System.MathF.Sin( motionTime * 0.53f + 1.7f ) * SwayAmplitude * 0.85f;
		var tremblePitch = (
			System.MathF.Sin( motionTime * frequency * 6.1f + 0.4f ) * 0.65f +
			System.MathF.Sin( motionTime * frequency * 9.7f + 2.1f ) * 0.35f
		) * trembleAmplitude;
		var trembleYaw = (
			System.MathF.Sin( motionTime * frequency * 5.3f + 3.2f ) * 0.6f +
			System.MathF.Sin( motionTime * frequency * 8.9f + 0.8f ) * 0.4f
		) * trembleAmplitude;

		var handheldOffset = new Angles(
			swayPitch + tremblePitch,
			swayYaw + trembleYaw,
			0f
		).ToRotation();

		WorldRotation = followedRotation * handheldOffset;
	}
}
