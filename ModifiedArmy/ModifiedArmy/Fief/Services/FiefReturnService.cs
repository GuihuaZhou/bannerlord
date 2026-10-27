using System;
using System.Linq;
using ModifiedArmy.common;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    internal static class FiefReturnService
    {
        public static FiefReturnResult TryReturn(FiefPartyManager manager,
            MobileParty party, Settlement source, string batchId, int maximumCount)
        {
            FiefReturnResult rejected = new FiefReturnResult
            {
                SourceFief = source, BatchId = batchId,
                RequestedCount = Math.Max(0, maximumCount), ReasonCode = "InvalidRequest"
            };
            if (party == null || !party.IsActive || party.MapEvent != null || source == null ||
                party.ActualClan == null || source.OwnerClan != party.ActualClan ||
                string.IsNullOrEmpty(batchId) || maximumCount <= 0)
            {
                FiefPartyManager.LogFiefReturn(party, rejected, 0, 0);
                return rejected;
            }

            manager.AdvanceServiceDays(party);
            manager.ReconcilePartyFiefTroops(party);
            ServingDetachmentRef selected = manager.GetServingDetachments(party)
                .FirstOrDefault(item => item.SourceFief == source &&
                    item.Detachment?.BatchId == batchId && !item.Detachment.IsEmpty());
            FiefPartyData data = manager.GetFiefData(source);
            if (selected.Detachment == null || data == null)
            {
                rejected.ReasonCode = "NoEligibleBatch";
                FiefPartyManager.LogFiefReturn(party, rejected, 0, 0);
                return rejected;
            }
            int tracked = manager.GetTrackedMobilizationCount(source);
            if (tracked <= 0)
            {
                rejected.ReasonCode = "NoTrackedMobilization";
                FiefPartyManager.LogFiefReturn(party, rejected,
                    selected.Detachment.GetTotalCount(), 0);
                return rejected;
            }
            FiefReturnResult result = data.TryCommitFiefReturn(manager, party,
                selected.Detachment, maximumCount, tracked);
            FiefPartyManager.LogFiefReturn(party, result,
                selected.Detachment.GetTotalCount(),
                MilitaryConfigManager.Instance.RequireActiveConfig()
                    .GetDoctrine(source).Service.ReturnCooldownWeeks);
            return result;
        }
    }
}
