// Integrates the tax-policy column into the native town-management top row.
// UIExtenderEx Prefabs2 patches are used because the runtime is UIExtenderEx
// 3.x; native governor, statistics, reserve, and popup widgets retain ownership.
using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using PatchAttribute = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace ModifiedPolitics.SettlementEconomy.Presentation
{
    /// <summary>Inserts the fifth tax-policy column before reserve funds.</summary>
    [PrefabExtension("TownManagement", "descendant::ListPanel[@Id='TopHalfListPanel']/Children")]
    public sealed class TownManagementTaxPolicyInsertPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => 2;

        [PrefabExtensionXmlNode(false)]
        public XmlNode Content
        {
            get => TaxPolicyPrefabResource.Load("TownManagementTaxPolicy.xml");
        }
    }

    /// <summary>Compresses the governor column while retaining its portrait control.</summary>
    [PrefabExtension("TownManagement", "descendant::Widget[@Id='GovernorPanel']")]
    public sealed class TownManagementGovernorWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("SuggestedWidth", "320") };
    }

    /// <summary>Fits the governor title divider inside the compressed column.</summary>
    [PrefabExtension("TownManagement", "descendant::Widget[@Id='GovernorPanel']/descendant::Widget[@Sprite='SPGeneral\\TownManagement\\title_divider']")]
    public sealed class TownManagementGovernorDividerWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("SuggestedWidth", "260") };
    }

    /// <summary>Routes controller navigation through the new policy selector.</summary>
    [PrefabExtension("TownManagement", "descendant::NavigationScopeTargeter[@ScopeID='GovernorPanelScope']")]
    public sealed class TownManagementGovernorNavigationPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("RightNavigationScope", "TaxPolicyScope") };
    }

    /// <summary>Compresses reserve funds enough to make room for the fifth column.</summary>
    [PrefabExtension("TownManagement", "descendant::Widget[@Id='ReservePanel']")]
    public sealed class TownManagementReserveWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("SuggestedWidth", "300") };
    }

    /// <summary>Fits the reserve title divider inside the compressed column.</summary>
    [PrefabExtension("TownManagement", "descendant::Widget[@Id='ReservePanel']/descendant::Widget[@Sprite='SPGeneral\\TownManagement\\title_divider']")]
    public sealed class TownManagementReserveDividerWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("SuggestedWidth", "260") };
    }

    /// <summary>Wraps the reserve bonus text within its reduced column width.</summary>
    [PrefabExtension("TownManagement", "descendant::Widget[@Id='ReservePanel']/descendant::TextWidget[@Text='@ReserveBonusText']")]
    public sealed class TownManagementReserveBonusWidthPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("SuggestedWidth", "270") };
    }

    /// <summary>Connects reserve controller navigation back to the policy selector.</summary>
    [PrefabExtension("TownManagement", "descendant::NavigationScopeTargeter[@ScopeID='ReserveScope']")]
    public sealed class TownManagementReserveNavigationPatch : PrefabExtensionSetAttributePatch
    {
        public override List<PatchAttribute> Attributes =>
            new List<PatchAttribute> { new PatchAttribute("LeftNavigationScope", "TaxPolicyScope") };
    }
}
