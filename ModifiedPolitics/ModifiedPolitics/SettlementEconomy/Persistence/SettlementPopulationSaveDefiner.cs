using System.Collections.Generic;
using ModifiedPolitics.SettlementEconomy.Behaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.SettlementEconomy.Persistence
{
    public sealed class SettlementPopulationSaveDefiner : SaveableTypeDefiner
    {
        public SettlementPopulationSaveDefiner() : base(2026110701) { }
        protected override void DefineClassTypes() { AddClassDefinition(typeof(PopulationTransactionRecord), 1); }
        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<Settlement, float>));
            ConstructContainerDefinition(typeof(Dictionary<string, PopulationTransactionRecord>));
            ConstructContainerDefinition(typeof(Dictionary<Settlement, bool>));
            ConstructContainerDefinition(typeof(Dictionary<Settlement, int>));
        }
    }
}
