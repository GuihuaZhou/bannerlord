// Marriage result notice: the proposal owns the outcome flags; this saved map item only identifies a snapshot.
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedFamily.UI.Notifications
{
    [SaveableRootClass(10)]
    public sealed class FamilyProposalResultMapNotification : InformationData
    {
        [SaveableField(1)] private string _proposalId;
        [SaveableField(2)] private int _resultFlag;
        [SaveableField(3)] private int _reasonCode;

        public FamilyProposalResultMapNotification() : base(TextObject.GetEmpty()) { }
        public FamilyProposalResultMapNotification(string proposalId, int resultFlag, int reasonCode)
            : base(new TextObject("{=MF_Result_Title}Marriage proposal update"))
        { _proposalId = proposalId; _resultFlag = resultFlag; _reasonCode = reasonCode; }

        public string ProposalId => _proposalId;
        public int ResultFlag => _resultFlag;
        public int ReasonCode => _reasonCode;
        public override TextObject TitleText => new TextObject("{=MF_Result_Title}Marriage proposal update");
        public override string SoundEventPath => "event:/ui/notification/kingdom_decision";
    }
}
