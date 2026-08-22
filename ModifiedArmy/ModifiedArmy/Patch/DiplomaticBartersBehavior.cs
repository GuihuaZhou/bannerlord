using HarmonyLib;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Patch
{
    /// <summary>
    /// 补丁：在 DailyTickClan 开始时，强制所有独立的 AI 氏族与所有王国停战。
    /// 这能极大加速它们被其他王国吸收的过程。
    /// </summary>
    [HarmonyPatch(typeof(DiplomaticBartersBehavior), "DailyTickClan")]
    public static class ForceIndependentClansToPeacePatch
    {
        private static void ConsiderPeace(Clan clan1, Clan clan2)
        {
            PeaceBarterable peaceBarterable = new PeaceBarterable(clan1.Leader, clan1.MapFaction, clan2.MapFaction, CampaignTime.Years(1f));
            if (peaceBarterable.GetValueForFaction(clan1) + peaceBarterable.GetValueForFaction(clan2) > 0)
            {
                Campaign.Current.BarterManager.ExecuteAiBarter(clan1, clan2, clan1.Leader, clan2.Leader, peaceBarterable);
            }
        }

        private static void ConsiderWar(Clan clan, IFaction otherMapFaction)
        {
            DeclareWarBarterable declareWarBarterable = new DeclareWarBarterable(clan, otherMapFaction);
            if (declareWarBarterable.GetValueForFaction(clan) > 1000)
            {
                declareWarBarterable.Apply();
            }
        }

        private static void ConsiderClanLeaveKingdom(Clan clan)
        {
            LeaveKingdomAsClanBarterable leaveKingdomAsClanBarterable = new LeaveKingdomAsClanBarterable(clan.Leader, null);
            if (leaveKingdomAsClanBarterable.GetValueForFaction(clan) > 0)
            {
                leaveKingdomAsClanBarterable.Apply();
            }
        }

        private static void ConsiderClanLeaveAsMercenary(Clan clan)
        {
            LeaveKingdomAsClanBarterable leaveKingdomAsClanBarterable = new LeaveKingdomAsClanBarterable(clan.Leader, null);
            if (leaveKingdomAsClanBarterable.GetValueForFaction(clan) > 500)
            {
                leaveKingdomAsClanBarterable.Apply();
            }
        }

        private static void ConsiderDefection(Clan clan1, Kingdom kingdom)
        {
            JoinKingdomAsClanBarterable joinKingdomAsClanBarterable = new JoinKingdomAsClanBarterable(clan1.Leader, kingdom, true);
            int valueForFaction = joinKingdomAsClanBarterable.GetValueForFaction(clan1);
            int valueForFaction2 = joinKingdomAsClanBarterable.GetValueForFaction(kingdom);
            int num = valueForFaction + valueForFaction2;
            int num2 = 0;
            if (valueForFaction < 0)
            {
                num2 = -valueForFaction;
            }
            if (num > 0 && (float)num2 <= (float)kingdom.Leader.Gold * 0.5f)
            {
                Campaign.Current.BarterManager.ExecuteAiBarter(clan1, kingdom, clan1.Leader, kingdom.Leader, joinKingdomAsClanBarterable);
            }
        }

        private static void ConsiderClanJoinAsMercenary(Clan clan, Kingdom kingdom)
        {
            MercenaryJoinKingdomBarterable mercenaryJoinKingdomBarterable = new MercenaryJoinKingdomBarterable(clan.Leader, null, kingdom);
            if (mercenaryJoinKingdomBarterable.GetValueForFaction(clan) + mercenaryJoinKingdomBarterable.GetValueForFaction(kingdom) > 0)
            {
                Campaign.Current.BarterManager.ExecuteAiBarter(clan, kingdom, clan.Leader, kingdom.Leader, mercenaryJoinKingdomBarterable);
            }
        }

        private static bool ConsiderClanJoin(Clan clan, Kingdom kingdom)
        {
            JoinKingdomAsClanBarterable joinKingdomAsClanBarterable = new JoinKingdomAsClanBarterable(clan.Leader, kingdom, false);

            int result_1 = joinKingdomAsClanBarterable.GetValueForFaction(clan);
            int result_2 = joinKingdomAsClanBarterable.GetValueForFaction(kingdom);

            ModLogger.Info($"{clan.Name} consider to join {kingdom.Name}, result 1: {result_1}, result 2: {result_2}");

            if (result_1 + result_2 > 0)
            {
                Campaign.Current.BarterManager.ExecuteAiBarter(clan, kingdom, clan.Leader, kingdom.Leader, joinKingdomAsClanBarterable);
                return true;
            }

            return false;
        }


        /// <summary>
        /// Prefix 补丁，在原方法执行前运行。
        /// </summary>
        public static void Prefix(Clan clan)
        {
            bool flag = false;
            using (List<WarPartyComponent>.Enumerator enumerator = clan.WarPartyComponents.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.MobileParty.MapEvent != null)
                    {
                        flag = true;
                        break;
                    }
                }
            }
            MBList<Clan> e = Clan.NonBanditFactions.ToMBList<Clan>();
            if (clan == Clan.PlayerClan || clan.CurrentTotalStrength <= 0f || clan.IsEliminated)
            {
                return;
            }
            if (clan.IsBanditFaction || clan.IsRebelClan)
            {
                return;
            }

            if (clan.Kingdom == null && MBRandom.RandomFloat < 0.5f)
            {
                // 独立的clan有50%的概率进入如下逻辑
                if (MBRandom.RandomFloat < 0.5f)
                {
                    Clan randomElement = e.GetRandomElement<Clan>();
                    if (randomElement.Kingdom == null && randomElement != Clan.PlayerClan && clan.IsAtWarWith(randomElement) && !clan.IsMinorFaction && !randomElement.IsMinorFaction)
                    {
                        ConsiderPeace(clan, randomElement);
                        return;
                    }
                }
                else
                {
                    bool flag2 = true;
                    if (clan.Settlements.Count > 0 && MBRandom.RandomFloat < 0.5f)
                    {
                        flag2 = false;
                    }
                    if (flag2)
                    {
                        Kingdom randomElementWithPredicate = Kingdom.All.GetRandomElementWithPredicate((Kingdom x) => x.IsAtWarWith(clan) && !x.IsAtConstantWarWith(clan));
                        // 因kingdom毁灭而独立的clan，永远不与玩家所在的kingdom和平，这不合理
                        if (!clan.IsMinorFaction)
                        {
                            if (randomElementWithPredicate != null)
                            {
                                MakePeaceAction.Apply(clan, randomElementWithPredicate);
                                return;
                            }
                        }
                        if (randomElementWithPredicate != null && randomElementWithPredicate != Clan.PlayerClan.Kingdom)
                        {
                            int relation = clan.Leader.GetRelation(randomElementWithPredicate.Leader);
                            if (relation > -65 && MBMath.Map((float)relation, -100f, 100f, 0f, 1f) < MBRandom.RandomFloat)
                            {
                                MakePeaceAction.Apply(clan, randomElementWithPredicate);
                                return;
                            }
                        }
                    }
                }
            }
            else if (MBRandom.RandomFloat < 0.2f && !clan.IsUnderMercenaryService && clan.Kingdom != null && !clan.IsClanTypeMercenary)
            {
                if (MBRandom.RandomFloat < 0.1f)
                {
                    Clan randomElement2 = e.GetRandomElement<Clan>();
                    int num = 0;
                    while (randomElement2.Kingdom == null || clan.Kingdom == randomElement2.Kingdom || randomElement2.IsEliminated)
                    {
                        randomElement2 = e.GetRandomElement<Clan>();
                        num++;
                        if (num >= 20)
                        {
                            break;
                        }
                    }
                    if (randomElement2.Kingdom != null && clan.Kingdom != randomElement2.Kingdom && !Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(clan, randomElement2.Kingdom) && !flag && randomElement2.MapFaction.IsKingdomFaction && !randomElement2.IsEliminated && randomElement2 != Clan.PlayerClan && randomElement2.MapFaction.Leader != Hero.MainHero)
                    {
                        if (clan.WarPartyComponents.All((WarPartyComponent x) => x.MobileParty.MapEvent == null))
                        {
                            ConsiderDefection(clan, randomElement2.MapFaction as Kingdom);
                            return;
                        }
                    }
                }
            }
            //else if (MBRandom.RandomFloat < ((clan.MapFaction.Leader == Hero.MainHero) ? 0.2f : 0.4f))
            // 提高独立clan加入新kingdom的概率
            else if (MBRandom.RandomFloat < ((clan.MapFaction.Leader == Hero.MainHero) ? 0.2f : 0.8f))
            {
                // === 第一步：分组收集候选王国 ===
                List<Kingdom> sameCultureKingdoms = new List<Kingdom>();
                List<Kingdom> otherCultureKingdoms = new List<Kingdom>();

                foreach (Kingdom k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated)
                        continue;

                    if (k.Culture == clan.Culture)
                        sameCultureKingdoms.Add(k);
                    else
                        otherCultureKingdoms.Add(k);
                }

                // === 第二步：组内随机打乱 ===
                sameCultureKingdoms.Randomize();
                otherCultureKingdoms.Randomize();

                // === 第三步：合并为最终的尝试顺序：先同文化（随机），再异文化（随机）===
                List<Kingdom> allCandidates = new List<Kingdom>();
                allCandidates.AddRange(sameCultureKingdoms);
                allCandidates.AddRange(otherCultureKingdoms);

                // === 第二步：遍历所有候选王国，尝试加入 ===
                foreach (Kingdom candidateKingdom in allCandidates)
                {
                    // 执行原版的所有前置条件检查
                    if (candidateKingdom.Leader != Hero.MainHero &&
                        !candidateKingdom.IsEliminated &&
                        (clan.Kingdom == null || clan.IsUnderMercenaryService) &&
                        clan.MapFaction != candidateKingdom &&
                        !clan.MapFaction.IsAtWarWith(candidateKingdom) &&
                        !Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(clan, candidateKingdom) &&
                        clan.WarPartyComponents.All((WarPartyComponent x) => x.MobileParty.MapEvent == null) &&
                        clan.ShouldStayInKingdomUntil.IsPast)
                    {
                        // 执行简化的安全检查（仅检查是否有任何敌人）
                        bool hasAnyEnemy = false;
                        foreach (Kingdom enemyKingdom in Kingdom.All)
                        {
                            if (enemyKingdom != null &&
                                enemyKingdom != candidateKingdom &&
                                clan.IsAtWarWith(enemyKingdom))
                            {
                                hasAnyEnemy = true;
                                break;
                            }
                        }

                        if (hasAnyEnemy)
                        {
                            continue;
                        }

                        // 找到合格的王国，执行加入！
                        if (clan.IsMinorFaction)
                        {
                            ConsiderClanJoinAsMercenary(clan, candidateKingdom);
                            return; // 立即退出整个 DailyTickClan 方法
                        }
                        else
                        {
                            if (ConsiderClanJoin(clan, candidateKingdom))
                            {
                                return;
                            }
                        }
                    }
                }
            }
            else if (MBRandom.RandomFloat < 0.4f)
            {
                if (clan.Kingdom != null && !flag && clan.Kingdom.RulingClan != clan && clan != Clan.PlayerClan && clan.ShouldStayInKingdomUntil.IsPast)
                {
                    if (clan.WarPartyComponents.All((WarPartyComponent x) => x.MobileParty.MapEvent == null))
                    {
                        if (clan.IsMinorFaction)
                        {
                            ConsiderClanLeaveAsMercenary(clan);
                            return;
                        }
                        ConsiderClanLeaveKingdom(clan);
                        return;
                    }
                }
            }
            else if (MBRandom.RandomFloat < 0.7f)
            {
                Clan randomElement3 = e.GetRandomElement<Clan>();
                IFaction mapFaction = randomElement3.MapFaction;
                if (!clan.IsMinorFaction && (!mapFaction.IsMinorFaction || mapFaction == Clan.PlayerClan) && clan.Kingdom == null && randomElement3 != clan && !mapFaction.IsEliminated && mapFaction.WarPartyComponents.Count > 0 && clan.WarPartyComponents.Count > 0 && !clan.IsAtWarWith(mapFaction) && clan != Clan.PlayerClan)
                {
                    ConsiderWar(clan, mapFaction);
                }
            }
        }
    }
}
