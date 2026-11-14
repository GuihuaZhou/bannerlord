// Owns the modal Gauntlet movie lifecycle, input focus, rollback, and host-screen
// boundary for the recruitment-weight presentation domain.
using ModifiedArmy.Recruitment.Preferences.Behaviors;
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

namespace ModifiedArmy.Recruitment.Preferences.Presentation
{
    internal static class RecruitmentWeightScreenService
    {
        private static ScreenBase _hostScreen;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movie;
        private static SpriteCategory _partySpriteCategory;
        private static RecruitmentWeightVM _dataSource;

        /// <summary>
        /// Creates one modal popup for the current top screen. Failure rolls
        /// back every partially acquired UI resource before returning false.
        /// </summary>
        internal static bool Open(Settlement settlement,
            SettlementProfessionalPreferenceBehavior behavior)
        {
            if (_layer != null || ScreenManager.TopScreen == null ||
                settlement?.OwnerClan == null || behavior == null) return false;
            try
            {
                _hostScreen = ScreenManager.TopScreen;
                _dataSource = new RecruitmentWeightVM(settlement, behavior, Close);
                _layer = new GauntletLayer("ModifiedArmyRecruitmentWeightLayer", 300, false)
                {
                    IsFocusLayer = true
                };
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(
                    HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                // The popup reuses the native Party/Member troop-card brush. Its
                // atlas is normally owned by PartyScreen, so a standalone movie
                // must acquire the category before the XML is instantiated.
                _partySpriteCategory = UIResourceManager.GetSpriteCategory("ui_partyscreen")
                    ?? throw new InvalidOperationException(
                        "Native sprite category 'ui_partyscreen' was not found.");
                _partySpriteCategory.Load();
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie("ModifiedArmyRecruitmentWeightPopup", _dataSource);
                if (_movie == null)
                    throw new InvalidOperationException("Recruitment-weight popup XML was not registered in ModifiedArmy/GUI/Prefabs.");
                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                ModLogger.FileError("[Recruitment preferences] Failed to open the recruitment-weight popup. " + exception);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ModifiedArmy_RecruitmentWeightOpenFailed}The recruitment-weight window could not be opened. Check the module installation and log.").ToString(),
                    Colors.Red));
                Close();
                return false;
            }
        }

        /// <summary>Handles the registered native Exit hotkey while the layer owns focus.</summary>
        internal static void Tick(float deltaTime)
        {
            if (_layer?.Input?.IsHotKeyReleased("Exit") == true) Close();
        }

        /// <summary>
        /// Idempotently releases focus, movie, layer, event subscription, and VM
        /// in reverse acquisition order, including partially failed opens.
        /// </summary>
        internal static void Close()
        {
            ScreenBase host = _hostScreen;
            GauntletLayer layer = _layer;
            GauntletMovieIdentifier movie = _movie;
            SpriteCategory partySpriteCategory = _partySpriteCategory;
            RecruitmentWeightVM dataSource = _dataSource;
            _hostScreen = null; _layer = null; _movie = null;
            _partySpriteCategory = null; _dataSource = null;
            ScreenManager.OnPopScreen -= OnScreenPopped;
            if (layer != null)
            {
                layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(layer);
                if (movie != null) layer.ReleaseMovie(movie);
                host?.RemoveLayer(layer);
            }
            partySpriteCategory?.Unload();
            dataSource?.OnFinalize();
        }

        private static void OnScreenPopped(ScreenBase screen)
        {
            if (screen == _hostScreen) Close();
        }
    }
}
