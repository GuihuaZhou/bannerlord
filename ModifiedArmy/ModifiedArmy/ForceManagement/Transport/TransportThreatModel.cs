using ModifiedArmy.common;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace ModifiedArmy.ForceManagement.Transport
{
    public static class TransportThreatModel
    {
        public static bool HasSuperiorEnemy(
            MobileParty carrier,
            CampaignVec2 position,
            IFaction faction)
        {
            if (faction == null) return false;
            AiRecruitmentConfig config = MilitaryConfigManager.Instance
                .GetActiveConfig()?.AiRecruitment;
            float radius = Math.Max(1f, config?.EnemyDetectRadius ?? 8f);
            float ratio = Math.Max(1f, config?.EnemyStrengthRatio ?? 1.25f);
            int strength = Math.Max(1,
                carrier?.Party?.NumberOfAllMembers ?? 50);
            float threshold = strength * ratio;
            foreach (MobileParty other in MobileParty.All)
            {
                if (other == null || other == carrier || !other.IsActive ||
                    other.MapFaction == null || other.MapEvent != null ||
                    !FactionManager.IsAtWarAgainstFaction(
                        faction, other.MapFaction) ||
                    other.Party.NumberOfAllMembers <= threshold)
                    continue;
                if (position.DistanceSquared(other.Position) <=
                    radius * radius)
                    return true;
            }
            return false;
        }

        public static bool IsNativeAvoidanceActive(MobileParty party) =>
            party?.Ai?.IsAlerted == true ||
            party?.ShortTermBehavior == AiBehavior.FleeToPoint ||
            party?.ShortTermBehavior == AiBehavior.FleeToGate ||
            party?.ShortTermBehavior == AiBehavior.FleeToParty ||
            party?.ShortTermBehavior == AiBehavior.GoAroundParty;
    }
}
