using ModifiedArmy.Garrison.Supply;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Centralizes garrison capacity calculations. Active supply-party
    /// escorts remain part of their home garrison's reserved establishment
    /// until the mission has completed.
    /// </summary>
    public static class GarrisonRecruitmentCapacity
    {
        private static readonly HashSet<string> ReportedMultipleParties =
            new HashSet<string>();

        public const int DefaultRecruitmentFillPercentage = 80;

        public static int GetActiveSupplyEscortCount(Settlement home)
        {
            if (home == null)
            {
                return 0;
            }

            int result = 0;
            int activePartyCount = 0;
            foreach (MobileParty party in MobileParty.All)
            {
                if (party == null || !party.IsActive ||
                    !(party.PartyComponent is SupplyPartyComponent component) ||
                    component.SupplyHomeSettlement != home ||
                    component.MissionState == SupplyPartyMissionState.Completed)
                {
                    continue;
                }

                result += Math.Max(0, party.Party.NumberOfAllMembers);
                activePartyCount++;
            }

            string settlementId = home.StringId ?? string.Empty;
            if (activePartyCount > 1 &&
                ReportedMultipleParties.Add(settlementId))
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_multiple_supply_parties");
                message.SetTextVariable("SETTLEMENT_ID", settlementId);
                message.SetTextVariable("PARTY_COUNT", activePartyCount);
                message.SetTextVariable("RESERVED", result);
                ModLogger.Debug(message.ToString());
            }
            else if (activePartyCount <= 1)
            {
                ReportedMultipleParties.Remove(settlementId);
            }

            return result;
        }

        public static int GetEffectiveMemberCount(MobileParty garrison)
        {
            return GetEffectiveMemberCount(
                garrison,
                GetActiveSupplyEscortCount(garrison?.CurrentSettlement));
        }

        public static int GetEffectiveMemberCount(
            MobileParty garrison,
            int reservedReturningMembers)
        {
            return Math.Max(0, garrison?.Party.NumberOfAllMembers ?? 0) +
                Math.Max(0, reservedReturningMembers);
        }

        public static int GetHardFreeSlots(MobileParty garrison)
        {
            return GetHardFreeSlots(
                garrison,
                GetActiveSupplyEscortCount(garrison?.CurrentSettlement));
        }

        public static int GetHardFreeSlots(Settlement home)
        {
            MobileParty garrison = home?.Town?.GarrisonParty;
            if (garrison != null)
            {
                return GetHardFreeSlots(garrison);
            }

            if (home?.Town == null || Campaign.Current == null)
            {
                return 0;
            }

            int limit = Math.Max(
                0,
                Campaign.Current.Models.PartySizeLimitModel
                    .CalculateGarrisonPartySizeLimit(home)
                    .RoundedResultNumber);
            return Math.Max(
                0,
                limit - GetActiveSupplyEscortCount(home));
        }

        public static int GetHardFreeSlots(
            MobileParty garrison,
            int reservedReturningMembers)
        {
            if (garrison == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                garrison.Party.PartySizeLimit -
                    GetEffectiveMemberCount(
                        garrison,
                        reservedReturningMembers));
        }

        public static int GetSoftRecruitmentFreeSlots(MobileParty garrison)
        {
            return GetSoftRecruitmentFreeSlots(
                garrison,
                GetActiveSupplyEscortCount(garrison?.CurrentSettlement));
        }

        public static int GetSoftRecruitmentFreeSlots(
            MobileParty garrison,
            int reservedReturningMembers)
        {
            if (garrison == null)
            {
                return 0;
            }

            int safePartySize = GetAutomaticRecruitmentTarget(
                garrison.Party.PartySizeLimit);
            return Math.Max(
                0,
                safePartySize -
                    GetEffectiveMemberCount(
                        garrison,
                        reservedReturningMembers));
        }

        public static int GetTransferFreeSlots(MobileParty garrison)
        {
            return GetTransferFreeSlots(
                garrison,
                GetActiveSupplyEscortCount(garrison?.CurrentSettlement));
        }

        public static int GetTransferFreeSlots(
            MobileParty garrison,
            int reservedReturningMembers)
        {
            if (garrison == null)
            {
                return 0;
            }

            float ratio = MilitaryConfigManager.Instance.GetActiveConfig()?
                .AiRecruitment.ForceManagement
                .ExistingTroopTransferRatio ?? 0.90f;
            int transferLimit = Math.Max(
                0,
                (int)Math.Floor(garrison.Party.PartySizeLimit * ratio));
            return Math.Max(
                0,
                transferLimit - GetEffectiveMemberCount(
                    garrison,
                    reservedReturningMembers));
        }

        public static int GetAutomaticRecruitmentTarget(int hardLimit)
        {
            float ratio = MilitaryConfigManager.Instance.GetActiveConfig()?
                .AiRecruitment.ForceManagement
                .GarrisonAutoRecruitmentRatio ??
                    DefaultRecruitmentFillPercentage / 100f;
            return Math.Max(
                0,
                (int)Math.Floor(Math.Max(0, hardLimit) * ratio));
        }
    }
}
