using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "OnSettlementEntered")]
    public static class PrisonerSaleOnSettlementEnteredPatch
    {
        public static bool Prefix(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (mobileParty != null
                && !mobileParty.IsMainParty
                && settlement.IsFortification
                && mobileParty.MapFaction != null
                && !mobileParty.IsDisbanding
                && !mobileParty.MapFaction.IsAtWarWith(settlement.MapFaction)
                && (mobileParty.PrisonRoster.TotalRegulars > 0 || (mobileParty.PrisonRoster.TotalHeroes > 0 && mobileParty.PrisonRoster.GetTroopRoster().Exists((TroopRosterElement x) => x.Character != CharacterObject.PlayerCharacter && x.Character.HeroObject.MapFaction.IsAtWarWith(settlement.MapFaction)))))
            {
                TroopRoster transferRoster = TroopRoster.CreateDummyTroopRoster();
                TroopRoster saleRoster = TroopRoster.CreateDummyTroopRoster();
                int retainCapacity = Math.Max(
                    0,
                    mobileParty.Party.PrisonerSizeLimit / 2);
                int transferCapacity = settlement.OwnerClan == mobileParty.Owner.Clan
                    ? Math.Max(
                        0,
                        settlement.Party.PrisonerSizeLimit -
                            settlement.Party.PrisonRoster.TotalManCount)
                    : 0;

                foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
                {
                    if (troopRosterElement.Character.IsHero)
                    {
                        // Match the native hero-prisoner rule while regular
                        // prisoners continue through the mod policy below.
                        if (!troopRosterElement.Character.IsPlayerCharacter &&
                            troopRosterElement.Character.HeroObject.MapFaction
                                .IsAtWarWith(settlement.MapFaction))
                        {
                            transferRoster.Add(troopRosterElement);
                        }
                        continue;
                    }

                    var troop = troopRosterElement.Character;
                    if (troop == null)
                    {
                        continue;
                    }

                    // Tier 3+ 对应原有“受训及以上”留用范围；
                    // 直接使用游戏数据，不再查询任何分类表。
                    bool useful = troop.Occupation == Occupation.Mercenary ||
                        troop.Tier >= 3;
                    int retained = useful
                        ? Math.Min(troopRosterElement.Number, retainCapacity)
                        : 0;
                    retainCapacity -= retained;
                    int remaining = troopRosterElement.Number - retained;
                    int transferred = useful
                        ? Math.Min(remaining, transferCapacity)
                        : 0;
                    transferCapacity -= transferred;
                    int sold = remaining - transferred;

                    AddPrisoners(
                        transferRoster,
                        troopRosterElement,
                        transferred);
                    AddPrisoners(saleRoster, troopRosterElement, sold);
                }

                if (transferRoster.TotalManCount > 0)
                {
                    MoveRegularPrisoners(
                        mobileParty.PrisonRoster,
                        settlement.Party.PrisonRoster,
                        transferRoster);

                    if (RecruitmentLogFilter.ShouldLog(mobileParty))
                    {
                        TextObject message = GameTexts.FindText(
                            "str_modifiedarmy_prisoner_transfer");
                        message.SetTextVariable(
                            "PARTY_NAME",
                            PartyLogFormatter.GetDisplayName(mobileParty));
                        message.SetTextVariable(
                            "COUNT",
                            transferRoster.TotalRegulars);
                        message.SetTextVariable(
                            "SETTLEMENT_NAME",
                            settlement.Name);
                        ModLogger.Info(message.ToString());
                    }
                }

                if (saleRoster.TotalRegulars > 0)
                {
                    SellPrisonersAction.ApplyForSelectedPrisoners(
                        mobileParty.Party,
                        null,
                        saleRoster);
                }
            }
            return false;
        }

        private static void AddPrisoners(
            TroopRoster target,
            TroopRosterElement source,
            int count)
        {
            if (count <= 0)
            {
                return;
            }

            int wounded = Math.Min(
                count,
                MBRandom.RoundRandomized(
                    source.WoundedNumber * (float)count / source.Number));
            target.AddToCounts(
                source.Character,
                count,
                false,
                wounded,
                0,
                true,
                -1);
        }

        private static void MoveRegularPrisoners(
            TroopRoster source,
            TroopRoster target,
            TroopRoster transfer)
        {
            foreach (TroopRosterElement element in transfer.GetTroopRoster())
            {
                source.AddToCounts(
                    element.Character,
                    -element.Number,
                    false,
                    -element.WoundedNumber,
                    0,
                    true,
                    -1);
                target.AddToCounts(
                    element.Character,
                    element.Number,
                    false,
                    element.WoundedNumber,
                    0,
                    true,
                    -1);
            }
        }
    }


    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "DailyTickSettlement")]
    public static class PrisonerSaleDailySettlementPatch
    {
        private static float GetAISellPrisonerRatio(Settlement settlement)
        {
            // 默认：每天卖 10%（与原版一致），可调整
            return 0.01f;
        }

        private static float GetAIRecruitPrisonerRatio(Settlement settlement)
        {
            // 默认：每天有 5% 概率尝试招募每个高价值俘虏
            return 0.1f;
        }

        public static bool Prefix(Settlement settlement)
        {
            // The unified behavior owns AI recruit-then-sell order. Player
            // prisons are deliberately never cleaned up automatically.
            return false;
        }
    }

}
