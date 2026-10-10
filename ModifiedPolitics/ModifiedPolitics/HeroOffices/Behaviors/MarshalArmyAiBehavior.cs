using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
{
    /// <summary>
    /// Adds vanilla military target scoring for marshals excluded by the native clan-leader gate.
    /// </summary>
    public sealed class MarshalArmyAiBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<Kingdom, string> _lastStates = new Dictionary<Kingdom, string>();
        private AiMilitaryBehavior _aiMilitaryBehavior;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _aiMilitaryBehavior = Campaign.Current?.GetCampaignBehavior<AiMilitaryBehavior>();
        }

        private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
        {
            if (party?.LeaderHero == null || thinkParams == null || !party.IsActive)
                return;

            Kingdom kingdom = party.MapFaction as Kingdom;
            if (kingdom == null || kingdom.IsEliminated)
                return;

            Hero marshal = GetMarshal(kingdom);
            if (marshal == null || party.LeaderHero != marshal)
                return;

            if (marshal == Hero.MainHero)
            {
                SetState(kingdom, "player", "MP_MarshalArmyAiPlayer", "marshal is the player and remains under manual control");
                return;
            }

            if (!OfficeRules.IsEligible(marshal, kingdom, OfficeType.Marshal))
            {
                SetState(kingdom, "ineligible", "MP_MarshalArmyAiIneligible", "marshal no longer leads an eligible war party");
                return;
            }

            if (party.Army != null)
            {
                bool leadsArmy = party.Army.LeaderParty == party;
                SetState(
                    kingdom,
                    leadsArmy ? "army-active" : "attached",
                    leadsArmy ? "MP_MarshalArmyAiArmyActive" : "MP_MarshalArmyAiAttached",
                    leadsArmy ? "marshal already leads an army" : "marshal is attached to another army");
                return;
            }

            if (ShouldUseVanillaPath(party))
            {
                SetState(kingdom, "vanilla", "MP_MarshalArmyAiVanilla", "marshal qualifies for the vanilla army creation path");
                return;
            }

            if (_aiMilitaryBehavior == null)
                _aiMilitaryBehavior = Campaign.Current?.GetCampaignBehavior<AiMilitaryBehavior>();
            if (_aiMilitaryBehavior == null)
            {
                SetState(kingdom, "missing-ai", "MP_MarshalArmyAiMissingBehavior", "native military AI behavior is unavailable");
                return;
            }

            if (party.IsCurrentlyAtSea || party.MapEvent != null
                || party.CurrentSettlement?.SiegeEvent != null)
            {
                SetState(kingdom, "busy", "MP_MarshalArmyAiBusy", "marshal party is currently busy");
                return;
            }

            if (party.PartySizeRatio <= 0.6f)
            {
                SetState(kingdom, "strength", "MP_MarshalArmyAiLowStrength", "marshal party strength is below 60 percent");
                return;
            }

            if (!kingdom.FactionsAtWarWith.Any(faction => faction?.Fiefs?.Any() == true))
            {
                SetState(kingdom, "peace", "MP_MarshalArmyAiNoTarget", "kingdom has no enemy faction with a fief");
                return;
            }

            // The current game model owns influence, food, distance and callable-party checks.
            // This bypasses only AiMilitaryBehavior's clan-leader identity gate.
            if (!Campaign.Current.Models.ArmyManagementCalculationModel.CanLordCreateArmy(
                    party,
                    out MBList<MobileParty> candidates))
            {
                SetState(kingdom, "conditions", "MP_MarshalArmyAiConditions", "native army creation conditions are not met");
                return;
            }

            if (candidates == null || candidates.Count == 0)
            {
                SetState(kingdom, "members", "MP_MarshalArmyAiNoMembers", "no party can be called to the army");
                return;
            }

            thinkParams.SetArmyMembers(candidates);
            float gatheringStrength = party.Party.GetCustomStrength(
                BattleSideEnum.Attacker,
                MapEvent.PowerCalculationContext.Siege);
            foreach (MobileParty candidate in candidates)
            {
                gatheringStrength += candidate.Party.GetCustomStrength(
                    BattleSideEnum.Attacker,
                    MapEvent.PowerCalculationContext.Siege);
            }

            thinkParams.WillGatherAnArmy = true;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    Army.ArmyTypes armyType = (Army.ArmyTypes)i;
                    if (armyType != Army.ArmyTypes.Raider)
                    {
                        _aiMilitaryBehavior.FindBestTargetAndItsValueForFaction(
                            armyType,
                            thinkParams,
                            gatheringStrength);
                    }
                }
            }
            finally
            {
                thinkParams.WillGatherAnArmy = false;
            }

            SetState(kingdom, "evaluating", "MP_MarshalArmyAiEvaluating", "marshal entered native army target evaluation");
        }

        private static bool ShouldUseVanillaPath(MobileParty party)
        {
            Hero leader = party?.LeaderHero;
            Clan clan = leader?.Clan;
            if (leader == null || clan == null)
                return false;

            if (clan.Leader == leader)
                return true;

            return clan.Leader?.PartyBelongedTo == null
                   && clan.WarPartyComponents?.FirstOrDefault() == party.WarPartyComponent;
        }

        private static Hero GetMarshal(Kingdom kingdom)
        {
            return HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Select(assignment => assignment?.Hero)
                .FirstOrDefault(hero => hero != null);
        }

        private void SetState(
            Kingdom kingdom,
            string state,
            string textId,
            string fallbackReason)
        {
            if (kingdom == null || _lastStates.TryGetValue(kingdom, out string previous) && previous == state)
                return;

            _lastStates[kingdom] = state;
            if (Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(
                $"{{={textId}}}[Hero Offices] Marshal army AI in {{KINGDOM}}: {fallbackReason}.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            if (state == "army-active")
                ModLogger.Notice(message.ToString());
            else
                ModLogger.Info(message.ToString());
        }
    }
}
