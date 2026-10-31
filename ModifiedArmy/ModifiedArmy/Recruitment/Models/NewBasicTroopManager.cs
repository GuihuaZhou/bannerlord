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

        /// <summary>招募此兵种所需的最低兵营等级，0 表示无要求。</summary>
        public int RequiredBarracksLevel { get; }

        public BasicTroopEntry(
            CharacterObject troop,
            int weight,
            int requiredBarracksLevel = 0)
        {
            Troop = troop;
            Weight = weight;
            RequiredBarracksLevel = Math.Max(0, requiredBarracksLevel);
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
        public List<BasicTroopEntry> Troops { get; } = new();

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

            // 基础兵配置只保存生产元数据，不承担身份或俘虏分类。
            foreach (XmlNode child in node.SelectNodes("troop"))
            {
                ParseTroopNode(objectManager, child);
            }

            // Culture.Name may still be empty while object XML is being
            // deserialized because localization has not finished initializing.
            // Successful per-culture messages also duplicate manager registration
            // logs, so only malformed cultures or troops are logged above.
            BasicTroopGroupManager.Instance.RegisterGroup(this);
        }

        /// <summary>
        /// 读取一个平铺基础兵种及其统一兵营等级。
        /// </summary>
        private void ParseTroopNode(MBObjectManager objectManager, XmlNode node)
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
            Troops.Add(new BasicTroopEntry(troop, 1, barracksLevel));
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

            // StringId is stable even during early loading, while the localized
            // display name may not be initialized yet.
            ModLogger.Debug($"[BasicTroopGroupManager] No troop configuration " +
                $"was found for culture '{culture.StringId}'.");
            return null;
        }


        public static void Clear()
        {
            int count = _groups.Count;
            _groups.Clear();
            ModLogger.Debug($"[BasicTroopGroupManager] Cleared cache ({count} entries)");
        }

        /// <summary>
        /// 按具体兵种查找基础配置。
        /// </summary>
        public static BasicTroopEntry FindBasicTroop(CultureObject culture, CharacterObject troop)
        {
            BasicTroopGroup group = GetGroupForCulture(culture);
            if (group == null || troop == null) return null;
            return group.Troops.FirstOrDefault(entry => entry?.Troop == troop);
        }
    }


}
