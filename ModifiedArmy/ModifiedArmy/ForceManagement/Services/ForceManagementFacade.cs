using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Models;
using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Services
{
    public sealed class ForceManagementFacade
    {
        private static readonly Lazy<ForceManagementFacade> Instance =
            new Lazy<ForceManagementFacade>(() => new ForceManagementFacade());

        public static ForceManagementFacade Current => Instance.Value;

        public IPartyForceTargetModel PartyTargets { get; }
        public IRecruitmentAdmissionModel RecruitmentAdmission { get; }
        public IGarrisonDefenseRequirementModel GarrisonDefense { get; }
        public IExternalReinforcementNeedModel ReinforcementNeed { get; }
        public IReinforcementRouteModel ReinforcementRoutes { get; }
        public IPartyGarrisonExchangeModel Exchanges { get; }
        public ITroopValueModel TroopValues { get; }
        public IDemobilizationPolicy Demobilization { get; }
        public TroopTransferService TroopTransfers { get; }
        public ReinforcementCargoService ReinforcementCargo { get; }
        public FoodTransferService FoodTransfers { get; }
        public ReinforcementMissionService ReinforcementMissions { get; }
        public IArmyMissionProtectionModel ArmyMissionProtection { get; }

        private ForceManagementFacade()
        {
            PartyTargets = new PartyForceTargetModel(
                new WarDispositionBridge());
            RecruitmentAdmission = new RecruitmentAdmissionModel();
            GarrisonDefense = new GarrisonDefenseRequirementModel();
            ReinforcementNeed = new ExternalReinforcementNeedModel();
            ReinforcementRoutes = new ReinforcementRouteStubModel();
            TroopValues = new BasicTroopValueModel();
            Exchanges = new DisabledPartyGarrisonExchangeModel();
            Demobilization = new DisabledDemobilizationPolicy();
            TroopTransfers = new TroopTransferService();
            ReinforcementCargo = new ReinforcementCargoService();
            FoodTransfers = new FoodTransferService();
            ReinforcementMissions = new ReinforcementMissionService();
            ArmyMissionProtection = ReinforcementMissions;
        }

        public PartyForceTargetResult GetPartyTarget(MobileParty party)
        {
            bool ownDefense = party?.TargetSettlement?.OwnerClan ==
                    party?.ActualClan &&
                party.TargetSettlement?.IsUnderSiege == true;
            return PartyTargets.Evaluate(new PartyForceContext
            {
                Party = party,
                IsOwnFiefDefense = ownDefense
            });
        }

        public int GetSafeFieldSurplus(MobileParty party)
        {
            PartyForceTargetResult target = GetPartyTarget(party);
            return Math.Max(0, target.CurrentCount - target.TargetCount);
        }
    }

    internal sealed class WarDispositionBridge : IWarDispositionProvider
    {
        private static readonly HashSet<string> ReportedFallbacks =
            new HashSet<string>();

        public bool TryGet(
            Clan clan,
            out float disposition,
            out bool atFormalWar)
        {
            disposition = 0f;
            atFormalWar = IsAtWar(clan);
            if (clan == null || Campaign.Current == null)
            {
                return false;
            }

            WarDispositionManager manager = Campaign.Current
                .GetCampaignBehavior<WarDispositionManager>();
            if (manager != null &&
                manager.TryGetData(clan, out WarDispositionData data) &&
                data != null)
            {
                disposition = data.Value;
                return true;
            }

            LogFallback(clan);
            return false;
        }

        private static bool IsAtWar(Clan clan)
        {
            IFaction faction = clan?.MapFaction;
            if (faction == null)
            {
                return false;
            }

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != faction && !kingdom.IsEliminated &&
                    FactionManager.IsAtWarAgainstFaction(faction, kingdom))
                {
                    return true;
                }
            }
            return false;
        }

        private static void LogFallback(Clan clan)
        {
            string id = clan?.StringId ?? "unknown";
            if (!ReportedFallbacks.Add(id))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_disposition_fallback");
            message.SetTextVariable("CLAN_ID", id);
            ModLogger.Debug(message.ToString());
        }

    }
}
