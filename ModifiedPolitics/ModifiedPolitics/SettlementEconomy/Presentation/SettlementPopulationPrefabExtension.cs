using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedPolitics.SettlementEconomy.Presentation
{
    [PrefabExtension(
        "SettlementOverlay",
        "descendant::ListPanel[@Id='OverlayTooltipsList']/Children")]
    public sealed class SettlementPopulationPrefabExtension :
        PrefabExtensionInsertPatch
    {
        public override string Id =>
            "ModifiedPolitics_SettlementPopulationItem";

        // Native child 8 is Prosperity; insert immediately after it and
        // before Loyalty so population and supported prosperity stay adjacent.
        public override int Position => 9;

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            var document = new XmlDocument();
            document.Load(Path.Combine(
                assemblyDirectory,
                "SettlementEconomy",
                "Presentation",
                "SettlementPopulationItem.xml"));
            return document;
        }
    }
}
