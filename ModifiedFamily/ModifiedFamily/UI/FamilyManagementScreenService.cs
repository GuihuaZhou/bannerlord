// Hosts the independent family management prefab in a modal Gauntlet layer.
using System;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace ModifiedFamily.UI
{
    public static class FamilyManagementScreenService
    {
        private static ScreenBase _hostScreen;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movie;
        private static FamilyManagementVM _dataSource;

        public static bool Open()
        {
            if (_layer != null || ScreenManager.TopScreen == null)
            {
                return false;
            }

            try
            {
                _hostScreen = ScreenManager.TopScreen;
                _dataSource = new FamilyManagementVM();
                _layer = new GauntletLayer("ModifiedFamilyManagementLayer", 300, false) { IsFocusLayer = true };
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie("FamilyManagementScreen", _dataSource);
                if (_movie == null)
                {
                    throw new InvalidOperationException("FamilyManagementScreen was not registered in the module GUI folder.");
                }

                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Error(FamilyLogText.Simple("MF_Log_FamilyScreenFailed", "The family management screen could not be opened."), exception);
                Close();
                return false;
            }
        }

        public static void Close()
        {
            ScreenBase host = _hostScreen;
            GauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            FamilyManagementVM dataSource = _dataSource;
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
