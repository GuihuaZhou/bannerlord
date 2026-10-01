using System;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// Hosts the independent kingdom negotiation VM in a modal Gauntlet layer.
    /// The movie is a local copy of Bannerlord's barter layout. Its visual
    /// structure remains familiar, while its VM and all negotiation state are
    /// owned entirely by ModifiedPolitics.
    /// </summary>
    public static class KingdomNegotiationScreenService
    {
        private static ScreenBase _hostScreen;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movie;
        private static SpriteCategory _barterCategory;
        private static KingdomNegotiationVM _dataSource;

        public static bool IsOpen => _layer != null;

        public static bool Open(Kingdom targetKingdom)
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            ScreenBase screen = ScreenManager.TopScreen;
            if (IsOpen
                || playerKingdom == null
                || targetKingdom == null
                || targetKingdom == playerKingdom
                || screen == null)
            {
                return false;
            }

            try
            {
                _hostScreen = screen;
                KingdomNegotiationDraft counterOffer =
                    KingdomNegotiationProposalService.GetCounterOffer(
                        playerKingdom,
                        targetKingdom);
                _dataSource = new KingdomNegotiationVM(
                    playerKingdom,
                    targetKingdom,
                    Close,
                    counterOffer);
                _layer = new GauntletLayer(
                    "ModifiedPoliticsKingdomNegotiationLayer",
                    300,
                    false)
                {
                    IsFocusLayer = true
                };
                _layer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.All);
                _layer.Input.RegisterHotKeyCategory(
                    HotKeyManager.GetCategory(
                        "GenericPanelGameKeyCategory"));
                var category = HotKeyManager.GetCategory(
                    "GenericPanelGameKeyCategory");
                _dataSource.SetInputKeys(
                    category.GetHotKey("Reset"),
                    category.GetHotKey("Confirm"),
                    category.GetHotKey("Exit"));
                _hostScreen.AddLayer(_layer);
                _movie = _layer.LoadMovie(
                    "KingdomNegotiationScreen",
                    _dataSource);
                if (_movie == null)
                {
                    throw new InvalidOperationException(
                        "KingdomNegotiationScreen was not registered. "
                        + "Copy the mod GUI folder to "
                        + "Modules/ModifiedPolitics/GUI before starting "
                        + "the game.");
                }

                // Barter decorations are stored in a lazily loaded sprite
                // category. Loading the XML alone leaves tuple frames,
                // category headers and other ornamental sprites invisible.
                _barterCategory = UIResourceManager.GetSpriteCategory(
                    "ui_barter");
                _barterCategory?.Load();

                // Consume a saved counteroffer only after the movie and its
                // resources were created successfully. A failed screen open
                // must not discard the player's draft.
                KingdomNegotiationProposalService.RemoveCounterOffer(
                    playerKingdom,
                    targetKingdom);

                ScreenManager.OnPopScreen += OnScreenPopped;
                ScreenManager.TrySetFocus(_layer);
                return true;
            }
            catch (Exception exception)
            {
                TextObject message = new TextObject(
                    "{=MP_KingdomNegotiationOpenFailed}[Kingdom diplomacy] Failed to open the kingdom negotiation screen. {ERROR}");
                message.SetTextVariable("ERROR", exception.Message);
                ModLogger.Error(message.ToString());
                Close();
                return false;
            }
        }

        public static void Close()
        {
            GauntletLayer layer = _layer;
            ScreenBase host = _hostScreen;
            GauntletMovieIdentifier movie = _movie;
            SpriteCategory barterCategory = _barterCategory;
            KingdomNegotiationVM dataSource = _dataSource;

            // Clear static state first so a close callback can never run the
            // cleanup sequence twice against an already released movie.
            _layer = null;
            _hostScreen = null;
            _movie = null;
            _barterCategory = null;
            _dataSource = null;
            ScreenManager.OnPopScreen -= OnScreenPopped;

            if (layer != null)
            {
                layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(layer);
                if (movie != null)
                {
                    layer.ReleaseMovie(movie);
                }

                host?.RemoveLayer(layer);
            }

            barterCategory?.Unload();

            dataSource?.OnFinalize();
        }

        private static void OnScreenPopped(ScreenBase poppedScreen)
        {
            if (poppedScreen == _hostScreen)
            {
                Close();
            }
        }
    }
}
