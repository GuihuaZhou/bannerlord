using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using TaleWorlds.CampaignSystem;

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

        private List<KingdomPoliticalData> _kingdomData =
            new List<KingdomPoliticalData>();
        private List<KingdomRelationData> _relationData =
            new List<KingdomRelationData>();
        private List<SubjectRelationData> _subjectData =
            new List<SubjectRelationData>();

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
                RemoveInvalidRecords);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(KingdomDataSaveKey, ref _kingdomData);
            dataStore.SyncData(RelationDataSaveKey, ref _relationData);
            dataStore.SyncData(SubjectDataSaveKey, ref _subjectData);

            _kingdomData = _kingdomData ?? new List<KingdomPoliticalData>();
            _relationData = _relationData ?? new List<KingdomRelationData>();
            _subjectData = _subjectData ?? new List<SubjectRelationData>();
            RemoveInvalidRecords();
            Current = this;
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
        }
    }
}
