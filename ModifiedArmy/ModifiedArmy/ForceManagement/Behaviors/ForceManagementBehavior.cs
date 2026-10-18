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
                if (party == null || !party.IsActive || party.IsMainParty ||
                    party.ActualClan == null ||
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
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_force_target");
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
                message.SetTextVariable(
                    "REASON",
                    target.GapCount > 0
                        ? new TextObject(
                            "{=ModifiedArmy_ForceReasonBelowTarget}" +
                            "Below target")
                        : new TextObject(
                            "{=ModifiedArmy_ForceReasonTargetReached}" +
                            "Target reached"));
                ModLogger.FileNotice(message.ToString());
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
                    (int)System.Math.Ceiling(snapshot.RequiredDefenseValue),
                    snapshot.IsEnemyBorder);
                string id = settlement.StringId ?? settlement.Name.ToString();
                if (_garrisonStates.TryGetValue(id, out string previous) &&
                    previous == state)
                {
                    continue;
                }

                _garrisonStates[id] = state;
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_garrison_defense_evaluation");
                message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                message.SetTextVariable(
                    "CURRENT",
                    snapshot.CurrentRegularCount);
                message.SetTextVariable(
                    "REQUIRED",
                    (int)System.Math.Ceiling(
                        snapshot.RequiredDefenseValue));
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
                    (int)System.Math.Floor(
                        snapshot.LocalSevenDayRecoveryValue));
                message.SetTextVariable(
                    "REQUESTED",
                    need.RequestedTroops);
                message.SetTextVariable(
                    "DECISION",
                    need.IsRequested
                        ? new TextObject(
                            "{=ModifiedArmy_ReinforcementDecisionRequest}" +
                            "request reinforcement")
                        : new TextObject(
                            "{=ModifiedArmy_ReinforcementDecisionWait}" +
                            "do not request reinforcement"));
                message.SetTextVariable(
                    "REASON",
                    GetReinforcementReasonText(need.ReasonCode));
                ModLogger.FileNotice(message.ToString());
            }
        }

        private static TextObject GetReinforcementReasonText(string reason)
        {
            switch (reason)
            {
                case "BorderGarrisonBelowMinimum":
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonBorder}" +
                        "the border garrison remains below its minimum");
                case "InteriorGarrisonBelowMinimum":
                    return new TextObject(
                        "{=ModifiedArmy_ReinforcementReasonInterior}" +
                        "the interior garrison remains below its minimum");
                case "LocalRecoveryExpected":
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
                case ForceScenario.ArmyGathering:
                    return new TextObject(
                        "{=ModifiedArmy_ForceScenarioArmyGathering}" +
                        "Army gathering");
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
