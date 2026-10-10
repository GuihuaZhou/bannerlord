using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.Governor.Notifications
{
    /// <summary>
    /// Persisted map notice for a successful player-clan governor appointment or transfer.
    /// </summary>
    public sealed class GovernorAppointmentMapNotification : InformationData
    {
        [SaveableField(1)]
        private Hero _hero;

        [SaveableField(2)]
        private Settlement _oldSettlement;

        [SaveableField(3)]
        private Settlement _newSettlement;

        public GovernorAppointmentMapNotification()
            : base(TextObject.GetEmpty())
        {
        }

        public GovernorAppointmentMapNotification(Hero hero, Settlement oldSettlement, Settlement newSettlement)
            : base(CreateTitle(hero, oldSettlement, newSettlement))
        {
            _hero = hero;
            _oldSettlement = oldSettlement;
            _newSettlement = newSettlement;
        }

        public Hero Hero => _hero;
        public Settlement OldSettlement => _oldSettlement;
        public Settlement NewSettlement => _newSettlement;

        public override TextObject TitleText => CreateTitle(_hero, _oldSettlement, _newSettlement);

        public override string SoundEventPath => "event:/ui/notification/kingdom_decision";

        public TextObject GetDescription()
        {
            TextObject text = _oldSettlement == null
                ? new TextObject("{=MP_GovernorNoticeAppointed}Your clan member {HERO} was appointed governor of {NEW_SETTLEMENT}.")
                : new TextObject("{=MP_GovernorNoticeMoved}Your clan member {HERO} was transferred from {OLD_SETTLEMENT} to {NEW_SETTLEMENT}.");
            text.SetTextVariable("HERO", _hero?.Name ?? TextObject.GetEmpty());
            text.SetTextVariable("OLD_SETTLEMENT", _oldSettlement?.Name ?? TextObject.GetEmpty());
            text.SetTextVariable("NEW_SETTLEMENT", _newSettlement?.Name ?? TextObject.GetEmpty());
            return text;
        }

        private static TextObject CreateTitle(Hero hero, Settlement oldSettlement, Settlement newSettlement)
        {
            TextObject title = new TextObject(
                oldSettlement == null
                    ? "{=MP_GovernorNoticeAppointmentTitle}Governor Appointment: {SETTLEMENT}"
                    : "{=MP_GovernorNoticeTransferTitle}Governor Transfer: {SETTLEMENT}");
            title.SetTextVariable("SETTLEMENT", newSettlement?.Name ?? TextObject.GetEmpty());
            return title;
        }
    }
}
