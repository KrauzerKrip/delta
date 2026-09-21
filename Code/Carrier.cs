/// <summary>
/// Owns one physical object carried at an authored player attachment.
/// </summary>
public sealed class Carrier : Component
{
	[RequireComponent]
	public PlayerController Controller { get; set; }

	[Property, Group( "Setup" )]
	public GameObject RightHandAttachment { get; set; }

	[Property, Group( "Setup" )]
	public GameObject LeftHandAttachment { get; set; }

	[Property, Range( 0f, 0.25f ), Group( "Setup" )]
	public float HandSwitchDeadZone { get; set; } = 0.025f;

	[Property, Group( "Input" )]
	public string DropAction { get; set; } = "Drop";

	public Carryable HeldItem { get; private set; }
	public bool IsCarrying => HeldItem is not null;
	public bool IsUsingLeftHand { get; private set; }

	protected override void OnStart()
	{
		if ( RightHandAttachment is null )
			Log.Warning( $"{nameof( Carrier )} on '{GameObject.Name}' has no RightHandAttachment assigned." );

		if ( LeftHandAttachment is null )
			Log.Warning( $"{nameof( Carrier )} on '{GameObject.Name}' has no LeftHandAttachment assigned." );

		UpdateFacingHand();
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		UpdateFacingHand();

		if ( HeldItem is not null )
		{
			if ( !HeldItem.IsValid() || !HeldItem.Active )
			{
				HeldItem = null;
			}
			else if ( !TryGetCarryPose( HeldItem, out var attachment, out var anchor ) )
			{
				Log.Warning( $"{nameof( Carrier )} on '{GameObject.Name}' lost a required hand attachment or carry anchor; dropping '{HeldItem.GameObject.Name}'." );
				Drop();
			}
			else
			{
				HeldItem.AttachTo( attachment, anchor );
			}
		}

		if ( IsCarrying
			&& Controller?.UseInputControls == true
			&& !string.IsNullOrWhiteSpace( DropAction )
			&& Input.Pressed( DropAction ) )
		{
			Drop();
		}
	}

	public bool CanCarry( Carryable item )
	{
		return !GameObject.IsProxy
			&& HeldItem is null
			&& item is not null
			&& item.Active
			&& RightHandAttachment.IsValid()
			&& RightHandAttachment.Active
			&& LeftHandAttachment.IsValid()
			&& LeftHandAttachment.Active
			&& item.RightAnchor.IsValid()
			&& item.RightAnchor.Active
			&& item.LeftAnchor.IsValid()
			&& item.LeftAnchor.Active;
	}

	public bool TryCarry( Carryable item )
	{
		if ( !CanCarry( item ) || !item.BeginCarry( this ) )
			return false;

		HeldItem = item;
		return true;
	}

	public bool Drop()
	{
		if ( HeldItem is null )
			return false;

		var item = HeldItem;
		HeldItem = null;
		if ( item.IsValid() )
			item.EndCarry( this );

		return true;
	}

	internal void NotifyCarryableDestroyed( Carryable item )
	{
		if ( HeldItem == item )
			HeldItem = null;
	}

	protected override void OnDisabled()
	{
		Drop();
	}

	protected override void OnDestroy()
	{
		Drop();
	}

	internal bool TryGetCarryPose( Carryable item, out GameObject attachment, out GameObject anchor )
	{
		attachment = IsUsingLeftHand ? LeftHandAttachment : RightHandAttachment;
		anchor = IsUsingLeftHand ? item?.LeftAnchor : item?.RightAnchor;
		return attachment.IsValid() && attachment.Active && anchor.IsValid() && anchor.Active;
	}

	private void UpdateFacingHand()
	{
		var facingY = WorldRotation.Forward.y;
		var deadZone = HandSwitchDeadZone.Clamp( 0f, 0.25f );

		if ( facingY > deadZone )
			IsUsingLeftHand = true;
		else if ( facingY < -deadZone )
			IsUsingLeftHand = false;
	}
}
