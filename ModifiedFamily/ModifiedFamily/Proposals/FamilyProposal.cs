using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedFamily.Proposals
{
    [SaveableRootClass(1)]
    public sealed class FamilyProposal
    {
        [SaveableProperty(1)] public string ProposalId { get; set; }
        [SaveableProperty(2)] public string ChainId { get; set; }
        [SaveableProperty(3)] public string ParentProposalId { get; set; }
        [SaveableProperty(4)] public FamilyProposalType ProposalType { get; set; }
        [SaveableProperty(5)] public FamilyProposalStatus Status { get; set; }
        [SaveableProperty(6)] public int CounterCount { get; set; }
        [SaveableProperty(7)] public Clan ProposerClan { get; set; }
        [SaveableProperty(8)] public Clan ReceiverClan { get; set; }
        [SaveableProperty(9)] public Hero HeroA { get; set; }
        [SaveableProperty(10)] public Hero HeroB { get; set; }
        [SaveableProperty(11)] public Hero AdopterHero { get; set; }
        [SaveableProperty(12)] public Hero AdopteeHero { get; set; }
        [SaveableProperty(13)] public Clan OriginClanA { get; set; }
        [SaveableProperty(14)] public Clan OriginClanB { get; set; }
        [SaveableProperty(15)] public Clan DestinationClan { get; set; }
        [SaveableProperty(16)] public Clan GoldPayerClan { get; set; }
        [SaveableProperty(17)] public Clan GoldReceiverClan { get; set; }
        [SaveableProperty(18)] public int GiftAmount { get; set; }
        [SaveableProperty(19)] public CampaignTime CreatedAt { get; set; }
        [SaveableProperty(20)] public CampaignTime ReplyDeadline { get; set; }
        [SaveableProperty(21)] public CampaignTime ExecutionDeadline { get; set; }
        [SaveableProperty(22)] public FamilyExecutionStep ExecutionStep { get; set; }
        [SaveableProperty(23)] public FamilyReasonCode ReasonCode { get; set; }

        public bool IsActive => Status == FamilyProposalStatus.PendingResponse || Status == FamilyProposalStatus.AcceptedPendingExecution || Status == FamilyProposalStatus.Executing || Status == FamilyProposalStatus.PaymentReconcileRequired;
    }

    public sealed class MarriageProposalDraft
    {
        public Clan ProposerClan { get; set; }
        public Clan ReceiverClan { get; set; }
        public Hero HeroA { get; set; }
        public Hero HeroB { get; set; }
        public MarriageDirection Direction { get; set; }
        public Clan GoldPayerClan { get; set; }
        public int GiftAmount { get; set; }
    }

    public sealed class AdoptionProposalDraft
    {
        public Clan ProposerClan { get; set; }
        public Clan ReceiverClan { get; set; }
        public Hero AdopterHero { get; set; }
        public Hero AdopteeHero { get; set; }
        public Clan DestinationClan { get; set; }
    }
}
