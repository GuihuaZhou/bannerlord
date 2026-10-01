using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Services
{
    /// <summary>
    /// Resolves wars inherited when a kingdom becomes a subject. A former
    /// enemy may accept peace with the subject or extend the war to its new
    /// overlord.
    /// </summary>
    public static class SubjectFormerWarResolutionService
    {
        private const float NativeDecisionWeight = 50f;
        private const float BlocStrengthWeight = 80f;
        private const float MaximumBlocStrengthScore = 120f;
        private const float SubjectStrengthContribution = 0.35f;
        private const float MaximumAdmissionWarRisk = 200f;

        private static readonly HashSet<Kingdom> ActiveSubjects =
            new HashSet<Kingdom>();

        public static bool IsResolutionInProgressFor(Kingdom subject)
        {
            return subject != null && ActiveSubjects.Contains(subject);
        }

        /// <summary>
        /// Calculates the receiving kingdom's total war pressure. The root
        /// kingdom contributes all of its strength while subjects contribute
        /// only part of theirs, so the overlord's own power remains decisive.
        /// </summary>
        public static float CalculateAdmissionWarRisk(
            Kingdom overlord,
            Kingdom subject,
            out int expectedEscalations,
            out float defenseCapacity,
            out float currentEnemyStrength,
            out float potentialEnemyStrength,
            out float availableWarCapacity,
            out float combinedEnemyStrength,
            out float pressureRatio)
        {
            expectedEscalations = 0;
            defenseCapacity = 0f;
            currentEnemyStrength = 0f;
            potentialEnemyStrength = 0f;
            availableWarCapacity = 0f;
            combinedEnemyStrength = 0f;
            pressureRatio = 0f;
            if (!IsUsable(overlord) || !IsUsable(subject))
            {
                return 0f;
            }

            defenseCapacity = GetDiplomaticBlocStrength(
                overlord,
                subject);
            HashSet<Kingdom> currentEnemyBlocs = new HashSet<Kingdom>();
            HashSet<Kingdom> potentialEnemyBlocs = new HashSet<Kingdom>();

            // Existing enemies already consume the overlord's military
            // capacity, even when the prospective subject shares that war.
            foreach (Kingdom enemy in Kingdom.All.Where(other =>
                IsUsable(other)
                && other != overlord
                && other != subject
                && FactionManager.IsAtWarAgainstFaction(overlord, other)))
            {
                currentEnemyBlocs.Add(GetDiplomaticRoot(enemy));
            }

            // A unique subject war contributes only when its enemy is likely
            // to reject peace and extend the conflict to the overlord.
            foreach (Kingdom enemy in Kingdom.All.Where(other =>
                IsUsable(other)
                && other != overlord
                && other != subject
                && FactionManager.IsAtWarAgainstFaction(subject, other)))
            {
                Kingdom enemyRoot = GetDiplomaticRoot(enemy);
                if (!IsUsable(enemyRoot)
                    || currentEnemyBlocs.Contains(enemyRoot)
                    || potentialEnemyBlocs.Contains(enemyRoot)
                    || FactionManager.IsAtWarAgainstFaction(
                        overlord,
                        enemyRoot))
                {
                    continue;
                }

                float enemyBlocStrength = GetDiplomaticBlocStrength(enemyRoot);
                if (!WouldAcceptPeace(
                    enemyRoot,
                    subject,
                    defenseCapacity,
                    enemyBlocStrength))
                {
                    potentialEnemyBlocs.Add(enemyRoot);
                    expectedEscalations++;
                }
            }

            currentEnemyStrength = currentEnemyBlocs.Sum(enemy =>
                GetDiplomaticBlocStrength(enemy));
            potentialEnemyStrength = potentialEnemyBlocs.Sum(enemy =>
                GetDiplomaticBlocStrength(enemy));
            combinedEnemyStrength = currentEnemyStrength
                + potentialEnemyStrength;
            availableWarCapacity = Math.Max(
                0f,
                defenseCapacity - currentEnemyStrength);

            // Existing wars are not a new cost created by admission. They
            // reduce the capacity available to absorb prospective enemies,
            // while zero prospective enemies always means zero admission risk.
            if (potentialEnemyStrength <= 0f)
            {
                pressureRatio = 0f;
                return 0f;
            }

            pressureRatio = potentialEnemyStrength
                / Math.Max(1f, availableWarCapacity);
            return -Clamp(
                pressureRatio * 100f,
                0f,
                MaximumAdmissionWarRisk);
        }

        public static void Resolve(
            Kingdom overlord,
            Kingdom subject,
            IEnumerable<Kingdom> formerEnemies)
        {
            if (!IsUsable(overlord)
                || !IsUsable(subject)
                || formerEnemies == null
                || !ActiveSubjects.Add(subject))
            {
                return;
            }

            try
            {
                foreach (Kingdom enemy in formerEnemies
                    .Where(IsUsable)
                    .Distinct()
                    .ToList())
                {
                    if (!FactionManager.IsAtWarAgainstFaction(subject, enemy)
                        || FactionManager.IsAtWarAgainstFaction(overlord, enemy))
                    {
                        continue;
                    }

                    ResolveFormerEnemy(overlord, subject, enemy);
                }
            }
            finally
            {
                ActiveSubjects.Remove(subject);
            }
        }

        private static void ResolveFormerEnemy(
            Kingdom overlord,
            Kingdom subject,
            Kingdom enemy)
        {
            float subjectBlocStrength = GetDiplomaticBlocStrength(overlord);
            float enemyBlocStrength = GetDiplomaticBlocStrength(enemy);
            float ratio = subjectBlocStrength / Math.Max(1f, enemyBlocStrength);
            float blocStrengthScore = Clamp(
                Log2(Math.Max(0.01f, ratio)) * BlocStrengthWeight,
                -MaximumBlocStrengthScore,
                MaximumBlocStrengthScore);

            // applyResults is false because this temporary decision is used
            // only to reuse the native clan peace evaluation.
            Clan proposer = enemy.RulingClan ?? EligibleClans(enemy).FirstOrDefault();
            if (proposer == null)
            {
                DeclareWarAction.ApplyByDefault(overlord, enemy);
                LogResolution(subject, overlord, enemy, 0f, false);
                return;
            }

            MakePeaceKingdomDecision nativeDecision =
                new MakePeaceKingdomDecision(
                    proposer,
                    subject,
                    0,
                    0,
                    false,
                    false);

            float acceptWeight = 0f;
            float totalWeight = 0f;
            foreach (Clan clan in EligibleClans(enemy))
            {
                float nativeSupport = nativeDecision.CalculateSupport(clan);
                float nativeScore = nativeSupport > 100f
                    ? NativeDecisionWeight
                    : -NativeDecisionWeight;
                float finalScore = nativeScore + blocStrengthScore;
                float voteWeight = Math.Max(1f, clan.Influence);
                bool supportsPeace = finalScore >= 0f;
                totalWeight += voteWeight;
                if (supportsPeace)
                {
                    acceptWeight += voteWeight;
                }

                LogClanEvaluation(
                    clan,
                    subject,
                    overlord,
                    enemy,
                    nativeSupport,
                    subjectBlocStrength,
                    enemyBlocStrength,
                    ratio,
                    blocStrengthScore,
                    finalScore,
                    supportsPeace);
            }

            float acceptShare = totalWeight <= 0f
                ? 0f
                : acceptWeight / totalWeight;
            bool acceptsPeace = totalWeight > 0f && acceptShare >= 0.5f;
            if (acceptsPeace)
            {
                MakePeaceAction.Apply(subject, enemy);
            }
            else
            {
                DeclareWarAction.ApplyByDefault(overlord, enemy);
            }

            LogResolution(
                subject,
                overlord,
                enemy,
                acceptShare,
                acceptsPeace);
        }

        /// <summary>
        /// Calculates effective bloc strength. The root kingdom contributes
        /// 100% and each direct subject contributes 35%. This recognizes
        /// subject assistance without treating a loose bloc as one army.
        /// </summary>
        private static bool WouldAcceptPeace(
            Kingdom enemy,
            Kingdom subject,
            float subjectBlocStrength,
            float enemyBlocStrength)
        {
            Clan proposer = enemy.RulingClan ?? EligibleClans(enemy).FirstOrDefault();
            if (proposer == null)
            {
                return false;
            }

            float ratio = subjectBlocStrength / Math.Max(1f, enemyBlocStrength);
            float blocStrengthScore = Clamp(
                Log2(Math.Max(0.01f, ratio)) * BlocStrengthWeight,
                -MaximumBlocStrengthScore,
                MaximumBlocStrengthScore);
            MakePeaceKingdomDecision nativeDecision =
                new MakePeaceKingdomDecision(
                    proposer,
                    subject,
                    0,
                    0,
                    false,
                    false);
            float acceptWeight = 0f;
            float totalWeight = 0f;
            foreach (Clan clan in EligibleClans(enemy))
            {
                float nativeSupport = nativeDecision.CalculateSupport(clan);
                float nativeScore = nativeSupport > 100f
                    ? NativeDecisionWeight
                    : -NativeDecisionWeight;
                float weight = Math.Max(1f, clan.Influence);
                totalWeight += weight;
                if (nativeScore + blocStrengthScore >= 0f)
                {
                    acceptWeight += weight;
                }
            }

            return totalWeight > 0f && acceptWeight / totalWeight >= 0.5f;
        }

        private static float GetDiplomaticBlocStrength(
            Kingdom kingdom,
            Kingdom additionalMember = null)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            Kingdom root = manager?.GetOverlord(kingdom) ?? kingdom;
            HashSet<Kingdom> members = new HashSet<Kingdom>();
            if (IsUsable(root))
            {
                members.Add(root);
            }

            if (IsUsable(additionalMember))
            {
                members.Add(additionalMember);
            }

            if (manager != null)
            {
                foreach (SubjectRelationData relation in manager.GetSubjects(root))
                {
                    if (IsUsable(relation?.SubjectKingdom))
                    {
                        members.Add(relation.SubjectKingdom);
                    }
                }
            }

            float rootStrength = SafeNumber(root?.CurrentTotalStrength ?? 0f);
            float subjectStrength = members
                .Where(member => member != root)
                .Sum(member => SafeNumber(member.CurrentTotalStrength));
            return Math.Max(
                1f,
                rootStrength
                    + subjectStrength * SubjectStrengthContribution);
        }

        private static Kingdom GetDiplomaticRoot(Kingdom kingdom)
        {
            return KingdomDiplomacyManager.Current?.GetOverlord(kingdom)
                ?? kingdom;
        }

        private static IEnumerable<Clan> EligibleClans(Kingdom kingdom)
        {
            return kingdom.Clans.Where(clan => clan != null
                && !clan.IsEliminated
                && !clan.IsUnderMercenaryService);
        }

        private static void LogClanEvaluation(
            Clan clan,
            Kingdom subject,
            Kingdom overlord,
            Kingdom enemy,
            float nativeSupport,
            float subjectBlocStrength,
            float enemyBlocStrength,
            float ratio,
            float blocStrengthScore,
            float finalScore,
            bool supportsPeace)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectFormerWarClanEvaluation}" +
                "[Subject diplomacy] {CLAN} evaluated peace with {SUBJECT} " +
                "after it joined {OVERLORD}. Native peace support was " +
                "{NATIVE}. The subject bloc had {SUBJECT_STRENGTH} strength " +
                "against {ENEMY}'s bloc strength of {ENEMY_STRENGTH}, a " +
                "ratio of {RATIO}, adding {BLOC_SCORE}. The final score was " +
                "{FINAL}, so the clan supported {DECISION}.");
            message.SetTextVariable("CLAN", clan.Name);
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("OVERLORD", overlord.Name);
            message.SetTextVariable("ENEMY", enemy.Name);
            message.SetTextVariable("NATIVE", nativeSupport.ToString("F0"));
            message.SetTextVariable(
                "SUBJECT_STRENGTH", subjectBlocStrength.ToString("F0"));
            message.SetTextVariable(
                "ENEMY_STRENGTH", enemyBlocStrength.ToString("F0"));
            message.SetTextVariable("RATIO", ratio.ToString("F2"));
            message.SetTextVariable(
                "BLOC_SCORE", blocStrengthScore.ToString("+0.0;-0.0;0.0"));
            message.SetTextVariable(
                "FINAL", finalScore.ToString("+0.0;-0.0;0.0"));
            message.SetTextVariable(
                "DECISION",
                supportsPeace
                    ? new TextObject("{=MP_SubjectFormerWarPeace}peace")
                    : new TextObject(
                        "{=MP_SubjectFormerWarEscalation}war escalation"));
            ModLogger.Info(message.ToString());
        }

        private static void LogResolution(
            Kingdom subject,
            Kingdom overlord,
            Kingdom enemy,
            float acceptShare,
            bool acceptsPeace)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectFormerWarResolution}" +
                "[Subject diplomacy] {ENEMY} gave {RESULT} after {SUBJECT} " +
                "became a subject of {OVERLORD}. Peace support was {SHARE}%.");
            message.SetTextVariable("ENEMY", enemy.Name);
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("OVERLORD", overlord.Name);
            message.SetTextVariable(
                "RESULT",
                acceptsPeace
                    ? new TextObject(
                        "{=MP_SubjectFormerWarAcceptedPeace}peace to the subject")
                    : new TextObject(
                        "{=MP_SubjectFormerWarDeclaredOverlord}war to the overlord"));
            message.SetTextVariable(
                "SHARE", (acceptShare * 100f).ToString("F1"));
            ModLogger.Notice(message.ToString());
        }

        private static float Log2(float value)
        {
            return (float)(Math.Log(value) / Math.Log(2d));
        }

        private static float SafeNumber(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Math.Max(0f, value);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool IsUsable(Kingdom kingdom)
        {
            return kingdom != null && !kingdom.IsEliminated;
        }
    }
}
