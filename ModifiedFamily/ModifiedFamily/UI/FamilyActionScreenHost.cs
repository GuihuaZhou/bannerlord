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
        private static SpriteCategory _spriteCategory;
        private static ViewModel _dataSource;

        internal static bool Open(
            string movieName,
            string layerName,
            ViewModel dataSource,
            string spriteCategoryName = null)
        {
            if (_layer != null || ScreenManager.TopScreen == null || dataSource == null) return false;
            try
            {
                _hostScreen = ScreenManager.TopScreen;
                _dataSource = dataSource;
                _layer = new GauntletLayer(layerName, 300, false) { IsFocusLayer = true };
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _spriteCategory = LoadOptionalSpriteCategory(spriteCategoryName);
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie(movieName, _dataSource);
                if (_movie == null) throw new InvalidOperationException(movieName + " was not registered in ModifiedFamily/GUI/Prefabs.");
                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Error("Failed to open the standalone family action screen.", exception);
                Close();
                return false;
            }
        }

        internal static void Close()
        {
            ScreenBase host = _hostScreen;
            GauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            SpriteCategory spriteCategory = _spriteCategory;
            ViewModel dataSource = _dataSource;
            _hostScreen = null; _layer = null; _movie = null;
            _spriteCategory = null; _dataSource = null;
            ScreenManager.OnPopScreen -= OnScreenPopped;
            if (layer != null)
            {
                layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(layer);
                if (movie != null) layer.ReleaseMovie(movie);
                host?.RemoveLayer(layer);
            }
            spriteCategory?.Unload();
            dataSource?.OnFinalize();
        }

        private static SpriteCategory LoadOptionalSpriteCategory(
            string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return null;

            SpriteCategory category = null;
            try
            {
                category = UIResourceManager.GetSpriteCategory(categoryName);
                if (category == null)
                {
                    FamilyDiagnostics.Warn(
                        $"Optional sprite category '{categoryName}' was not found. "
                        + "The family action screen will use its fallback visuals.");
                    return null;
                }

                category.Load();
                return category;
            }
            catch (Exception exception)
            {
                try { category?.Unload(); }
                catch (Exception unloadException)
                {
                    FamilyDiagnostics.Warn(
                        $"Optional sprite category '{categoryName}' cleanup failed: "
                        + unloadException.Message);
                }

                FamilyDiagnostics.Warn(
                    $"Optional sprite category '{categoryName}' could not be loaded. "
                    + "The family action screen will use its fallback visuals. "
                    + $"Error: {exception.Message}");
                return null;
            }
        }

        private static void OnScreenPopped(ScreenBase screen) { if (screen == _hostScreen) Close(); }
    }
}
