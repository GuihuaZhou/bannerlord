using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedFamily.UI.Clan
{
    [PrefabExtension(
        "ClanMembers",
        "descendant::NavigatableListPanel[@Id='ClanElementsListPanel']/Children")]
    public sealed class ClanMembersMarriagePrefabExtension : PrefabExtensionInsertPatch
    {
        public override string Id => "ModifiedFamily_ClanMembersMarriageEntry";

        // Keep the marriage entry as the first actionable row in the members list.
        public override int Position => 0;

        public override XmlDocument GetPrefabExtension()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "UI",
                "Clan",
                "MarriageEntryButton.xml");
            var document = new XmlDocument();
            document.Load(path);
            return document;
        }
    }
}
