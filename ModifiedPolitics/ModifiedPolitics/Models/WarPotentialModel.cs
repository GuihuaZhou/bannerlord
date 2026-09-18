using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedPolitics.Models
{
    /// <summary>
    /// 战争潜力等级。
    ///
    /// 所有王国、所有 Clan 共用同一套绝对标准：
    ///
    /// 0 ~ 299       极其弱小
    /// 300 ~ 599     弱小
    /// 600 ~ 999     一般
    /// 1000 ~ 1499   强大
    /// 1500+         极其强大
    /// </summary>
    public enum WarPotentialLevel
    {
        VeryWeak,
        Weak,
        Average,
        Strong,
        VeryStrong
    }


    /// <summary>
    /// Clan 战争潜力完整计算结果。
    ///
    /// UI、政治集团、战争倾向、AI 等模块
    /// 可以统一读取此结果。
    /// </summary>
    public sealed class WarPotentialResult
    {
        // =============================================================
        // 最终结果
        // =============================================================

        /// <summary>
        /// 原始战争潜力。
        ///
        /// WarPotential
        /// =
        /// FieldTroops
        /// +
        /// EffectiveReserve × FinancialFactor
        /// </summary>
        public int WarPotential { get; set; }


        /// <summary>
        /// 战争潜力等级。
        /// </summary>
        public WarPotentialLevel WarPotentialLevel { get; set; }


        // =============================================================
        // 当前军事力量
        // =============================================================

        /// <summary>
        /// 当前 Clan 所有 WarParty 的实际兵力。
        /// </summary>
        public int FieldTroops { get; set; }


        /// <summary>
        /// Clan 所有城镇/城堡驻军实际人数。
        /// </summary>
        public int GarrisonTroops { get; set; }


        /// <summary>
        /// Clan 封地中的军役兵数量。
        /// </summary>
        public int FiefTroops { get; set; }


        /// <summary>
        /// 后备兵员池。
        ///
        /// TroopPool
        /// =
        /// GarrisonTroops
        /// +
        /// FiefTroops
        /// </summary>
        public int TroopPool { get; set; }


        // =============================================================
        // Hero 动员能力
        // =============================================================

        /// <summary>
        /// 当前实际具备带队资格的 Hero 总数。
        ///
        /// 包括符合条件的 Companion，
        /// 不要求 Occupation == Lord。
        /// </summary>
        public int AvailableCommanderCount { get; set; }


        /// <summary>
        /// Clan 当前还能创建多少支 WarParty。
        /// </summary>
        public int FreePartySlots { get; set; }


        /// <summary>
        /// 最终实际选中的统兵 Hero 数量。
        ///
        /// SelectedCommanderCount
        /// =
        /// min(
        ///     AvailableCommanderCount,
        ///     FreePartySlots
        /// )
        /// </summary>
        public int SelectedCommanderCount { get; set; }


        /// <summary>
        /// 被选中的 Hero 如果各自创建新 Party，
        /// 可以提供的总 Party 容量。
        ///
        /// NewPartyCapacity
        /// =
        /// Σ GetAssumedPartySizeForLordParty(hero)
        /// </summary>
        public int NewPartyCapacity { get; set; }


        /// <summary>
        /// 真正能够转化成未来野战军的后备兵力。
        ///
        /// EffectiveReserve
        /// =
        /// min(
        ///     TroopPool,
        ///     NewPartyCapacity
        /// )
        /// </summary>
        public int EffectiveReserve { get; set; }


        // =============================================================
        // 财政
        // =============================================================

        /// <summary>
        /// Clan 财富。
        ///
        /// 当前定义：
        /// Clan Heroes + Companions 的 Gold 总和。
        /// </summary>
        public int ClanWealth { get; set; }


        /// <summary>
        /// Clan 每日收入。
        ///
        /// 这里只计算收入，
        /// 不包含 Party 工资等支出。
        /// </summary>
        public float DailyIncome { get; set; }


        /// <summary>
        /// 当前 Clan 所有 WarParty 的每日工资。
        ///
        /// 不包含战争 ×2。
        /// </summary>
        public int PartyDailyWage { get; set; }


        /// <summary>
        /// 战时 WarParty 每日工资。
        ///
        /// WarDailyWage
        /// =
        /// PartyDailyWage × 2
        /// </summary>
        public float WarDailyWage { get; set; }


        /// <summary>
        /// 战时每日净财政消耗。
        ///
        /// WarDailyBurn
        /// =
        /// max(
        ///     0,
        ///     WarDailyWage - DailyIncome
        /// )
        /// </summary>
        public float WarDailyBurn { get; set; }


        /// <summary>
        /// 当前财富可以按照当前净消耗维持多少天。
        /// </summary>
        public float FinancialEndurance { get; set; }


        /// <summary>
        /// 财政系数。
        ///
        /// 0 ~ 1
        /// </summary>
        public float FinancialFactor { get; set; }


        /// <summary>
        /// 财政修正后的持续战力。
        ///
        /// SustainedMilitaryPower
        /// =
        /// EffectiveReserve × FinancialFactor
        /// </summary>
        public float SustainedMilitaryPower { get; set; }
    }


    /// <summary>
    /// Clan 战争潜力模型。
    ///
    /// ===============================================================
    /// 军事部分
    /// ===============================================================
    ///
    /// FieldTroops
    /// =
    /// Σ 当前 Clan WarParty 实际人数
    ///
    ///
    /// TroopPool
    /// =
    /// FiefTroops
    /// +
    /// GarrisonTroops
    ///
    ///
    /// FreePartySlots
    /// =
    /// ClanTierModel.GetPartyLimitForTier(clan, clan.Tier)
    /// -
    /// 当前 WarParty 数量
    ///
    ///
    /// 可用统兵 Hero：
    ///
    /// IsAlive
    /// IsActive
    /// !IsDisabled
    /// !IsChild
    /// 非俘虏
    /// CanLeadParty()
    /// 当前不是其他 Party Leader
    ///
    ///
    /// 如果可用 Hero 多于 Party 空位，
    /// 按 HeroSpawnCampaignBehavior 的统兵评分排序。
    ///
    ///
    /// NewPartyCapacity
    /// =
    /// Σ PartySizeLimitModel.GetAssumedPartySizeForLordParty(hero)
    ///
    ///
    /// EffectiveReserve
    /// =
    /// min(
    ///     TroopPool,
    ///     NewPartyCapacity
    /// )
    ///
    ///
    /// ===============================================================
    /// 财政部分
    /// ===============================================================
    ///
    /// WarDailyWage
    /// =
    /// PartyDailyWage × 2
    ///
    ///
    /// WarDailyBurn
    /// =
    /// max(
    ///     0,
    ///     WarDailyWage - DailyIncome
    /// )
    ///
    ///
    /// FinancialEndurance
    /// =
    /// ClanWealth / WarDailyBurn
    ///
    ///
    /// FinancialFactor
    /// =
    /// min(
    ///     FinancialEndurance / 60,
    ///     1
    /// )
    ///
    ///
    /// ===============================================================
    /// 最终战争潜力
    /// ===============================================================
    ///
    /// WarPotential
    /// =
    /// FieldTroops
    /// +
    /// EffectiveReserve × FinancialFactor
    ///
    /// ===============================================================
    /// </summary>
    public sealed class WarPotentialModel : GameModel
    {
        // =============================================================
        // 等级阈值
        // =============================================================

        /// <summary>
        /// 小于 300：
        /// 极其弱小
        /// </summary>
        public const int WeakThreshold = 300;


        /// <summary>
        /// 300 ~ 599：
        /// 弱小
        /// </summary>
        public const int AverageThreshold = 600;


        /// <summary>
        /// 600 ~ 999：
        /// 一般
        /// </summary>
        public const int StrongThreshold = 1000;


        /// <summary>
        /// 1000 ~ 1499：
        /// 强大
        ///
        /// 1500+：
        /// 极其强大
        /// </summary>
        public const int VeryStrongThreshold = 1500;


        // =============================================================
        // 战争潜力计算常量
        // =============================================================

        /// <summary>
        /// 战时 Clan WarParty 工资倍率。
        ///
        /// 驻军不参与翻倍。
        /// </summary>
        public const float WarWageMultiplier = 2f;


        /// <summary>
        /// 财政持续能力参考值。
        ///
        /// 能够维持 60 天战争时，
        /// FinancialFactor 达到 1。
        /// </summary>
        public const float ReferenceWarDays = 60f;


        // =============================================================
        // Instance
        // =============================================================

        /// <summary>
        /// 当前 Campaign 中注册的战争潜力模型。
        /// </summary>
        public static WarPotentialModel Instance { get; private set; }


        public WarPotentialModel()
        {
            Instance = this;
        }


        // =============================================================
        // 主入口
        // =============================================================

        /// <summary>
        /// 计算 Clan 完整战争潜力。
        /// </summary>
        public WarPotentialResult CalculateWarPotential(
            Clan clan)
        {
            WarPotentialResult result =
                new WarPotentialResult();


            if (clan == null
                ||
                Campaign.Current == null
                ||
                clan.IsBanditFaction
                ||
                clan.IsMinorFaction
                ||
                clan.IsEliminated)
            {
                result.WarPotentialLevel =
                    WarPotentialLevel.VeryWeak;

                return result;
            }


            // =========================================================
            // 1. 当前机动兵力
            // =========================================================

            result.FieldTroops =
                CalculateFieldTroops(
                    clan);


            // =========================================================
            // 2. 后备兵员池
            // =========================================================

            result.GarrisonTroops =
                CalculateGarrisonTroops(
                    clan);


            result.FiefTroops =
                CalculateFiefTroops(
                    clan);


            result.TroopPool =
                result.GarrisonTroops
                +
                result.FiefTroops;


            // =========================================================
            // 3. 可用统兵 Hero
            // =========================================================

            List<Hero> availableCommanders =
                GetAvailableCommanders(clan)
                    .OrderByDescending(
                        GetHeroPartyCommandScore)
                    .ToList();


            result.AvailableCommanderCount =
                availableCommanders.Count;


            // =========================================================
            // 4. Clan 剩余 Party 槽位
            // =========================================================

            result.FreePartySlots =
                CalculateFreePartySlots(
                    clan);


            // =========================================================
            // 5. 选出真正能够建立新 Party 的 Hero
            // =========================================================

            List<Hero> selectedCommanders =
                availableCommanders
                    .Take(
                        result.FreePartySlots)
                    .ToList();


            result.SelectedCommanderCount =
                selectedCommanders.Count;


            // =========================================================
            // 6. 新 Party 总容量
            // =========================================================

            result.NewPartyCapacity =
                CalculateNewPartyCapacity(
                    clan,
                    selectedCommanders);


            // =========================================================
            // 7. 实际有效后备兵力
            // =========================================================

            result.EffectiveReserve =
                Math.Min(
                    result.TroopPool,
                    result.NewPartyCapacity);


            // =========================================================
            // 8. Clan 财富
            // =========================================================

            result.ClanWealth =
                CalculateClanWealth(
                    clan);


            // =========================================================
            // 9. 每日收入
            // =========================================================

            result.DailyIncome =
                CalculateDailyIncome(
                    clan);


            // =========================================================
            // 10. 当前 WarParty 工资
            // =========================================================

            result.PartyDailyWage =
                CalculatePartyDailyWage(
                    clan);


            // =========================================================
            // 11. 战时工资
            // =========================================================

            result.WarDailyWage =
                result.PartyDailyWage
                *
                WarWageMultiplier;


            // =========================================================
            // 12. 每日战争财政消耗
            // =========================================================

            result.WarDailyBurn =
                Math.Max(
                    0f,
                    result.WarDailyWage
                    -
                    result.DailyIncome);


            // =========================================================
            // 13. 财政持续能力
            // =========================================================

            if (result.WarDailyBurn <= 0f)
            {
                // 当前收入足够覆盖战时 Party 工资。
                result.FinancialEndurance =
                    float.PositiveInfinity;


                result.FinancialFactor =
                    1f;
            }
            else
            {
                float availableWealth =
                    Math.Max(
                        0f,
                        result.ClanWealth);


                result.FinancialEndurance =
                    availableWealth
                    /
                    result.WarDailyBurn;


                result.FinancialFactor =
                    Math.Min(
                        result.FinancialEndurance
                        /
                        ReferenceWarDays,
                        1f);


                result.FinancialFactor =
                    Math.Max(
                        0f,
                        result.FinancialFactor);
            }


            // =========================================================
            // 14. 持续战力
            // =========================================================

            result.SustainedMilitaryPower =
                result.EffectiveReserve
                *
                result.FinancialFactor;


            // =========================================================
            // 15. 最终战争潜力
            // =========================================================

            float finalWarPotential =
                result.FieldTroops
                +
                result.SustainedMilitaryPower;


            result.WarPotential =
                (int)Math.Round(
                    finalWarPotential,
                    MidpointRounding.AwayFromZero);


            // =========================================================
            // 16. 战争潜力等级
            // =========================================================

            result.WarPotentialLevel =
                GetWarPotentialLevel(
                    result.WarPotential);


            return result;
        }


        // =============================================================
        // 战争潜力等级
        // =============================================================

        /// <summary>
        /// 所有王国、所有 Clan 使用同一套绝对标准。
        ///
        /// 0 ~ 299       极其弱小
        /// 300 ~ 599     弱小
        /// 600 ~ 999     一般
        /// 1000 ~ 1499   强大
        /// 1500+         极其强大
        /// </summary>
        public static WarPotentialLevel GetWarPotentialLevel(
            int warPotential)
        {
            if (warPotential < WeakThreshold)
            {
                return WarPotentialLevel.VeryWeak;
            }


            if (warPotential < AverageThreshold)
            {
                return WarPotentialLevel.Weak;
            }


            if (warPotential < StrongThreshold)
            {
                return WarPotentialLevel.Average;
            }


            if (warPotential < VeryStrongThreshold)
            {
                return WarPotentialLevel.Strong;
            }


            return WarPotentialLevel.VeryStrong;
        }


        /// <summary>
        /// 将战争潜力等级转换为 UI 中文文本。
        /// </summary>
        public static string GetWarPotentialLevelText(
            WarPotentialLevel level)
        {
            switch (level)
            {
                case WarPotentialLevel.VeryWeak:
                    return "极其弱小";

                case WarPotentialLevel.Weak:
                    return "弱小";

                case WarPotentialLevel.Average:
                    return "一般";

                case WarPotentialLevel.Strong:
                    return "强大";

                case WarPotentialLevel.VeryStrong:
                    return "极其强大";

                default:
                    return "未知";
            }
        }


        /// <summary>
        /// 获取面板最终显示文本。
        ///
        /// 示例：
        ///
        /// 强大(1200)
        /// </summary>
        public static string GetWarPotentialDisplayText(
            int warPotential)
        {
            WarPotentialLevel level =
                GetWarPotentialLevel(
                    warPotential);


            string levelText =
                GetWarPotentialLevelText(
                    level);


            return
                $"{levelText}({warPotential})";
        }


        // =============================================================
        // 当前机动兵力
        // =============================================================

        private static int CalculateFieldTroops(
            Clan clan)
        {
            int total = 0;


            foreach (
                WarPartyComponent warPartyComponent
                in clan.WarPartyComponents)
            {
                if (warPartyComponent == null)
                {
                    continue;
                }


                if (warPartyComponent.Party == null)
                {
                    continue;
                }


                total +=
                    warPartyComponent
                        .Party
                        .NumberOfAllMembers;
            }


            return total;
        }


        // =============================================================
        // 驻军兵力
        // =============================================================

        private static int CalculateGarrisonTroops(
            Clan clan)
        {
            int total = 0;


            foreach (
                Settlement settlement
                in clan.Settlements)
            {
                if (settlement == null)
                {
                    continue;
                }


                if (!settlement.IsTown
                    &&
                    !settlement.IsCastle)
                {
                    continue;
                }


                if (settlement.Town == null)
                {
                    continue;
                }


                if (settlement.Town.GarrisonParty == null)
                {
                    continue;
                }


                if (settlement.Town.GarrisonParty.Party == null)
                {
                    continue;
                }


                total +=
                    settlement
                        .Town
                        .GarrisonParty
                        .Party
                        .NumberOfAllMembers;
            }


            return total;
        }


        // =============================================================
        // 军役兵力
        // =============================================================

        private static int CalculateFiefTroops(
            Clan clan)
        {
            FiefPartyManager fiefPartyManager =
                Campaign.Current
                    .GetCampaignBehavior<FiefPartyManager>();


            if (fiefPartyManager == null)
            {
                return 0;
            }


            int total = 0;


            foreach (
                Settlement settlement
                in clan.Settlements)
            {
                if (settlement == null)
                {
                    continue;
                }


                if (!settlement.IsTown
                    &&
                    !settlement.IsCastle)
                {
                    continue;
                }


                var troopCounts =
                    fiefPartyManager
                        .GetFiefTroopCounts(
                            settlement);


                if (troopCounts == null)
                {
                    continue;
                }


                total +=
                    troopCounts
                        .Values
                        .Sum();
            }


            return total;
        }


        // =============================================================
        // 可用统兵 Hero
        // =============================================================

        private static IEnumerable<Hero> GetAvailableCommanders(
            Clan clan)
        {
            HashSet<Hero> heroes =
                new HashSet<Hero>();


            foreach (Hero hero in clan.Heroes)
            {
                if (hero != null)
                {
                    heroes.Add(
                        hero);
                }
            }


            foreach (Hero companion in clan.Companions)
            {
                if (companion != null)
                {
                    heroes.Add(
                        companion);
                }
            }


            foreach (Hero hero in heroes)
            {
                if (IsAvailableCommander(hero))
                {
                    yield return hero;
                }
            }
        }


        /// <summary>
        /// 判断一个 Hero 当前是否可以用于建立新的 Clan Party。
        ///
        /// 不限定 Occupation == Lord。
        /// </summary>
        private static bool IsAvailableCommander(
            Hero hero)
        {
            if (hero == null)
            {
                return false;
            }


            if (!hero.IsAlive)
            {
                return false;
            }


            if (!hero.IsActive)
            {
                return false;
            }


            if (hero.IsDisabled)
            {
                return false;
            }


            if (hero.IsChild)
            {
                return false;
            }


            if (hero.PartyBelongedToAsPrisoner != null)
            {
                return false;
            }


            if (!hero.CanLeadParty())
            {
                return false;
            }


            // Hero 如果已经是某支 Party 的 Leader，
            // 就不能再次建立新 Party。
            if (hero.PartyBelongedTo != null
                &&
                hero.PartyBelongedTo.LeaderHero == hero)
            {
                return false;
            }


            return true;
        }


        // =============================================================
        // 空余 Party 槽位
        // =============================================================

        private static int CalculateFreePartySlots(
            Clan clan)
        {
            if (clan == null
                ||
                Campaign.Current == null)
            {
                return 0;
            }


            int partyLimit =
                Campaign.Current
                    .Models
                    .ClanTierModel
                    .GetPartyLimitForTier(
                        clan,
                        clan.Tier);


            int currentPartyCount =
                clan.WarPartyComponents.Count;


            int freeSlots =
                partyLimit
                -
                currentPartyCount;


            return Math.Max(
                0,
                freeSlots);
        }


        // =============================================================
        // Hero 统兵评分
        // =============================================================

        /// <summary>
        /// 参考本体：
        ///
        /// HeroSpawnCampaignBehavior.GetHeroPartyCommandScore()
        ///
        /// 此分数只用于：
        ///
        /// 当可用 Hero 数量大于 Party 空位时，
        /// 决定优先使用哪个 Hero。
        ///
        /// 此评分本身不会直接进入 WarPotential。
        /// </summary>
        private static float GetHeroPartyCommandScore(
            Hero hero)
        {
            if (hero == null)
            {
                return float.MinValue;
            }


            float score = 0f;


            score +=
                3f
                *
                hero.GetSkillValue(
                    DefaultSkills.Tactics);


            score +=
                2f
                *
                hero.GetSkillValue(
                    DefaultSkills.Leadership);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.Scouting);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.Steward);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.OneHanded);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.TwoHanded);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.Polearm);


            score +=
                hero.GetSkillValue(
                    DefaultSkills.Riding);


            // Clan Leader 优先。
            if (hero.Clan != null
                &&
                hero.Clan.Leader == hero)
            {
                score += 1000f;
            }


            // 非 Governor 更适合成为机动 Party Leader。
            if (hero.GovernorOf == null)
            {
                score += 500f;
            }


            // 非战斗人员基本排到最后。
            if (hero.IsNoncombatant)
            {
                score -= 5000f;
            }


            return score;
        }


        // =============================================================
        // 新 Party 总容量
        // =============================================================

        /// <summary>
        /// 不真正创建 MobileParty。
        ///
        /// 直接调用本体 PartySizeLimitModel：
        ///
        /// GetAssumedPartySizeForLordParty(...)
        ///
        /// 来估算每个 Hero 如果创建 Party 后能够带多少兵。
        /// </summary>
        private static int CalculateNewPartyCapacity(
            Clan clan,
            IEnumerable<Hero> selectedCommanders)
        {
            if (selectedCommanders == null)
            {
                return 0;
            }


            int totalCapacity = 0;


            foreach (Hero hero in selectedCommanders)
            {
                if (hero == null)
                {
                    continue;
                }


                int capacity =
                    Campaign.Current
                        .Models
                        .PartySizeLimitModel
                        .GetAssumedPartySizeForLordParty(
                            hero,
                            clan.MapFaction,
                            clan);


                if (capacity <= 0)
                {
                    continue;
                }


                totalCapacity +=
                    capacity;
            }


            return totalCapacity;
        }


        // =============================================================
        // Clan 财富
        // =============================================================

        /// <summary>
        /// Clan 财富：
        ///
        /// Clan.Heroes
        /// +
        /// Clan.Companions
        ///
        /// 的 Hero.Gold 总和。
        ///
        /// HashSet 防止重复计算。
        /// </summary>
        private static int CalculateClanWealth(
            Clan clan)
        {
            HashSet<Hero> heroes =
                new HashSet<Hero>();


            foreach (Hero hero in clan.Heroes)
            {
                if (hero != null)
                {
                    heroes.Add(
                        hero);
                }
            }


            foreach (Hero companion in clan.Companions)
            {
                if (companion != null)
                {
                    heroes.Add(
                        companion);
                }
            }


            long total = 0L;


            foreach (Hero hero in heroes)
            {
                total +=
                    hero.Gold;
            }


            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }


            if (total <= int.MinValue)
            {
                return int.MinValue;
            }


            return (int)total;
        }


        // =============================================================
        // 每日收入
        // =============================================================

        /// <summary>
        /// 只计算 Clan 收入。
        ///
        /// 不使用 CalculateClanGoldChange，
        /// 避免 Party 工资在战争潜力公式中被重复扣除。
        /// </summary>
        private static float CalculateDailyIncome(
            Clan clan)
        {
            return Campaign.Current
                .Models
                .ClanFinanceModel
                .CalculateClanIncome(
                    clan,
                    includeDescriptions: false,
                    applyWithdrawals: false,
                    includeDetails: false)
                .ResultNumber;
        }


        // =============================================================
        // 当前 WarParty 工资
        // =============================================================

        /// <summary>
        /// 这里只统计 Clan WarParty。
        ///
        /// 不包含：
        /// - 驻军
        /// - Caravan
        ///
        /// 战时 ×2 在 WarPotentialModel 内部单独应用。
        /// </summary>
        private static int CalculatePartyDailyWage(
            Clan clan)
        {
            long total = 0L;


            foreach (
                WarPartyComponent warPartyComponent
                in clan.WarPartyComponents)
            {
                if (warPartyComponent == null)
                {
                    continue;
                }


                if (warPartyComponent.MobileParty == null)
                {
                    continue;
                }


                total +=
                    warPartyComponent
                        .MobileParty
                        .TotalWage;
            }


            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }


            if (total <= 0L)
            {
                return 0;
            }


            return (int)total;
        }
    }
}
