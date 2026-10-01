using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Models
{
    /// <summary>
    /// Persists an approved proposal while its native campaign actions are
    /// being applied. NextTermIndex prevents already completed clauses from
    /// being repeated after a save/load or a recoverable execution failure.
    /// </summary>
    [SaveableRootClass(16)]
    public sealed class KingdomNegotiationExecutionRecord
    {
        [SaveableProperty(1)] public string ProposalId { get; private set; }
        [SaveableProperty(2)] public Kingdom PlayerKingdom { get; private set; }
        [SaveableProperty(3)] public Kingdom TargetKingdom { get; private set; }
        [SaveableProperty(4)]
        public List<KingdomNegotiationTermRecord> Terms { get; private set; }
        [SaveableProperty(5)] public int NextTermIndex { get; set; }
        [SaveableProperty(6)] public int RetryCount { get; set; }

        public KingdomNegotiationExecutionRecord(
            KingdomNegotiationDraft draft)
        {
            ProposalId = Guid.NewGuid().ToString("N");
            PlayerKingdom = draft.PlayerKingdom;
            TargetKingdom = draft.TargetKingdom;
            Terms = draft.Terms
                .Select(KingdomNegotiationTermRecord.FromDraftTerm)
                .ToList();
        }

        public KingdomNegotiationDraft CreateDraft()
        {
            return new KingdomNegotiationDraft(
                PlayerKingdom,
                TargetKingdom,
                (Terms ?? new List<KingdomNegotiationTermRecord>())
                    .Select(x => x.ToDraftTerm()));
        }
    }
}
