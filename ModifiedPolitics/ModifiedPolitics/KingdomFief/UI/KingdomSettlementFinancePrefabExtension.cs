using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.KingdomFief.UI
{
    /// <summary>
    /// Adds only the Clan Fiefs finance list beside the existing kingdom
    /// settlement statistics.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::NavigatableListPanel[@Id='StatsList']")]
    public class KingdomSettlementFinancePrefabExtension
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomSettlementFinance";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomFief",
                "UI",
                "KingdomSettlementFinance.xml");
            XmlDocument document = new XmlDocument();

            document.Load(xmlPath);
            return document;
        }
    }
}
