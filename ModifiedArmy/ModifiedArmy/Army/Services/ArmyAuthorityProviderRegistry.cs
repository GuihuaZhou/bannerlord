using System;
using ModifiedArmy.Army.Contracts;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.Army.Services
{
    public static class ArmyAuthorityProviderRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IArmyAuthorityProvider _provider;
        private static bool _failureReported;

        public static void Register(IArmyAuthorityProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (SyncRoot)
            {
                _provider = provider;
                _failureReported = false;
            }
            ModLogger.FileInfo("[ModifiedArmy][Army] phase=AUTHORITY_PROVIDER_REGISTERED provider=" +
                provider.GetType().FullName + ".");
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _provider = null;
                _failureReported = false;
            }
        }

        public static bool HasAppointedMarshal(Kingdom kingdom) =>
            Invoke(provider => provider.HasAppointedMarshal(kingdom));

        public static bool IsAppointedMarshal(Hero hero, Kingdom kingdom) =>
            Invoke(provider => provider.IsAppointedMarshal(hero, kingdom));

        public static bool IsEligibleMarshal(Hero hero, Kingdom kingdom) =>
            Invoke(provider => provider.IsEligibleMarshal(hero, kingdom));

        public static bool IsArmyAuthority(Hero hero, Kingdom kingdom) =>
            hero != null && kingdom != null &&
            (hero == kingdom.Leader || IsEligibleMarshal(hero, kingdom));

        public static string ProviderTypeName
        {
            get
            {
                lock (SyncRoot) return _provider?.GetType().FullName ?? "none";
            }
        }

        private static bool Invoke(Func<IArmyAuthorityProvider, bool> query)
        {
            IArmyAuthorityProvider provider;
            lock (SyncRoot) provider = _provider;
            if (provider == null) return false;
            try
            {
                return query(provider);
            }
            catch (Exception exception)
            {
                lock (SyncRoot)
                {
                    if (!_failureReported)
                    {
                        _failureReported = true;
                        ModLogger.FileWarn(
                            "[ModifiedArmy][Army] phase=AUTHORITY_PROVIDER_FAILED " +
                            "reasonCode=ProviderException provider=" +
                            provider.GetType().FullName + " exception=" +
                            exception.GetType().FullName + ".");
                    }
                }
                return false;
            }
        }
    }
}
