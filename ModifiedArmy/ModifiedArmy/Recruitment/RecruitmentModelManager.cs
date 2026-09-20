using ModifiedArmy.Recruitment.Models;

namespace ModifiedArmy.Recruitment
{
    /// <summary>
    /// Provides the single recruitment model instance used by all integrations.
    /// The first batch intentionally does not call this from live behaviors.
    /// </summary>
    public static class RecruitmentModelManager
    {
        static RecruitmentModelManager()
        {
            Templates = new RecruitmentTemplateRepository();
            Model = new DefaultAIRecruitmentModel(Templates);
        }

        public static RecruitmentTemplateRepository Templates { get; }

        public static AIRecruitmentModel Model { get; }
    }
}
