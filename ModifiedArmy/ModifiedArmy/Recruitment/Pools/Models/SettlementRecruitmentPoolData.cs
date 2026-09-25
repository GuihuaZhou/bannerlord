using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Save data for both manpower pools owned by one settlement. Template
    /// definitions are intentionally excluded because they are rebuilt from
    /// XML whenever the game starts.
    /// </summary>
    [SaveableRootClass(5)]
    public sealed class SettlementRecruitmentPoolData
    {
        [SaveableProperty(1)]
        public Dictionary<CharacterObject, int> ProfessionalTroops
        {
            get;
            private set;
        } = new Dictionary<CharacterObject, int>();

        [SaveableProperty(2)]
        public Dictionary<CharacterObject, int> FiefTroops
        {
            get;
            private set;
        } = new Dictionary<CharacterObject, int>();

        [SaveableProperty(3)]
        public float ProfessionalProductionProgress
        {
            get;
            private set;
        }

        [SaveableProperty(4)]
        public float FiefProductionProgress
        {
            get;
            private set;
        }

        public Dictionary<CharacterObject, int> GetTroops(
            RecruitmentPoolKind kind)
        {
            EnsureInitialized();
            return kind == RecruitmentPoolKind.Professional
                ? ProfessionalTroops
                : FiefTroops;
        }

        public float AddProductionProgress(
            RecruitmentPoolKind kind,
            float amount)
        {
            if (kind == RecruitmentPoolKind.Professional)
            {
                ProfessionalProductionProgress = Math.Max(
                    0f,
                    ProfessionalProductionProgress + amount);
                return ProfessionalProductionProgress;
            }

            FiefProductionProgress = Math.Max(
                0f,
                FiefProductionProgress + amount);
            return FiefProductionProgress;
        }

        public float GetProductionProgress(RecruitmentPoolKind kind)
        {
            return kind == RecruitmentPoolKind.Professional
                ? ProfessionalProductionProgress
                : FiefProductionProgress;
        }

        public void ConsumeProductionProgress(
            RecruitmentPoolKind kind,
            int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (kind == RecruitmentPoolKind.Professional)
            {
                ProfessionalProductionProgress = Math.Max(
                    0f,
                    ProfessionalProductionProgress - amount);
            }
            else
            {
                FiefProductionProgress = Math.Max(
                    0f,
                    FiefProductionProgress - amount);
            }
        }

        public void EnsureInitialized()
        {
            ProfessionalTroops ??= new Dictionary<CharacterObject, int>();
            FiefTroops ??= new Dictionary<CharacterObject, int>();
        }
    }
}
