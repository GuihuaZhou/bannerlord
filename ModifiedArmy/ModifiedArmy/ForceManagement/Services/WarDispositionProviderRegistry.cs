using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Holds the optional war-disposition integration supplied by an upstream
    /// module. Providers must not retain campaign-scoped game objects.
    /// </summary>
    public static class WarDispositionProviderRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWarDispositionProvider _provider;
        private static bool _failureReported;

        public static void Register(IWarDispositionProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (SyncRoot)
            {
                _provider = provider;
                _failureReported = false;
            }

            ModLogger.Info(
                "[Force Management] War-disposition provider registered. " +
                $"Provider={provider.GetType().FullName}");
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _provider = null;
                _failureReported = false;
            }
        }

        public static bool TryGet(
            Clan clan,
            out float disposition,
            out bool atFormalWar)
        {
            disposition = 0f;
            atFormalWar = false;

            IWarDispositionProvider provider;
            lock (SyncRoot)
            {
                provider = _provider;
            }

            if (provider == null)
                return false;

            try
            {
                return provider.TryGet(
                    clan,
                    out disposition,
                    out atFormalWar);
            }
            catch (Exception exception)
            {
                bool shouldReport;
                lock (SyncRoot)
                {
                    shouldReport = !_failureReported;
                    _failureReported = true;
                }

                if (shouldReport)
                {
                    ModLogger.Warn(
                        "[Force Management] War-disposition provider failed; " +
                        "neutral fallback is active. " +
                        "ReasonCode=ProviderException " +
                        $"Provider={provider.GetType().FullName} " +
                        $"Exception={exception}");
                }

                disposition = 0f;
                atFormalWar = false;
                return false;
            }
        }
    }
}
