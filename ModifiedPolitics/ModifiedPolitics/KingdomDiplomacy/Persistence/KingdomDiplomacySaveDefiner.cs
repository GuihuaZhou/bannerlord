using System.Collections.Generic;
using ModifiedPolitics.KingdomDiplomacy.Models;
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
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(SubjectType), 1, null);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<KingdomPoliticalData>));
            ConstructContainerDefinition(typeof(List<KingdomRelationData>));
            ConstructContainerDefinition(typeof(List<SubjectRelationData>));
        }
    }
}
