using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedArmy.Recruitment.Pools.UI
{
    /// <summary>
    /// Removes the notable portrait from the reused recruitment tuple. Pool
    /// recruits belong to the settlement and have no individual Hero owner.
    /// </summary>
    [PrefabExtension(
        "RecruitVolunteerTuple",
        "descendant::GameMenuPartyItemButtonWidget[@Id='PartyItemWidget']/../..")]
    public sealed class HideRecruitmentPoolOwnerPortraitPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedArmy_HideRecruitmentPoolOwnerPortrait";

        public override string Attribute => "IsVisible";

        public override string Value => "false";
    }

    /// <summary>
    /// Removes the notable name paired with the hidden portrait.
    /// </summary>
    [PrefabExtension(
        "RecruitVolunteerTuple",
        "descendant::TextWidget[@DataSource='{Owner}']")]
    public sealed class HideRecruitmentPoolOwnerNamePatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedArmy_HideRecruitmentPoolOwnerName";

        public override string Attribute => "IsVisible";

        public override string Value => "false";
    }

    /// <summary>
    /// Uses the space formerly reserved for the notable portrait.
    /// </summary>
    [PrefabExtension(
        "RecruitVolunteerTuple",
        "descendant::NavigatableListPanel[@DataSource='{Troops}']")]
    public sealed class AlignRecruitmentPoolTroopsPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id =>
            "ModifiedArmy_AlignRecruitmentPoolTroops";

        public override string Attribute => "MarginLeft";

        public override string Value => "10";
    }
}
