using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedPolitics.HeroOffices.UI
{
    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='ClanTabButton']")]
    public sealed class KingdomOfficeClanTabWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedPolitics_KingdomClanTabWidth";
        public override string Attribute => "SuggestedWidth";
        public override string Value => "230";
    }

    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='FiefsTabButton']")]
    public sealed class KingdomOfficeFiefsTabWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedPolitics_KingdomFiefsTabWidth";
        public override string Attribute => "SuggestedWidth";
        public override string Value => "230";
    }

    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='PoliciesTabButton']")]
    public sealed class KingdomOfficePoliciesTabWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedPolitics_KingdomPoliciesTabWidth";
        public override string Attribute => "SuggestedWidth";
        public override string Value => "230";
    }

    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='ArmiesTabButton']")]
    public sealed class KingdomOfficeArmiesTabWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedPolitics_KingdomArmiesTabWidth";
        public override string Attribute => "SuggestedWidth";
        public override string Value => "230";
    }

    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='DiplomacyTabButton']")]
    public sealed class KingdomOfficeDiplomacyTabWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedPolitics_KingdomDiplomacyTabWidth";
        public override string Attribute => "SuggestedWidth";
        public override string Value => "230";
    }

    /// <summary>
    /// Appends the offices tab after the five native kingdom tabs.
    /// </summary>
    [PrefabExtension(
        "KingdomManagement",
        "descendant::KingdomTabControlListPanel/Children")]
    public sealed class KingdomOfficeTabPrefabExtension : PrefabExtensionInsertPatch
    {
        public override string Id => "ModifiedPolitics_KingdomOfficesTab";

        // UIExtender insert positions are one-based relative to the existing children.
        // Position 3 places Offices between Armies and Diplomacy in this container.
        public override int Position => 3;

        public override XmlDocument GetPrefabExtension()
        {
            return LoadUiDocument("KingdomOfficeTab.xml");
        }

        private static XmlDocument LoadUiDocument(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(assemblyDirectory, "HeroOffices", "UI", fileName);
            XmlDocument document = new XmlDocument();
            document.Load(xmlPath);
            return document;
        }
    }

    /// <summary>
    /// Adds a static offices content panel without reading office assignment data.
    /// </summary>
    [PrefabExtension(
        "KingdomManagement",
        "descendant::Window/Widget/Children")]
    public sealed class KingdomOfficePanelPrefabExtension : PrefabExtensionInsertPatch
    {
        public override string Id => "ModifiedPolitics_KingdomOfficesPanel";

        // Insert beside the five native content panels and before the bottom controls.
        public override int Position => 6;

        public override XmlDocument GetPrefabExtension()
        {
            return LoadUiDocument("KingdomOfficesPanel.xml");
        }

        private static XmlDocument LoadUiDocument(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string xmlPath = Path.Combine(assemblyDirectory, "HeroOffices", "UI", fileName);
            XmlDocument document = new XmlDocument();
            document.Load(xmlPath);
            return document;
        }
    }

}
