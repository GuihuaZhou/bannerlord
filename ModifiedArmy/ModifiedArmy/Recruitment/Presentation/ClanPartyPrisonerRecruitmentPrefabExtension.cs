using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedArmy.Recruitment.Presentation
{
    [PrefabExtension(
        "ClanPartiesRightPanel",
        "descendant::ListPanel[@Id='PartiesRightPanelControls']/Children")]
    public sealed class ClanPartyPrisonerRecruitmentPrefabExtension
        : PrefabExtensionInsertPatch
    {
        public override string Id =>
            "ModifiedArmy_ClanPartyPrisonerRecruitment";

        public override int Position => 2;

        public override XmlDocument GetPrefabExtension()
        {
            string directory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            var document = new XmlDocument();
            document.Load(Path.Combine(
                directory,
                "Recruitment",
                "Presentation",
                "ClanPartyPrisonerRecruitment.xml"));
            return document;
        }
    }
}
