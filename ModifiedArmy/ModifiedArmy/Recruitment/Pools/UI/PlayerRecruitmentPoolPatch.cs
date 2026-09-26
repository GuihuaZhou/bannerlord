using HarmonyLib;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Pools.UI
{
    /// <summary>
    /// Reuses Bannerlord's recruitment popup and cart while replacing its
    /// notable-owned slots with the current fortification's professional pool.
    /// The original VolunteerTypes collection remains untouched.
    /// </summary>
    [HarmonyPatch(typeof(RecruitmentVM))]
    public static class PlayerRecruitmentPoolPatch
    {
        private const int SlotsPerRow = 6;

        private static readonly MethodInfo RefreshPartyPropertiesMethod =
            AccessTools.Method(typeof(RecruitmentVM),
                "RefreshPartyProperties");

        [HarmonyPostfix]
        [HarmonyPatch("RefreshScreen")]
        private static void RefreshScreenPostfix(RecruitmentVM __instance)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            if (settlement?.IsFortification != true)
            {
                return;
            }

            SettlementRecruitmentPoolBehavior pools = Campaign.Current
                ?.GetCampaignBehavior<SettlementRecruitmentPoolBehavior>();
            if (pools == null)
            {
                return;
            }

            __instance.VolunteerList.Clear();
            __instance.TroopsInCart.Clear();

            Hero displayOwner = settlement.Town?.Governor
                ?? settlement.OwnerClan?.Leader
                ?? Hero.MainHero;
            if (displayOwner == null)
            {
                return;
            }

            List<CharacterObject> availableTroops = pools
                .GetAvailableTroops(
                    settlement,
                    RecruitmentPoolKind.Professional)
                .Where(entry => entry.Key != null && entry.Value > 0)
                .OrderBy(entry => entry.Key.Tier)
                .ThenBy(entry => entry.Key.Name.ToString())
                .SelectMany(entry => Enumerable.Repeat(
                    entry.Key,
                    entry.Value))
                .ToList();
            bool canRecruit = settlement.OwnerClan == Clan.PlayerClan;

            for (int offset = 0;
                offset < availableTroops.Count;
                offset += SlotsPerRow)
            {
                List<CharacterObject> row = availableTroops
                    .Skip(offset)
                    .Take(SlotsPerRow)
                    .ToList();
                RecruitVolunteerVM rowVm = new RecruitVolunteerVM(
                    displayOwner,
                    row,
                    (owner, troop) => AddToCart(
                        __instance,
                        owner,
                        troop),
                    (owner, troop) => RemoveFromCart(
                        __instance,
                        owner,
                        troop));

                // Professional manpower is owned by the settlement rather
                // than a notable, so relation-based slot locks do not apply.
                foreach (RecruitVolunteerTroopVM troop in rowVm.Troops)
                {
                    troop.PlayerHasEnoughRelation = true;
                    troop.CanBeRecruited =
                        canRecruit && troop.Character != null;
                }

                __instance.VolunteerList.Add(rowVm);
            }

            RefreshPartyPropertiesMethod?.Invoke(__instance, null);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnDone")]
        private static bool OnDonePrefix(RecruitmentVM __instance)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            if (settlement?.IsFortification != true)
            {
                return true;
            }

            // Foreign pools are inspectable but never mutable. Normally their
            // grey troop entries keep the cart empty; this guard also blocks
            // programmatic or UI-level attempts to bypass that restriction.
            if (settlement.OwnerClan != Clan.PlayerClan)
            {
                __instance.Deactivate();
                return false;
            }

            SettlementRecruitmentPoolBehavior pools = Campaign.Current
                ?.GetCampaignBehavior<SettlementRecruitmentPoolBehavior>();
            if (pools == null)
            {
                return true;
            }

            List<RecruitVolunteerTroopVM> cart =
                __instance.TroopsInCart.ToList();
            int totalCost = cart.Sum(item => item.Cost);
            if (totalCost > Hero.MainHero.Gold)
            {
                return false;
            }

            Dictionary<CharacterObject, int> requested = cart
                .Where(item => item.Character != null)
                .GroupBy(item => item.Character)
                .ToDictionary(group => group.Key, group => group.Count());
            IReadOnlyDictionary<CharacterObject, int> available =
                pools.GetAvailableTroops(
                    settlement,
                    RecruitmentPoolKind.Professional);

            // Validate the complete cart before mutating either the saved pool
            // or the player's party, keeping the transaction all-or-nothing.
            if (requested.Any(entry =>
                !available.TryGetValue(entry.Key, out int count) ||
                count < entry.Value))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GameTexts.FindText(
                        "str_modifiedarmy_recruits_unavailable").ToString()));
                return false;
            }

            foreach (KeyValuePair<CharacterObject, int> entry in requested)
            {
                if (!pools.TryConsume(
                    settlement,
                    RecruitmentPoolKind.Professional,
                    entry.Key,
                    entry.Value))
                {
                    return false;
                }

                MobileParty.MainParty.MemberRoster.AddToCounts(
                    entry.Key,
                    entry.Value,
                    false,
                    0,
                    0,
                    true,
                    -1);
                CampaignEventDispatcher.Instance.OnUnitRecruited(
                    entry.Key,
                    entry.Value);
            }

            GiveGoldAction.ApplyBetweenCharacters(
                Hero.MainHero,
                null,
                totalCost,
                true);
            if (totalCost > 0)
            {
                MBTextManager.SetTextVariable(
                    "GOLD_AMOUNT",
                    Math.Abs(totalCost));
                InformationManager.DisplayMessage(new InformationMessage(
                    GameTexts.FindText(
                        "str_gold_removed_with_icon").ToString(),
                    "event:/ui/notification/coins_negative"));
            }

            __instance.Deactivate();
            return false;
        }

        private static void AddToCart(
            RecruitmentVM screen,
            RecruitVolunteerVM owner,
            RecruitVolunteerTroopVM troop)
        {
            if (!troop.CanBeRecruited)
            {
                return;
            }

            owner.OnRecruitMoveToCart(troop);
            troop.CanBeRecruited = false;
            troop.IsInCart = true;
            screen.TroopsInCart.Add(troop);
            CampaignEventDispatcher.Instance.OnPlayerStartRecruitment(
                troop.Character);
            RefreshPartyPropertiesMethod?.Invoke(screen, null);
        }

        private static void RemoveFromCart(
            RecruitmentVM screen,
            RecruitVolunteerVM owner,
            RecruitVolunteerTroopVM troop)
        {
            if (!screen.TroopsInCart.Contains(troop))
            {
                return;
            }

            owner.OnRecruitRemovedFromCart(troop);
            troop.CanBeRecruited = true;
            troop.IsInCart = false;
            troop.IsHiglightEnabled = false;
            screen.TroopsInCart.Remove(troop);
            RefreshPartyPropertiesMethod?.Invoke(screen, null);
        }
    }
}
