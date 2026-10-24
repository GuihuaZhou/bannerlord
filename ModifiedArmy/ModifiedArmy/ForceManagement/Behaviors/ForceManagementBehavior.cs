using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Tool;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Records the daily force target selected by the shared policy model.
    /// Strategic reinforcement remains disabled until its safety gates exist.
    /// </summary>
    public sealed class ForceManagementBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<string, string> _garrisonStates =
            new Dictionary<string, string>();
        private readonly Dictionary<MobileParty, string> _partyStates =
            new Dictionary<MobileParty, string>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailyTick()
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            if (playerKingdom == null)
            {
                return;
            }

            foreach (MobileParty party in MobileParty.AllLordParties)
            {
                if (!ManagedPartyPolicy.IsModManagedAiParty(party) ||
                    party.ActualClan.Kingdom != playerKingdom)
                {
                    continue;
                }

                PartyForceTargetResult target = ForceManagementFacade.Current
                    .GetPartyTarget(party);
                string partyState = string.Join(
                    "|",
                    target.Scenario,
                    target.TargetCount,
                    target.GapCount > 0);
                if (_partyStates.TryGetValue(
                    party,
                    out string previousPartyState) &&
                    previousPartyState == partyState)
                {
                    continue;
                }

                _partyStates[party] = partyState;
                string templateId = target.GapCount > 0
                    ? "str_modifiedarmy_force_target_below"
                    : target.CurrentCount > target.TargetCount
                        ? "str_modifiedarmy_force_target_above"
                        : "str_modifiedarmy_force_target_met";
                TextObject message = GameTexts.FindText(
                    templateId);
                message.SetTextVariable(
                    "PARTY_NAME",
                    PartyLogFormatter.GetDisplayName(party));
                message.SetTextVariable(
                    "SCENARIO",
                    GetScenarioText(target.Scenario));
                message.SetTextVariable("DISPOSITION", target.WarDisposition);
                message.SetTextVariable("CURRENT", target.CurrentCount);
                message.SetTextVariable("TARGET", target.TargetCount);
                message.SetTextVariable("LIMIT", target.PartyLimit);
                message.SetTextVariable("GAP", target.GapCount);
                // Routine force targets are diagnostic state, even when the state
                // changed since the previous observation. Keeping them at Debug
                // prevents normal daily planning from dominating the event log.
                ModLogger.Debug(message.ToString());
            }

            LogChangedGarrisonStates(playerKingdom);
        }

        private void LogChangedGarrisonStates(Kingdom playerKingdom)
        {
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement?.IsFortification != true ||
                    settlement.OwnerClan?.Kingdom != playerKingdom ||
                    settlement.Town?.GarrisonParty == null)
                {
                    continue;
                }

                GarrisonDefenseSnapshot snapshot = ForceManagementFacade
                    .Current.GarrisonDefense.Evaluate(settlement);
                ReinforcementNeedResult need = ForceManagementFacade.Current
                    .ReinforcementNeed.Evaluate(snapshot);
                string state = string.Join(
                    "|",
                    need.IsRequested,
                    need.ReasonCode,
                    snapshot.EffectiveGarrisonCount,
                    snapshot.MinimumCount,
                    snapshot.PredictedSevenDayRecruitment,
                    need.RequestedTroops,
                    snapshot.IsEnemyBorder);
                string id = settlement.StringId ?? settlement.Name.ToString();
                if (_garrisonStates.TryGetValue(id, out string previous) &&
                    previous == state)
                {
                    continue;
                }

                _garrisonStates[id] = state;
                // A reinforcement request is an action, while a satisfied defense
                // requirement is only a status observation. Separate templates keep
                // the normal path from reading like a zero-sized request.
                TextObject message = GameTexts.FindText(
                    need.IsRequested
                        ? "str_modifiedarmy_garrison_reinforcement_requested"
                        : "str_modifiedarmy_garrison_reinforcement_not_needed");
                message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                message.SetTextVariable(
                    "CURRENT",
                    snapshot.EffectiveGarrisonCount);
                message.SetTextVariable(
                    "REQUIRED",
                    snapshot.MinimumCount);
                message.SetTextVariable(
                    "LOCATION",
                    snapshot.IsEnemyBorder
                        ? new TextObject(
                            "{=ModifiedArmy_GarrisonLocationBorder}" +
                            "border settlement")
                        : new TextObject(
                            "{=ModifiedArmy_GarrisonLocationInterior}" +
                            "interior settlement"));
                message.SetTextVariable(
                    "RECOVERY",
                    snapshot.PredictedSevenDayRecruitment);
                if (need.IsRequested)
                    message.SetTextVariable("REQUESTED", need.RequestedTroops);
                message.SetTextVariable(
                    "REASON",
                    GetReinforcementReasonText(need.ReasonCode));
                // A settlement that already meets its defense requirement is not
                // an incident. The changed-state cache still preserves useful
                // diagnostics without promoting evaluations to Notice.
                ModLogger.Debug(message.ToString());
            }
        }

        private static TextObject GetReinforcementReasonText(string reason)
        {
            switch (reason)
            {
                case "BorderBelowMinimum":
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonBorder}" +
                        "the current border garrison is below its minimum");
                case "InteriorBelowMinimumAfterForecast":
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonInterior}" +
                        "the interior garrison remains below its minimum after forecast recruitment");
                case "InteriorForecastCoversMinimum":
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonRecovery}" +
                        "local recruitment should cover the shortage");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonMet}" +
                        "the defense requirement is already met");
            }
        }

        private static TextObject GetScenarioText(ForceScenario scenario)
        {
            switch (scenario)
            {
                case ForceScenario.WarRoutine:
                    return new TextObject(
                        "{=ModifiedArmy_ForceScenarioWarRoutine}" +
                        "Routine war");
                case ForceScenario.OwnFiefDefense:
                    return new TextObject(
                        "{=ModifiedArmy_ForceScenarioOwnFiefDefense}" +
                        "Own fief defense");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_ForceScenarioPeace}Peace");
            }
        }
    }
}
