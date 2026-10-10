using System.Collections.Generic;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using Helpers;
using ModifiedPolitics.Governor.Config;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.Governor.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomFief.UI
{
    /// <summary>
    /// Adds governor display and ruler-only kingdom assignment controls to a kingdom fief item.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    public sealed class KingdomSettlementGovernorVMMixin
        : BaseViewModelMixin<KingdomSettlementItemVM>
    {
        private readonly KingdomSettlementItemVM _vm;
        private HeroVM _governorVisual;
        private BasicTooltipViewModel _governorHint;
        private ClanCardSelectionPopupVM _governorSelectionPopup;
        private string _governorText;
        private string _governorName;
        private bool _hasGovernor;
        private bool _canManageGovernor;
        private readonly GovernorManualAssignmentService _manualAssignmentService =
            new GovernorManualAssignmentService();

        public KingdomSettlementGovernorVMMixin(KingdomSettlementItemVM vm)
            : base(vm)
        {
            _vm = vm;
            _governorVisual = new HeroVM(null, true);
            _governorHint = new BasicTooltipViewModel();
            _governorSelectionPopup = new ClanCardSelectionPopupVM();
            RefreshGovernor();
        }

        [DataSourceProperty]
        public HeroVM GovernorVisual
        {
            get => _governorVisual;
            private set
            {
                if (value == _governorVisual)
                    return;

                _governorVisual = value;
                _vm.OnPropertyChangedWithValue(value, nameof(GovernorVisual));
            }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel GovernorHint
        {
            get => _governorHint;
            private set
            {
                if (value == _governorHint)
                    return;

                _governorHint = value;
                _vm.OnPropertyChangedWithValue(value, nameof(GovernorHint));
            }
        }

        [DataSourceProperty]
        public ClanCardSelectionPopupVM GovernorSelectionPopup
        {
            get => _governorSelectionPopup;
            private set
            {
                if (value == _governorSelectionPopup)
                    return;

                _governorSelectionPopup = value;
                _vm.OnPropertyChangedWithValue(value, nameof(GovernorSelectionPopup));
            }
        }

        [DataSourceProperty]
        public string GovernorText
        {
            get => _governorText;
            private set
            {
                if (value == _governorText)
                    return;

                _governorText = value;
                _vm.OnPropertyChangedWithValue(value, nameof(GovernorText));
            }
        }

        [DataSourceProperty]
        public string GovernorName
        {
            get => _governorName;
            private set
            {
                if (value == _governorName)
                    return;

                _governorName = value;
                _vm.OnPropertyChangedWithValue(value, nameof(GovernorName));
            }
        }

        [DataSourceProperty]
        public bool HasGovernor
        {
            get => _hasGovernor;
            private set
            {
                if (value == _hasGovernor)
                    return;

                _hasGovernor = value;
                _vm.OnPropertyChangedWithValue(value, nameof(HasGovernor));
            }
        }

        [DataSourceProperty]
        public bool CanManageGovernor
        {
            get => _canManageGovernor;
            private set
            {
                if (value == _canManageGovernor)
                    return;

                _canManageGovernor = value;
                _vm.OnPropertyChangedWithValue(value, nameof(CanManageGovernor));
            }
        }

        public override void OnRefresh()
        {
            RefreshGovernor();
        }

        public void ExecuteManageGovernor()
        {
            RefreshGovernor();
            if (!CanManageGovernor)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        GetGovernorManagementUnavailableReason().ToString()));
                return;
            }

            GovernorSelectionPopup.Open(
                new ClanCardSelectionInfo(
                    new TextObject("{=MP_AssignGovernor}Assign Governor"),
                    BuildGovernorCandidates(),
                    OnGovernorSelectionOver,
                    false,
                    1,
                    1));
        }

        private void RefreshGovernor()
        {
            GovernorText = GameTexts.FindText("str_sort_by_governor_label").ToString();

            Hero governor = GetCurrentGovernor();
            HasGovernor = governor != null;
            GovernorName = governor?.Name?.ToString()
                           ?? new TextObject("{=MP_NoGovernor}No Governor").ToString();
            GovernorVisual = new HeroVM(governor, true);
            GovernorHint = governor == null
                ? new BasicTooltipViewModel()
                : new BasicTooltipViewModel(
                    () => CampaignUIHelper.GetHeroGovernorEffectsTooltip(
                        governor,
                        _vm.Settlement));

            CanManageGovernor = CanPlayerManageGovernor(governor);
        }

        private Hero GetCurrentGovernor()
        {
            if (_vm.Settlement?.Town == null)
                return null;

            return _vm.Settlement.Town.Governor
                   ?? CampaignUIHelper.GetTeleportingGovernor(
                       _vm.Settlement,
                       Campaign.Current.GetCampaignBehavior<ITeleportationCampaignBehavior>());
        }

        private bool CanPlayerManageGovernor(Hero currentGovernor)
        {
            Kingdom kingdom = _vm.Settlement?.OwnerClan?.Kingdom;
            return kingdom != null
                   && !kingdom.IsEliminated
                   && Clan.PlayerClan?.Kingdom == kingdom
                   && kingdom.RulingClan == Clan.PlayerClan
                   && GovernorPolicyManager.Instance.IsCentralizedAssignment(kingdom.Culture)
                   && (currentGovernor == null || !currentGovernor.IsTraveling);
        }

        private TextObject GetGovernorManagementUnavailableReason()
        {
            Kingdom kingdom = _vm.Settlement?.OwnerClan?.Kingdom;
            if (kingdom == null || Clan.PlayerClan?.Kingdom != kingdom)
            {
                return new TextObject(
                    "{=MP_GovernorManageWrongKingdom}You can only manage governors in your current kingdom.");
            }

            if (kingdom.RulingClan != Clan.PlayerClan)
            {
                return new TextObject(
                    "{=MP_GovernorManageRulerOnly}Only the kingdom ruler can assign governors from this page.");
            }

            if (!GovernorPolicyManager.Instance.IsCentralizedAssignment(kingdom.Culture))
            {
                return new TextObject(
                    "{=MP_GovernorManagePolicyDisabled}This kingdom does not use kingdom-wide governor assignment.");
            }

            Hero governor = GetCurrentGovernor();
            if (governor?.IsTraveling == true)
            {
                TextObject reason = new TextObject(
                    "{=MP_GovernorManageTraveling}{GOVERNOR} is traveling to take office at {SETTLEMENT}; the appointment cannot be changed yet.");
                reason.SetTextVariable("GOVERNOR", governor.Name);
                reason.SetTextVariable("SETTLEMENT", _vm.Settlement.Name);
                return reason;
            }

            return new TextObject(
                "{=MP_GovernorManageUnavailable}Governor assignment is currently unavailable.");
        }

        private IEnumerable<ClanCardSelectionItemInfo> BuildGovernorCandidates()
        {
            // The native special-action card represents removing the current governor.
            yield return new ClanCardSelectionItemInfo(
                new TextObject("{=MP_NoGovernor}No Governor"),
                false,
                TextObject.GetEmpty(),
                new TextObject("{=MP_RemoveGovernorResult}Remove the current governor."));

            Hero currentGovernor = GetCurrentGovernor();
            Kingdom kingdom = _vm.Settlement.OwnerClan.Kingdom;
            foreach (Hero hero in kingdom.Clans
                         .Where(IsEligibleClan)
                         .SelectMany(clan => clan.Heroes)
                         .Where(hero => IsEligibleCandidate(hero, kingdom, currentGovernor))
                         .OrderBy(hero => hero.Name.ToString()))
            {
                yield return CreateGovernorCandidate(hero);
            }
        }

        private ClanCardSelectionItemInfo CreateGovernorCandidate(Hero hero)
        {
            CharacterImageIdentifier image = new CharacterImageIdentifier(
                CharacterCode.CreateFrom(hero.CharacterObject));
            TextObject actionResult = new TextObject(
                "{=MP_GovernorSelectionResult}{HERO} will become governor of {SETTLEMENT}.");
            actionResult.SetTextVariable("HERO", hero.Name);
            actionResult.SetTextVariable("SETTLEMENT", _vm.Settlement.Name);

            return new ClanCardSelectionItemInfo(
                hero,
                hero.Name,
                image,
                CardSelectionItemSpriteType.Skill,
                DefaultSkills.Steward.StringId,
                hero.GetSkillValue(DefaultSkills.Steward).ToString(),
                GetGovernorCandidateProperties(hero),
                false,
                TextObject.GetEmpty(),
                actionResult);
        }

        private IEnumerable<ClanCardSelectionItemPropertyInfo>
            GetGovernorCandidateProperties(Hero hero)
        {
            yield return new ClanCardSelectionItemPropertyInfo(
                CampaignUIHelper.GetTeleportationDelayText(
                    hero,
                    _vm.Settlement.Party));

            var engineeringEffect =
                PerkHelper.GetGovernorEngineeringSkillEffectForHero(hero);
            yield return new ClanCardSelectionItemPropertyInfo(
                new TextObject("{=MP_GovernorEffects}Governor Effects"),
                engineeringEffect.Item2);

            foreach (PerkObject perk in PerkHelper.GetGovernorPerksForHero(hero))
            {
                TextObject description = perk.PrimaryRole == PartyRole.Governor
                    ? perk.PrimaryDescription
                    : perk.SecondaryDescription;
                yield return new ClanCardSelectionItemPropertyInfo(
                    perk.Name,
                    description);
            }
        }

        private static bool IsEligibleClan(Clan clan)
        {
            return clan != null
                   && !clan.IsEliminated
                   && !clan.IsClanTypeMercenary;
        }

        private bool IsEligibleCandidate(
            Hero hero,
            Kingdom kingdom,
            Hero currentGovernor)
        {
            if (hero == currentGovernor)
                return true;

            return hero != null
                   && hero.IsAlive
                   && hero.IsActive
                   && hero.Clan?.Kingdom == kingdom
                   && hero.PartyBelongedTo == null
                   && !hero.IsPrisoner
                   && !hero.IsTraveling
                   && GovernorCandidateSelector.IsOfficeCompatible(
                       hero,
                       GovernorAssignmentModel.BuildContext(kingdom, _vm.Settlement.Town))
                   && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
        }

        private void OnGovernorSelectionOver(
            List<object> selectedItems,
            System.Action closePopup)
        {
            Hero selectedGovernor = selectedItems?.FirstOrDefault() as Hero;
            Hero currentGovernor = GetCurrentGovernor();
            if (!CanPlayerManageGovernor(currentGovernor))
            {
                closePopup?.Invoke();
                RefreshGovernor();
                return;
            }

            Kingdom kingdom = _vm.Settlement.OwnerClan.Kingdom;
            if (selectedGovernor != null
                && !IsEligibleCandidate(selectedGovernor, kingdom, currentGovernor))
            {
                closePopup?.Invoke();
                RefreshGovernor();
                return;
            }

            if (selectedGovernor == currentGovernor)
            {
                closePopup?.Invoke();
                return;
            }

            bool applied = _manualAssignmentService.TryApply(
                kingdom,
                _vm.Settlement.Town,
                selectedGovernor);
            if (applied && selectedGovernor == null)
                LogGovernorRemoved(kingdom, currentGovernor);
            else if (applied)
                LogGovernorAssigned(kingdom, selectedGovernor);
            else
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=MP_GovernorManualChangeFailed}The governor change could not be applied because it would invalidate an office or campaign state changed.").ToString()));

            closePopup?.Invoke();
            RefreshGovernor();
        }

        private void LogGovernorAssigned(Kingdom kingdom, Hero governor)
        {
            TextObject message = new TextObject(
                "{=MP_GovernorPlayerAssigned}[Kingdom Governor Assignment] {KINGDOM} appointed {HERO} of {CLAN} as governor of {SETTLEMENT} by ruler order.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("HERO", governor.Name);
            message.SetTextVariable("CLAN", governor.Clan.Name);
            message.SetTextVariable("SETTLEMENT", _vm.Settlement.Name);
            ModLogger.Notice(message.ToString());
        }

        private void LogGovernorRemoved(Kingdom kingdom, Hero formerGovernor)
        {
            if (formerGovernor == null)
                return;

            TextObject message = new TextObject(
                "{=MP_GovernorPlayerRemoved}[Kingdom Governor Assignment] {KINGDOM} removed {HERO} from the governorship of {SETTLEMENT} by ruler order.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("HERO", formerGovernor.Name);
            message.SetTextVariable("SETTLEMENT", _vm.Settlement.Name);
            ModLogger.Notice(message.ToString());
        }
    }
}
