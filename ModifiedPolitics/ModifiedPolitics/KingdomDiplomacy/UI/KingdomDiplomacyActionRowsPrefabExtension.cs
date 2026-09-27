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

    /// <summary>
    /// The strategy selector is copied into the scrollable overview. Hide the
    /// original fixed overlay so it does not remain on screen while scrolling.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::Widget[not(@Id) and @MarginLeft='55' and @MarginTop='125' and @IsVisible='@IsAcceptableItemSelected']")]
    public sealed class KingdomDiplomacyFixedStrategyOverlayPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_HideFixedDiplomacyStrategy";

        public override string Attribute => "IsVisible";

        public override string Value => "false";
    }

    /// <summary>
    /// Treaty and war banners are also copied into the scrollable overview.
    /// This removes Bannerlord's original fixed-position relation overlay.
    /// </summary>
    [PrefabExtension(
        "DiplomacyPanel",
        "descendant::ListPanel[not(@Id) and @DataSource='{CurrentSelectedDiplomacyItem}' and @SuggestedWidth='120' and @MarginRight='105' and @MarginTop='122']")]
    public sealed class KingdomDiplomacyFixedRelationsOverlayPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_HideFixedDiplomacyRelations";

        public override string Attribute => "IsVisible";

        public override string Value => "false";
    }
}
