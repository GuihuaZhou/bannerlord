using System;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Finance;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.Models.WarDisposition;
using ModifiedPolitics.Tool;
using ModifiedPolitics.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Clan-by-clan subject proposal scoring modeled after native kingdom
    /// decisions. Enforcement remains disabled until real campaign logs have
    /// been used to tune these weights.
    /// </summary>
    public static class SubjectProposalEvaluationService
    {
        public static SubjectProposalEvaluation EvaluateDemand(
            Kingdom overlord, Kingdom subject, SubjectType type)
        {
            SubjectProposalEvaluation result = CreateEvaluation(
                overlord, subject, subject, type, false);
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
                overlord, subject, overlord, type, true);
            foreach (Clan clan in EligibleClans(overlord))
            {
                result.ClanEvaluations.Add(EvaluateReceivingClan(result, clan));
            }
            return result;
        }

        public static void Log(SubjectProposalEvaluation evaluation)
        {
            if (evaluation?.Overlord == null || evaluation.Subject == null)
            {
                return;
            }

            TextObject summary = new TextObject(
                "{=ModifiedPolitics_SubjectProposalVoteSummary}" +
                "[Subject diplomacy] {KINGDOM} clans predict {DECISION} for " +
                "{SUBJECT_TYPE}. Support is {PERCENT}%, with {ACCEPTED} clans " +
                "accepting and {REJECTED} rejecting.");
            summary.SetTextVariable("KINGDOM", evaluation.EvaluatingKingdom.Name);
            summary.SetTextVariable("DECISION", DecisionText(evaluation.WouldAccept));
            summary.SetTextVariable("SUBJECT_TYPE", TypeText(evaluation.SubjectType));
            summary.SetTextVariable("PERCENT", (evaluation.AcceptShare * 100f).ToString("F0"));
            summary.SetTextVariable("ACCEPTED", evaluation.ClanEvaluations.Count(x => x.WouldAccept));
            summary.SetTextVariable("REJECTED", evaluation.ClanEvaluations.Count(x => !x.WouldAccept));
            ModLogger.Notice(summary.ToString());

            foreach (SubjectClanSupportEvaluation clanResult in evaluation.ClanEvaluations)
            {
                LogClanDetail(clanResult);
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
            return new SubjectClanSupportEvaluation
            {
                Clan = clan,
                SovereigntyScore = proposal.SubjectType == SubjectType.Puppet ? -10f : 5f,
                MilitaryScore = Clamp(Log2(1f + relativeSubjectStrength) * 20f, 0f, 30f),
                WarProgressScore = atWar ? -35f : 0f,
                WarPotential = GetWarPotential(clan),
                WarPotentialScore = 0f,
                WarDisposition = disposition,
                WarDispositionScore = atWar ? Clamp(disposition * 0.25f, -15f, 15f) : 0f,
                TerritoryScore = Clamp(proposal.SubjectSettlementCount * 3f, 0f, 30f),
                DailyTribute = 0,
                TributeScore = 0f,
                RulingClanScore = clan == proposal.Overlord.RulingClan ? 10f : 0f
            };
        }

        private static SubjectProposalEvaluation CreateEvaluation(
            Kingdom overlord, Kingdom subject, Kingdom evaluatingKingdom,
            SubjectType type, bool isSubmissionOffer)
        {
            float overlordStrength = Math.Max(1f, overlord?.CurrentTotalStrength ?? 0f);
            float subjectStrength = Math.Max(1f, subject?.CurrentTotalStrength ?? 0f);
            bool atWar = overlord != null && subject != null
                && FactionManager.IsAtWarAgainstFaction(overlord, subject);
            return new SubjectProposalEvaluation
            {
                Overlord = overlord,
                Subject = subject,
                EvaluatingKingdom = evaluatingKingdom,
                SubjectType = type,
                IsSubmissionOffer = isSubmissionOffer,
                StrengthRatio = overlordStrength / subjectStrength,
                SubjectSettlementCount = subject?.Settlements.Count(x =>
                    x != null && (x.IsTown || x.IsCastle)) ?? 0,
                OverlordWarProgress = atWar ? GetWarProgress(overlord, subject) : 0f,
                SubjectWarProgress = atWar ? GetWarProgress(subject, overlord) : 0f
            };
        }

        private static void LogClanDetail(SubjectClanSupportEvaluation result)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_SubjectProposalClanDetail}" +
                "[Subject diplomacy] {CLAN} scores {TOTAL} and would {DECISION}. " +
                "Sovereignty {SOVEREIGNTY}, military {MILITARY}, war progress " +
                "{WAR_PROGRESS}, war potential {WAR_POTENTIAL} adding " +
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

        private static Clan[] EligibleClans(Kingdom kingdom)
        {
            return kingdom?.Clans.Where(x => x != null && !x.IsEliminated
                && !x.IsUnderMercenaryService).ToArray() ?? new Clan[0];
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

        private static TextObject TypeText(SubjectType type)
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

        private static string Format(float value)
        {
            return value >= 0f ? "+" + value.ToString("F1") : value.ToString("F1");
        }
    }
}
