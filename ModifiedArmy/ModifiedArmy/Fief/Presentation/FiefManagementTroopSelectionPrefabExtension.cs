using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedArmy.Models.Fief.Presentation
{
    [PrefabExtension("GameMenuTroopSelection",
        "descendant::Widget[@Sprite='SPGeneral\\GameMenu\\troop_selection_panel']")]
    public sealed class HideNativeFiefTroopPanelPatch
        : PrefabExtensionSetAttributePatch
    {
        public override string Id => "ModifiedArmy_HideNativeFiefTroopPanel";
        public override string Attribute => "IsVisible";
        public override string Value => "@IsNotFiefManagement";
    }

    [PrefabExtension("GameMenuTroopSelection",
        "descendant::Window/Widget/Children/ListPanel/Children")]
    public sealed class FiefTroopPanelPatch : PrefabExtensionInsertPatch
    {
        public override string Id => "ModifiedArmy_FiefTroopPanel";
        public override int Position => 2;

        public override XmlDocument GetPrefabExtension()
        {
            string directory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            var document = new XmlDocument();
            document.Load(Path.Combine(directory, "Fief", "Presentation",
                "FiefManagementTroopSelection.xml"));
            return document;
        }
    }
}
