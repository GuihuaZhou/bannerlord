using ModifiedArmy.Recruitment.Preferences.Behaviors;
using ModifiedArmy.Recruitment.Preferences.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Preferences.Presentation
{
    public sealed class SettlementProfessionalPreferenceMenuBehavior : CampaignBehaviorBase
    {
        private const string OptionId = "modifiedarmy_professional_preferences";

        public override void RegisterEvents() =>
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);

        public override void SyncData(IDataStore dataStore) { }

        private static void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_keep", OptionId,
                "{=ModifiedArmy_ProfessionalPreferences}Professional recruitment preferences",
                CanOpen, Open, false, -1, false);
            starter.AddGameMenuOption("castle", OptionId,
                "{=ModifiedArmy_ProfessionalPreferences}Professional recruitment preferences",
                CanOpen, Open, false, -1, false);
        }

        private static bool CanOpen(MenuCallbackArgs args)
        {
            bool allowed = Settlement.CurrentSettlement?.IsFortification == true &&
                Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
            if (allowed) args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            return allowed;
        }

        private static void Open(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            SettlementProfessionalPreferenceBehavior behavior = Campaign.Current?
                .GetCampaignBehavior<SettlementProfessionalPreferenceBehavior>();
            ProfessionalRecruitmentEligibilitySnapshot snapshot =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (behavior == null || snapshot?.Template == null) return;
            IReadOnlyDictionary<string, int> weights = behavior.Reconcile(settlement);
            var items = snapshot.EligibleEntries.Select(entry => new InquiryElement(
                entry.Troop.StringId,
                entry.Troop.Name + "  [" + weights[entry.Troop.StringId] + "]",
                null, true,
                new TextObject("{=ModifiedArmy_ProfessionalPreferenceHint}Barracks {REQUIRED}; effective {EFFECTIVE}; default {DEFAULT}")
                    .SetTextVariable("REQUIRED", entry.RequiredBarracksLevel)
                    .SetTextVariable("EFFECTIVE", snapshot.AllowedRecruitmentLevel)
                    .SetTextVariable("DEFAULT", entry.Weight).ToString())).ToList();
            items.Add(new InquiryElement("__reset_all__",
                new TextObject("{=ModifiedArmy_ProfessionalResetAll}Reset all to defaults").ToString(),
                null, true, string.Empty));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=ModifiedArmy_ProfessionalPreferences}Professional recruitment preferences").ToString(),
                new TextObject("{=ModifiedArmy_ProfessionalPreferencesDesc}Select a troop to edit its weight (0 disables new professional offers).").ToString(),
                items, true, 1, 1,
                new TextObject("{=ModifiedArmy_ProfessionalEdit}Edit").ToString(),
                new TextObject("{=ModifiedArmy_Cancel}Cancel").ToString(),
                selected => EditSelected(settlement, snapshot, weights, selected), null,
                string.Empty, false), false, false);
        }

        private static void EditSelected(Settlement settlement,
            ProfessionalRecruitmentEligibilitySnapshot snapshot,
            IReadOnlyDictionary<string, int> current, List<InquiryElement> selected)
        {
            string id = selected?.FirstOrDefault()?.Identifier as string;
            if (id == null) return;
            var draft = current.ToDictionary(x => x.Key, x => x.Value);
            if (id == "__reset_all__")
            {
                foreach (var entry in snapshot.EligibleEntries)
                    draft[entry.Troop.StringId] = entry.Weight;
                Commit(settlement, draft);
                return;
            }
            var templateEntry = snapshot.EligibleEntries.First(x => x.Troop.StringId == id);
            InformationManager.ShowTextInquiry(new TextInquiryData(
                templateEntry.Troop.Name.ToString(), draft[id].ToString(), true, true,
                new TextObject("{=ModifiedArmy_Save}Save").ToString(),
                new TextObject("{=ModifiedArmy_Cancel}Cancel").ToString(),
                value => { draft[id] = int.Parse(value); Commit(settlement, draft); }, null,
                false, value =>
                {
                    bool valid = int.TryParse(value, out int weight) && weight >= 0 &&
                        weight <= SettlementProfessionalPreferenceBehavior.MaximumCustomWeight;
                    return Tuple.Create(valid, valid ? string.Empty :
                        new TextObject("{=ModifiedArmy_ProfessionalWeightRange}Weight must be between 0 and 20.").ToString());
                }, string.Empty, string.Empty), false, false);
        }

        private static void Commit(Settlement settlement, Dictionary<string, int> draft) =>
            Campaign.Current?.GetCampaignBehavior<SettlementProfessionalPreferenceBehavior>()?
                .TrySetWeights(settlement, draft, Clan.PlayerClan);
    }
}
