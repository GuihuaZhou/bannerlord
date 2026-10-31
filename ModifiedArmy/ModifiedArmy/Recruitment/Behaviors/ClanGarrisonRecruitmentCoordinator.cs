using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.Recruitment.Behaviors
{
    public sealed class ClanGarrisonRecruitmentCoordinator : CampaignBehaviorBase
    {
        public override void RegisterEvents() =>
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyClan);

        public override void SyncData(IDataStore dataStore) { }

        private static void OnDailyClan(Clan clan)
        {
            if (clan == null || clan == Clan.PlayerClan || clan.IsEliminated) return;
            GarrisonRecruitFromPrisonersBehavior worker = Campaign.Current?
                .GetCampaignBehavior<GarrisonRecruitFromPrisonersBehavior>();
            if (worker == null) return;
            foreach (var town in clan.Fiefs.Where(x => x?.Settlement?.IsFortification == true)
                .OrderByDescending(x => (x.GarrisonParty?.MemberRoster.TotalManCount ?? 0) < 75)
                .ThenByDescending(x => GapRatio(x.Settlement))
                .ThenByDescending(x => GarrisonTransferPolicyModel.IsEnemyBorderFortification(x.Settlement))
                .ThenBy(x => x.Settlement.StringId, StringComparer.Ordinal))
                worker.ProcessSettlement(town.Settlement);
        }

        private static float GapRatio(TaleWorlds.CampaignSystem.Settlements.Settlement settlement)
        {
            int target = GarrisonRecruitmentCapacity.GetUpperTarget(settlement,
                settlement?.Town?.GarrisonParty?.Party.PartySizeLimit ?? 0);
            int current = settlement?.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0;
            return target <= 0 ? 0f : Math.Max(0, target - current) / (float)target;
        }
    }
}
