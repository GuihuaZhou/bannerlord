using System;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Finance;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Models.WarDisposition;
using ModifiedPolitics.Tool;
using ModifiedPolitics.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Clan-by-clan subject proposal scoring modeled after native kingdom
    /// decisions. The influence-weighted result authoritatively decides
    /// whether a subject proposal is accepted.
    /// </summary>
    public static class SubjectProposalEvaluationService
    {
        public static SubjectProposalEvaluation EvaluateDemand(
            Kingdom overlord, Kingdom subject, SubjectType type)
        {
            SubjectProposalEvaluation result = CreateEvaluation(
                overlord, subject, subject, type, false, true);
            foreach (Clan clan in EligibleClans(subject))
            {
                result.ClanEvaluations.Add(EvaluateSubjectClan(result, clan));
            }
            return result;
        }

        public static SubjectProposalEvaluation EvaluateSubmissionOffer(
            Kingdom overlord, Kingdom subject, SubjectType type)
        {
            // The receiving kingdom evaluates the offered realm as an asset and
            // as a possible military obligation.
            SubjectProposalEvaluation result = CreateEvaluation(
                overlord, subject, overlord, type, true, false);
            foreach (Clan clan in EligibleClans(overlord))
            {
                result.ClanEvaluations.Add(EvaluateReceivingClan(result, clan));
            }
            return result;
        }

        /// <summary>
        /// Evaluates whether the submitting kingdom's own council is willing
        /// to surrender sovereignty. This is separate from the prospective
        /// overlord's decision to accept the offer.
        /// </summary>
        public static SubjectProposalEvaluation EvaluateSubmissionIntent(
            Kingdom overlord, Kingdom subject, SubjectType type)
        {
            SubjectProposalEvaluation result = CreateEvaluation(
                overlord, subject, subject, type, true, true);
            foreach (Clan clan in EligibleClans(subject))
            {
                result.ClanEvaluations.Add(EvaluateSubjectClan(result, clan));
            }
            return result;
        }

        public static void Log(SubjectProposalEvaluation evaluation)
        {
            if (evaluation?.Overlord == null || evaluation.Subject == null)
            {
                return;
            }

            TextObject summary = evaluation.EvaluatesSubjectConsent
                ? new TextObject(
                    "{=MP_SubjectConsentVote}" +
                    "[Subject diplomacy] {KINGDOM} clans predict {DECISION} " +
                    "becoming a {SUBJECT_TYPE}. Support is {PERCENT}%, with " +
                    "{ACCEPTED} clans accepting and {REJECTED} rejecting.")
                : new TextObject(
                    "{=MP_SubjectAdmissionVote}" +
                    "[Subject diplomacy] {KINGDOM} clans predict {DECISION} " +
                    "admitting {SUBJECT} as a {SUBJECT_TYPE}. Support is " +
                    "{PERCENT}%, with {ACCEPTED} clans accepting and " +
                    "{REJECTED} rejecting.");
            summary.SetTextVariable("KINGDOM", evaluation.EvaluatingKingdom.Name);
            summary.SetTextVariable("SUBJECT", evaluation.Subject.Name);
            summary.SetTextVariable("DECISION", DecisionText(evaluation.WouldAccept));
            summary.SetTextVariable("SUBJECT_TYPE", TypeText(evaluation.SubjectType));
            summary.SetTextVariable("PERCENT", (evaluation.AcceptShare * 100f).ToString("F0"));
            summary.SetTextVariable("ACCEPTED", evaluation.ClanEvaluations.Count(x => x.WouldAccept));
            summary.SetTextVariable("REJECTED", evaluation.ClanEvaluations.Count(x => !x.WouldAccept));
            ModLogger.Notice(summary.ToString());

            foreach (SubjectClanSupportEvaluation clanResult in evaluation.ClanEvaluations)
            {
                if (evaluation.EvaluatesSubjectConsent)
                {
                    LogClanDetail(evaluation, clanResult);
                }
                else
                {
                    LogReceivingClanDetail(evaluation, clanResult);
                }
            }
        }

        private static SubjectClanSupportEvaluation EvaluateSubjectClan(
            SubjectProposalEvaluation proposal, Clan clan)
        {
            bool atWar = FactionManager.IsAtWarAgainstFaction(
                proposal.Overlord, proposal.Subject);
            float military = Clamp(Log2(proposal.StrengthRatio) * 30f, -60f, 60f);
            if (!atWar)
            {
                // Peacetime superiority can intimidate, but cannot by itself
                // justify surrendering sovereignty.
                military = Math.Min(15f, military);
            }

            int potential = GetWarPotential(clan);
            float disposition = GetWarDisposition(clan);
            int tribute = SubjectTributeCalculator.GetAssessedTributeForClan(
                proposal.Subject, clan);
            float wealth = Math.Max(1000f, (float)clan.Gold);

            return new SubjectClanSupportEvaluation
            {
                Clan = clan,
                SovereigntyScore = proposal.SubjectType == SubjectType.Puppet ? -135f : -90f,
                MilitaryScore = military,
                WarProgressScore = atWar
                    ? Clamp((proposal.OverlordWarProgress - proposal.SubjectWarProgress) / 10f, -60f, 60f)
                    : 0f,
                ExternalEnemyPressureScore = proposal.ExternalEnemyPressureScore,
                MultiFrontPressureScore = proposal.MultiFrontPressureScore,
                ProtectorStrengthScore = proposal.ProtectorStrengthScore,
                WarPotential = potential,
                WarPotentialScore = Clamp((600f - potential) / 15f, -35f, 35f),
                WarDisposition = disposition,
                WarDispositionScore = Clamp(-disposition * 0.5f, -35f, 35f),
                TerritoryScore = Clamp(35f - proposal.SubjectSettlementCount * 5f, -35f, 30f),
                DailyTribute = tribute,
                TributeScore = -Clamp(tribute * 30f / wealth * 20f, 0f, 35f),
                RulingClanScore = clan == proposal.Subject.RulingClan ? -20f : 0f
            };
        }

        private static SubjectClanSupportEvaluation EvaluateReceivingClan(
            SubjectProposalEvaluation proposal, Clan clan)
        {
            bool atWar = FactionManager.IsAtWarAgainstFaction(
                proposal.Overlord, proposal.Subject);
            float disposition = GetWarDisposition(clan);
            float relativeSubjectStrength = 1f / Math.Max(0.01f, proposal.StrengthRatio);
            float directWarDelta = proposal.OverlordWarProgress
                - proposal.SubjectWarProgress;
            bool isRulingClan = clan == proposal.Overlord.RulingClan;
            return new SubjectClanSupportEvaluation
            {
                Clan = clan,
                SovereigntyScore = 0f,
                MilitaryScore = 0f,
                WarProgressScore = atWar
                    ? Clamp(-directWarDelta / 15f, -30f, 30f)
                    : 0f,
                ExternalEnemyPressureScore = 0f,
                MultiFrontPressureScore = 0f,
                ProtectorStrengthScore = 0f,
                WarPotential = GetWarPotential(clan),
                WarPotentialScore = 0f,
                WarDisposition = disposition,
                WarDispositionScore = atWar
                    ? Clamp(-disposition * 0.35f, -20f, 20f)
                    : 0f,
                TerritoryScore = 0f,
                DailyTribute = 0,
                TributeScore = 0f,
                RulingClanScore = isRulingClan ? 5f : 0f,
                ControlValueScore = proposal.SubjectType == SubjectType.Puppet
                    ? 15f
                    : 5f,
                SubjectMilitaryValueScore = Clamp(
                    Log2(1f + relativeSubjectStrength) * 20f,
                    0f,
                    30f),
                StrategicTerritoryScore = Clamp(
                    proposal.SubjectSettlementCount
                        * (proposal.SubjectType == SubjectType.Puppet ? 3f : 2f),
                    0f,
                    30f),
                TributeIncomeScore = isRulingClan
                    ? Clamp(proposal.NominalDailyTribute / 150f, 0f, 30f)
                    : 0f,
                WarBurdenScore = proposal.DynamicWarRiskScore,
                ExistingSubjectsScore = -Clamp(
                    proposal.ExistingSubjectCount
                        * (proposal.SubjectType == SubjectType.Puppet ? 10f : 6f),
                    0f,
                    30f)
            };
        }

        private static SubjectProposalEvaluation CreateEvaluation(
            Kingdom overlord, Kingdom subject, Kingdom evaluatingKingdom,
            SubjectType type, bool isSubmissionOffer,
            bool evaluatesSubjectConsent)
        {
            float overlordStrength = Math.Max(
                1f,
                SafeNumber(overlord?.CurrentTotalStrength ?? 0f, 1f));
            float subjectStrength = Math.Max(
                1f,
                SafeNumber(subject?.CurrentTotalStrength ?? 0f, 1f));
            bool atWar = overlord != null && subject != null
                && FactionManager.IsAtWarAgainstFaction(overlord, subject);
            SubjectProposalEvaluation result = new SubjectProposalEvaluation
            {
                Overlord = overlord,
                Subject = subject,
                EvaluatingKingdom = evaluatingKingdom,
                SubjectType = type,
                IsSubmissionOffer = isSubmissionOffer,
                EvaluatesSubjectConsent = evaluatesSubjectConsent,
                StrengthRatio = overlordStrength / subjectStrength,
                SubjectSettlementCount = subject?.Settlements.Count(x =>
                    x != null && (x.IsTown || x.IsCastle)) ?? 0,
                OverlordWarProgress = atWar ? GetWarProgress(overlord, subject) : 0f,
                SubjectWarProgress = atWar ? GetWarProgress(subject, overlord) : 0f
            };
            result.OverlordActiveWarCount = CountActiveKingdomWars(overlord);
            result.NewWarObligationCount = CountNewWarObligations(
                overlord,
                subject);
            result.UnsharedSubjectWarCount = CountUnsharedSubjectWars(
                overlord,
                subject);
            result.DynamicWarRiskScore =
                SubjectFormerWarResolutionService.CalculateAdmissionWarRisk(
                    overlord,
                    subject,
                    out int expectedEscalations,
                    out float defenseCapacity,
                    out float currentEnemyStrength,
                    out float potentialEnemyStrength,
                    out float availableWarCapacity,
                    out float combinedEnemyStrength,
                    out float pressureRatio);
            result.ExpectedFormerWarEscalationCount = expectedEscalations;
            result.OverlordDefenseCapacity = defenseCapacity;
            result.CurrentEnemyStrength = currentEnemyStrength;
            result.PotentialEnemyStrength = potentialEnemyStrength;
            result.AvailableWarCapacity = availableWarCapacity;
            result.CombinedEnemyStrength = combinedEnemyStrength;
            result.CombinedWarPressureRatio = pressureRatio;
            result.ExistingSubjectCount = KingdomDiplomacyManager.Current
                ?.GetSubjects(overlord).Count ?? 0;
            result.NominalDailyTribute = EligibleClans(subject).Sum(clan =>
                SubjectTributeCalculator.GetAssessedTributeForClan(
                    subject,
                    clan));
            PopulateExternalWarPressure(result, subjectStrength);
            return result;
        }

        /// <summary>
        /// Measures wars against third parties. Direct capitulation to the
        /// proposed overlord is already represented by bilateral war progress,
        /// so that enemy is excluded here to avoid counting one defeat twice.
        /// </summary>
        private static void PopulateExternalWarPressure(
            SubjectProposalEvaluation result,
            float subjectStrength)
        {
            Kingdom[] enemies = Kingdom.All.Where(kingdom =>
                kingdom != null
                && !kingdom.IsEliminated
                && kingdom != result.Subject
                && result.Subject.IsAtWarWith(kingdom)).ToArray();
            result.ActiveWarCount = enemies.Length;
            result.MultiFrontPressureScore = Clamp(
                Math.Max(0, enemies.Length - 1) * 10f,
                0f,
                30f);

            // Start below zero so an active external enemy is still named in
            // diagnostics when neither side has established an advantage yet.
            float greatestPressure = -1f;
            Kingdom greatestEnemy = null;
            foreach (Kingdom enemy in enemies)
            {
                if (enemy == result.Overlord)
                {
                    continue;
                }

                float enemyProgress = SafeNumber(
                    GetWarProgress(enemy, result.Subject));
                float subjectProgress = SafeNumber(
                    GetWarProgress(result.Subject, enemy));
                float progressPressure = Clamp(
                    (enemyProgress - subjectProgress) / 10f,
                    0f,
                    60f);
                float enemyStrength = SafeNumber(
                    enemy.CurrentTotalStrength,
                    1f);
                float strengthRatio = Math.Max(1f, enemyStrength)
                    / subjectStrength;
                float strengthPressure = Clamp(
                    Log2(strengthRatio) * 20f,
                    0f,
                    40f);
                // Raw strength is a risk, not proof of collapse. It amplifies
                // an observed battlefield setback but cannot cause surrender
                // immediately after war is declared.
                float defeatScale = Clamp(progressPressure / 30f, 0f, 1f);
                float pressure = progressPressure
                    + strengthPressure * defeatScale;
                LogExternalEnemyPressure(
                    result.Subject,
                    enemy,
                    enemyProgress,
                    subjectProgress,
                    strengthRatio,
                    progressPressure,
                    strengthPressure,
                    pressure);
                if (greatestEnemy == null || pressure > greatestPressure)
                {
                    greatestPressure = pressure;
                    greatestEnemy = enemy;
                }
            }

            if (greatestEnemy == null)
            {
                return;
            }

            result.MostDangerousExternalEnemy = greatestEnemy;
            result.ExternalEnemyPressureScore = Clamp(
                greatestPressure,
                0f,
                80f);

            // A realm in crisis only seeks a useful protector. A proposed
            // overlord weaker than the greatest enemy reduces the benefit,
            // while a clearly stronger protector increases it.
            float protectorRatio = Math.Max(
                1f,
                SafeNumber(result.Overlord.CurrentTotalStrength, 1f))
                / Math.Max(
                    1f,
                    SafeNumber(greatestEnemy.CurrentTotalStrength, 1f));
            float crisisScale = Clamp(
                result.ExternalEnemyPressureScore / 40f,
                0f,
                1f);
            result.ProtectorStrengthScore = Clamp(
                Log2(protectorRatio) * 15f,
                -25f,
                25f) * crisisScale;
        }

        private static void LogExternalEnemyPressure(
            Kingdom subject,
            Kingdom enemy,
            float enemyProgress,
            float subjectProgress,
            float strengthRatio,
            float progressPressure,
            float strengthPressure,
            float combinedPressure)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectEnemyPressure}" +
                "[Subject diplomacy] {SUBJECT} against {ENEMY}. Enemy war " +
                "progress {ENEMY_PROGRESS}, subject war progress " +
                "{SUBJECT_PROGRESS}, strength ratio {STRENGTH_RATIO}, " +
                "progress pressure {PROGRESS_PRESSURE}, strength pressure " +
                "{STRENGTH_PRESSURE}, combined external pressure " +
                "{COMBINED_PRESSURE}.");
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("ENEMY", enemy.Name);
            message.SetTextVariable("ENEMY_PROGRESS", enemyProgress.ToString("F1"));
            message.SetTextVariable("SUBJECT_PROGRESS", subjectProgress.ToString("F1"));
            message.SetTextVariable("STRENGTH_RATIO", strengthRatio.ToString("F2"));
            message.SetTextVariable("PROGRESS_PRESSURE", Format(progressPressure));
            message.SetTextVariable("STRENGTH_PRESSURE", Format(strengthPressure));
            message.SetTextVariable("COMBINED_PRESSURE", Format(combinedPressure));
            ModLogger.Info(message.ToString());
        }

        private static void LogClanDetail(
            SubjectProposalEvaluation proposal,
            SubjectClanSupportEvaluation result)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectClanVoteDetail}" +
                "[Subject diplomacy] {CLAN} scores {TOTAL} and would {DECISION}. " +
                "Sovereignty {SOVEREIGNTY}, military {MILITARY}, war progress " +
                "{WAR_PROGRESS}, external enemy pressure {EXTERNAL_PRESSURE}, " +
                "greatest external enemy {EXTERNAL_ENEMY}, active wars " +
                "{WAR_COUNT}, multi-front pressure {MULTI_FRONT}, protector strength " +
                "{PROTECTOR}, war potential {WAR_POTENTIAL} adding " +
                "{POTENTIAL_SCORE}, war disposition {DISPOSITION} adding " +
                "{DISPOSITION_SCORE}, territory {TERRITORY}, daily tribute " +
                "{TRIBUTE} adding {TRIBUTE_SCORE}, ruling clan {RULER}. " +
                "Support converts to accept {ACCEPT} and reject {REJECT}.");
            message.SetTextVariable("CLAN", result.Clan.Name);
            message.SetTextVariable("TOTAL", Format(result.RawScore));
            message.SetTextVariable("DECISION", DecisionText(result.WouldAccept));
            message.SetTextVariable("SOVEREIGNTY", Format(result.SovereigntyScore));
            message.SetTextVariable("MILITARY", Format(result.MilitaryScore));
            message.SetTextVariable("WAR_PROGRESS", Format(result.WarProgressScore));
            message.SetTextVariable(
                "EXTERNAL_PRESSURE", Format(result.ExternalEnemyPressureScore));
            message.SetTextVariable(
                "EXTERNAL_ENEMY",
                proposal.MostDangerousExternalEnemy?.Name
                    ?? new TextObject("{=ModifiedPolitics_None}none"));
            message.SetTextVariable("WAR_COUNT", proposal.ActiveWarCount);
            message.SetTextVariable(
                "MULTI_FRONT", Format(result.MultiFrontPressureScore));
            message.SetTextVariable(
                "PROTECTOR", Format(result.ProtectorStrengthScore));
            message.SetTextVariable("WAR_POTENTIAL", result.WarPotential);
            message.SetTextVariable("POTENTIAL_SCORE", Format(result.WarPotentialScore));
            message.SetTextVariable("DISPOSITION", Format(result.WarDisposition));
            message.SetTextVariable("DISPOSITION_SCORE", Format(result.WarDispositionScore));
            message.SetTextVariable("TERRITORY", Format(result.TerritoryScore));
            message.SetTextVariable("TRIBUTE", result.DailyTribute);
            message.SetTextVariable("TRIBUTE_SCORE", Format(result.TributeScore));
            message.SetTextVariable("RULER", Format(result.RulingClanScore));
            message.SetTextVariable("ACCEPT", result.AcceptSupport.ToString("F0"));
            message.SetTextVariable("REJECT", result.RejectSupport.ToString("F0"));
            // Keep the detailed per-clan calculation in the file log. The
            // screen only needs the concise kingdom-level Notice summary.
            ModLogger.Info(message.ToString());
        }

        private static void LogReceivingClanDetail(
            SubjectProposalEvaluation proposal,
            SubjectClanSupportEvaluation result)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectAdmissionDetail}" +
                "[Subject diplomacy] {CLAN} scores {TOTAL} and would " +
                "{DECISION} admission. Control value {CONTROL}, subject " +
                "military value {MILITARY_VALUE}, strategic territory " +
                "{TERRITORY_VALUE}, nominal daily tribute {TRIBUTE} adding " +
                "{TRIBUTE_VALUE}, direct war progress {WAR_PROGRESS}, war " +
                "disposition {DISPOSITION} adding {DISPOSITION_SCORE}, " +
                "overlord active wars {OVERLORD_WARS}, subject active wars " +
                "{SUBJECT_WARS}, new war obligations {NEW_WARS}, unshared " +
                "subject wars {UNSHARED_WARS}, likely escalations " +
                "{ESCALATIONS}, defense capacity {DEFENSE}, combined enemy " +
                "strength {ENEMY_STRENGTH}, current enemies {CURRENT_ENEMIES}, " +
                "potential enemies {POTENTIAL_ENEMIES}, available capacity " +
                "{AVAILABLE}, incremental pressure ratio {PRESSURE}, war " +
                "risk {WAR_BURDEN}, existing subjects " +
                "{SUBJECT_COUNT} adding {SUBJECT_PENALTY}, ruling clan " +
                "{RULER}. Support converts to accept {ACCEPT} and reject " +
                "{REJECT}.");
            message.SetTextVariable("CLAN", result.Clan.Name);
            message.SetTextVariable("TOTAL", Format(result.RawScore));
            message.SetTextVariable("DECISION", DecisionText(result.WouldAccept));
            message.SetTextVariable("CONTROL", Format(result.ControlValueScore));
            message.SetTextVariable(
                "MILITARY_VALUE", Format(result.SubjectMilitaryValueScore));
            message.SetTextVariable(
                "TERRITORY_VALUE", Format(result.StrategicTerritoryScore));
            message.SetTextVariable("TRIBUTE", proposal.NominalDailyTribute);
            message.SetTextVariable(
                "TRIBUTE_VALUE", Format(result.TributeIncomeScore));
            message.SetTextVariable(
                "WAR_PROGRESS", Format(result.WarProgressScore));
            message.SetTextVariable(
                "DISPOSITION", Format(result.WarDisposition));
            message.SetTextVariable(
                "DISPOSITION_SCORE", Format(result.WarDispositionScore));
            message.SetTextVariable(
                "OVERLORD_WARS", proposal.OverlordActiveWarCount);
            message.SetTextVariable("SUBJECT_WARS", proposal.ActiveWarCount);
            message.SetTextVariable(
                "NEW_WARS", proposal.NewWarObligationCount);
            message.SetTextVariable(
                "UNSHARED_WARS", proposal.UnsharedSubjectWarCount);
            message.SetTextVariable(
                "ESCALATIONS", proposal.ExpectedFormerWarEscalationCount);
            message.SetTextVariable(
                "DEFENSE", proposal.OverlordDefenseCapacity.ToString("F0"));
            message.SetTextVariable(
                "ENEMY_STRENGTH", proposal.CombinedEnemyStrength.ToString("F0"));
            message.SetTextVariable(
                "CURRENT_ENEMIES", proposal.CurrentEnemyStrength.ToString("F0"));
            message.SetTextVariable(
                "POTENTIAL_ENEMIES", proposal.PotentialEnemyStrength.ToString("F0"));
            message.SetTextVariable(
                "AVAILABLE", proposal.AvailableWarCapacity.ToString("F0"));
            message.SetTextVariable(
                "PRESSURE", proposal.CombinedWarPressureRatio.ToString("F2"));
            message.SetTextVariable("WAR_BURDEN", Format(result.WarBurdenScore));
            message.SetTextVariable("SUBJECT_COUNT", proposal.ExistingSubjectCount);
            message.SetTextVariable(
                "SUBJECT_PENALTY", Format(result.ExistingSubjectsScore));
            message.SetTextVariable("RULER", Format(result.RulingClanScore));
            message.SetTextVariable("ACCEPT", result.AcceptSupport.ToString("F0"));
            message.SetTextVariable("REJECT", result.RejectSupport.ToString("F0"));
            ModLogger.Info(message.ToString());
        }

        private static Clan[] EligibleClans(Kingdom kingdom)
        {
            return kingdom?.Clans.Where(x => x != null && !x.IsEliminated
                && !x.IsUnderMercenaryService).ToArray() ?? new Clan[0];
        }

        private static int CountActiveKingdomWars(Kingdom kingdom)
        {
            return kingdom == null
                ? 0
                : Kingdom.All.Count(other => other != null
                    && !other.IsEliminated
                    && other != kingdom
                    && kingdom.IsAtWarWith(other));
        }

        /// <summary>
        /// Counts overlord wars that the prospective subject has not already
        /// joined. These are the actual new wars created by subject status.
        /// </summary>
        private static int CountNewWarObligations(
            Kingdom overlord,
            Kingdom subject)
        {
            return overlord == null || subject == null
                ? 0
                : Kingdom.All.Count(other => other != null
                    && !other.IsEliminated
                    && other != overlord
                    && other != subject
                    && overlord.IsAtWarWith(other)
                    && !subject.IsAtWarWith(other));
        }

        /// <summary>
        /// Counts wars unique to the prospective subject. Vassals retain these
        /// wars, while puppets discard them when synchronizing with overlord.
        /// </summary>
        private static int CountUnsharedSubjectWars(
            Kingdom overlord,
            Kingdom subject)
        {
            return overlord == null || subject == null
                ? 0
                : Kingdom.All.Count(other => other != null
                    && !other.IsEliminated
                    && other != overlord
                    && other != subject
                    && subject.IsAtWarWith(other)
                    && !overlord.IsAtWarWith(other));
        }

        private static int GetWarPotential(Clan clan)
        {
            return WarPotentialModel.Instance?.CalculateWarPotential(clan)?.WarPotential ?? 0;
        }

        private static float GetWarDisposition(Clan clan)
        {
            WarDispositionManager manager = Campaign.Current
                ?.GetCampaignBehavior<WarDispositionManager>();
            return manager != null
                && manager.TryGetData(clan, out WarDispositionData data)
                    ? data.Value : 0f;
        }

        private static float GetWarProgress(Kingdom attacker, Kingdom defender)
        {
            return Campaign.Current.Models.DiplomacyModel
                .GetWarProgressScore(attacker, defender, false).ResultNumber;
        }

        public static TextObject TypeText(SubjectType type)
        {
            return type == SubjectType.Puppet
                ? new TextObject("{=ModifiedPolitics_SubjectTypePuppet}puppet")
                : new TextObject("{=ModifiedPolitics_SubjectTypeVassal}vassal");
        }

        private static TextObject DecisionText(bool accept)
        {
            return accept
                ? new TextObject("{=ModifiedPolitics_SubjectProposalAccept}accept")
                : new TextObject("{=ModifiedPolitics_SubjectProposalReject}reject");
        }

        private static float Log2(float value)
        {
            return (float)(Math.Log(Math.Max(0.01f, value)) / Math.Log(2d));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float SafeNumber(float value, float fallback = 0f)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        private static string Format(float value)
        {
            return value >= 0f ? "+" + value.ToString("F1") : value.ToString("F1");
        }
    }
}
