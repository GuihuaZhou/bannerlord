using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.KingdomFief.UI
{
    /// <summary>
    /// Positions the governor card that replaces the native owner portrait.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::ButtonWidget[@Id='OwnerButton']")]
    public class KingdomFiefGovernorMarginPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomFiefGovernorMargin";

        public override string Attribute => "MarginLeft";

        // The native owner card used 250. The governor card keeps the compact layout.
        public override string Value => "100";
    }

    /// <summary>
    /// Reduces the gap between the owner and settlement sections while
    /// preserving the native ordering and navigation structure.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::ButtonWidget[@Id='SettlementButton']")]
    public class KingdomFiefSettlementMarginPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomFiefSettlementMargin";

        public override string Attribute => "MarginLeft";

        // The native value is 40. Keep a small visual separation.
        public override string Value => "20";
    }

    /// <summary>
    /// Replaces the native owner portrait with the selected settlement's governor card.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::ButtonWidget[@Id='OwnerButton']")]
    public sealed class KingdomFiefGovernorCardPatch
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomFiefGovernorCard";

        public override XmlDocument GetPrefabExtension()
        {
            return LoadUiDocument("KingdomGovernorCard.xml");
        }

        private static XmlDocument LoadUiDocument(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomFief",
                "UI",
                fileName);
            XmlDocument document = new XmlDocument();
            document.Load(xmlPath);
            return document;
        }
    }

    /// <summary>
    /// Adds the native large card-selection popup above the kingdom fief details.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::Window/Widget[@IsVisible='@Show']")]
    public sealed class KingdomFiefGovernorPopupPatch
        : PrefabExtensionInsertPatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomFiefGovernorPopup";

        // Insert after the main layout so the full-screen popup remains centered and on top.
        public override int Position => 1;

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomFief",
                "UI",
                "KingdomGovernorPopup.xml");
            XmlDocument document = new XmlDocument();
            document.Load(xmlPath);
            return document;
        }
    }
}
