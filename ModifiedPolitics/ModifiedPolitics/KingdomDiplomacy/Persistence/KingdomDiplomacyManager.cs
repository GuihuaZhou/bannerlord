using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Persistence
{
    /// <summary>
    /// Owns all persistent kingdom-rank, bilateral-relation and subject data.
    /// Other modules must use this manager instead of maintaining parallel state.
    /// </summary>
    public sealed class KingdomDiplomacyManager : CampaignBehaviorBase
    {
        private const string KingdomDataSaveKey =
            "_modifiedPoliticsKingdomPoliticalData";
        private const string RelationDataSaveKey =
            "_modifiedPoliticsKingdomRelationData";
        private const string SubjectDataSaveKey =
            "_modifiedPoliticsSubjectRelationData";
        private const string NegotiationExecutionSaveKey =
            "_modifiedPoliticsNegotiationExecutions";
        private const string CounterOfferSaveKey =
            "_modifiedPoliticsNegotiationCounterOffers";

        private List<KingdomPoliticalData> _kingdomData =
            new List<KingdomPoliticalData>();
        private List<KingdomRelationData> _relationData =
            new List<KingdomRelationData>();
        private List<SubjectRelationData> _subjectData =
            new List<SubjectRelationData>();
        private List<KingdomNegotiationExecutionRecord>
            _negotiationExecutions =
                new List<KingdomNegotiationExecutionRecord>();
        private List<KingdomNegotiationCounterOfferRecord> _counterOffers =
            new List<KingdomNegotiationCounterOfferRecord>();

        public static KingdomDiplomacyManager Current { get; private set; }

        public KingdomDiplomacyManager()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(KingdomDataSaveKey, ref _kingdomData);
            dataStore.SyncData(RelationDataSaveKey, ref _relationData);
            dataStore.SyncData(SubjectDataSaveKey, ref _subjectData);
            dataStore.SyncData(
                NegotiationExecutionSaveKey,
                ref _negotiationExecutions);
            dataStore.SyncData(CounterOfferSaveKey, ref _counterOffers);

            _kingdomData = _kingdomData ?? new List<KingdomPoliticalData>();
            _relationData = _relationData ?? new List<KingdomRelationData>();
            _subjectData = _subjectData ?? new List<SubjectRelationData>();
            _negotiationExecutions = _negotiationExecutions
                ?? new List<KingdomNegotiationExecutionRecord>();
            _counterOffers = _counterOffers
                ?? new List<KingdomNegotiationCounterOfferRecord>();
            RemoveInvalidRecords();
            Current = this;
        }

        /// <summary>
        /// Persists an approved proposal before invoking any native campaign
        /// action. A failed partial execution remains queued and resumes from
        /// its next unfinished clause on a later daily tick or after loading.
        /// </summary>
        public bool BeginNegotiationExecution(
            KingdomNegotiationDraft draft)
        {
            if (!KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out _))
            {
                return false;
            }

            KingdomNegotiationExecutionRecord record =
                new KingdomNegotiationExecutionRecord(draft);
            _negotiationExecutions.Add(record);
            ResumeNegotiationExecution(record);
            return true;
        }

        public void StoreCounterOffer(
            Kingdom playerKingdom,
            Kingdom targetKingdom,
            IEnumerable<KingdomNegotiationDraftTerm> terms)
        {
            if (!AreDistinctKingdoms(playerKingdom, targetKingdom))
            {
                return;
            }

            _counterOffers.RemoveAll(record =>
                record?.PlayerKingdom == playerKingdom
                && record.TargetKingdom == targetKingdom);
            _counterOffers.Add(new KingdomNegotiationCounterOfferRecord(
                playerKingdom,
                targetKingdom,
                terms));
        }

        public KingdomNegotiationDraft GetCounterOffer(
            Kingdom playerKingdom,
            Kingdom targetKingdom)
        {
            KingdomNegotiationCounterOfferRecord record = _counterOffers
                .LastOrDefault(item => item?.PlayerKingdom == playerKingdom
                    && item.TargetKingdom == targetKingdom);
            if (record == null)
            {
                return null;
            }

            return record.CreateDraft();
        }

        public void RemoveCounterOffer(
            Kingdom playerKingdom,
            Kingdom targetKingdom)
        {
            _counterOffers.RemoveAll(record =>
                record?.PlayerKingdom == playerKingdom
                && record.TargetKingdom == targetKingdom);
        }

        public int GetKingdomRank(Kingdom kingdom)
        {
            return GetOrCreateKingdomData(kingdom)?.Rank ?? 1;
        }

        public void SetKingdomRank(Kingdom kingdom, int rank)
        {
            KingdomPoliticalData data = GetOrCreateKingdomData(kingdom);
            if (data != null)
            {
                data.Rank = Math.Max(1, rank);
            }
        }

        public int GetKingdomRelation(Kingdom first, Kingdom second)
        {
            if (!AreDistinctKingdoms(first, second))
            {
                return 0;
            }

            return FindRelation(first, second)?.Relation ?? 0;
        }

        public void SetKingdomRelation(Kingdom first, Kingdom second, int value)
        {
            if (!AreDistinctKingdoms(first, second))
            {
                return;
            }

            KingdomRelationData data = FindRelation(first, second);
            int clampedValue = Math.Max(-100, Math.Min(100, value));
            if (data == null)
            {
                _relationData.Add(new KingdomRelationData(
                    first,
                    second,
                    clampedValue));
                return;
            }

            data.Relation = clampedValue;
        }

        public SubjectRelationData GetSubjectRelation(Kingdom subject)
        {
            return subject == null
                ? null
                : _subjectData.FirstOrDefault(data =>
                    data?.SubjectKingdom == subject);
        }

        public Kingdom GetOverlord(Kingdom subject)
        {
            return GetSubjectRelation(subject)?.OverlordKingdom;
        }

        public SubjectType GetSubjectType(Kingdom subject)
        {
            return GetSubjectRelation(subject)?.Type ?? SubjectType.None;
        }

        public IReadOnlyList<SubjectRelationData> GetSubjects(Kingdom overlord)
        {
            if (overlord == null)
            {
                return new List<SubjectRelationData>();
            }

            return _subjectData
                .Where(data => data?.OverlordKingdom == overlord)
                .ToList();
        }

        public bool TryEstablishSubjectRelation(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type,
            int dailyTribute)
        {
            if (!CanEstablishSubjectRelation(overlord, subject, type))
            {
                return false;
            }

            _subjectData.Add(new SubjectRelationData(
                subject,
                overlord,
                type,
                Math.Max(0, dailyTribute)));
            return true;
        }

        public bool SetDailyTribute(Kingdom subject, int dailyTribute)
        {
            SubjectRelationData relation = GetSubjectRelation(subject);
            if (relation == null)
            {
                return false;
            }

            relation.DailyTribute = Math.Max(0, dailyTribute);
            return true;
        }

        public bool RemoveSubjectRelation(Kingdom subject)
        {
            SubjectRelationData relation = GetSubjectRelation(subject);
            return relation != null && _subjectData.Remove(relation);
        }

        private static bool AreDistinctKingdoms(Kingdom first, Kingdom second)
        {
            return first != null && second != null && first != second;
        }

        public bool CanEstablishSubjectRelation(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type)
        {
            if (!AreDistinctKingdoms(overlord, subject)
                || type == SubjectType.None
                || overlord.IsEliminated
                || subject.IsEliminated
                || GetSubjectRelation(subject) != null)
            {
                return false;
            }

            // Multi-level subject hierarchies are deliberately excluded from
            // the first version: a subject cannot also become an overlord.
            return GetSubjectRelation(overlord) == null
                && !_subjectData.Any(data =>
                    data?.OverlordKingdom == subject);
        }

        private KingdomPoliticalData GetOrCreateKingdomData(Kingdom kingdom)
        {
            if (kingdom == null)
            {
                return null;
            }

            KingdomPoliticalData data = _kingdomData.FirstOrDefault(item =>
                item?.Kingdom == kingdom);
            if (data == null)
            {
                data = new KingdomPoliticalData(kingdom, 1);
                _kingdomData.Add(data);
            }

            return data;
        }

        private KingdomRelationData FindRelation(Kingdom first, Kingdom second)
        {
            return _relationData.FirstOrDefault(data =>
                data != null && data.Matches(first, second));
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RemoveInvalidRecords();
            foreach (Kingdom kingdom in Kingdom.All)
            {
                GetOrCreateKingdomData(kingdom);
            }

            RetryNegotiationExecutions();
        }

        private void OnDailyTick()
        {
            RemoveInvalidRecords();
            RetryNegotiationExecutions();
        }

        private void RetryNegotiationExecutions()
        {
            foreach (KingdomNegotiationExecutionRecord record
                in _negotiationExecutions.ToList())
            {
                ResumeNegotiationExecution(record);
            }
        }

        private void ResumeNegotiationExecution(
            KingdomNegotiationExecutionRecord record)
        {
            if (record == null)
            {
                return;
            }

            bool completed = KingdomNegotiationExecutionService
                .TryExecuteSupportedProposal(
                    record.CreateDraft(),
                    record.NextTermIndex,
                    nextIndex => record.NextTermIndex = nextIndex);
            if (completed)
            {
                _negotiationExecutions.Remove(record);
                return;
            }

            record.RetryCount++;
            TextObject message = new TextObject(
                "{=MP_NegotiationExecutionQueued}[Kingdom negotiation] Proposal {ID} remains queued at term {INDEX} and will be retried later.");
            message.SetTextVariable("ID", record.ProposalId);
            message.SetTextVariable("INDEX", record.NextTermIndex + 1);
            ModLogger.Info(message.ToString());
        }

        private void RemoveInvalidRecords()
        {
            _kingdomData.RemoveAll(data => data?.Kingdom == null);
            _relationData.RemoveAll(data =>
                data?.KingdomA == null
                || data.KingdomB == null
                || data.KingdomA == data.KingdomB);
            _subjectData.RemoveAll(data =>
                data?.SubjectKingdom == null
                || data.OverlordKingdom == null
                || data.SubjectKingdom == data.OverlordKingdom
                || data.SubjectKingdom.IsEliminated
                || data.OverlordKingdom.IsEliminated
                || data.Type == SubjectType.None);
            foreach (KingdomNegotiationExecutionRecord record
                in _negotiationExecutions.Where(ShouldDiscardExecution)
                    .ToList())
            {
                if (record != null)
                {
                    TextObject message = new TextObject(
                        "{=MP_NegotiationExecutionAbandoned}[Kingdom negotiation] Proposal {ID} was abandoned after its execution state became invalid.");
                    message.SetTextVariable("ID", record.ProposalId);
                    ModLogger.Warn(message.ToString());
                }

                _negotiationExecutions.Remove(record);
            }
            _counterOffers.RemoveAll(record =>
                record?.PlayerKingdom == null
                || record.TargetKingdom == null
                || record.PlayerKingdom.IsEliminated
                || record.TargetKingdom.IsEliminated);
        }

        private static bool ShouldDiscardExecution(
            KingdomNegotiationExecutionRecord record)
        {
            return record == null
                || record.PlayerKingdom == null
                || record.TargetKingdom == null
                || record.PlayerKingdom.IsEliminated
                || record.TargetKingdom.IsEliminated
                || record.RetryCount >= 7;
        }
    }
}
