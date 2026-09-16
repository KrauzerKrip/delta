namespace Sandbox;

/// <summary>Matias's introductory repair-work conversation and rewards.</summary>
public sealed class MatiasDialogue : NpcDialogue
{
	[Property, Group( "Dialogue" )]
	public override string SpeakerName { get; set; } = "Matias";

	[Property, Group( "Dialogue" ), TextArea]
	public override List<string> Lines { get; set; } = new()
	{
		"Glad to see you, Marie! I know you want to have your breakfast, but we got an urgent situation for you. You need to conduct some repair work. Good?",
		"Here is the work order.",
		"Almost forgot! Here I return your access card that me and Henry borrowed yesterday.",
		"Good luck! I pay for your dessert."
	};

	protected override void OnDialogueCompleted( PlayerInventory inventory )
	{
		inventory.GiveItem<KeyCard>( card => card.AccessLevel = AccessLevel.Maintenance, slot: 0 );
		inventory.GiveItem<WorkOrder>( slot: 1 );
	}
}
