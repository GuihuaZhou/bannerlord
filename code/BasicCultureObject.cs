using System;
using System.Xml;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TaleWorlds.Core
{
	// Token: 0x0200001B RID: 27
	public class BasicCultureObject : MBObjectBase
	{
		// Token: 0x17000067 RID: 103
		// (get) Token: 0x06000162 RID: 354 RVA: 0x00006517 File Offset: 0x00004717
		// (set) Token: 0x06000163 RID: 355 RVA: 0x0000651F File Offset: 0x0000471F
		public TextObject Name { get; private set; }

		// Token: 0x17000068 RID: 104
		// (get) Token: 0x06000164 RID: 356 RVA: 0x00006528 File Offset: 0x00004728
		// (set) Token: 0x06000165 RID: 357 RVA: 0x00006530 File Offset: 0x00004730
		public bool IsMainCulture { get; private set; }

		// Token: 0x17000069 RID: 105
		// (get) Token: 0x06000166 RID: 358 RVA: 0x00006539 File Offset: 0x00004739
		// (set) Token: 0x06000167 RID: 359 RVA: 0x00006541 File Offset: 0x00004741
		public bool IsBandit { get; private set; }

		// Token: 0x1700006A RID: 106
		// (get) Token: 0x06000168 RID: 360 RVA: 0x0000654A File Offset: 0x0000474A
		// (set) Token: 0x06000169 RID: 361 RVA: 0x00006552 File Offset: 0x00004752
		public bool CanHaveSettlement { get; private set; }

		// Token: 0x1700006B RID: 107
		// (get) Token: 0x0600016A RID: 362 RVA: 0x0000655B File Offset: 0x0000475B
		// (set) Token: 0x0600016B RID: 363 RVA: 0x00006563 File Offset: 0x00004763
		public uint Color { get; private set; }

		// Token: 0x1700006C RID: 108
		// (get) Token: 0x0600016C RID: 364 RVA: 0x0000656C File Offset: 0x0000476C
		// (set) Token: 0x0600016D RID: 365 RVA: 0x00006574 File Offset: 0x00004774
		public uint Color2 { get; private set; }

		// Token: 0x1700006D RID: 109
		// (get) Token: 0x0600016E RID: 366 RVA: 0x0000657D File Offset: 0x0000477D
		// (set) Token: 0x0600016F RID: 367 RVA: 0x00006585 File Offset: 0x00004785
		public uint ClothAlternativeColor { get; private set; }

		// Token: 0x1700006E RID: 110
		// (get) Token: 0x06000170 RID: 368 RVA: 0x0000658E File Offset: 0x0000478E
		// (set) Token: 0x06000171 RID: 369 RVA: 0x00006596 File Offset: 0x00004796
		public uint ClothAlternativeColor2 { get; private set; }

		// Token: 0x1700006F RID: 111
		// (get) Token: 0x06000172 RID: 370 RVA: 0x0000659F File Offset: 0x0000479F
		// (set) Token: 0x06000173 RID: 371 RVA: 0x000065A7 File Offset: 0x000047A7
		public uint BackgroundColor1 { get; private set; }

		// Token: 0x17000070 RID: 112
		// (get) Token: 0x06000174 RID: 372 RVA: 0x000065B0 File Offset: 0x000047B0
		// (set) Token: 0x06000175 RID: 373 RVA: 0x000065B8 File Offset: 0x000047B8
		public uint ForegroundColor1 { get; private set; }

		// Token: 0x17000071 RID: 113
		// (get) Token: 0x06000176 RID: 374 RVA: 0x000065C1 File Offset: 0x000047C1
		// (set) Token: 0x06000177 RID: 375 RVA: 0x000065C9 File Offset: 0x000047C9
		public uint BackgroundColor2 { get; private set; }

		// Token: 0x17000072 RID: 114
		// (get) Token: 0x06000178 RID: 376 RVA: 0x000065D2 File Offset: 0x000047D2
		// (set) Token: 0x06000179 RID: 377 RVA: 0x000065DA File Offset: 0x000047DA
		public uint ForegroundColor2 { get; private set; }

		// Token: 0x17000073 RID: 115
		// (get) Token: 0x0600017A RID: 378 RVA: 0x000065E3 File Offset: 0x000047E3
		// (set) Token: 0x0600017B RID: 379 RVA: 0x000065EB File Offset: 0x000047EB
		public string EncounterBackgroundMesh { get; set; }

		// Token: 0x17000074 RID: 116
		// (get) Token: 0x0600017C RID: 380 RVA: 0x000065F4 File Offset: 0x000047F4
		// (set) Token: 0x0600017D RID: 381 RVA: 0x000065FC File Offset: 0x000047FC
		public Banner Banner { get; private set; }

		// Token: 0x0600017E RID: 382 RVA: 0x00006605 File Offset: 0x00004805
		public override string ToString()
		{
			return this.Name.ToString();
		}

		// Token: 0x0600017F RID: 383 RVA: 0x00006614 File Offset: 0x00004814
		public override void Deserialize(MBObjectManager objectManager, XmlNode node)
		{
			base.Deserialize(objectManager, node);
			this.Name = new TextObject(node.Attributes["name"].Value, null);
			this.Color = ((node.Attributes["color"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["color"].Value, 16));
			this.Color2 = ((node.Attributes["color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["color2"].Value, 16));
			this.ClothAlternativeColor = ((node.Attributes["cloth_alternative_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["cloth_alternative_color1"].Value, 16));
			this.ClothAlternativeColor2 = ((node.Attributes["cloth_alternative_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["cloth_alternative_color2"].Value, 16));
			this.BackgroundColor1 = ((node.Attributes["banner_background_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_background_color1"].Value, 16));
			this.ForegroundColor1 = ((node.Attributes["banner_foreground_color1"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_foreground_color1"].Value, 16));
			this.BackgroundColor2 = ((node.Attributes["banner_background_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_background_color2"].Value, 16));
			this.ForegroundColor2 = ((node.Attributes["banner_foreground_color2"] == null) ? uint.MaxValue : Convert.ToUInt32(node.Attributes["banner_foreground_color2"].Value, 16));
			this.IsMainCulture = (node.Attributes["is_main_culture"] != null && Convert.ToBoolean(node.Attributes["is_main_culture"].Value));
			this.EncounterBackgroundMesh = ((node.Attributes["encounter_background_mesh"] == null) ? null : node.Attributes["encounter_background_mesh"].Value);
			this.Banner = ((node.Attributes["faction_banner_key"] == null) ? new Banner() : new Banner(node.Attributes["faction_banner_key"].Value));
			this.IsBandit = false;
			this.IsBandit = (node.Attributes["is_bandit"] != null && Convert.ToBoolean(node.Attributes["is_bandit"].Value));
			this.CanHaveSettlement = false;
			this.CanHaveSettlement = (node.Attributes["can_have_settlement"] != null && Convert.ToBoolean(node.Attributes["can_have_settlement"].Value));
		}
	}
}
