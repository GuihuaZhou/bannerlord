using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Core
{
    /// <summary>
    /// Common result returned by eligibility checks. UI, AI and execution can
    /// therefore display the same rejection reason instead of reimplementing
    /// the rule at each call site.
    /// </summary>
    public sealed class DiplomacyCheckResult
    {
        private DiplomacyCheckResult(bool isAllowed, TextObject reason)
        {
            IsAllowed = isAllowed;
            Reason = reason ?? TextObject.GetEmpty();
        }

        public bool IsAllowed { get; }

        public TextObject Reason { get; }

        public static DiplomacyCheckResult Allowed()
        {
            return new DiplomacyCheckResult(true, TextObject.GetEmpty());
        }

        public static DiplomacyCheckResult Rejected(TextObject reason)
        {
            return new DiplomacyCheckResult(false, reason);
        }
    }
}
