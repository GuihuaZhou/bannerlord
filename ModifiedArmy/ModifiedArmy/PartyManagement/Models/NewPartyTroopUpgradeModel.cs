using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class NewPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
    {
        // 提高troop升级所需的经验
        public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
        {
            //InformationManager.DisplayMessage(new InformationMessage($"[MOD] Call NewPartyTroopUpgradeModel::GetXpCostForUpgrade"));

            if (upgradeTarget != null && characterObject.UpgradeTargets.Contains(upgradeTarget))
            {
                int tier = upgradeTarget.Tier;
                int num = 0;
                for (int i = characterObject.Tier + 1; i <= tier; i++)
                {
                    if (i <= 1)
                    {
                        num += 100;
                    }
                    else if (i == 2)
                    {
                        num += 300;
                    }
                    else if (i == 3)
                    {
                        num += 550;
                    }
                    else if (i == 4)
                    {
                        num += 1800; // 900
                    }
                    else if (i == 5)
                    {
                        num += 2600; // 1300
                    }
                    else if (i == 6)
                    {
                        num += 3400; // 1700
                    }
                    else if (i == 7)
                    {
                        num += 4200; // 2100
                    }
                    else
                    {
                        int num2 = upgradeTarget.Level + 4;
                        num += (int)(1.333f * (float)num2 * (float)num2);
                    }
                }
                return num;
            }
            return 100000000;
        }
    }
}
