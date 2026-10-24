using System.Collections.Generic;
using ModifiedFamily.Proposals;
using TaleWorlds.SaveSystem;

namespace ModifiedFamily.Persistence
{
    public sealed class FamilySaveDefiner : SaveableTypeDefiner
    {
        public FamilySaveDefiner() : base(2026102401) { }
        protected override void DefineClassTypes() { AddClassDefinition(typeof(FamilyProposal), 1); AddClassDefinition(typeof(FamilyCooldownRecord), 2); AddClassDefinition(typeof(AdoptiveKinshipRecord), 3); }
        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(FamilyProposalType), 4, null); AddEnumDefinition(typeof(FamilyProposalStatus), 5, null);
            AddEnumDefinition(typeof(MarriageDirection), 6, null); AddEnumDefinition(typeof(FamilyExecutionStep), 7, null); AddEnumDefinition(typeof(FamilyReasonCode), 8, null);
        }
        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<FamilyProposal>)); ConstructContainerDefinition(typeof(List<FamilyCooldownRecord>)); ConstructContainerDefinition(typeof(List<AdoptiveKinshipRecord>));
        }
    }
}
