using System;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.PartyManagement.Models
{
    /// <summary>Limits initial-roster suppression to one synchronous lord creation.</summary>
    public static class PartyCreationScope
    {
        [ThreadStatic] private static string _leaderId;

        public static bool Matches(Hero leader) => leader?.StringId != null &&
            leader.StringId == _leaderId;

        public static IDisposable Begin(Hero leader)
        {
            if (leader?.StringId == null || _leaderId != null)
                throw new InvalidOperationException("PartyCreationScopeAlreadyActive");
            _leaderId = leader.StringId;
            return new Scope(leader.StringId);
        }

        private sealed class Scope : IDisposable
        {
            private readonly string _expected;
            private bool _disposed;

            public Scope(string expected) => _expected = expected;

            public void Dispose()
            {
                if (_disposed) return;
                if (_leaderId == _expected) _leaderId = null;
                _disposed = true;
            }
        }
    }
}
