using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedPolitics.KingdomFief.UI
{
    /// <summary>
    /// Moves the complete fief detail group to the left so the finance panel
    /// has enough room without separating it visually from the native fields.
    /// </summary>
    [PrefabExtension(
        "FiefsPanel",
        "descendant::ButtonWidget[@Id='OwnerButton']")]
    public class KingdomFiefOwnerMarginPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedPolitics_KingdomFiefOwnerMargin";

        public override string Attribute => "MarginLeft";

        // The native value is 250. This shifts Owner and every following
        // horizontal sibling 150 pixels to the left.
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
}
