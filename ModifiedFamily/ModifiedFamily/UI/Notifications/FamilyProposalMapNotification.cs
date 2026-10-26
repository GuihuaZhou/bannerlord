// Persisted map notification that points to a player-facing family proposal.
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedFamily.UI.Notifications
{
    [SaveableRootClass(9)]
    public sealed class FamilyProposalMapNotification : InformationData
    {
        [SaveableField(1)] private string _proposalId;

        public FamilyProposalMapNotification() : base(TextObject.GetEmpty()) { }
        public FamilyProposalMapNotification(string proposalId, TextObject title) : base(title) { _proposalId = proposalId; }
        public string ProposalId => _proposalId;
        public override TextObject TitleText => new TextObject("{=MF_UI_IncomingTitle}Incoming Title");
        public override string SoundEventPath => "event:/ui/notification/kingdom_decision";
    }
}
