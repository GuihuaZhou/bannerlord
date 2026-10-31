using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Recruitment.Preferences.Models
{
    public sealed class SettlementProfessionalPreferenceRecord
    {
        [SaveableProperty(1)] public string SettlementId { get; set; }
        [SaveableProperty(2)] public string OwnerClanId { get; set; }
        [SaveableProperty(3)] public string TemplateId { get; set; }
        [SaveableProperty(4)] public Dictionary<string, int> ActiveWeights { get; set; } =
            new Dictionary<string, int>();
    }
}
