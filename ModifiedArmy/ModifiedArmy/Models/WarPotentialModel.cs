using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Clan 战争潜力完整计算结果。
    ///
    /// 这个对象不仅给 UI 使用，
    /// 后续政治集团、战争倾向、AI 战争判断也可以直接使用。
    /// </summary>
    public sealed class WarPotentialResult
    {
        // =============================================================
        // 最终结果
        // =============================================================

        /// <summary>
        /// 最终战争潜力。
        ///
        /// WarPotential
        /// =
        /// FieldTroops
        /// +
        /// EffectiveReserve × FinancialFactor
        /// </summary>
        public int WarPotential { get; set; }


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
        /// GarrisonTroops + FiefTroops
        /// </summary>
        public int TroopPool { get; set; }


        // =============================================================
        // Hero 动员能力
        // =============================================================

        /// <summary>
        /// 当前真正具备带队条件的 Hero 总数。
        ///
        /// 注意：
        /// 这里包含实际可用 Companion，
        /// 不限制 Occupation == Lord。
        /// </summary>
        public int AvailableCommanderCount { get; set; }

        /// <summary>
        /// Clan 当前还能创建多少支 WarParty。
        /// </summary>
        public int FreePartySlots { get; set; }

        /// <summary>
        /// 实际参与后备动员计算的 Hero 数量。
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
        /// 所有选中 Hero 如果创建新 Party，
        /// 能够提供的总 Party 容量。
        ///
        /// NewPartyCapacity
        /// =
        /// Σ GetAssumedPartySizeForLordParty(hero)
        /// </summary>
        public int NewPartyCapacity { get; set; }

        /// <summary>
        /// 真正可以转化成未来野战军的后备兵力。
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
        /// Clan 每日总收入。
        ///
        /// 注意：
        /// 这里只计算收入，不包含支出。
        /// </summary>
        public float DailyIncome { get; set; }

        /// <summary>
        /// 当前所有 Clan WarParty 的每日工资。
        ///
        /// 这是和平/当前状态下的基础 Party 工资，
        /// 不包含战争 ×2。
        /// </summary>
        public int PartyDailyWage { get; set; }

        /// <summary>
        /// 战时 Party 工资。
        ///
        /// WarDailyWage
        /// =
        /// PartyDailyWage × WarWageMultiplier
        /// </summary>
        public float WarDailyWage { get; set; }

        /// <summary>
        /// 战时每日净消耗。
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
        /// 按当前财富可以支撑的战争天数。
        ///
        /// 如果 WarDailyBurn == 0，
        /// 则为 PositiveInfinity。
        /// </summary>
        public float FinancialEndurance { get; set; }

        /// <summary>
        /// 财政系数，范围 0~1。
        /// </summary>
        public float FinancialFactor { get; set; }

        /// <summary>
        /// 财政修正后的有效持续战力。
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
    /// ---------------------------------------------------------------
    /// 最终公式
    /// ---------------------------------------------------------------
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
    /// CommanderLimit
    /// -
    /// 当前 WarParty 数
    ///
    ///
    /// SelectedCommanders
    /// =
    /// 可用统兵 Hero
    /// 按统兵能力评分降序
    /// 取前 FreePartySlots 个
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
    /// FinancialFactor
    /// =
    /// min(
    ///     FinancialEndurance / 60,
    ///     1
    /// )
    ///
    ///
    /// SustainedMilitaryPower
    /// =
    /// EffectiveReserve
    /// ×
    /// FinancialFactor
    ///
    ///
    /// WarPotential
    /// =
    /// FieldTroops
    /// +
    /// SustainedMilitaryPower
    ///
    /// ---------------------------------------------------------------
    /// </summary>
    public sealed class WarPotentialModel : GameModel
    {
        /// <summary>
        /// 战争状态下 Clan WarParty 工资倍率。
        ///
        /// 驻军工资不翻倍。
        /// </summary>
        public const float WarWageMultiplier = 2f;

        /// <summary>
        /// 财政持续时间达到多少天后，
        /// 财政不再限制战争潜力。
        /// </summary>
        public const float ReferenceWarDays = 60f;


        /// <summary>
        /// 当前 Campaign 中注册的 WarPotentialModel。
        ///
        /// 因为这是 Mod 自己增加的 GameModel，
        /// Campaign.Current.Models 没有对应的强类型属性，
        /// 所以提供统一 Instance 给 UI / 政治系统调用。
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
        /// 计算一个 Clan 当前的完整战争潜力。
        /// </summary>
        public WarPotentialResult CalculateWarPotential(Clan clan)
        {
            WarPotentialResult result =
                new WarPotentialResult();


            if (clan == null)
            {
                return result;
            }


            if (Campaign.Current == null)
            {
                return result;
            }


            // =========================================================
            // 1. 当前野战力量
            // =========================================================

            result.FieldTroops =
                CalculateFieldTroops(clan);


            // =========================================================
            // 2. 后备兵员池
            // =========================================================

            result.GarrisonTroops =
                CalculateGarrisonTroops(clan);

            result.FiefTroops =
                CalculateFiefTroops(clan);

            result.TroopPool =
                result.GarrisonTroops
                +
                result.FiefTroops;


            // =========================================================
            // 3. Hero 动员能力
            // =========================================================

            List<Hero> availableCommanders =
                GetAvailableCommanders(clan)
                    .OrderByDescending(
                        GetHeroPartyCommandScore)
                    .ToList();


            result.AvailableCommanderCount =
                availableCommanders.Count;


            result.FreePartySlots =
                CalculateFreePartySlots(clan);


            List<Hero> selectedCommanders =
                availableCommanders
                    .Take(result.FreePartySlots)
                    .ToList();


            result.SelectedCommanderCount =
                selectedCommanders.Count;


            result.NewPartyCapacity =
                CalculateNewPartyCapacity(
                    clan,
                    selectedCommanders);


            result.EffectiveReserve =
                Math.Min(
                    result.TroopPool,
                    result.NewPartyCapacity);


            // =========================================================
            // 4. Clan 财政
            // =========================================================

            result.ClanWealth =
                CalculateClanWealth(clan);


            result.DailyIncome =
                CalculateDailyIncome(clan);


            result.PartyDailyWage =
                CalculatePartyDailyWage(clan);


            result.WarDailyWage =
                result.PartyDailyWage
                *
                WarWageMultiplier;


            result.WarDailyBurn =
                Math.Max(
                    0f,
                    result.WarDailyWage
                    -
                    result.DailyIncome);


            // =========================================================
            // 5. 财政持续能力
            // =========================================================

            if (result.WarDailyBurn <= 0f)
            {
                // 收入已经能够完全覆盖战时 Party 工资。
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


                if (result.FinancialFactor < 0f)
                {
                    result.FinancialFactor = 0f;
                }
            }


            // =========================================================
            // 6. 持续战力
            // =========================================================

            result.SustainedMilitaryPower =
                result.EffectiveReserve
                *
                result.FinancialFactor;


            // =========================================================
            // 7. 最终战争潜力
            // =========================================================

            float finalWarPotential =
                result.FieldTroops
                +
                result.SustainedMilitaryPower;


            result.WarPotential =
                (int)Math.Round(
                    finalWarPotential,
                    MidpointRounding.AwayFromZero);


            return result;
        }


        // =============================================================
        // 当前野战兵力
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
                        .GetFiefTroopCounts(settlement);


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

        /// <summary>
        /// 获取 Clan 当前实际能够拿来创建 Party 的 Hero。
        ///
        /// 这里不限制 Occupation == Lord。
        ///
        /// 因此：
        /// - Clan Lord 可以参与
        /// - Companion 也可以参与
        ///
        /// 只要游戏认为这个 Hero 实际能够领导 Party。
        /// </summary>
        private static IEnumerable<Hero> GetAvailableCommanders(
            Clan clan)
        {
            HashSet<Hero> heroes =
                new HashSet<Hero>();


            // Clan Heroes
            foreach (Hero hero in clan.Heroes)
            {
                if (hero != null)
                {
                    heroes.Add(hero);
                }
            }


            // Companions
            foreach (Hero companion in clan.Companions)
            {
                if (companion != null)
                {
                    heroes.Add(companion);
                }
            }


            foreach (Hero hero in heroes)
            {
                if (!IsAvailableCommander(hero))
                {
                    continue;
                }


                yield return hero;
            }
        }


        /// <summary>
        /// 判断 Hero 是否能够实际用于创建新的 Clan Party。
        ///
        /// 这个条件更接近 Clan Management UI，
        /// 而不是 AI 专用的 Occupation.Lord 限制。
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


            // 如果 Hero 已经是某支 Party 的 Leader，
            // 那么他已经被占用，不能再建立新的 Party。
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

        private static int CalculateFreePartySlots(Clan clan)
        {
            if (clan == null || Campaign.Current == null)
            {
                return 0;
            }

            int partyLimit = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(
                        clan,
                        clan.Tier);

            int currentPartyCount =
                clan.WarPartyComponents.Count;

            int freeSlots =
                partyLimit - currentPartyCount;

            return Math.Max(0, freeSlots);
        }


        // =============================================================
        // Hero 统兵评分
        // =============================================================

        /// <summary>
        /// 参考 HeroSpawnCampaignBehavior.GetHeroPartyCommandScore。
        ///
        /// 这个评分只用于：
        ///
        /// 当可用 Hero 数量 > 空余 Party 槽位时，
        /// 决定优先选择哪些 Hero。
        ///
        /// 它本身不会直接乘进 WarPotential。
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


            // 非 Governor Hero 更适合出去带队。
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
        /// 使用游戏本体专门提供的：
        ///
        /// GetAssumedPartySizeForLordParty
        ///
        /// 直接计算一个尚未真正建立 Party 的 Hero，
        /// 如果创建 Lord Party 后能够带多少兵。
        ///
        /// 不需要创建临时 MobileParty。
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


                totalCapacity += capacity;
            }


            return totalCapacity;
        }


        // =============================================================
        // Clan 财富
        // =============================================================

        /// <summary>
        /// Clan 财富定义：
        ///
        /// Clan Heroes + Companions 的 Hero.Gold 总和。
        ///
        /// 使用 HashSet 防止 Companion 同时已经包含于 Heroes 时重复计算。
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
                    heroes.Add(hero);
                }
            }


            foreach (Hero companion in clan.Companions)
            {
                if (companion != null)
                {
                    heroes.Add(companion);
                }
            }


            long total = 0L;


            foreach (Hero hero in heroes)
            {
                total += hero.Gold;
            }


            // 财富为负数对 WarPotential 没有意义。
            if (total <= 0L)
            {
                return 0;
            }


            // 防止非常极端的长期存档溢出 int。
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }


            return (int)total;
        }


        // =============================================================
        // 每日收入
        // =============================================================

        /// <summary>
        /// 只计算 Clan Income。
        ///
        /// 不调用 CalculateClanGoldChange，
        /// 因为那里面还会包含支出，
        /// 会导致 Party 工资被重复扣除。
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
        // 当前 Party 工资
        // =============================================================

        /// <summary>
        /// 这里只统计 Clan WarParty。
        ///
        /// 不包含：
        /// - Garrison
        /// - Caravan
        ///
        /// 战争 ×2 在 WarPotentialModel 自己内部应用。
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


            if (total <= 0L)
            {
                return 0;
            }


            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }


            return (int)total;
        }
    }
}