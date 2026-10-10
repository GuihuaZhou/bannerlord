using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;

namespace ModifiedPolitics.Governor.Notifications
{
    /// <summary>
    /// Displays a governor event in the existing round map-notification container.
    /// </summary>
    public sealed class GovernorAppointmentMapNotificationVM : MapNotificationItemBaseVM
    {
        public GovernorAppointmentMapNotificationVM(GovernorAppointmentMapNotification data)
            : base(data)
        {
            // Reuse a native graphical identifier so this feature needs no fragile TPAC override.
            NotificationIdentifier = "armycreation";
            TitleText = data.TitleText.ToString();
            DescriptionText = data.GetDescription().ToString();
            SoundId = data.SoundEventPath;
            ForceInspection = false;
            _onInspect = () =>
            {
                if (data.NewSettlement != null)
                    FastMoveCameraToPosition?.Invoke(data.NewSettlement.Position);
            };
        }
    }
}
