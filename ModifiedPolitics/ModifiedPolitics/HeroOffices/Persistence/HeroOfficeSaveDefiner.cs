using System.Collections.Generic;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Persistence
{
    /// <summary>
    /// Registers office save data with a module-unique save identifier.
    /// </summary>
    public sealed class HeroOfficeSaveDefiner : SaveableTypeDefiner
    {
        public HeroOfficeSaveDefiner()
            : base(2026100701)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(OfficeAssignment), 1);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(OfficeType), 4);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<OfficeAssignment>));
        }
    }
}
