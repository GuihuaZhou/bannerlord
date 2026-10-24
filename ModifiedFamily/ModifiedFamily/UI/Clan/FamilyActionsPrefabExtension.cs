// Injects family action buttons into the selected member details data context.
using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedFamily.UI.Clan
{
    [PrefabExtension("ClanMembers", "descendant::Widget[@VisualDefinition='RightPanel']/Children")]
    public sealed class FamilyActionsPrefabExtension : PrefabExtensionInsertPatch
    {
        public override string Id => "ModifiedFamily_ClanMemberActions";
        public override int Position => 2;
        public override XmlDocument GetPrefabExtension()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UI", "Clan", "FamilyActionsButton.xml");
            var document = new XmlDocument(); document.Load(path); return document;
        }
    }
}
