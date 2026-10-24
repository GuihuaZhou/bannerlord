using System;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Execution
{
    public static class MarriageExecutionScope
    {
        [ThreadStatic] private static Scope _current;
        public static IDisposable Enter(FamilyProposal proposal)
        {
            if (_current != null) throw new InvalidOperationException("Nested marriage execution is not allowed.");
            _current = new Scope(proposal); return _current;
        }
        public static bool TryGetDestination(Hero first, Hero second, out Clan clan)
        {
            var c = _current;
            if (c != null && ((c.First == first && c.Second == second) || (c.First == second && c.Second == first))) { clan = c.Destination; return true; }
            clan = null; return false;
        }
        public static string CurrentProposalId => _current?.ProposalId;
        public static void Clear() { _current = null; }
        private sealed class Scope : IDisposable
        {
            internal readonly string ProposalId; internal readonly Hero First; internal readonly Hero Second; internal readonly Clan Destination;
            internal Scope(FamilyProposal p) { ProposalId = p.ProposalId; First = p.HeroA; Second = p.HeroB; Destination = p.DestinationClan; }
            public void Dispose() { if (ReferenceEquals(_current, this)) _current = null; }
        }
    }
}
