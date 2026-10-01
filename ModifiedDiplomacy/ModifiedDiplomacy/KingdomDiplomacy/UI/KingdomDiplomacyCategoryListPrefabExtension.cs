using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.KingdomDiplomacy.UI
{
    /// <summary>
    /// Extends the native diplomacy list with direct subject-relation groups.
    /// All five categories continue to use Bannerlord's single left scrollbar.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::NavigatableListPanel[@Id='WarsList']")]
    public sealed class KingdomDiplomacyCategoryListPrefabExtension
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomDiplomacyCategoryList";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomDiplomacy",
                "UI",
                "KingdomDiplomacyCategoryList.xml");
            XmlDocument document = new XmlDocument();

            document.Load(xmlPath);
            return document;
        }
    }
}
