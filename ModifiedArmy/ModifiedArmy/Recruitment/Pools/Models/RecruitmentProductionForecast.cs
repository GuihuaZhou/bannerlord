namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Read-only daily manpower allocation forecast shared by production and UI.
    /// Values may contain fractions because production progress is accumulated.
    /// </summary>
    public sealed class RecruitmentProductionForecast
    {
        public float SharedSupply { get; internal set; }
        public float ProfessionalRatio { get; internal set; }
        public int FiefDoctrineWeight { get; internal set; }
        public int ProfessionalDoctrineWeight { get; internal set; }
        public float ProfessionalTrainingBase { get; internal set; }
        public float ProfessionalTrainingStep { get; internal set; }
        public int BarracksLevel { get; internal set; }
        public int AllowedRecruitmentLevel { get; internal set; }
        public float CultureEfficiency { get; internal set; }
        public float RawProfessionalTrainingBeforeExternalEffects
        {
            get;
            internal set;
        }
        public float ExternalTrainingBonusRate { get; internal set; }
        public float ProfessionalTrainingCapacity { get; internal set; }
        public float ProfessionalAllocation { get; internal set; }
        public float FiefDemand { get; internal set; }
        public float FiefAllocation { get; internal set; }
        public int FiefCurrent { get; internal set; }
        public int FiefCapacity { get; internal set; }
        public float FiefBaseDailyRate { get; internal set; }
        public bool ProfessionalRecruitmentEnabled { get; internal set; }
    }
}
