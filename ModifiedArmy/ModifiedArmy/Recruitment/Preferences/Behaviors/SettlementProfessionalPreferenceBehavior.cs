// Owns the persisted per-settlement professional recruitment weights and keeps
// them aligned with the current eligibility snapshot and save-compatible record.
using ModifiedArmy.Recruitment.Preferences.Models;
using ModifiedArmy.Recruitment.Preferences.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Preferences.Behaviors
{
    public sealed class SettlementProfessionalPreferenceBehavior : CampaignBehaviorBase
    {
        public const int MaximumCustomWeight = 9;
        private Dictionary<string, SettlementProfessionalPreferenceRecord> _records =
            new Dictionary<string, SettlementProfessionalPreferenceRecord>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
                this, OnSettlementOwnerChanged);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(
                this, OnClanChangedKingdom);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_settlement_professional_preferences_v1", ref _records);
            _records ??= new Dictionary<string, SettlementProfessionalPreferenceRecord>();
        }

        /// <summary>
        /// Rebuilds the save-compatible record only when owner/template identity
        /// changes, then incrementally aligns eligible keys and normalizes legacy values.
        /// </summary>
        public IReadOnlyDictionary<string, int> Reconcile(Settlement settlement)
        {
            ProfessionalRecruitmentEligibilitySnapshot snapshot =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (snapshot?.Template == null || settlement?.OwnerClan == null)
                return new Dictionary<string, int>();
            string id = settlement.StringId;
            string owner = settlement.OwnerClan.StringId;
            string template = snapshot.Template.TemplateId;
            bool rebuild = !_records.TryGetValue(id, out var record) || record == null ||
                record.OwnerClanId != owner || record.TemplateId != template;
            int beforeCount = record?.ActiveWeights?.Count ?? 0;
            if (rebuild)
                record = new SettlementProfessionalPreferenceRecord
                {
                    SettlementId = id, OwnerClanId = owner, TemplateId = template,
                    ActiveWeights = new Dictionary<string, int>()
                };
            record.ActiveWeights ??= new Dictionary<string, int>();
            var eligible = snapshot.EligibleEntries.ToDictionary(
                x => x.Troop.StringId, x => x, StringComparer.Ordinal);
            int removed = 0;
            foreach (string key in record.ActiveWeights.Keys.ToList())
                if (!eligible.ContainsKey(key))
                {
                    record.ActiveWeights.Remove(key);
                    removed++;
                }
            int added = 0;
            foreach (var pair in eligible)
                if (!record.ActiveWeights.ContainsKey(pair.Key))
                {
                    record.ActiveWeights[pair.Key] = NormalizeWeight(pair.Value.Weight);
                    added++;
                }
            int normalized = 0;
            foreach (string key in record.ActiveWeights.Keys.ToList())
            {
                int value = record.ActiveWeights[key];
                int safeValue = NormalizeWeight(value);
                if (safeValue == value) continue;
                record.ActiveWeights[key] = safeValue;
                normalized++;
            }
            _records[id] = record;
            if (rebuild || added > 0 || removed > 0 || normalized > 0)
                ProfessionalPreferenceDiagnostics.LogReconciled(settlement,
                    rebuild ? "OwnerOrTemplateRebuild" : normalized > 0
                        ? "WeightRangeNormalized" : "EligibilityChanged",
                    beforeCount, record.ActiveWeights.Count, added, removed);
            return new Dictionary<string, int>(record.ActiveWeights);
        }

        /// <summary>
        /// Returns a read-safe eligible snapshot. Values are normalized even
        /// before campaign recovery has had an opportunity to persist migration.
        /// </summary>
        public IReadOnlyDictionary<string, int> GetEffectiveWeights(Settlement settlement)
        {
            var snapshot = ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (snapshot?.Template == null) return new Dictionary<string, int>();
            if (_records.TryGetValue(settlement.StringId, out var record) &&
                record?.OwnerClanId == settlement.OwnerClan?.StringId &&
                record.TemplateId == snapshot.Template.TemplateId)
            {
                var eligible = new HashSet<string>(snapshot.EligibleEntries
                    .Select(x => x.Troop.StringId), StringComparer.Ordinal);
                return (record.ActiveWeights ?? new Dictionary<string, int>())
                    .Where(x => eligible.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => NormalizeWeight(x.Value),
                        StringComparer.Ordinal);
            }
            return snapshot.EligibleEntries.ToDictionary(
                x => x.Troop.StringId, x => NormalizeWeight(x.Weight),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// Atomically replaces the complete eligible key set for the owning clan,
        /// then delegates offer cleanup to the existing production behavior.
        /// </summary>
        public bool TrySetWeights(Settlement settlement,
            IReadOnlyDictionary<string, int> weights, Clan actor)
        {
            if (settlement?.OwnerClan == null || actor != settlement.OwnerClan ||
                weights == null)
            {
                ProfessionalPreferenceDiagnostics.LogWriteRejected(
                    settlement, "InvalidOwnerOrPayload");
                return false;
            }
            var current = Reconcile(settlement);
            if (weights.Count != current.Count || weights.Any(x =>
                !current.ContainsKey(x.Key) || x.Value != NormalizeWeight(x.Value)))
            {
                ProfessionalPreferenceDiagnostics.LogWriteRejected(
                    settlement, "StaleEligibilityOrWeightOutOfRange");
                return false;
            }
            var record = _records[settlement.StringId];
            record.ActiveWeights = weights.ToDictionary(x => x.Key, x => x.Value);
            Campaign.Current?.GetCampaignBehavior<
                ModifiedArmy.Recruitment.Pools.Behaviors.SettlementManpowerProductionBehavior>()?
                .FilterProfessionalOffers(settlement);
            return true;
        }

        public void RevalidateAfterCampaignRecovery()
        {
            foreach (Town town in Town.AllFiefs) Reconcile(town.Settlement);
        }

        /// <summary>
        /// Applies the single effective weight policy used by templates, old
        /// save records, production reads, and the presentation layer.
        /// </summary>
        internal static int NormalizeWeight(int value) =>
            Math.Max(0, Math.Min(MaximumCustomWeight, value));

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim,
            Hero newOwner, Hero oldOwner, Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (oldOwner?.Clan != null && oldOwner.Clan == newOwner?.Clan)
            {
                Reconcile(settlement);
                return;
            }
            if (settlement?.StringId != null) _records.Remove(settlement.StringId);
            Campaign.Current?.GetCampaignBehavior<
                ModifiedArmy.Recruitment.Pools.Behaviors.SettlementManpowerProductionBehavior>()?
                .InvalidateProfessionalOffersForOwnershipChange(settlement);
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom,
            Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail,
            bool showNotification)
        {
            foreach (Town town in clan?.Fiefs ?? Enumerable.Empty<Town>())
                Reconcile(town.Settlement);
        }
    }
}
