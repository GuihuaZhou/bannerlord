using TaleWorlds.Localization;

namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// Resolves player-facing, localized reasons for disposition changes.
    /// </summary>
    public static class WarDispositionEventText
    {
        public static TextObject GetReason(
            WarDispositionEventType eventType,
            float? baseValueOverride)
        {
            if (eventType == WarDispositionEventType.WeeklyWealthChange)
            {
                if (baseValueOverride > 0f)
                {
                    return new TextObject(
                        "{=ModifiedPolitics_WarDispositionEventWealthIncreased}" +
                        "clan wealth increased");
                }

                if (baseValueOverride < 0f)
                {
                    return new TextObject(
                        "{=ModifiedPolitics_WarDispositionEventWealthDecreased}" +
                        "clan wealth decreased");
                }
            }

            switch (eventType)
            {
                case WarDispositionEventType.PartyVictory:
                    return Create("PartyVictory", "a party victory");
                case WarDispositionEventType.PartyDefeat:
                    return Create("PartyDefeat", "a party defeat");
                case WarDispositionEventType.PartyDestroyed:
                    return Create("PartyDestroyed", "a party was destroyed");
                case WarDispositionEventType.ClanMemberCaptured:
                    return Create("ClanMemberCaptured", "a clan member was captured");
                case WarDispositionEventType.ClanMemberKilled:
                    return Create("ClanMemberKilled", "a clan member was killed");
                case WarDispositionEventType.ClanMemberExecuted:
                    return Create("ClanMemberExecuted", "a clan member was executed");
                case WarDispositionEventType.VillageRaided:
                    return Create("VillageRaided", "a village was raided");
                case WarDispositionEventType.CastleLost:
                    return Create("CastleLost", "a castle was lost");
                case WarDispositionEventType.TownLost:
                    return Create("TownLost", "a town was lost");
                case WarDispositionEventType.EnemyCaptured:
                    return Create("EnemyCaptured", "an enemy hero was captured");
                case WarDispositionEventType.CastleCaptured:
                    return Create("CastleCaptured", "a castle was captured");
                case WarDispositionEventType.TownCaptured:
                    return Create("TownCaptured", "a town was captured");
                case WarDispositionEventType.CastleGranted:
                    return Create("CastleGranted", "a castle was granted");
                case WarDispositionEventType.TownGranted:
                    return Create("TownGranted", "a town was granted");
                default:
                    return Create("WeeklyWealthChange", "clan wealth changed");
            }
        }

        private static TextObject Create(string idSuffix, string fallback)
        {
            return new TextObject(
                $"{{=ModifiedPolitics_WarDispositionEvent{idSuffix}}}" +
                fallback);
        }
    }
}
