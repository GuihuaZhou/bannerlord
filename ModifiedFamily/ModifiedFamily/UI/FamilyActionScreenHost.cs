// Owns the shared lifecycle for the independent marriage and adoption movies.
using System;
using ModifiedFamily.Diagnostics;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace ModifiedFamily.UI
{
    internal static class FamilyActionScreenHost
    {
        private static ScreenBase _hostScreen;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movie;
        private static ViewModel _dataSource;

        internal static bool Open(string movieName, string layerName, ViewModel dataSource)
        {
            if (_layer != null || ScreenManager.TopScreen == null || dataSource == null) return false;
            try
            {
                _hostScreen = ScreenManager.TopScreen;
                _dataSource = dataSource;
                _layer = new GauntletLayer(layerName, 300, false) { IsFocusLayer = true };
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie(movieName, _dataSource);
                if (_movie == null) throw new InvalidOperationException(movieName + " was not registered in ModifiedFamily/GUI/Prefabs.");
                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Error("家族事务独立界面打开失败。", exception);
                Close();
                return false;
            }
        }

        internal static void Close()
        {
            ScreenBase host = _hostScreen;
            GauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            ViewModel dataSource = _dataSource;
            _hostScreen = null; _layer = null; _movie = null; _dataSource = null;
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

        private static void OnScreenPopped(ScreenBase screen) { if (screen == _hostScreen) Close(); }
    }
}
