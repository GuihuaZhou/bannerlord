using ModifiedArmy.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    // =============== 新增：SaveableTypeDefiner ===============
    public class FiefSaveDefiner : SaveableTypeDefiner
    {
        // 唯一 ID: CRC32("ModifiedArmy_FiefSquad") = 0x7A3F1B8E → 2051496846
        public FiefSaveDefiner() : base(20251116) { }

        protected override void DefineClassTypes()
        {
            // 无需注册自定义类
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<Settlement>));
            ConstructContainerDefinition(typeof(List<TroopRoster>));
        }
    }

    // 预期的各个兵种最大数量
    public static class FiefSquadTroopsMaxNum
    {
        public static int _nobleTroopsMaxNum { get;} = 1;
        public static int _professionalTroopsMaxNum { get; } = 3;
        public static int _levyTroopsMaxNum { get; } = 6;
    }

    /// <summary>
    /// 定义一个采邑小队的组成：1个贵族兵，3个职业军，6个征召兵。使用 List<CharacterObject> 来分别存储这三类兵种。
    /// </summary>
    public class FiefSquad
    {
        private List<CharacterObject> _nobleTroops;
        private List<CharacterObject> _professionalTroops;
        private List<CharacterObject> _levyTroops;

        public int NobleCount => _nobleTroops.Count;
        public int ProfessionalCount => _professionalTroops.Count;
        public int LevyCount => _levyTroops.Count;

        // ====== 新增：征召状态标志 ======
        private bool _isConscripted = false;
        public bool IsConscripted => _isConscripted;
        public IReadOnlyList<CharacterObject> NobleTroops => _nobleTroops;
        public IReadOnlyList<CharacterObject> ProfessionalTroops => _professionalTroops;
        public IReadOnlyList<CharacterObject> LevyTroops => _levyTroops;

        /// <summary>
        /// 获取小队总人数 (1 + 3 + 6 = 10)、是否满员
        /// </summary>
        public int TotalTroopCount => _nobleTroops.Count + _professionalTroops.Count + _levyTroops.Count;
        public bool IsFilled => TotalTroopCount >= (FiefSquadTroopsMaxNum._nobleTroopsMaxNum +
                                                  FiefSquadTroopsMaxNum._professionalTroopsMaxNum +
                                                  FiefSquadTroopsMaxNum._levyTroopsMaxNum);

        public FiefSquad()
        {
            _nobleTroops = new List<CharacterObject>();
            _professionalTroops = new List<CharacterObject>();
            _levyTroops = new List<CharacterObject>();
        }

        public FiefSquad(List<CharacterObject> newNobleTroops, List<CharacterObject> newProfTroops, List<CharacterObject> newLevyTroops)
        {
            _nobleTroops = new List<CharacterObject>();
            _professionalTroops = new List<CharacterObject>();
            _levyTroops = new List<CharacterObject>();

            FillSquad(newNobleTroops, newProfTroops, newLevyTroops);
        }

        /// <summary>
        /// 填充小队。如果小队已满员，则直接返回。
        /// </summary>
        /// 
        private void FillTroopList(
                ref List<CharacterObject> currentList,
                List<CharacterObject> newTroops,
                int maxCount)
        {
            if (newTroops == null || currentList.Count >= maxCount)
                return;

            int remainingSlots = maxCount - currentList.Count;
            int takeCount = Math.Min(remainingSlots, newTroops.Count);

            var filledNames = new List<string>(); // 用于收集本次填充的兵种名

            for (int i = 0; i < takeCount; i++)
            {
                var troop = newTroops[i];
                if (troop != null && !troop.IsPlayerCharacter)
                {
                    currentList.Add(troop);
                    filledNames.Add(troop.Name.ToString()); // 收集名称（ToString() 确保安全）
                }
            }

            if (filledNames.Count > 0)
            {
                string logMessage = $"[Fief] 填充了 {filledNames.Count} troops: {string.Join(", ", filledNames)}";
                InformationManager.DisplayMessage(new InformationMessage(logMessage));
            }
        }

        public void FillSquad(List<CharacterObject> newNobleTroops, List<CharacterObject> newProfessionalTroops, List<CharacterObject> newLevyTroops)
        {
            if (IsFilled)
            {
                //InformationManager.DisplayMessage(new InformationMessage("Squad already filled, skipping FillSquad."));
                return;
            }

            FillTroopList(ref _nobleTroops, newNobleTroops, FiefSquadTroopsMaxNum._nobleTroopsMaxNum);
            FillTroopList(ref _professionalTroops, newProfessionalTroops, FiefSquadTroopsMaxNum._professionalTroopsMaxNum);
            FillTroopList(ref _levyTroops, newLevyTroops, FiefSquadTroopsMaxNum._levyTroopsMaxNum);
        }

        /// <summary>
        /// 清空小队并标记为已征召。
        /// </summary>
        public void Conscript()
        {
            if (_isConscripted) return;
            ClearSquad();
            _isConscripted = true;
        }

        internal void ResetConscription()
        {
            _isConscripted = false;
        }

        /// <summary>
        /// 清空小队。
        /// </summary>
        public void ClearSquad()
        {
            _nobleTroops.Clear();
            _professionalTroops.Clear();
            _levyTroops.Clear();
        }

        /// <summary>
        /// 获取当前小队的所有士兵（贵族兵、职业兵、征召兵）的列表。
        /// </summary>
        public List<CharacterObject> allTroops
        {
            get
            {
                var allTroops = new List<CharacterObject>();
                allTroops.AddRange(_nobleTroops);
                allTroops.AddRange(_professionalTroops);
                allTroops.AddRange(_levyTroops);
                return allTroops;
            }
        }

        /// <summary>
        /// 获取当前小队的所有士兵（去重计数），用于整体征召。
        /// </summary>
        public List<(CharacterObject troop, int count)> GetAllTroopsForRecruitment()
        {
            if (_isConscripted || TotalTroopCount == 0)
                return new List<(CharacterObject, int)>();

            var result = new List<(CharacterObject, int)>();
            AddGroup(_nobleTroops);
            AddGroup(_professionalTroops);
            AddGroup(_levyTroops);
            return result;

            void AddGroup(List<CharacterObject> list)
            {
                if (list?.Count > 0)
                {
                    foreach (var group in list.GroupBy(t => t))
                    {
                        result.Add((group.Key, group.Count()));
                    }
                }
            }
        }

    }


    /// <summary>
    /// 管理特定 Settlement 的采邑小队数据。
    /// </summary>
    public class FiefSettlementData
    {
        private Settlement _settlement;
        private TroopRoster _fiefSquadTroopRoster;


        [NonSerialized]
        private List<FiefSquad> _squads;
        [NonSerialized]
        List<CharacterWeightPair> volunteerCandidates;

        public int MaxSquadCount
        {
            get
            {
                if (_settlement == null) return 0;
                if (_settlement.IsVillage) return 3;
                if (_settlement.IsCastle) return 10;
                if (_settlement.IsTown) return 20;
                return 0; 
            }
        }

        public Settlement GetSettlement()
        {
            return _settlement;
        }

        public List<FiefSquad> GetFiefSquads()
        { 
            return _squads; 
        }

        // 无参构造（反序列化需要）
        public FiefSettlementData()
        {
            _fiefSquadTroopRoster = TroopRoster.CreateDummyTroopRoster();
        }

        public FiefSettlementData(Settlement settlement)
        {
            _settlement = settlement;
            _fiefSquadTroopRoster = TroopRoster.CreateDummyTroopRoster();
            volunteerCandidates = CultureVolunteerGroupsCache.Instance.GetVolunteerCandidateCache(settlement.Culture);

            _squads = new List<FiefSquad>();
            for (int i = 0; i < MaxSquadCount; i++)
            {
                _squads.Add(new FiefSquad());
            }
        }

        // 将所有 squad 转为 TroopRoster
        public TroopRoster GetAsTroopRoster()
        {
            _fiefSquadTroopRoster.Clear();
            foreach (var squad in _squads)
            {
                foreach (var troop in squad.allTroops)
                {
                    if (troop.IsPlayerCharacter) continue;
                    _fiefSquadTroopRoster.AddToCounts(troop, 1, false, 0, 0, true, -1);
                }
            }

            // === 调试：每次调用都打印 _fiefSquadTroopRoster 的内容 ===
            var debugLines = new List<string>();
            int totalCount = 0;

            foreach (var element in _fiefSquadTroopRoster.GetTroopRoster())
            {
                if (element.Character == null) continue;
                int num = element.Number + element.WoundedNumber;
                if (num <= 0) continue;

                totalCount += num;
                debugLines.Add($"{num}× {element.Character.Name}");
            }

            //string settlementName = _settlement?.Name.ToString() ?? "Unknown";
            //string msg = $"[ROSTER DEBUG] '{settlementName}' exported {totalCount} troops: {string.Join(", ", debugLines.Take(10))}";
            //if (debugLines.Count > 10) msg += ", ...";

            //InformationManager.DisplayMessage(new InformationMessage(msg, new Color(1f, 1f, 0f))); // Yellow

            return _fiefSquadTroopRoster;
        }

        /// <summary>
        /// 从 CharacterWeightPair 列表中根据权重随机选择一个 CharacterObject。
        /// </summary>
        /// <param name="candidates">候选列表。</param>
        /// <returns>选中的 CharacterObject，如果列表为空或选择失败则返回 null。</returns>
        private CharacterObject WeightedRandomSelectFromCharacterWeightPairs(List<CharacterWeightPair> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            // 计算总权重
            float totalWeight = candidates.Sum(c => c.Weight);
            if (totalWeight <= 0)
            {
                // 如果总权重 <= 0，返回列表中的第一个单位，或者 null
                return candidates[0].Character; // 或者 return null;
            }

            // 生成随机数
            float rand = MBRandom.RandomFloat * totalWeight;
            float sum = 0;

            // 遍历列表，根据权重选择
            foreach (var candidate in candidates)
            {
                sum += candidate.Weight;
                if (rand < sum)
                {
                    return candidate.Character;
                }
            }

            // 理论上不应该到达这里，但如果到达了，返回最后一个单位
            return candidates[candidates.Count - 1].Character;
        }

        public void WeeklyUpdate()
        {
            if (volunteerCandidates == null || volunteerCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No volunteer candidates found for {_settlement.Name} (Culture: {_settlement.Culture.Name})."));
                return;
            }

            // 从候选池中筛选出对应类型的单位
            // 注意：这里需要确保 TroopType 枚举在当前命名空间下是可访问的。
            // 如果编译报错找不到 TroopType，请检查 VolunteerPatches.cs 是否已编译且包含该枚举定义。
            var nobleCandidates = volunteerCandidates.Where(c => c.Type == TroopType.EliteBasic).ToList();
            var professionalCandidates = volunteerCandidates.Where(c => c.Type == TroopType.Professional).ToList();
            var levyCandidates = volunteerCandidates.Where(c => c.Type == TroopType.Basic).ToList();

            // 检查是否有对应类型的单位存在 (即使只有一种单位)
            if (nobleCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No noble candidates found for {_settlement.Name}."));
                return; // 没有贵族兵类型，无法组成小队
            }
            if (professionalCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No professional candidates found for {_settlement.Name}."));
                return; // 没有职业军类型，无法组成小队
            }
            if (levyCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No levy candidates found for {_settlement.Name}."));
                return; // 没有征召兵类型，无法组成小队
            }

            // 随机选择单位 (使用加权随机选择)
            // 贵族兵 (随机选一个，从 nobleCandidates 中)
            var newNobleTroops = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadTroopsMaxNum._nobleTroopsMaxNum; i++)
            {
                var noble = WeightedRandomSelectFromCharacterWeightPairs(nobleCandidates);
                if (noble == null)
                    return;
                newNobleTroops.Add(noble);
            }

            // 职业军 (随机选三个，从 professionalCandidates 中，允许重复选择)
            var newProfessionalTroops = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadTroopsMaxNum._professionalTroopsMaxNum; i++)
            {
                var pro = WeightedRandomSelectFromCharacterWeightPairs(professionalCandidates);
                if (pro == null) 
                    return; // 如果选择失败
                newProfessionalTroops.Add(pro);
            }

            // 征召兵 (随机选六个，从 levyCandidates 中，允许重复选择)
            var newLevyTroops = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadTroopsMaxNum._levyTroopsMaxNum; i++)
            {
                var levy = WeightedRandomSelectFromCharacterWeightPairs(levyCandidates);
                if (levy == null) 
                    return; // 如果选择失败
                newLevyTroops.Add(levy);
            }

            foreach (var squad in _squads)
            {
                if (!squad.IsFilled)
                {
                    squad.FillSquad(newNobleTroops, newProfessionalTroops, newLevyTroops);
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                    $"[DEBUG] {(_settlement?.Name)} 共 {_squads.Count} 个小队"));
        }

        // === 防刷屏日志统计 ===
        private static int _fillCount = 0;
        private static int _fillTotalTroops = 0;

        // 从 TroopRoster 填充 squads
        public void FillFromTroopRoster(TroopRoster savedRoster)
        {
            if (savedRoster == null || savedRoster.Count == 0)
            {
                return;
            }

            var nobleList = new List<CharacterObject>();
            var professionalList = new List<CharacterObject>();
            var levyList = new List<CharacterObject>();

            foreach (var element in savedRoster.GetTroopRoster())
            {
                if (element.Character == null) continue;
                int totalCount = element.Number + element.WoundedNumber;
                if (totalCount <= 0) continue;

                var troop = element.Character;
                var type = SoldierTypeClassifier.GetSoldierType(troop);

                for (int i = 0; i < totalCount; i++)
                {
                    switch (type)
                    {
                        case SoldierType.Noble:
                            nobleList.Add(troop);
                            break;
                        case SoldierType.Professional:
                            professionalList.Add(troop);
                            break;
                        case SoldierType.Levy:
                            levyList.Add(troop);
                            break;
                    }
                }
            }

            // 2. 填充 squads，逐个“消耗”列表
            foreach (var squad in _squads)
            {
                if (squad.IsFilled) continue;

                // 贵族兵：最多 1 个
                var nobleToAdd = new List<CharacterObject>();
                if (nobleList.Count > 0 && nobleToAdd.Count < FiefSquadTroopsMaxNum._nobleTroopsMaxNum)
                {
                    nobleToAdd.Add(nobleList[0]);
                    nobleList.RemoveAt(0);
                }

                // 职业兵：最多 3 个
                var profToAdd = new List<CharacterObject>();
                while (profToAdd.Count < FiefSquadTroopsMaxNum._professionalTroopsMaxNum && professionalList.Count > 0)
                {
                    profToAdd.Add(professionalList[0]);
                    professionalList.RemoveAt(0);
                }

                // 征召兵：最多 6 个
                var levyToAdd = new List<CharacterObject>();
                while (levyToAdd.Count < FiefSquadTroopsMaxNum._levyTroopsMaxNum && levyList.Count > 0)
                {
                    levyToAdd.Add(levyList[0]);
                    levyList.RemoveAt(0);
                }

                // 如果没有任何兵可填，说明后续 squads 也无法填充，提前退出
                if (nobleToAdd.Count == 0 && profToAdd.Count == 0 && levyToAdd.Count == 0)
                {
                    break;
                }

                // 填充当前 squad
                squad.FillSquad(nobleToAdd, profToAdd, levyToAdd);

                // 可选：如果所有列表已空，也可提前退出
                if (nobleList.Count == 0 && professionalList.Count == 0 && levyList.Count == 0)
                {
                    break;
                }
            }

            // 更新统计（可选）
            _fillCount++;
            _fillTotalTroops += savedRoster.Count;
        }

        // === 日志控制 ===
        public static void LogFillSummaryAndReset()
        {
            if (_fillCount > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief Squad Mod] FillFromTroopRoster summary: filled {_fillCount} settlements with {_fillTotalTroops} troops.",
                    new Color(0.8f, 0.9f, 0.2f)
                ));
            }

            // 重置状态，为下次初始化准备
            _fillCount = 0;
            _fillTotalTroops = 0;
        }

        /// <summary>
        /// 征召所有可用的采邑小队到指定 MobileParty。
        /// 每次征召一个完整的小队（不拆散），若容量不足则停止后续征召。
        /// </summary>
        public void RecruitAvailableSquads(MobileParty targetParty)
        {
            if (targetParty == null || _settlement == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[Fief] 征召失败：目标部队或领地为空。", new Color(1f, 0.3f, 0.3f)));
                return;
            }

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int recruitedSquads = 0;
            int totalRecruited = 0;

            foreach (var squad in _squads)
            {
                if (squad.IsConscripted || squad.TotalTroopCount == 0)
                    continue;

                int squadSize = squad.TotalTroopCount;

                // ✅ 参考官方 RecruitmentCampaignBehavior 的判断逻辑
                if (currentMembers >= partySizeLimit)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Fief] 部队已满（{currentMembers}/{partySizeLimit}），停止征召。",
                        new Color(1f, 0.7f, 0.2f)));
                    break;
                }

                if (currentMembers + squadSize > partySizeLimit)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Fief] 剩余容量 {partySizeLimit - currentMembers} 不足容纳小队（需 {squadSize} 人），停止征召。",
                        new Color(1f, 0.7f, 0.2f)));
                    break;
                }

                // 执行征召
                var troopsToRecruit = squad.GetAllTroopsForRecruitment();
                foreach (var (troop, count) in troopsToRecruit)
                {
                    if (troop != null && count > 0)
                    {
                        targetParty.AddElementToMemberRoster(troop, count, false);
                    }
                }

                squad.Conscript(); // 清空 + 标记
                currentMembers += squadSize;
                recruitedSquads++;
                totalRecruited += squadSize;

                //// 日志：列出征召的兵种
                //var names = troopsToRecruit.Select(t => $"{t.count}×{t.troop.Name}");
                //InformationManager.DisplayMessage(new InformationMessage(
                //    $"[Fief] 成功征召 1 个小队（{squadSize}人）: {string.Join(", ", names)}",
                //    new Color(0.3f, 1f, 0.5f)));
            }

            if (recruitedSquads > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 共征召 {recruitedSquads} 个小队，总计 {totalRecruited} 名士兵到 {targetParty.Name}。",
                    new Color(0.2f, 0.9f, 0.8f)));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Fief] 无可征召的小队（可能已全部征召、未填充或容量不足）。",
                    new Color(0.8f, 0.8f, 0.8f)));
            }
        }

        // ====== 【改动点 3】新增：从 MobileParty 安全移除兵员 ======
        private void RemoveTroopsFromParty(MobileParty party, CharacterObject troop, int countToRemove)
        {
            if (party == null || troop == null || countToRemove <= 0) return;

            var roster = party.MemberRoster;
            int currentCount = roster.GetElementNumber(troop);
            if (currentCount <= 0) return;

            int actualRemove = Math.Min(countToRemove, currentCount);
            if (actualRemove > 0)
            {
                // 使用官方推荐方式移除
                roster.RemoveTroop(troop, actualRemove, default(UniqueTroopDescriptor), 0);
            }
        }

        // ====== 【改动点 4】新增：解散并尝试回收士兵 ======
        /// <summary>
        /// 解散采邑军队：重置所有 squad 征召状态，并尝试从 MobileParty 回收士兵填充未满 squad。
        /// </summary>
        public void DisbandAndRefillFromParty(MobileParty sourceParty)
        {
            foreach (var squad in _squads)
            {
                squad.ResetConscription();
            }

            if (sourceParty == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 已重置 {_settlement.Name} 所有采邑小队状态（无部队可供回收）。",
                    new Color(0.7f, 0.7f, 0.9f)));
                return;
            }

            // 收集可回收兵员
            var recoverableNobles = new List<CharacterObject>();
            var recoverableProfessionals = new List<CharacterObject>();
            var recoverableLevies = new List<CharacterObject>();

            foreach (var element in sourceParty.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.Occupation != Occupation.Soldier) 
                    continue;

                int totalCount = element.Number + element.WoundedNumber;
                if (totalCount <= 0) continue;

                var troop = element.Character;
                var type = SoldierTypeClassifier.GetSoldierType(troop);

                for (int i = 0; i < totalCount; i++)
                {
                    switch (type)
                    {
                        case SoldierType.Noble:
                            recoverableNobles.Add(troop);
                            break;
                        case SoldierType.Professional:
                            recoverableProfessionals.Add(troop);
                            break;
                        case SoldierType.Levy:
                            recoverableLevies.Add(troop);
                            break;
                        default:
                            // 可选：只在首次遇到该 troop 时提示，避免刷屏
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[Fief 调试] 发现未分类的 Soldier 兵种：{troop.Name}（ID: {troop.StringId}），类型判定为：{type}。该单位将不会被安置到采邑小队。",
                                new Color(1f, 0.7f, 0.2f) // 橙黄色，表示警告但非错误
                            ));
                            break;
                    }
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"[Fief DEBUG] 可回收兵员 - 贵族: {recoverableNobles.Count}, 职业: {recoverableProfessionals.Count}, 征召: {recoverableLevies.Count}",
                new Color(0.8f, 0.9f, 0.4f)));


            // 计算总容量
            int totalNobleCapacity = 0, totalProfCapacity = 0, totalLevyCapacity = 0;
            foreach (var squad in _squads)
            {
                totalNobleCapacity += FiefSquadTroopsMaxNum._nobleTroopsMaxNum - squad.NobleCount;
                totalProfCapacity += FiefSquadTroopsMaxNum._professionalTroopsMaxNum - squad.ProfessionalCount;
                totalLevyCapacity += FiefSquadTroopsMaxNum._levyTroopsMaxNum - squad.LevyCount;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"[Fief DEBUG] 采邑总空位 - 贵族: {totalNobleCapacity}, 职业: {totalProfCapacity}, 征召: {totalLevyCapacity}",
                new Color(0.8f, 0.9f, 0.4f)));


            // 【关键】备份原始列表（用于差值统计）
            var originalNobles = new List<CharacterObject>(recoverableNobles);
            var originalPros = new List<CharacterObject>(recoverableProfessionals);
            var originalLevies = new List<CharacterObject>(recoverableLevies);

            foreach (var squad in _squads)
            {
                if (squad.IsFilled) continue;

                int nobleSlots = FiefSquadTroopsMaxNum._nobleTroopsMaxNum - squad.NobleCount;
                int profSlots = FiefSquadTroopsMaxNum._professionalTroopsMaxNum - squad.ProfessionalCount;
                int levySlots = FiefSquadTroopsMaxNum._levyTroopsMaxNum - squad.LevyCount;

                var nobleToAdd = new List<CharacterObject>();
                var profToAdd = new List<CharacterObject>();
                var levyToAdd = new List<CharacterObject>();

                // 补充贵族兵
                for (int i = 0; i < nobleSlots && recoverableNobles.Count > 0; i++)
                {
                    nobleToAdd.Add(recoverableNobles[0]);
                    recoverableNobles.RemoveAt(0);
                }

                // 补充职业兵
                for (int i = 0; i < profSlots && recoverableProfessionals.Count > 0; i++)
                {
                    profToAdd.Add(recoverableProfessionals[0]);
                    recoverableProfessionals.RemoveAt(0);
                }

                // 补充征召兵
                for (int i = 0; i < levySlots && recoverableLevies.Count > 0; i++)
                {
                    levyToAdd.Add(recoverableLevies[0]);
                    recoverableLevies.RemoveAt(0);
                }

                // 【必须保留】真正把兵加到 squad 中！
                if (nobleToAdd.Count > 0 || profToAdd.Count > 0 || levyToAdd.Count > 0)
                {
                    squad.FillSquad(nobleToAdd, profToAdd, levyToAdd);
                    // 注意：不再标记为 _isConscripted = true（因为这是正式驻军）
                }
            }

            // 【关键】通过原始 vs 剩余列表计算实际取走的兵
            var troopsToRemove = new Dictionary<CharacterObject, int>();
            CountRemovedTroops(originalNobles, recoverableNobles, troopsToRemove);
            CountRemovedTroops(originalPros, recoverableProfessionals, troopsToRemove);
            CountRemovedTroops(originalLevies, recoverableLevies, troopsToRemove);

            // 从 sourceParty 中移除这些兵
            foreach (var kvp in troopsToRemove)
            {
                RemoveTroopsFromParty(sourceParty, kvp.Key, kvp.Value);
            }

            int totalAdded = troopsToRemove.Values.Sum();
            if (totalAdded > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 已从 {sourceParty.Name} 补充 {totalAdded} 名士兵到 {_settlement.Name} 的采邑小队。",
                    new Color(0.8f, 0.9f, 0.4f)));
            }
        }

        private static void CountRemovedTroops(
            List<CharacterObject> original,
            List<CharacterObject> current,
            Dictionary<CharacterObject, int> result)
        {
            int removedCount = original.Count - current.Count;
            for (int i = 0; i < removedCount; i++)
            {
                var troop = original[i];
                if (result.ContainsKey(troop))
                    result[troop]++;
                else
                    result[troop] = 1;
            }
        }


        /// <summary>
        /// 获取当前已填充的小队数量。
        /// </summary>
        public int CurrentFilledSquadCount => _squads.Count(s => s.IsFilled);

        /// <summary>
        /// 获取当前未填充的小队数量。
        /// </summary>
        public int CurrentEmptySquadCount => _squads.Count(s => !s.IsFilled);
    }

    /// <summary>
    /// 负责管理所有 Settlement 的采邑小队数据的 CampaignBehavior。
    /// </summary>
    public class FiefSquadManager : CampaignBehaviorBase
    {
        // 存储所有 Settlement 与对应 FiefSettlementData 的映射
        private Dictionary<Settlement, FiefSettlementData> _fiefDataMap = new Dictionary<Settlement, FiefSettlementData>();
        private List<Settlement> _savedSettlements = new();
        private List<TroopRoster> _savedRosters = new();

        public override void RegisterEvents()
        {
            // 注册每周事件
            //CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _savedSettlements.Clear();
                _savedRosters.Clear();

                foreach (var kvp in _fiefDataMap)
                {
                    var settlement = kvp.Key;
                    var data = kvp.Value;

                    if (settlement.OwnerClan != Clan.PlayerClan)
                        continue;

                    // 只保存有效数据（非空 roster）
                    var roster = data.GetAsTroopRoster();
                    if (roster.Count > 0)
                    {
                        _savedSettlements.Add(settlement);
                        _savedRosters.Add(roster);
                    }
                }

                int total = _savedRosters.Sum(r => r?.Count ?? 0);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief Squad Mod] Saving {_savedSettlements.Count} settlements with {total} unique troop types.",
                    new Color(0.2f, 0.8f, 1.0f)
                ));
            }

            // 同步字段（加载或保存）
            dataStore.SyncData("_savedSettlements", ref _savedSettlements);
            dataStore.SyncData("_savedRosters", ref _savedRosters);

            if (!dataStore.IsSaving)
            {
                // 加载后日志（可选保留）
                int total = _savedRosters.Sum(r => r?.Count ?? 0);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief Squad Mod] Loaded raw save data: {_savedSettlements?.Count ?? 0} settlements, {total} unique troop types.",
                    new Color(0.2f, 1.0f, 0.6f)
                ));
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SoldierTypeClassifier.InitializeAll();
            InitializeFiefData();
        }

        public void InitializeFiefData()
        {
            _fiefDataMap.Clear();

            // 初始化所有有效领地
            foreach (var settlement in Settlement.All)
            {
                if (settlement.OwnerClan != Clan.PlayerClan)
                    continue;
                if (settlement.IsVillage || settlement.IsCastle || settlement.IsTown)
                {
                    _fiefDataMap[settlement] = new FiefSettlementData(settlement);
                }
            }

            // 用存档数据填充
            if (_savedSettlements != null && _savedRosters != null && _savedSettlements.Count == _savedRosters.Count)
            {
                for (int i = 0; i < _savedSettlements.Count; i++)
                {
                    var settlement = _savedSettlements[i];
                    var roster = _savedRosters[i];

                    if (settlement != null && roster != null && _fiefDataMap.TryGetValue(settlement, out var data))
                    {
                        data.FillFromTroopRoster(roster); 
                    }
                }
            }

            // 3. 打印最终状态
            FiefSettlementData.LogFillSummaryAndReset();

            int total = _fiefDataMap.Values.Sum(d => d.GetAsTroopRoster().Count);
            InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief Squad Mod] Loaded {_fiefDataMap.Count} fiefs containing {total} unique troop types.",
                    new Color(0.2f, 0.9f, 0.2f)
                ));
        }

        //private void OnWeeklyTick()
        private void OnDailyTick()
        {
            foreach (var data in _fiefDataMap.Values)
            {
                data?.WeeklyUpdate();
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Fief Squad Mod] Weekly squad reinforcement completed.",
                new Color(0.4f, 0.85f, 0.4f)
            ));
        }

        /// <summary>
        /// 获取指定 Settlement 的 FiefSettlementData。
        /// </summary>
        /// <param name="settlement">目标 Settlement。</param>
        /// <returns>对应的 FiefSettlementData，如果不存在则返回 null。</returns>
        public FiefSettlementData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        /// <summary>
        /// 获取所有定居点的采邑数据。
        /// </summary>
        /// <returns>包含所有 FiefSettlementData 的字典。</returns>
        public Dictionary<Settlement, FiefSettlementData> GetAllFiefData()
        {
            return _fiefDataMap;
        }

        /// <summary>
        /// 获取指定定居点的所有采邑士兵（展开为单个 CharacterObject 列表）。
        /// </summary>
        /// <param name="settlement">目标定居点</param>
        /// <returns>包含所有采邑士兵的列表；若无数据则返回空列表</returns>
        public List<CharacterObject> GetAllFiefTroopsForSettlement(Settlement settlement)
        {
            if (settlement == null)
                return new List<CharacterObject>();

            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData))
                return new List<CharacterObject>();

            var allTroops = new List<CharacterObject>();
            foreach (var squad in fiefData.GetFiefSquads())
            {
                if (squad.IsFilled) 
                {
                    allTroops.AddRange(squad.allTroops);
                }
            }

            return allTroops;
        }


        /// <summary>
        /// 获取所有定居点中所有FiefSquad列表。
        /// </summary>
        /// <returns>一个包含多个列表的列表，每个内层列表代表一个小队的所有士兵。</returns>
        /// 
        public List<List<CharacterObject>> GetAllFiefSquads(Settlement settlement)
        {
            if (settlement == null)
                return new List<List<CharacterObject>>();

            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData))
                return new List<List<CharacterObject>>();

            var allFiefSquads = new List<List<CharacterObject>>();

            foreach (var squad in fiefData.GetFiefSquads())
            {
                if (squad.IsFilled) 
                {
                    allFiefSquads.Add(squad.allTroops);
                }
            }

            return allFiefSquads;
        }

        public TroopRoster GetFiefTroopRoster(Settlement settlement)
        {
            if (settlement == null)
                return TroopRoster.CreateDummyTroopRoster();

            if (_fiefDataMap.TryGetValue(settlement, out FiefSettlementData data) && data != null)
            {
                return data.GetAsTroopRoster();
            }

            return TroopRoster.CreateDummyTroopRoster();
        }

        // <summary>
        /// 从指定定居点征召所有可用的采邑小队到目标部队。
        /// </summary>
        public void RecruitFiefSquadsFromSettlement(Settlement settlement, MobileParty targetParty)
        {
            if (settlement == null || targetParty == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[Fief] 征召参数无效。", new Color(1f, 0.3f, 0.3f)));
                return;
            }

            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 开始从 {settlement.Name} 征召采邑军队...",
                    new Color(0.4f, 0.8f, 1f)));
                data.RecruitAvailableSquads(targetParty);
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 未找到 {settlement.Name} 的采邑数据（可能非玩家领地）。",
                    new Color(1f, 0.7f, 0.3f)));
            }
        }

        // ====== 【改动点 5】新增统一解散接口 ======
        /// <summary>
        /// 解散指定定居点的采邑军队。
        /// - 总是重置所有 squads 的征召状态（允许 WeeklyUpdate 重新填充）
        /// - 如果提供了 mobileParty，则尝试从中回收士兵填充未满 squad，并从 roster 中移除
        /// </summary>
        public void DisbandFiefSquadsFromSettlement(Settlement settlement, MobileParty mobileParty = null)
        {
            if (settlement == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[Fief] 解散失败：定居点为空。", new Color(1f, 0.3f, 0.3f)));
                return;
            }

            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 开始解散 {settlement.Name} 的采邑军队...",
                    new Color(0.9f, 0.8f, 0.3f)));

                data.DisbandAndRefillFromParty(mobileParty); // mobileParty 可为 null
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief] 未找到 {settlement.Name} 的采邑数据（可能非玩家领地）。",
                    new Color(1f, 0.7f, 0.3f)));
            }
        }
    }
}
