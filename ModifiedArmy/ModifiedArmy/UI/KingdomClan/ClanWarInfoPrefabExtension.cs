using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ModifiedArmy.UI.KingdomClan
{
    /// <summary>
    /// 在原版 Kingdom -> Clan 页面右侧，
    /// Clan 大旗下面插入战争信息。
    /// </summary>
    [PrefabExtension(
        "ClansPanel",
        "descendant::MaskedTextureWidget[@Brush='Kingdom.TornBanner.Big']")]
    public class ClanWarInfoPrefabExtension
        : PrefabExtensionInsertPatch
    {
        /// <summary>
        /// 插在 Clan 大旗之后，作为同级 Widget。
        /// </summary>
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string GetPrefabExtension()
        {
            return
                "<ListPanel " +
                "DataSource=\"{CurrentSelectedClan}\" " +
                "WidthSizePolicy=\"StretchToParent\" " +
                "HeightSizePolicy=\"CoverChildren\" " +
                "StackLayout.LayoutMethod=\"VerticalTopToBottom\" " +
                "MarginTop=\"10\" " +
                "MarginBottom=\"15\">" +

                    "<Children>" +

                        "<TextWidget " +
                        "WidthSizePolicy=\"CoverChildren\" " +
                        "HeightSizePolicy=\"CoverChildren\" " +
                        "HorizontalAlignment=\"Center\" " +
                        "Brush=\"Kingdom.ParagraphSmall.Text\" " +
                        "Text=\"@MilitaryInfo\" " +
                        "ClipContents=\"false\" />" +

                        "<TextWidget " +
                        "WidthSizePolicy=\"CoverChildren\" " +
                        "HeightSizePolicy=\"CoverChildren\" " +
                        "HorizontalAlignment=\"Center\" " +
                        "Brush=\"Kingdom.ParagraphSmall.Text\" " +
                        "Text=\"@FinancialInfo\" " +
                        "ClipContents=\"false\" />" +

                    "</Children>" +

                "</ListPanel>";
        }
    }
}