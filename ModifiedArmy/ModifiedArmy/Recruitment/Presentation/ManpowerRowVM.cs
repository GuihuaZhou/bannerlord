// Presents one read-only manpower fact; campaign state remains owned by the
// population, production, and fief domains that supplied the snapshot.
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Presentation
{
    internal sealed class ManpowerRowVM : ViewModel
    {
        internal ManpowerRowVM(string labelText, string valueText,
            bool isWarning = false)
        {
            LabelText = labelText ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            IsWarning = isWarning;
        }

        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public string ValueText { get; }
        [DataSourceProperty] public bool IsWarning { get; }
    }
}
