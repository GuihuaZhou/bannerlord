using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Services
{
    public sealed class ReinforcementCargoService
    {
        public int RegisterOwnTroopCargo(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            if (carrier == null) return 0;
            int surplus = ForceManagementFacade.Current.GetSafeFieldSurplus(carrier);
            int amount = surplus < 20 ? 0 : Math.Min(surplus,
                Math.Max(0, mission.RequestedTroopCount));
            mission.RequestedTroopCount -= amount;
            return amount;
        }

        public int LoadFromSourceGarrison(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            MobileParty source = mission?.TroopSource?.Town?.GarrisonParty;
            if (carrier == null || source == null ||
                carrier.CurrentSettlement != mission.TroopSource ||
                mission.TroopSource.OwnerClan != carrier.ActualClan ||
                mission.TroopSource.IsUnderSiege) return 0;

            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(mission.TroopSource);
            int safe = ForceManagementFacade.Current.GarrisonDefense
                .GetSafeTransferOutCount(defense, source.MemberRoster);
            int free = Math.Max(0, carrier.Party.PartySizeLimit -
                carrier.MemberRoster.TotalRegulars);
            int remaining = Math.Min(Math.Max(0, mission.RequestedTroopCount),
                Math.Min(safe, free));
            int loaded = 0;
            Dictionary<CharacterObject, int> protectedCounts =
                GetProtectedCounts(source);
            foreach (TroopRosterElement element in source.MemberRoster
                .GetTroopRoster().ToList())
            {
                CharacterObject troop = element.Character;
                if (troop == null || troop.IsHero || remaining <= 0) continue;
                int protectedCount = protectedCounts.TryGetValue(troop,
                    out int value) ? value : 0;
                int amount = Math.Min(remaining,
                    Math.Max(0, element.Number - protectedCount));
                if (amount <= 0) continue;
                int wounded = Math.Min(amount, element.WoundedNumber);
                source.MemberRoster.AddToCounts(troop, -amount, false,
                    -wounded, 0, true, -1);
                carrier.MemberRoster.AddToCounts(troop, amount, false,
                    wounded, 0, true, -1);
                loaded += amount;
                remaining -= amount;
            }
            mission.RequestedTroopCount = Math.Max(0,
                mission.RequestedTroopCount - loaded);
            return loaded;
        }

        public int UnloadTroops(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            MobileParty target = mission?.Destination?.Town?.GarrisonParty;
            if (carrier == null || target == null ||
                carrier.CurrentSettlement != mission.Destination ||
                mission.Destination.OwnerClan != carrier.ActualClan ||
                mission.Destination.IsUnderSiege) return 0;

            int remaining = Math.Min(
                GarrisonRecruitmentCapacity.GetTransferFreeSlots(target),
                ForceManagementFacade.Current.GetSafeFieldSurplus(carrier));
            int delivered = 0;
            Dictionary<CharacterObject, int> protectedCounts =
                GetProtectedCounts(carrier);
            foreach (TroopRosterElement element in carrier.MemberRoster
                .GetTroopRoster().ToList())
            {
                CharacterObject troop = element.Character;
                if (troop == null || troop.IsHero || remaining <= 0) continue;
                int protectedCount = protectedCounts.TryGetValue(troop,
                    out int value) ? value : 0;
                int amount = Math.Min(remaining,
                    Math.Max(0, element.Number - protectedCount));
                if (amount <= 0) continue;
                int wounded = Math.Min(amount, element.WoundedNumber);
                carrier.MemberRoster.AddToCounts(troop, -amount, false,
                    -wounded, 0, true, -1);
                target.MemberRoster.AddToCounts(troop, amount, false,
                    wounded, 0, true, -1);
                delivered += amount;
                remaining -= amount;
            }
            return delivered;
        }

        private static Dictionary<CharacterObject, int> GetProtectedCounts(
            MobileParty party) =>
            Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                .GetServingTroopCounts(party) ??
                    new Dictionary<CharacterObject, int>();
    }
}
