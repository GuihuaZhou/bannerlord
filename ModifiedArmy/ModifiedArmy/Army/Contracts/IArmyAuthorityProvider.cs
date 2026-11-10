using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.Army.Contracts
{
    /// <summary>
    /// Optional upstream policy for kingdom army authority. Implementations
    /// must query current campaign state and must not retain Hero or Kingdom.
    /// </summary>
    public interface IArmyAuthorityProvider
    {
        bool HasAppointedMarshal(Kingdom kingdom);
        bool IsAppointedMarshal(Hero hero, Kingdom kingdom);
        bool IsEligibleMarshal(Hero hero, Kingdom kingdom);
    }
}
