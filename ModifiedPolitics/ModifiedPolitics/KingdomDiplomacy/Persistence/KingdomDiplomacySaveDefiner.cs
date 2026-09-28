using System.Collections.Generic;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Decisions;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Persistence
{
    /// <summary>
    /// Registers the persistent records used by the kingdom diplomacy system.
    /// </summary>
    public sealed class KingdomDiplomacySaveDefiner : SaveableTypeDefiner
    {
        public KingdomDiplomacySaveDefiner()
            : base(2026092601)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(KingdomPoliticalData), 1);
            AddClassDefinition(typeof(KingdomRelationData), 2);
            AddClassDefinition(typeof(SubjectRelationData), 3);
            AddClassDefinition(typeof(SubjectProposalKingdomDecision), 5);
            AddClassDefinition(
                typeof(SubjectProposalKingdomDecision
                    .SubjectProposalDecisionOutcome),
                6);
            AddClassDefinition(typeof(SubjectReleaseKingdomDecision), 7);
            AddClassDefinition(
                typeof(SubjectReleaseKingdomDecision
                    .SubjectReleaseDecisionOutcome),
                8);
            AddClassDefinition(
                typeof(SubjectIndependenceKingdomDecision),
                9);
            AddClassDefinition(typeof(SubjectResponseKingdomDecision), 10);
            AddClassDefinition(
                typeof(SubjectResponseKingdomDecision
                    .SubjectResponseDecisionOutcome),
                11);
        }

        protected override void DefineEnumTypes()
        {
            // Class and enum definitions share the same local ID space.
            AddEnumDefinition(typeof(SubjectType), 4, null);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<KingdomPoliticalData>));
            ConstructContainerDefinition(typeof(List<KingdomRelationData>));
            ConstructContainerDefinition(typeof(List<SubjectRelationData>));
        }
    }
}
