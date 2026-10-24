// Wraps the native selected-member details in a vertical scroll panel without copying its contents.
using System.IO;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ModifiedFamily.UI.Clan
{
    [PrefabExtension("ClanMembers", "descendant::Widget[@VisualDefinition='RightPanel']/Children/ListPanel[@StackLayout.LayoutMethod='VerticalTopToBottom']")]
    public sealed class ClanMemberDetailsScrollablePatch : PrefabExtensionReplacePatch
    {
        public override string Id => "ModifiedFamily_ClanMemberDetailsScrollable";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string modulesDirectory = Directory.GetParent(Directory.GetParent(assemblyDirectory).FullName).Parent.FullName;
            string nativePath = Path.Combine(modulesDirectory, "SandBox", "GUI", "Prefabs", "Clan", "ClanMembers.xml");
            var nativeDocument = new XmlDocument();
            nativeDocument.Load(nativePath);
            XmlElement nativeContent = (XmlElement)nativeDocument.SelectSingleNode("//Widget[@VisualDefinition='RightPanel']/Children/ListPanel[@StackLayout.LayoutMethod='VerticalTopToBottom']");
            if (nativeContent == null) throw new InvalidDataException("The native clan member detail panel could not be found.");

            nativeContent.SetAttribute("Id", "ModifiedFamilyMemberDetailsContent");
            nativeContent.SetAttribute("HeightSizePolicy", "CoverChildren");
            nativeContent.SetAttribute("MarginRight", "35");

            var result = new XmlDocument();
            result.LoadXml("<Widget Id='ModifiedFamilyMemberDetailsScrollHost' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' MarginBottom='82'><Children><ScrollablePanel Id='ModifiedFamilyMemberDetailsScrollablePanel' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' InnerPanel='ModifiedFamilyMemberDetailsClipRect\\ModifiedFamilyMemberDetailsContent' ClipRect='ModifiedFamilyMemberDetailsClipRect' MouseScrollAxis='Vertical' VerticalScrollbar='..\\ModifiedFamilyMemberDetailsScrollbar\\Scrollbar' AutoHideScrollBars='true'><Children><Widget Id='ModifiedFamilyMemberDetailsClipRect' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' ClipContents='true'><Children /></Widget></Children></ScrollablePanel><Standard.VerticalScrollbar Id='ModifiedFamilyMemberDetailsScrollbar' HeightSizePolicy='StretchToParent' HorizontalAlignment='Right' VerticalAlignment='Bottom' MarginRight='4' /></Children></Widget>");
            XmlNode destination = result.SelectSingleNode("//Widget[@Id='ModifiedFamilyMemberDetailsClipRect']/Children");
            destination.AppendChild(result.ImportNode(nativeContent, true));
            return result;
        }
    }
}
