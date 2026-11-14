// Owns the recruitment-weight popup draft and commits the complete eligible
// key set atomically through the persisted preference behavior.
using ModifiedArmy.Recruitment.Preferences.Behaviors;
using ModifiedArmy.Recruitment.Preferences.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Preferences.Presentation
{
    internal sealed class RecruitmentWeightVM : ViewModel
    {
        private readonly Settlement _settlement;
        private readonly SettlementProfessionalPreferenceBehavior _behavior;
        private readonly Action _close;
        private readonly string _templateId;
        private readonly string _ownerClanId;

        internal RecruitmentWeightVM(Settlement settlement,
            SettlementProfessionalPreferenceBehavior behavior, Action close)
        {
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _close = close ?? throw new ArgumentNullException(nameof(close));

            ProfessionalRecruitmentEligibilitySnapshot snapshot =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (snapshot?.Template == null)
                throw new InvalidOperationException("The settlement has no professional recruitment template.");
            _templateId = snapshot.Template.TemplateId;
            _ownerClanId = settlement.OwnerClan?.StringId;
            IReadOnlyDictionary<string, int> current = behavior.Reconcile(settlement);
            Troops = new MBBindingList<RecruitmentWeightTroopItemVM>();
            foreach (var entry in snapshot.EligibleEntries)
            {
                int weight = current.TryGetValue(entry.Troop.StringId, out int value)
                    ? value : entry.Weight;
                Troops.Add(new RecruitmentWeightTroopItemVM(entry.Troop, weight,
                    entry.Weight, OpenEncyclopedia));
            }
            TitleText = new TextObject("{=ModifiedArmy_RecruitmentWeightTitle}Recruitment management").ToString();
            ResetText = new TextObject("{=ModifiedArmy_RecruitmentWeightReset}Restore defaults").ToString();
            ApplyText = new TextObject("{=ModifiedArmy_RecruitmentWeightApply}Apply changes").ToString();
            EmptyText = new TextObject("{=ModifiedArmy_RecruitmentWeightEmpty}No professional troops are currently eligible.").ToString();
        }

        [DataSourceProperty] public string TitleText { get; }
        [DataSourceProperty] public string ResetText { get; }
        [DataSourceProperty] public string ApplyText { get; }
        [DataSourceProperty] public string EmptyText { get; }
        [DataSourceProperty] public MBBindingList<RecruitmentWeightTroopItemVM> Troops { get; }
        [DataSourceProperty] public bool IsEmpty => Troops.Count == 0;
        [DataSourceProperty] public bool CanApply => Troops.Count > 0;

        /// <summary>Restores XML template defaults in the draft only.</summary>
        public void ExecuteResetDefaults()
        {
            foreach (RecruitmentWeightTroopItemVM troop in Troops)
                troop.Weight = troop.DefaultWeight;
        }

        /// <summary>
        /// Revalidates the opening identity and submits the complete draft once.
        /// A stale owner/template/eligibility snapshot remains open for review.
        /// </summary>
        public void ExecuteApply()
        {
            if (!CanApply || !OpeningIdentityStillValid() ||
                !_behavior.TrySetWeights(_settlement,
                    Troops.ToDictionary(x => x.TroopId, x => x.Weight,
                        StringComparer.Ordinal), Clan.PlayerClan))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ModifiedArmy_RecruitmentWeightApplyFailed}Recruitment weights could not be applied because settlement ownership or troop eligibility changed.").ToString(),
                    Colors.Red));
                return;
            }
            _close();
        }

        /// <summary>Closes the modal layer without writing the draft.</summary>
        public void ExecuteCancel() => _close();

        private bool OpeningIdentityStillValid()
        {
            var snapshot = ProfessionalRecruitmentEligibilityModel.Evaluate(_settlement);
            return snapshot?.Template?.TemplateId == _templateId &&
                _settlement.OwnerClan?.StringId == _ownerClanId;
        }

        /// <summary>
        /// Pushes the native encyclopedia page without closing this popup or its
        /// draft. The encyclopedia becomes the top screen; popping it exposes
        /// the unchanged recruitment-weight layer on its original host screen.
        /// </summary>
        private void OpenEncyclopedia(RecruitmentWeightTroopItemVM item)
        {
            if (item?.Troop == null || string.IsNullOrEmpty(item.Troop.EncyclopediaLink))
                return;

            Campaign.Current?.EncyclopediaManager.GoToLink(item.Troop.EncyclopediaLink);
        }
    }
}
