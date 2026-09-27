using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.KingdomDiplomacy.UI
{
    /// <summary>
    /// Replaces Bannerlord's single diplomacy action row with a vertical
    /// container. Native actions remain on the first row, while subject
    /// diplomacy actions are rendered on a separate row below them.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::ListPanel[@DataSource='{Actions}']")]
    public sealed class KingdomDiplomacyActionRowsPrefabExtension
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomDiplomacyActionRows";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomDiplomacy",
                "UI",
                "KingdomDiplomacyActionRows.xml");
            XmlDocument document = new XmlDocument();

            document.Load(xmlPath);
            return document;
        }
    }
}
