// Creation-entry guard. Initial mobilization is committed synchronously by the
// creation coordinator; ordinary settlement visits retain their native owner.
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Models
{
    internal static class PartyCreationInitializationScope
    {
        [ThreadStatic] private static HashSet<string> _parties;

        internal static IDisposable Begin(MobileParty party)
        {
            string id = party?.StringId;
            if (string.IsNullOrEmpty(id)) return EmptyScope.Instance;
            (_parties ??= new HashSet<string>(StringComparer.Ordinal)).Add(id);
            return new Scope(id);
        }

        internal static bool IsInitializing(MobileParty party) =>
            party?.StringId != null && _parties?.Contains(party.StringId) == true;

        private sealed class Scope : IDisposable
        {
            private readonly string _id;
            internal Scope(string id) => _id = id;
            public void Dispose() => _parties?.Remove(_id);
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();
            public void Dispose() { }
        }
    }
}
