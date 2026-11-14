// Owns the modal manpower movie, input focus, and host-screen boundary. The
// movie receives one read-only snapshot and never participates in save data.
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace ModifiedArmy.Recruitment.Presentation
{
    internal static class ManpowerOverviewScreenService
    {
        private static ScreenBase _hostScreen;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movie;
        private static ManpowerOverviewVM _dataSource;

        /// <summary>
        /// Creates at most one modal overview. Any partial acquisition is
        /// released through the same idempotent close path on failure.
        /// </summary>
        internal static bool Open(Settlement settlement)
        {
            if (_layer != null || ScreenManager.TopScreen == null ||
                settlement?.IsFortification != true) return false;
            try
            {
                _hostScreen = ScreenManager.TopScreen;
                _dataSource = new ManpowerOverviewVM(settlement, Close);
                _layer = new GauntletLayer(
                    "ModifiedArmyManpowerOverviewLayer", 300, false)
                {
                    IsFocusLayer = true
                };
                _layer.InputRestrictions.SetInputRestrictions(
                    true, InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(
                    HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie(
                    "ModifiedArmyManpowerOverview", _dataSource);
                if (_movie == null)
                    throw new InvalidOperationException(
                        "Manpower overview XML was not registered in ModifiedArmy/GUI/Prefabs.");
                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                ModLogger.FileError(
                    "[Manpower overview] Failed to open popup. " + exception);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject(
                        "{=ModifiedArmy_Manpower_OpenFailed}" +
                        "The manpower overview could not be opened. Check the module installation and log.").ToString(),
                    Colors.Red));
                Close();
                return false;
            }
        }

        /// <summary>Closes through the native focused-layer Exit hotkey.</summary>
        internal static void Tick(float deltaTime)
        {
            if (_layer?.Input?.IsHotKeyReleased("Exit") == true) Close();
        }

        /// <summary>
        /// Clears static ownership first, then releases focus, movie, layer,
        /// event subscription, and VM safely after failures or screen changes.
        /// </summary>
        internal static void Close()
        {
            ScreenBase host = _hostScreen;
            GauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            ManpowerOverviewVM dataSource = _dataSource;
            _hostScreen = null;
            _layer = null;
            _movie = null;
            _dataSource = null;
            ScreenManager.OnPopScreen -= OnScreenPopped;
            if (layer != null)
            {
                layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(layer);
                if (movie != null) layer.ReleaseMovie(movie);
                host?.RemoveLayer(layer);
            }
            dataSource?.OnFinalize();
        }

        private static void OnScreenPopped(ScreenBase screen)
        {
            if (screen == _hostScreen) Close();
        }
    }
}
