using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ModifiedArmy.UI.KingdomClan
{
    /// <summary>
    /// 在 Kingdom -> Clans 页面：
    ///
    /// Members
    /// [成员列表]
    /// [战争潜力]
    ///
    /// 的形式插入战争潜力模块。
    /// </summary>
    [PrefabExtension(
        "ClansPanel",
        "descendant::ListPanel[@Id='MembersList']")]
    public class ClanWarPotentialPrefabExtension
        : PrefabExtensionInsertPatch
    {
        /// <summary>
        /// 关键：
        /// 必须是 Child，而不是 Append。
        ///
        /// Child = 插入 MembersList 内部
        /// Append = 插到 MembersList 后面，与 FiefsList 并列
        /// </summary>
        public override InsertType Type => InsertType.Child;

        /// <summary>
        /// MembersList 原本有两个子节点：
        ///
        /// 0 = Members 标题
        /// 1 = Members Grid
        ///
        /// 所以插入位置 2，也就是成员 Grid 后面。
        /// </summary>
        public override int Index => 2;

        [PrefabExtensionText]
        public string GetPrefabExtension()
        {
            return @"
<ListPanel
    Id=""WarPotentialPanel""
    DataSource=""{CurrentSelectedClan}""
    WidthSizePolicy=""StretchToParent""
    HeightSizePolicy=""CoverChildren""
    StackLayout.LayoutMethod=""VerticalTopToBottom""
    MarginLeft=""30""
    MarginRight=""30""
    MarginTop=""5"">

    <Children>

        <!-- ====================================================== -->
        <!-- 标题 -->
        <!-- ====================================================== -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            UpdateChildrenStates=""true""
            DoNotPassEventsToChildren=""true"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    Text=""战争潜力""
                    Brush=""Kingdom.TitleMedium.Text""
                    Brush.FontSize=""32""
                    MarginLeft=""5""
                    VerticalAlignment=""Center""
                    ClipContents=""false"" />

                <Widget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""Fixed""
                    SuggestedHeight=""2""
                    MarginLeft=""10""
                    VerticalAlignment=""Bottom""
                    MarginBottom=""10""
                    Sprite=""GradientDivider_9""
                    AlphaFactor=""0.4"" />

            </Children>

        </ListPanel>

        <!-- ====================================================== -->
        <!-- 战争潜力主值 -->
        <!-- ====================================================== -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight""
            MarginTop=""2"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""战争潜力""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    Brush.FontSize=""22""
                    VerticalAlignment=""Center""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@WarPotential""
                    Brush=""Kingdom.TitleMedium.Text""
                    Brush.FontSize=""26""
                    VerticalAlignment=""Center""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- ====================================================== -->
        <!-- 军事 -->
        <!-- ====================================================== -->

        <TextWidget
            WidthSizePolicy=""CoverChildren""
            HeightSizePolicy=""CoverChildren""
            Text=""军事资源""
            Brush=""Kingdom.ParagraphSmall.Text""
            Brush.FontSize=""20""
            MarginTop=""6""
            ClipContents=""false"" />

        <!-- 封建 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""封建部队""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@FiefTroops""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- 驻军 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""驻军""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@GarrisonTroops""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- 野战 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""野战部队""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@FieldTroops""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- ====================================================== -->
        <!-- 财政 -->
        <!-- ====================================================== -->

        <TextWidget
            WidthSizePolicy=""CoverChildren""
            HeightSizePolicy=""CoverChildren""
            Text=""财政资源""
            Brush=""Kingdom.ParagraphSmall.Text""
            Brush.FontSize=""20""
            MarginTop=""6""
            ClipContents=""false"" />

        <!-- 财富 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""财富""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@ClanWealth""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- 每日收入 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""每日收入""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@DailyIncome""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

        <!-- Party工资 -->

        <ListPanel
            WidthSizePolicy=""StretchToParent""
            HeightSizePolicy=""CoverChildren""
            StackLayout.LayoutMethod=""HorizontalLeftToRight"">

            <Children>

                <TextWidget
                    WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""CoverChildren""
                    Text=""Party工资""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

                <TextWidget
                    WidthSizePolicy=""CoverChildren""
                    HeightSizePolicy=""CoverChildren""
                    IntText=""@PartyDailyWage""
                    Brush=""Kingdom.ParagraphSmall.Text""
                    ClipContents=""false"" />

            </Children>

        </ListPanel>

    </Children>

</ListPanel>";
        }
    }
}