using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnDailyTick()
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
