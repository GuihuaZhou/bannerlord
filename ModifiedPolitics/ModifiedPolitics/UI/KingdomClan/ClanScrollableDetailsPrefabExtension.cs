using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ModifiedPolitics.UI.KingdomClan
{
    [PrefabExtension(
        "ClansPanel",
        "descendant::ListPanel[@IsVisible='@IsAcceptableItemSelected' and @StackLayout.LayoutMethod='VerticalTopToBottom' and Children/MaskedTextureWidget[@Brush='Kingdom.TornBanner.Big']]")]
    public class ClanScrollableDetailsPrefabExtension
        : PrefabExtensionReplacePatch
    {
        public override string Id =>
            "ModifiedPolitics_ClanScrollableDetails";

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory =
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);

            string xmlPath =
                Path.Combine(
                    assemblyDirectory,
                    "UI",
                    "KingdomClan",
                    "ClanScrollableDetails.xml");

            XmlDocument document = new XmlDocument();

            document.Load(xmlPath);

            return document;
        }
    }
}
