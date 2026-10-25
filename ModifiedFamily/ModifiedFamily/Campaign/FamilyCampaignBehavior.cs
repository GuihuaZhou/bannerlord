// Campaign owner for persistence, event scheduling, lifecycle advancement, and migration.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Kinship;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.AI;
using ModifiedFamily.Marriage.Execution;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Adoption.Execution;
using ModifiedFamily.Adoption.Models;
using ModifiedFamily.Persistence;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Actions;
using ModifiedFamily.UI.Notifications;

namespace ModifiedFamily.Campaign
{
    public sealed class FamilyCampaignBehavior : CampaignBehaviorBase
    {
        private List<FamilyProposal> _proposals = new List<FamilyProposal>();
        private List<FamilyCooldownRecord> _cooldowns = new List<FamilyCooldownRecord>();
        private List<AdoptiveKinshipRecord> _adoptions = new List<AdoptiveKinshipRecord>();
        private FamilyProposalService _service;
        private MarriageProposalModel _marriageProposalModel;
        private ClanMarriageDecisionService _marriageAi;
        private MarriageExecutionService _execution;
        private AdoptionExecutionService _adoptionExecution;
        private FamilyProposalNotificationService _notificationService;
        private readonly HashSet<Clan> _pendingPoliticalRevalidation = new HashSet<Clan>();
        private readonly MarriagePoliticalValidationModel _politicalValidation = new MarriagePoliticalValidationModel();
        public IReadOnlyList<AdoptiveKinshipRecord> Adoptions => _adoptions;
        public FamilyProposalService Service => _service;

        public override void RegisterEvents()
        {
            // Clan ticks discover candidates; the global daily tick advances only existing records.
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyClan);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDaily);
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
            CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClan);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore store)
        {
            // Never mutate the campaign world while the save system is serializing or restoring data.
            store.SyncData("ModifiedFamily.Proposals", ref _proposals); store.SyncData("ModifiedFamily.Cooldowns", ref _cooldowns); store.SyncData("ModifiedFamily.Adoptions", ref _adoptions);
            _proposals = _proposals ?? new List<FamilyProposal>(); _cooldowns = _cooldowns ?? new List<FamilyCooldownRecord>(); _adoptions = _adoptions ?? new List<AdoptiveKinshipRecord>();
            FamilyDiagnostics.DebugLog(FamilyLogText.Persistence(_proposals.Count, _cooldowns.Count, _adoptions.Count));
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            BuildServices(); MigrateVanillaOffers();
            foreach (FamilyProposal proposal in _proposals) _notificationService.Publish(proposal);
            bool correctModel = TaleWorlds.CampaignSystem.Campaign.Current.Models.MarriageModel is ModifiedMarriageModel;
            FamilyDiagnostics.Notice(FamilyLogText.SessionReady(correctModel, _proposals.Count, _cooldowns.Count, _adoptions.Count));
            if (!correctModel) FamilyDiagnostics.Warn(FamilyLogText.Simple("MF_Log_ModelConflict", "Another mod replaced the marriage model; automatic execution is disabled."));
        }

        private void BuildServices()
        {
            var adoptionModel = new AdoptionModel();
            _marriageProposalModel = new MarriageProposalModel(_adoptions); _service = new FamilyProposalService(_proposals, _cooldowns, _marriageProposalModel, adoptionModel);
            _notificationService = new FamilyProposalNotificationService();
            _service.ProposalCreated += _notificationService.Publish;
            _marriageAi = new ClanMarriageDecisionService(_service, _marriageProposalModel); _execution = new MarriageExecutionService(_marriageProposalModel);
            _adoptionExecution = new AdoptionExecutionService(adoptionModel, new AdoptiveKinshipService(_adoptions));
        }

        private void OnDailyClan(Clan clan) { if (_service != null && FamilyDiagnostics.IsSafeToRunAi) _marriageAi.ConsiderClan(clan); }
        private void OnDaily()
        {
            if (_service == null) BuildServices();
            // CampaignTime deadlines survive saves, so loading a save resumes the same seven-day window.
            foreach (var p in _proposals.Where(x => x != null && x.IsActive).ToList())
            {
                if (p.ProposalType == FamilyProposalType.Marriage && IsPreExecution(p))
                {
                    FamilyResult political = _politicalValidation.Validate(p);
                    if (!political.IsAllowed)
                    {
                        _service.TryInvalidateDueToPoliticalChange(p, political.ReasonCode);
                        continue;
                    }
                }
                if (p.Status == FamilyProposalStatus.PendingResponse && p.ReplyDeadline.IsPast)
                {
                    // AI receivers decide once the response window closes; unattended player offers expire.
                    if (p.ProposalType == FamilyProposalType.Adoption || p.ReceiverClan == Clan.PlayerClan) _service.TryReject(p.ProposalId, p.ReceiverClan, FamilyReasonCode.Expired); else _marriageAi.Respond(p);
                }
                else if (p.Status == FamilyProposalStatus.AcceptedPendingExecution)
                {
                    // Temporary blockers are retried daily, but never beyond the execution deadline.
                    if (p.ExecutionDeadline.IsPast) _service.Invalidate(p, FamilyReasonCode.Expired, false);
                    else if (p.ProposalType == FamilyProposalType.Marriage && TaleWorlds.CampaignSystem.Campaign.Current.Models.MarriageModel is ModifiedMarriageModel)
                    {
                        var r = _execution.TryExecuteMarriage(p);
                        if (p.Status == FamilyProposalStatus.Completed) _service.ReleaseCompleted(p);
                        else if (p.Status == FamilyProposalStatus.AcceptedPendingExecution && !r.IsAllowed && r.TemporaryBlockers.Count == 0 && r.ReasonCode != FamilyReasonCode.InsufficientFunds)
                            _service.Invalidate(p, r.ReasonCode, false);
                    }
                    else if (p.ProposalType == FamilyProposalType.Adoption)
                    { _adoptionExecution.TryExecute(p); if (p.Status == FamilyProposalStatus.Completed) _service.ReleaseCompleted(p); }
                }
            }
            _cooldowns.RemoveAll(c => c == null || c.Until.IsPast);
        }

        private void OnBeforeHeroesMarried(Hero first, Hero second, bool notification)
        {
            // Ignore the expected vanilla event raised inside this module's own execution scope.
            if (MarriageExecutionScope.CurrentProposalId != null) return;
            foreach (var p in _proposals.Where(x => x.IsActive && (x.HeroA == first || x.HeroB == first || x.HeroA == second || x.HeroB == second)).ToList()) _service.Invalidate(p, FamilyReasonCode.ExternalMarriage, false);
        }
        private void OnHeroChangedClan(Hero hero, Clan oldClan)
        {
            if (MarriageExecutionScope.CurrentProposalId != null) return;
            foreach (var p in _proposals.Where(x => x.IsActive && (x.HeroA == hero || x.HeroB == hero)).ToList())
                if ((p.HeroA == hero && hero.Clan != p.OriginClanA) || (p.HeroB == hero && hero.Clan != p.OriginClanB)) _service.Invalidate(p, FamilyReasonCode.ClanChanged, false);
        }

        private void OnWarDeclared(IFaction first, IFaction second, DeclareWarAction.DeclareWarDetail detail)
        {
            if (_service == null) BuildServices();
            // Validate current clan/kingdom state instead of trusting the event's faction shape.
            RevalidateMarriageProposals(null, false);
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            if (clan == null) return;
            // Kingdom switches can expose a short-lived null/intermediate state. Coalesce them until the next safe hourly update.
            _pendingPoliticalRevalidation.Add(clan);
        }

        private void OnClanDestroyed(Clan clan)
        {
            if (_service == null) BuildServices();
            RevalidateMarriageProposals(clan, false);
        }

        private void OnHourlyTick()
        {
            if (_pendingPoliticalRevalidation.Count == 0) return;
            if (_service == null) BuildServices();
            Clan[] changed = _pendingPoliticalRevalidation.ToArray();
            _pendingPoliticalRevalidation.Clear();
            foreach (Clan clan in changed) RevalidateMarriageProposals(clan, true);
        }

        private void RevalidateMarriageProposals(Clan changedClan, bool reassessAccepted)
        {
            foreach (FamilyProposal p in _proposals.Where(x => x != null && x.ProposalType == FamilyProposalType.Marriage && IsPreExecution(x) &&
                (changedClan == null || x.OriginClanA == changedClan || x.OriginClanB == changedClan || x.ProposerClan == changedClan || x.ReceiverClan == changedClan)).ToList())
            {
                FamilyResult validity = _politicalValidation.Validate(p);
                if (!validity.IsAllowed)
                {
                    _service.TryInvalidateDueToPoliticalChange(p, validity.ReasonCode);
                    continue;
                }
                if (reassessAccepted && p.Status == FamilyProposalStatus.AcceptedPendingExecution && !_marriageAi.ShouldContinueAcceptedProposal(p))
                    _service.TryInvalidateDueToPoliticalChange(p, FamilyReasonCode.PoliticalInterestChanged);
            }
        }

        private static bool IsPreExecution(FamilyProposal p) => p.Status == FamilyProposalStatus.PendingResponse || p.Status == FamilyProposalStatus.AcceptedPendingExecution;

        private void MigrateVanillaOffers()
        {
            // Reflection is intentionally version-contained and must never erase the main hero's romance.
            try
            {
                var vanilla = TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<MarriageOfferCampaignBehavior>(); if (vanilla == null) return;
                var playerField = AccessTools.Field(vanilla.GetType(), "_currentOfferedPlayerClanHero"); var otherField = AccessTools.Field(vanilla.GetType(), "_currentOfferedOtherClanHero");
                Hero player = playerField?.GetValue(vanilla) as Hero;
                if (player != null && player != Hero.MainHero) { playerField.SetValue(vanilla, null); otherField?.SetValue(vanilla, null); FamilyDiagnostics.Info(FamilyLogText.Simple("MF_Log_LegacyOfferCleared", "A legacy arranged-marriage offer for a player clan member was isolated.")); }
                var waitingField = AccessTools.Field(vanilla.GetType(), "_acceptedMarriageOffersThatWaitingForAvailability");
                var waiting = waitingField?.GetValue(vanilla) as System.Collections.IDictionary;
                if (waiting != null)
                {
                    var remove = new List<object>(); foreach (System.Collections.DictionaryEntry e in waiting) if (e.Key != Hero.MainHero && e.Value != Hero.MainHero) remove.Add(e.Key);
                    foreach (object key in remove) waiting.Remove(key); if (remove.Count > 0) FamilyDiagnostics.Info(FamilyLogText.Count("MF_Log_LegacyWaitingCleared", "Isolated {COUNT} legacy arranged marriages.", remove.Count));
                }
            }
            catch (Exception ex) { FamilyDiagnostics.Error(FamilyLogText.Simple("MF_Log_LegacyMigrationFailed", "Legacy marriage migration failed."), ex); }
        }
    }
}
