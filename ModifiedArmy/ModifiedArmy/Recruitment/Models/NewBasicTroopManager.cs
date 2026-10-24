using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    public class BasicTroopEntry
    {
        public CharacterObject Troop { get; }
        public int Weight { get; set; }
        public PrisonerPolicyClass PrisonerPolicy { get; }

        /// <summary>
        /// 招募此兵种所需的最低兵营等级。
        /// 0 表示无要求。
        /// </summary>
        public int RequiredBarracksLevel { get; }

        public BasicTroopEntry(CharacterObject troop, int weight, PrisonerPolicyClass prisonerPolicy, int requiredBarracksLevel = 0)
        {
            Troop = troop;
            Weight = weight;
            PrisonerPolicy = prisonerPolicy;
            RequiredBarracksLevel = requiredBarracksLevel;
        }

        /// <summary>
        /// 设置此兵种条目的权重。
        /// </summary>
        /// <param name="newWeight">新的权重值。必须大于0。</param>
        public void SetWeight(int newWeight)
        {
            if (newWeight <= 0)
            {
                Weight = 1;
            }
            else
            {
                Weight = newWeight;
            }
        }
    }

    public class BasicTroopGroup : MBObjectBase
    {
        public CultureObject Culture { get; private set; }
        public Dictionary<PrisonerPolicyClass, List<BasicTroopEntry>> TroopsByPolicy { get; } = new()
        {
            [PrisonerPolicyClass.Retinue] = new List<BasicTroopEntry>(),
            [PrisonerPolicyClass.Sergeant] = new List<BasicTroopEntry>(),
            [PrisonerPolicyClass.Slave] = new List<BasicTroopEntry>(),
            [PrisonerPolicyClass.Marine] = new List<BasicTroopEntry>(),
            [PrisonerPolicyClass.Militia] = new List<BasicTroopEntry>(),
            [PrisonerPolicyClass.Other] = new List<BasicTroopEntry>()
        };

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            string groupId = XmlHelper.ReadString(node, "id");
            string cultureId = XmlHelper.ReadString(node, "culture");

            //ModLogger.Debug($"[BasicTroopGroup] Starting deserialization: id='{groupId}' for {cultureId}");

            // 读取 culture="xxx"
            Culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("culture", node);
            if (Culture == null)
            {
                ModLogger.Warn($"[BasicTroopGroup] Failed to resolve culture='{cultureId}', skipping group (id='{groupId}')");
                return;
            }

            //ModLogger.Info($"[BasicTroopGroup] Successfully loaded culture: {Culture.StringId} (id='{groupId}')");

            foreach (XmlNode child in node.SelectNodes("troop"))
            {
                PrisonerPolicyClass policy = PrisonerPolicyClass.Other;
                Enum.TryParse(child.Attributes?["prisonerPolicy"]?.Value,
                    true, out policy);
                ParseTroopNode(objectManager, child, policy);
            }
            int total = TroopsByPolicy.Values.Sum(entries => entries.Count);

            ModLogger.Debug($"[BasicTroopGroup] Finished loading troop config for '{Culture.Name}', total entries: {total}");
            BasicTroopGroupManager.Instance.RegisterGroup(this);
        }

        /// <summary>
        /// 读取一个平铺基础兵种。prisonerPolicy 只服务于独立俘虏规则，
        /// 封邑容量、生产和征召均不会读取该值。
        /// </summary>
        private void ParseTroopNode(MBObjectManager objectManager, XmlNode node, PrisonerPolicyClass policy)
        {
            CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("id", node);
            int barracksLevel = node.Attributes?["barracksLevel"] == null
                ? 0
                : XmlHelper.ReadInt(node, "barracksLevel");
            if (troop == null)
            {
                ModLogger.Warn($"[BasicTroopGroup] Failed to load troop: id='{XmlHelper.ReadString(node, "id")}'");
                return;
            }
            TroopsByPolicy[policy].Add(new BasicTroopEntry(troop, 1, policy, barracksLevel));
        }
    }

    public class BasicTroopGroupManager
    {
        public static readonly BasicTroopGroupManager Instance = new();
        private static readonly Dictionary<CultureObject, BasicTroopGroup> _groups = new();

        public void RegisterGroup(BasicTroopGroup group)
        {
            if (group?.Culture != null)
            {
                _groups[group.Culture] = group;
                ModLogger.Debug($"[BasicTroopGroupManager] Registered culture group: {group.Culture.Name}");
            }
        }

        public static BasicTroopGroup GetGroupForCulture(CultureObject culture)
        {
            if (culture == null)
            {
                ModLogger.Warn("[BasicTroopGroupManager] GetGroupForCulture called with null culture");
                return null;
            }

            if (_groups.TryGetValue(culture, out var group))
            {
                //ModLogger.Debug($"[BasicTroopGroupManager] Cache hit: {culture.Name}");
                return group;
            }

            ModLogger.Debug($"[BasicTroopGroupManager] No troop configuration found for culture: {culture.Name}");
            return null;
        }


        public static void Clear()
        {
            int count = _groups.Count;
            _groups.Clear();
            ModLogger.Debug($"[BasicTroopGroupManager] Cleared cache ({count} entries)");
        }

        /// <summary>
        /// 按具体兵种查找基础配置。封邑和职业兵生产只关心兵营等级，
        /// 不应要求调用方先知道俘虏政策分组。
        /// </summary>
        public static BasicTroopEntry FindBasicTroop(CultureObject culture, CharacterObject troop)
        {
            BasicTroopGroup group = GetGroupForCulture(culture);
            if (group == null || troop == null) return null;
            return group.TroopsByPolicy.Values
                .SelectMany(entries => entries)
                .FirstOrDefault(entry => entry?.Troop == troop);
        }
    }


}
