using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    [SaveableRootClass(5)]
    public class FiefPartyComponent : PartyComponent
    {
        // === 1. 保存字段（与 Militia 完全一致）===
        [SaveableProperty(1)]
        public Settlement Settlement { get; private set; }

        // === 2. 构造函数（仿 Militia）===
        protected FiefPartyComponent(Settlement settlement, FiefPartyComponent.InitializationArgs args)
        {
            this.Settlement = settlement;
            this._initializationArgs = args;
        }

        protected FiefPartyComponent() { }

        // === 3. 静态工厂方法（核心创建入口，仿 CreateMilitiaParty）===
        public static MobileParty CreateFiefParty(string stringId, Settlement settlement)
        {
            var args = new FiefPartyComponent.InitializationArgs();
            var mobileParty = MobileParty.CreateParty(
                "fief_party_" + stringId,
                new FiefPartyComponent(settlement, args)
            );
            mobileParty.DesiredAiNavigationType = MobileParty.NavigationType.None;
            EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            return mobileParty;
        }

        // === 4. 转换方法（可选，仿 ConvertPartyToMilitiaParty）===
        public static void ConvertPartyToFiefParty(MobileParty mobileParty, Settlement settlement)
        {
            mobileParty.SetPartyComponent(new FiefPartyComponent(settlement, null), true);
        }

        // === 5. 生命周期回调（仿 OnMobilePartySetOnCreation）===
        protected override void OnMobilePartySetOnCreation()
        {
            base.MobileParty.Party.SetVisualAsDirty();
            base.MobileParty.Ai.DisableAi();
            base.MobileParty.Aggressiveness = 0f;
            if (this._initializationArgs != null)
            {
                this._initializationArgs.InitializeFiefPartyProperties(base.MobileParty, this.Settlement);
                this._initializationArgs = null;
            }
        }

        // === 6. 抽象成员实现（仿 Militia）===
        public override Hero PartyOwner => null; // 注意：Militia 返回 OwnerClan.Leader，但你说不需要 Leader

        public override Banner GetDefaultComponentBanner()
        {
            return this.Settlement.Banner;
        }

        public override bool CanHaveNavalNavigationCapability => false;

        public override TextObject Name
        {
            get
            {
                if (_cachedName == null)
                {
                    _cachedName = new TextObject("{=FiefParty}Fief Troops of {SETTLEMENT_NAME}")
                        .SetTextVariable("SETTLEMENT_NAME", this.Settlement?.Name);
                }
                return _cachedName;
            }
        }

        public override Settlement HomeSettlement => this.Settlement;

        // === 7. 初始化参数类（仿 InitializationArgs）===
        protected class InitializationArgs
        {
            public void InitializeFiefPartyProperties(MobileParty mobileParty, Settlement settlement)
            {
                PartyTemplateObject fiefTemplate = settlement.Culture.MilitiaPartyTemplate;
                mobileParty.InitializeMobilePartyAtPosition(fiefTemplate, settlement.GatePosition);
            }
        }

        // === 8. 私有字段 ===
        private FiefPartyComponent.InitializationArgs _initializationArgs;
        [CachedData] private TextObject _cachedName;
    }
}
