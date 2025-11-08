using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ModifiedArmy.Patches
{
    // ================================
    // AI MobileParty 权限管理器（内存表）
    // ================================
    public static class MobilePartyPermissionManager
    {
        // 可配置：每个 Clan 最多授权的 Hero 数量（实际取值范围：1 ~ MAX_AUTHORIZED_HEROES_PER_CLAN）
        private const int MAX_AUTHORIZED_HEROES_PER_CLAN = 6;

        private static readonly Dictionary<Clan, List<Hero>> _allowedHeroes = new Dictionary<Clan, List<Hero>>();
        private static bool _debugEnabled = true;

        public static void RebuildPermissions()
        {
            _allowedHeroes.Clear();

            if (_debugEnabled)
                InformationManager.DisplayMessage(new InformationMessage("[ModifiedArmy] 正在重建部队创建权限...", new Color(0.7f, 0.7f, 1f)));

            foreach (Clan clan in Campaign.Current.Clans)
            {
                if (clan.IsMinorFaction)
                {
                    if (_debugEnabled)
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[ModifiedArmy] 跳过小派系（交由原版处理）: {clan.Name}",
                            new Color(0.8f, 0.8f, 0.8f)));
                    continue;
                }

                if (clan.IsEliminated || clan.Leader == null) continue;

                var candidates = new List<Hero>();
                foreach (Hero hero in clan.Heroes)
                {
                    if (hero.IsHumanPlayerCharacter || hero.IsChild || hero.IsDead || hero.IsDisabled)
                        continue;
                    if (hero.PartyBelongedToAsPrisoner != null)
                        continue;
                    if (hero.GovernorOf != null)
                        continue;
                    if (!hero.IsLord)
                        continue;

                    candidates.Add(hero);
                }

                if (candidates.Count == 0) continue;

                // 排序：Leader > 男性 > 女性，同组按 Tactics 降序
                candidates.Sort((a, b) =>
                {
                    bool aIsLeader = (a == clan.Leader);
                    bool bIsLeader = (b == clan.Leader);
                    if (aIsLeader != bIsLeader)
                        return aIsLeader ? -1 : 1;

                    bool aIsFemale = a.CharacterObject.IsFemale;
                    bool bIsFemale = b.CharacterObject.IsFemale;
                    if (aIsFemale != bIsFemale)
                        return aIsFemale ? 1 : -1;

                    return b.GetSkillValue(DefaultSkills.Tactics)
                           .CompareTo(a.GetSkillValue(DefaultSkills.Tactics));
                });

                // 使用常量：至少 1 人，最多 MAX_AUTHORIZED_HEROES_PER_CLAN
                int count = Math.Max(1, Math.Min(candidates.Count, MAX_AUTHORIZED_HEROES_PER_CLAN));
                var selected = candidates.GetRange(0, count);
                _allowedHeroes[clan] = selected;

                if (_debugEnabled)
                {
                    string names = "";
                    foreach (Hero h in selected)
                    {
                        string genderTag = h.CharacterObject.IsFemale ? "♀" : "♂";
                        names += $"{h.Name}{genderTag} (Tactics: {h.GetSkillValue(DefaultSkills.Tactics)}), ";
                    }
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[ModifiedArmy] Clan '{clan.Name}' 授权 ({count}/{candidates.Count}): {names.TrimEnd(',', ' ')}",
                        new Color(0.5f, 1f, 0.5f)
                    ));
                }
            }
        }

        public static bool IsAllowedToCreateParty(Hero hero)
        {
            // 安全兜底：任何异常 Hero 直接放行（避免崩溃）
            if (hero == null || hero.Clan == null || hero.CharacterObject == null)
            {
                if (_debugEnabled)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[ModifiedArmy] 跳过未初始化 Hero: {hero?.StringId}",
                        new Color(1f, 0.5f, 0.5f)
                    ));
                }
                return true; // 放行 → 原版逻辑
            }

            // WeeklyTick 未执行前，放行
            if (_allowedHeroes.Count == 0)
                return true;

            // 小派系放行
            if (hero.Clan.IsMinorFaction)
                return true;

            bool allowed = _allowedHeroes.TryGetValue(hero.Clan, out List<Hero> list) && list.Contains(hero);

            if (!allowed && _debugEnabled && !hero.IsHumanPlayerCharacter)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ModifiedArmy] 拦截: {hero.Name} ({hero.Clan.Name}) 无权限创建部队",
                    new Color(1f, 0.5f, 0.5f)
                ));
            }

            return allowed;
        }
    }

    [HarmonyPatch(typeof(LordPartyComponent), "CreateLordParty")]
    public static class Patch_LordPartyComponent_CreateLordParty
    {
        public static bool Prefix(
            string stringId,
            Hero hero,
            CampaignVec2 position,
            float spawnRadius,
            Settlement spawnSettlement,
            Hero partyLeader,
            ref MobileParty __result)
        {
            if (!MobilePartyPermissionManager.IsAllowedToCreateParty(hero))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
