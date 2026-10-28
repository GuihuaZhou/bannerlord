using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Models;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Services
{
    public sealed class ForceManagementFacade
    {
        private static readonly object SyncRoot = new object();
        private static Campaign _campaign;
        private static ForceManagementFacade _current;

        public static ForceManagementFacade Current
        {
            get
            {
                Campaign campaign = Campaign.Current;
                lock (SyncRoot)
                {
                    if (_current == null || !ReferenceEquals(_campaign, campaign))
                    {
                        _campaign = campaign;
                        GarrisonRecruitmentCapacity.ResetRuntimeDiagnostics();
                        NewSettlementFoodModel.ResetRuntimeState();
                        ClanRecruitmentBudgetManager.ResetRuntimeState();
                        _current = new ForceManagementFacade();
                    }
                    return _current;
                }
            }
        }

        public IPartyForceTargetModel PartyTargets { get; }
        public IRecruitmentAdmissionModel RecruitmentAdmission { get; }
        public IGarrisonDefenseRequirementModel GarrisonDefense { get; }
        public IExternalReinforcementNeedModel ReinforcementNeed { get; }
        public IReinforcementRouteModel ReinforcementRoutes { get; }
        public IAiForcePlanningModel ForcePlanning { get; }
        public IPartyExchangeModel PartyExchange { get; }
        public PartyExchangeService PartyExchangeRunner { get; }
        public IWarRestPolicyModel WarRest { get; }
        public WarRestPlanningService WarRestPlanning { get; }
        public IPeaceDemobilizationModel PeaceDemobilization { get; }
        public DemobilizationService DemobilizationRunner { get; }
        public TroopTransferService TroopTransfers { get; }

        private ForceManagementFacade()
        {
            PartyTargets = new PartyForceTargetModel(
                new WarDispositionBridge());
            RecruitmentAdmission = new RecruitmentAdmissionModel();
            GarrisonDefense = new GarrisonDefenseRequirementModel();
            ReinforcementNeed = new ExternalReinforcementNeedModel();
            ReinforcementRoutes = new ReinforcementRouteModel();
            ForcePlanning = new AiForcePlanningModel();
            PartyExchange = new PartyExchangeModel();
            PartyExchangeRunner = new PartyExchangeService();
            WarRest = new WarRestPolicyModel();
            WarRestPlanning = new WarRestPlanningService();
            PeaceDemobilization = new PeaceDemobilizationModel();
            DemobilizationRunner = new DemobilizationService();
            TroopTransfers = new TroopTransferService();
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

        /// <summary>
        /// Reports whether the clan faction is formally at war with any live
        /// kingdom. A low war disposition never counts as peace by itself.
        /// </summary>
        public static bool IsAtFormalWar(Clan clan)
        {
            IFaction faction = clan?.MapFaction;
            if (faction == null) return false;
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
    }

    internal sealed class WarDispositionBridge : IWarDispositionProvider
    {
        private readonly HashSet<string> ReportedFallbacks =
            new HashSet<string>();

        public bool TryGet(
            Clan clan,
            out float disposition,
            out bool atFormalWar)
        {
            disposition = 0f;
            atFormalWar = ForceManagementFacade.IsAtFormalWar(clan);
            // Minor factions deliberately have no war-disposition record.
            // They are handled by native party logic and are not a missing-data
            // condition worth reporting.
            if (clan == null || clan.IsMinorFaction || Campaign.Current == null)
            {
                return false;
            }

            if (TryReadExternalDisposition(clan, out disposition))
            {
                return true;
            }

            LogFallback(clan);
            return false;
        }

        private static bool TryReadExternalDisposition(
            Clan clan,
            out float disposition)
        {
            disposition = 0f;
            try
            {
                Type managerType = Type.GetType(
                    "ModifiedPolitics.Models.WarDisposition.WarDispositionManager, ModifiedPolitics",
                    false);
                if (managerType == null) return false;
                MethodInfo getter = typeof(Campaign).GetMethods(
                        BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                        method.Name == "GetCampaignBehavior" &&
                        method.IsGenericMethodDefinition &&
                        method.GetParameters().Length == 0);
                object manager = getter?.MakeGenericMethod(managerType)
                    .Invoke(Campaign.Current, null);
                if (manager == null) return false;
                MethodInfo tryGet = managerType.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                        method.Name == "TryGetData" &&
                        method.GetParameters().Length == 2);
                if (tryGet == null) return false;
                object[] arguments = { clan, null };
                if (!(tryGet.Invoke(manager, arguments) is bool found) ||
                    !found || arguments[1] == null) return false;
                PropertyInfo valueProperty = arguments[1].GetType()
                    .GetProperty("Value",
                        BindingFlags.Instance | BindingFlags.Public);
                object value = valueProperty?.GetValue(arguments[1]);
                if (value == null) return false;
                disposition = Convert.ToSingle(value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void LogFallback(Clan clan)
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
