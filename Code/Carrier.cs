/// <summary>
/// Owns one physical object carried at an authored player attachment.
/// </summary>
public sealed class Carrier : Component
{
	[RequireComponent]
	public PlayerController Controller { get; set; }

	[Property, Group( "Setup" )]
	public GameObject Attachment { get; set; }

	[Property, Group( "Input" )]
	public string DropAction { get; set; } = "Drop";

	public Carryable HeldItem { get; private set; }
	public bool IsCarrying => HeldItem is not null;

	protected override void OnStart()
	{
		if ( Attachment is null )
			Log.Warning( $"{nameof( Carrier )} on '{GameObject.Name}' has no Attachment assigned." );
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy )
			return;

		if ( HeldItem is not null )
		{
			if ( !HeldItem.IsValid() || !HeldItem.Active )
			{
				HeldItem = null;
			}
			else
			{
				HeldItem.AlignAnchor( Attachment );
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
			&& item.Anchor.IsValid()
			&& Attachment.IsValid()
			&& Attachment.Active;
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
}
