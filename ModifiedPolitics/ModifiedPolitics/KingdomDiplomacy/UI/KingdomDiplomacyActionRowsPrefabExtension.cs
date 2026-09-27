using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.KingdomDiplomacy.UI
{
    /// <summary>
    /// Replaces the complete diplomacy detail region with a scrollable page.
    /// Native and subject actions are rendered together at the bottom of the
    /// same page instead of occupying a fixed overlay above other details.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::ListPanel[@IsVisible='@IsAcceptableItemSelected' and @StackLayout.LayoutMethod='VerticalTopToBottom' and @HeightSizePolicy='StretchToParent' and @MarginLeft='15']")]
    public sealed class KingdomDiplomacyScrollableDetailsPrefabExtension
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomDiplomacyScrollableDetails";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(
                assemblyDirectory,
                "KingdomDiplomacy",
                "UI",
                "KingdomDiplomacyScrollableDetails.xml");
            XmlDocument document = new XmlDocument();

            document.Load(xmlPath);
            return document;
        }
    }

    /// <summary>
    /// Hides Bannerlord's original fixed action overlay. Its native action
    /// collection is preserved inside the new scrollable detail page.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::Widget[Children/ListPanel[@DataSource='{Actions}']]")]
    public sealed class KingdomDiplomacyNativeActionOverlayPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_HideNativeDiplomacyActionOverlay";

        public override string Attribute => "IsVisible";

        public override string Value => "false";
    }
}
