using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.Models.WarDisposition
{
    /// <summary>
    /// 注册战争倾向存档类型和容器。
    /// </summary>
    public sealed class WarDispositionSaveDefiner : SaveableTypeDefiner
    {
        public WarDispositionSaveDefiner()
            : base(2026091701)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(WarDispositionData), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(
                typeof(Dictionary<Clan, WarDispositionData>));
        }
    }
}
