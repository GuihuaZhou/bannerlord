using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Raises the experience cost of high-tier troop upgrades while preserving
    /// a positive cost for every valid upgrade edge.
    /// </summary>
    public class NewPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
    {
        private const int MinimumUpgradeXp = 100;

        public override int GetXpCostForUpgrade(
            PartyBase party,
            CharacterObject characterObject,
            CharacterObject upgradeTarget)
        {
            if (characterObject == null || upgradeTarget == null)
            {
                return 100000000;
            }

            if (
                characterObject.UpgradeTargets == null ||
                !characterObject.UpgradeTargets.Contains(upgradeTarget))
            {
                return 100000000;
            }

            int customCost = 0;
            for (int tier = characterObject.Tier + 1;
                tier <= upgradeTarget.Tier;
                tier++)
            {
                customCost += GetTierStepCost(
                    tier,
                    upgradeTarget.Level);
            }

            if (customCost <= 0)
            {
                // Same-tier conversions still pay one complete step at the
                // target tier, rather than the token minimum alone.
                customCost = GetTierStepCost(
                    Math.Max(1, upgradeTarget.Tier),
                    upgradeTarget.Level);
            }

            // Bannerlord and the former implementation both produce zero for
            // a same-tier upgrade because their tier loop has no iterations.
            // Such an edge must still consume experience; otherwise every
            // troop on that edge appears immediately upgradeable after load.
            int nativeCost = base.GetXpCostForUpgrade(
                party,
                characterObject,
                upgradeTarget);
            return Math.Max(
                MinimumUpgradeXp,
                Math.Max(nativeCost, customCost));
        }

        private static int GetTierStepCost(int tier, int targetLevel)
        {
            if (tier <= 1)
            {
                return 100;
            }

            if (tier == 2)
            {
                return 300;
            }

            if (tier == 3)
            {
                return 550;
            }

            if (tier == 4)
            {
                return 1800;
            }

            if (tier == 5)
            {
                return 2600;
            }

            if (tier == 6)
            {
                return 3400;
            }

            if (tier == 7)
            {
                return 4200;
            }

            int levelFactor = targetLevel + 4;
            return (int)(1.333f * levelFactor * levelFactor);
        }
    }
}
