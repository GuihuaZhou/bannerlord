using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using System.Collections.Generic;

namespace ModifiedArmy.UI.KingdomClan
{
    /// <summary>
    /// 缩短 Members Grid 高度，
    /// 给下方战争潜力模块腾出空间。
    /// </summary>
    [PrefabExtension(
        "ClansPanel",
        "descendant::ListPanel[@Id='MembersList']/Children/ListPanel[2]")]
    public class ClanMembersHeightPrefabExtension
        : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes =>
            new List<Attribute>
            {
                new Attribute(
                    "SuggestedHeight",
                    "285")
            };
    }
}