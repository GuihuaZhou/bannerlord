// Stable persisted enums describing proposal, execution, and rejection states.
namespace ModifiedFamily.Proposals
{
    public enum FamilyProposalType { Marriage, Adoption }
    public enum FamilyProposalStatus { PendingResponse, AcceptedPendingExecution, Executing, SupersededByCounter, Rejected, Withdrawn, TimedOut, Invalidated, Completed, ExecutionFailure, PaymentReconcileRequired }
    public enum MarriageDirection { FemaleJoinsMaleClan, MaleJoinsFemaleClan }
    public enum FamilyExecutionStep { NotStarted, MarriageApplied, GoldSettled, Completed }
    public enum FamilyReasonCode { None, InvalidProposal, InvalidClan, InvalidHero, Dead, AlreadyMarried, UnderAge, SameSex, Related, AdoptiveRelated, ClanChanged, AtWar, CultureForbidden, LeaderCannotLeave, HeroLocked, PairCooldown, CounterLimitReached, WrongResponder, WrongProposer, InvalidGift, InsufficientFunds, TemporaryInactive, InBattle, InArmy, Prisoner, Fugitive, Expired, ExternalMarriage, ExecutionVerificationFailed, PaymentUncertain }
}
