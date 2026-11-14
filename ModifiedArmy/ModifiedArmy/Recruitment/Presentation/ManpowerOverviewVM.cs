// Owns the detached manpower overview snapshot and presentation commands only;
// closing and campaign resource ownership remain outside this view model.
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Presentation
{
    internal sealed class ManpowerOverviewVM : ViewModel
    {
        private readonly Action _close;

        internal ManpowerOverviewVM(Settlement settlement, Action close)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            _close = close ?? throw new ArgumentNullException(nameof(close));
            Title = Text("ModifiedArmy_Manpower_Title", "Manpower overview");
            DoneText = Text("ModifiedArmy_Manpower_Done", "Done");
            Sections = ManpowerOverviewDataProvider.CreateSections(settlement);
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string DoneText { get; }
        [DataSourceProperty] public MBBindingList<ManpowerSectionVM> Sections { get; }

        public void ExecuteClose() => _close();

        private static string Text(string id, string fallback) =>
            new TextObject("{=" + id + "}" + fallback).ToString();
    }
}
