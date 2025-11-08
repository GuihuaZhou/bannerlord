using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TaleWorlds.Core
{
	// Token: 0x0200001A RID: 26
	public class BasicCharacterObject : MBObjectBase
	{
		// Token: 0x17000046 RID: 70
		// (get) Token: 0x06000113 RID: 275 RVA: 0x00005402 File Offset: 0x00003602
		public virtual TextObject Name
		{
			get
			{
				return this._basicName;
			}
		}

		// Token: 0x06000114 RID: 276 RVA: 0x0000540A File Offset: 0x0000360A
		private void SetName(TextObject name)
		{
			this._basicName = name;
		}

		// Token: 0x06000115 RID: 277 RVA: 0x00005413 File Offset: 0x00003613
		public override TextObject GetName()
		{
			return this.Name;
		}

		// Token: 0x06000116 RID: 278 RVA: 0x0000541B File Offset: 0x0000361B
		public override string ToString()
		{
			return this.Name.ToString();
		}

		// Token: 0x17000047 RID: 71
		// (get) Token: 0x06000117 RID: 279 RVA: 0x00005428 File Offset: 0x00003628
		// (set) Token: 0x06000118 RID: 280 RVA: 0x00005430 File Offset: 0x00003630
		public virtual MBBodyProperty BodyPropertyRange { get; protected set; }

		// Token: 0x17000048 RID: 72
		// (get) Token: 0x06000119 RID: 281 RVA: 0x00005439 File Offset: 0x00003639
		// (set) Token: 0x0600011A RID: 282 RVA: 0x00005441 File Offset: 0x00003641
		public int DefaultFormationGroup { get; set; }

		// Token: 0x17000049 RID: 73
		// (get) Token: 0x0600011B RID: 283 RVA: 0x0000544A File Offset: 0x0000364A
		// (set) Token: 0x0600011C RID: 284 RVA: 0x00005452 File Offset: 0x00003652
		public FormationClass DefaultFormationClass { get; protected set; }

		// Token: 0x1700004A RID: 74
		// (get) Token: 0x0600011D RID: 285 RVA: 0x0000545B File Offset: 0x0000365B
		// (set) Token: 0x0600011E RID: 286 RVA: 0x00005463 File Offset: 0x00003663
		public float KnockbackResistance { get; private set; }

		// Token: 0x1700004B RID: 75
		// (get) Token: 0x0600011F RID: 287 RVA: 0x0000546C File Offset: 0x0000366C
		// (set) Token: 0x06000120 RID: 288 RVA: 0x00005474 File Offset: 0x00003674
		public float KnockdownResistance { get; private set; }

		// Token: 0x1700004C RID: 76
		// (get) Token: 0x06000121 RID: 289 RVA: 0x0000547D File Offset: 0x0000367D
		// (set) Token: 0x06000122 RID: 290 RVA: 0x00005485 File Offset: 0x00003685
		public float DismountResistance { get; private set; }

		// Token: 0x1700004D RID: 77
		// (get) Token: 0x06000123 RID: 291 RVA: 0x0000548E File Offset: 0x0000368E
		// (set) Token: 0x06000124 RID: 292 RVA: 0x00005496 File Offset: 0x00003696
		public FormationPositionPreference FormationPositionPreference { get; protected set; }

		// Token: 0x1700004E RID: 78
		// (get) Token: 0x06000125 RID: 293 RVA: 0x0000549F File Offset: 0x0000369F
		public bool IsInfantry
		{
			get
			{
				return !this.IsRanged && !this.IsMounted;
			}
		}

		// Token: 0x1700004F RID: 79
		// (get) Token: 0x06000126 RID: 294 RVA: 0x000054B4 File Offset: 0x000036B4
		public virtual bool IsMounted
		{
			get
			{
				return this._isMounted;
			}
		}

		// Token: 0x17000050 RID: 80
		// (get) Token: 0x06000127 RID: 295 RVA: 0x000054BC File Offset: 0x000036BC
		public virtual bool IsRanged
		{
			get
			{
				return this._isRanged;
			}
		}

		// Token: 0x17000051 RID: 81
		// (get) Token: 0x06000128 RID: 296 RVA: 0x000054C4 File Offset: 0x000036C4
		public float SkillFactor
		{
			get
			{
				return (float)MathF.Min(this.Level, BasicCharacterObject.SkillAffectingMaxLevel) / (float)BasicCharacterObject.SkillAffectingMaxLevel;
			}
		}

		// Token: 0x17000052 RID: 82
		// (get) Token: 0x06000129 RID: 297 RVA: 0x000054DE File Offset: 0x000036DE
		// (set) Token: 0x0600012A RID: 298 RVA: 0x000054E6 File Offset: 0x000036E6
		public int Race { get; set; }

		// Token: 0x17000053 RID: 83
		// (get) Token: 0x0600012B RID: 299 RVA: 0x000054EF File Offset: 0x000036EF
		// (set) Token: 0x0600012C RID: 300 RVA: 0x000054F7 File Offset: 0x000036F7
		public virtual bool IsFemale { get; set; }

		// Token: 0x17000054 RID: 84
		// (get) Token: 0x0600012D RID: 301 RVA: 0x00005500 File Offset: 0x00003700
		// (set) Token: 0x0600012E RID: 302 RVA: 0x00005508 File Offset: 0x00003708
		public bool FaceMeshCache { get; private set; }

		// Token: 0x17000055 RID: 85
		// (get) Token: 0x0600012F RID: 303 RVA: 0x00005511 File Offset: 0x00003711
		protected virtual MBReadOnlyList<Equipment> AllEquipments
		{
			get
			{
				if (this._equipmentRoster == null)
				{
					return new MBList<Equipment>
					{
						MBEquipmentRoster.EmptyEquipment
					};
				}
				return this._equipmentRoster.AllEquipments;
			}
		}

		// Token: 0x17000056 RID: 86
		// (get) Token: 0x06000130 RID: 304 RVA: 0x00005537 File Offset: 0x00003737
		public virtual Equipment Equipment
		{
			get
			{
				if (this._equipmentRoster == null)
				{
					return MBEquipmentRoster.EmptyEquipment;
				}
				return this._equipmentRoster.DefaultEquipment;
			}
		}

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x06000131 RID: 305 RVA: 0x00005552 File Offset: 0x00003752
		public virtual IEnumerable<Equipment> BattleEquipments
		{
			get
			{
				return this.AllEquipments.WhereQ((Equipment e) => e.IsBattle);
			}
		}

		// Token: 0x17000058 RID: 88
		// (get) Token: 0x06000132 RID: 306 RVA: 0x0000557E File Offset: 0x0000377E
		public virtual Equipment FirstBattleEquipment
		{
			get
			{
				return this.AllEquipments.FirstOrDefaultQ((Equipment e) => e.IsBattle);
			}
		}

		// Token: 0x17000059 RID: 89
		// (get) Token: 0x06000133 RID: 307 RVA: 0x000055AA File Offset: 0x000037AA
		public virtual Equipment RandomBattleEquipment
		{
			get
			{
				return this.AllEquipments.GetRandomElementWithPredicate((Equipment e) => e.IsBattle);
			}
		}

		// Token: 0x1700005A RID: 90
		// (get) Token: 0x06000134 RID: 308 RVA: 0x000055D6 File Offset: 0x000037D6
		public virtual IEnumerable<Equipment> CivilianEquipments
		{
			get
			{
				return this.AllEquipments.WhereQ((Equipment e) => e.IsCivilian);
			}
		}

		// Token: 0x1700005B RID: 91
		// (get) Token: 0x06000135 RID: 309 RVA: 0x00005602 File Offset: 0x00003802
		public virtual Equipment FirstCivilianEquipment
		{
			get
			{
				return this.AllEquipments.FirstOrDefaultQ((Equipment e) => e.IsCivilian);
			}
		}

		// Token: 0x1700005C RID: 92
		// (get) Token: 0x06000136 RID: 310 RVA: 0x0000562E File Offset: 0x0000382E
		public virtual Equipment RandomCivilianEquipment
		{
			get
			{
				return this.AllEquipments.GetRandomElementWithPredicate((Equipment e) => e.IsCivilian);
			}
		}

		// Token: 0x1700005D RID: 93
		// (get) Token: 0x06000137 RID: 311 RVA: 0x0000565A File Offset: 0x0000385A
		public virtual Equipment GetRandomEquipment
		{
			get
			{
				return this.AllEquipments.GetRandomElementWithPredicate((Equipment x) => !x.IsEmpty());
			}
		}

		// Token: 0x1700005E RID: 94
		// (get) Token: 0x06000138 RID: 312 RVA: 0x00005686 File Offset: 0x00003886
		// (set) Token: 0x06000139 RID: 313 RVA: 0x0000568E File Offset: 0x0000388E
		public bool IsObsolete { get; private set; }

		// Token: 0x0600013A RID: 314 RVA: 0x00005697 File Offset: 0x00003897
		public void InitializeEquipmentsOnLoad(BasicCharacterObject character)
		{
			this._equipmentRoster = character._equipmentRoster;
		}

		// Token: 0x0600013B RID: 315 RVA: 0x000056A8 File Offset: 0x000038A8
		public Equipment GetFirstEquipment(Func<Equipment, bool> predicate)
		{
			Equipment equipment = this.AllEquipments.FirstOrDefault(predicate);
			if (equipment != null)
			{
				return equipment;
			}
			return this.Equipment;
		}

		// Token: 0x1700005F RID: 95
		// (get) Token: 0x0600013C RID: 316 RVA: 0x000056CD File Offset: 0x000038CD
		// (set) Token: 0x0600013D RID: 317 RVA: 0x000056D5 File Offset: 0x000038D5
		public virtual int Level { get; set; }

		// Token: 0x17000060 RID: 96
		// (get) Token: 0x0600013E RID: 318 RVA: 0x000056DE File Offset: 0x000038DE
		// (set) Token: 0x0600013F RID: 319 RVA: 0x000056E6 File Offset: 0x000038E6
		public BasicCultureObject Culture
		{
			get
			{
				return this._culture;
			}
			set
			{
				this._culture = value;
			}
		}

		// Token: 0x17000061 RID: 97
		// (get) Token: 0x06000140 RID: 320 RVA: 0x000056EF File Offset: 0x000038EF
		public virtual bool IsPlayerCharacter
		{
			get
			{
				return Game.Current.PlayerTroop == this;
			}
		}

		// Token: 0x17000062 RID: 98
		// (get) Token: 0x06000141 RID: 321 RVA: 0x000056FE File Offset: 0x000038FE
		// (set) Token: 0x06000142 RID: 322 RVA: 0x00005706 File Offset: 0x00003906
		public virtual float Age
		{
			get
			{
				return this._age;
			}
			set
			{
				this._age = value;
			}
		}

		// Token: 0x17000063 RID: 99
		// (get) Token: 0x06000143 RID: 323 RVA: 0x0000570F File Offset: 0x0000390F
		public virtual int HitPoints
		{
			get
			{
				return this.MaxHitPoints();
			}
		}

		// Token: 0x06000144 RID: 324 RVA: 0x00005717 File Offset: 0x00003917
		public virtual BodyProperties GetBodyPropertiesMin(bool returnBaseValue = false)
		{
			return this.BodyPropertyRange.BodyPropertyMin;
		}

		// Token: 0x06000145 RID: 325 RVA: 0x00005724 File Offset: 0x00003924
		protected void FillFrom(BasicCharacterObject character)
		{
			this._culture = character._culture;
			this.DefaultFormationClass = character.DefaultFormationClass;
			this.DefaultFormationGroup = character.DefaultFormationGroup;
			this.BodyPropertyRange = character.BodyPropertyRange;
			this.FormationPositionPreference = character.FormationPositionPreference;
			this.IsFemale = character.IsFemale;
			this.Race = character.Race;
			this.Level = character.Level;
			this._basicName = character._basicName;
			this._age = character._age;
			this.KnockbackResistance = character.KnockbackResistance;
			this.KnockdownResistance = character.KnockdownResistance;
			this.DismountResistance = character.DismountResistance;
			this.DefaultCharacterSkills = character.DefaultCharacterSkills;
			this.InitializeEquipmentsOnLoad(character);
		}

		// Token: 0x06000146 RID: 326 RVA: 0x000057E0 File Offset: 0x000039E0
		public virtual BodyProperties GetBodyPropertiesMax(bool returnBaseValue = false)
		{
			return this.BodyPropertyRange.BodyPropertyMax;
		}

		// Token: 0x06000147 RID: 327 RVA: 0x000057F0 File Offset: 0x000039F0
		public virtual BodyProperties GetBodyProperties(Equipment equipment, int seed = -1)
		{
			BodyProperties bodyPropertiesMin = this.GetBodyPropertiesMin(false);
			BodyProperties bodyPropertiesMax = this.GetBodyPropertiesMax(false);
			return FaceGen.GetRandomBodyProperties(this.Race, this.IsFemale, bodyPropertiesMin, bodyPropertiesMax, (int)((equipment != null) ? equipment.HairCoverType : ArmorComponent.HairCoverTypes.None), seed, this.BodyPropertyRange.HairTags, this.BodyPropertyRange.BeardTags, this.BodyPropertyRange.TattooTags, 0f);
		}

		// Token: 0x06000148 RID: 328 RVA: 0x00005853 File Offset: 0x00003A53
		public virtual void UpdatePlayerCharacterBodyProperties(BodyProperties properties, int race, bool isFemale)
		{
			this.BodyPropertyRange.Init(properties, properties);
			this.Race = race;
			this.IsFemale = isFemale;
		}

		// Token: 0x17000064 RID: 100
		// (get) Token: 0x06000149 RID: 329 RVA: 0x00005870 File Offset: 0x00003A70
		// (set) Token: 0x0600014A RID: 330 RVA: 0x00005878 File Offset: 0x00003A78
		public float FaceDirtAmount { get; set; }

		// Token: 0x17000065 RID: 101
		// (get) Token: 0x0600014B RID: 331 RVA: 0x00005881 File Offset: 0x00003A81
		public virtual bool IsHero
		{
			get
			{
				return this._isBasicHero;
			}
		}

		// Token: 0x17000066 RID: 102
		// (get) Token: 0x0600014C RID: 332 RVA: 0x00005889 File Offset: 0x00003A89
		// (set) Token: 0x0600014D RID: 333 RVA: 0x00005891 File Offset: 0x00003A91
		public bool IsSoldier { get; private set; }

		// Token: 0x0600014E RID: 334 RVA: 0x0000589A File Offset: 0x00003A9A
		public BasicCharacterObject()
		{
			this.DefaultFormationClass = FormationClass.Infantry;
		}

		// Token: 0x0600014F RID: 335 RVA: 0x000058AC File Offset: 0x00003AAC
		public int GetDefaultFaceSeed(int rank)
		{
			int num = base.StringId.GetDeterministicHashCode() * 6791 + rank * 197;
			return ((num >= 0) ? num : (-num)) % 2000;
		}

		// Token: 0x06000150 RID: 336 RVA: 0x000058E2 File Offset: 0x00003AE2
		public float GetStepSize()
		{
			return Math.Min(0.8f + 0.2f * (float)this.GetSkillValue(DefaultSkills.Athletics) * 0.00333333f, 1f);
		}

		// Token: 0x06000151 RID: 337 RVA: 0x0000590C File Offset: 0x00003B0C
		public bool HasMount()
		{
			return this.Equipment[10].Item != null;
		}

		// Token: 0x06000152 RID: 338 RVA: 0x00005931 File Offset: 0x00003B31
		public virtual int MaxHitPoints()
		{
			return FaceGen.GetBaseMonsterFromRace(this.Race).HitPoints;
		}

		// Token: 0x06000153 RID: 339 RVA: 0x00005944 File Offset: 0x00003B44
		public virtual float GetPower()
		{
			int num = this.Level + 10;
			return 0.2f + (float)(num * num) * 0.0025f;
		}

		// Token: 0x06000154 RID: 340 RVA: 0x0000596B File Offset: 0x00003B6B
		public virtual float GetBattlePower()
		{
			return 1f;
		}

		// Token: 0x06000155 RID: 341 RVA: 0x00005972 File Offset: 0x00003B72
		public virtual float GetMoraleResistance()
		{
			return 1f;
		}

		// Token: 0x06000156 RID: 342 RVA: 0x00005979 File Offset: 0x00003B79
		public virtual int GetMountKeySeed()
		{
			return MBRandom.RandomInt();
		}

		// Token: 0x06000157 RID: 343 RVA: 0x00005980 File Offset: 0x00003B80
		public virtual int GetBattleTier()
		{
			if (this.IsHero)
			{
				return 7;
			}
			return MathF.Min(MathF.Max(MathF.Ceiling(((float)this.Level - 5f) / 5f), 0), 7);
		}

		// Token: 0x06000158 RID: 344 RVA: 0x000059B0 File Offset: 0x00003BB0
		public MBCharacterSkills GetDefaultCharacterSkills()
		{
			return this.DefaultCharacterSkills;
		}

		// Token: 0x06000159 RID: 345 RVA: 0x000059B8 File Offset: 0x00003BB8
		public virtual int GetSkillValue(SkillObject skill)
		{
			return this.DefaultCharacterSkills.Skills.GetPropertyValue(skill);
		}

		// Token: 0x0600015A RID: 346 RVA: 0x000059CC File Offset: 0x00003BCC
		protected void InitializeHeroBasicCharacterOnAfterLoad(BasicCharacterObject originCharacter)
		{
			this.IsSoldier = originCharacter.IsSoldier;
			this._isBasicHero = originCharacter._isBasicHero;
			this.DefaultCharacterSkills = originCharacter.DefaultCharacterSkills;
			this.BodyPropertyRange = originCharacter.BodyPropertyRange;
			this.IsFemale = originCharacter.IsFemale;
			this.Race = originCharacter.Race;
			this.Culture = originCharacter.Culture;
			this.DefaultFormationGroup = originCharacter.DefaultFormationGroup;
			this.DefaultFormationClass = originCharacter.DefaultFormationClass;
			this.FormationPositionPreference = originCharacter.FormationPositionPreference;
			this._equipmentRoster = originCharacter._equipmentRoster;
			this.KnockbackResistance = originCharacter.KnockbackResistance;
			this.KnockdownResistance = originCharacter.KnockdownResistance;
			this.DismountResistance = originCharacter.DismountResistance;
		}

		// Token: 0x0600015B RID: 347 RVA: 0x00005A84 File Offset: 0x00003C84
		public override void Deserialize(MBObjectManager objectManager, XmlNode node)
		{
			base.Deserialize(objectManager, node);
			XmlAttribute xmlAttribute = node.Attributes["name"];
			if (xmlAttribute != null)
			{
				this.SetName(new TextObject(xmlAttribute.Value, null));
			}
			this.Race = 0;
			XmlAttribute xmlAttribute2 = node.Attributes["race"];
			if (xmlAttribute2 != null)
			{
				this.Race = FaceGen.GetRaceOrDefault(xmlAttribute2.Value);
			}
			XmlNode xmlNode = node.Attributes["occupation"];
			if (xmlNode != null)
			{
				this.IsSoldier = (xmlNode.InnerText.IndexOf("soldier", StringComparison.OrdinalIgnoreCase) >= 0);
			}
			this._isBasicHero = XmlHelper.ReadBool(node, "is_hero");
			this.FaceMeshCache = XmlHelper.ReadBool(node, "face_mesh_cache");
			this.IsObsolete = XmlHelper.ReadBool(node, "is_obsolete");
			MBCharacterSkills mbcharacterSkills = objectManager.ReadObjectReferenceFromXml("skill_template", typeof(MBCharacterSkills), node) as MBCharacterSkills;
			if (mbcharacterSkills != null)
			{
				this.DefaultCharacterSkills = mbcharacterSkills;
			}
			else
			{
				this.DefaultCharacterSkills = MBObjectManager.Instance.CreateObject<MBCharacterSkills>(base.StringId);
			}
			BodyProperties bodyPropertyMin = default(BodyProperties);
			BodyProperties bodyProperties = default(BodyProperties);
			string text = "";
			string text2 = "";
			string text3 = "";
			foreach (object obj in node.ChildNodes)
			{
				XmlNode xmlNode2 = (XmlNode)obj;
				if (xmlNode2.Name == "Skills" || xmlNode2.Name == "skills")
				{
					if (mbcharacterSkills == null)
					{
						this.DefaultCharacterSkills.Init(objectManager, xmlNode2);
					}
				}
				else if (xmlNode2.Name == "Equipments" || xmlNode2.Name == "equipments")
				{
					List<XmlNode> list = new List<XmlNode>();
					foreach (object obj2 in xmlNode2.ChildNodes)
					{
						XmlNode xmlNode3 = (XmlNode)obj2;
						if (xmlNode3.Name == "equipment")
						{
							list.Add(xmlNode3);
						}
					}
					foreach (object obj3 in xmlNode2.ChildNodes)
					{
						XmlNode xmlNode4 = (XmlNode)obj3;
						if (xmlNode4.Name == "EquipmentRoster" || xmlNode4.Name == "equipmentRoster")
						{
							if (this._equipmentRoster == null)
							{
								this._equipmentRoster = MBObjectManager.Instance.CreateObject<MBEquipmentRoster>(base.StringId);
							}
							this._equipmentRoster.Init(objectManager, xmlNode4);
						}
						else if (xmlNode4.Name == "EquipmentSet" || xmlNode4.Name == "equipmentSet")
						{
							string innerText = xmlNode4.Attributes["id"].InnerText;
							Equipment.EquipmentType equipmentType = Equipment.EquipmentType.Battle;
							if (xmlNode4.Attributes["equipmentType"] != null)
							{
								if (!Enum.TryParse<Equipment.EquipmentType>(xmlNode4.Attributes["equipmentType"].Value, out equipmentType))
								{
									Debug.FailedAssert("This equipment definition is wrong", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.Core\\BasicCharacterObject.cs", "Deserialize", 450);
								}
							}
							else if (xmlNode4.Attributes["civilian"] != null && bool.Parse(xmlNode4.Attributes["civilian"].InnerText))
							{
								equipmentType = Equipment.EquipmentType.Civilian;
							}
							if (this._equipmentRoster == null)
							{
								this._equipmentRoster = MBObjectManager.Instance.CreateObject<MBEquipmentRoster>(base.StringId);
							}
							this._equipmentRoster.AddEquipmentRoster(MBObjectManager.Instance.GetObject<MBEquipmentRoster>(innerText), equipmentType);
						}
					}
					if (list.Count > 0)
					{
						this._equipmentRoster.AddOverridenEquipments(objectManager, list);
					}
				}
				else
				{
					if (xmlNode2.Name == "face")
					{
						using (IEnumerator enumerator2 = xmlNode2.ChildNodes.GetEnumerator())
						{
							while (enumerator2.MoveNext())
							{
								object obj4 = enumerator2.Current;
								XmlNode xmlNode5 = (XmlNode)obj4;
								if (xmlNode5.Name == "hair_tags")
								{
									using (IEnumerator enumerator3 = xmlNode5.ChildNodes.GetEnumerator())
									{
										while (enumerator3.MoveNext())
										{
											object obj5 = enumerator3.Current;
											XmlNode xmlNode6 = (XmlNode)obj5;
											text = text + xmlNode6.Attributes["name"].Value + ",";
										}
										continue;
									}
								}
								if (xmlNode5.Name == "beard_tags")
								{
									using (IEnumerator enumerator3 = xmlNode5.ChildNodes.GetEnumerator())
									{
										while (enumerator3.MoveNext())
										{
											object obj6 = enumerator3.Current;
											XmlNode xmlNode7 = (XmlNode)obj6;
											text2 = text2 + xmlNode7.Attributes["name"].Value + ",";
										}
										continue;
									}
								}
								if (xmlNode5.Name == "tattoo_tags")
								{
									using (IEnumerator enumerator3 = xmlNode5.ChildNodes.GetEnumerator())
									{
										while (enumerator3.MoveNext())
										{
											object obj7 = enumerator3.Current;
											XmlNode xmlNode8 = (XmlNode)obj7;
											text3 = text3 + xmlNode8.Attributes["name"].Value + ",";
										}
										continue;
									}
								}
								if (xmlNode5.Name == "BodyProperties")
								{
									if (!BodyProperties.FromXmlNode(xmlNode5, out bodyPropertyMin))
									{
										Debug.FailedAssert("cannot read body properties", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.Core\\BasicCharacterObject.cs", "Deserialize", 509);
									}
								}
								else if (xmlNode5.Name == "BodyPropertiesMax")
								{
									if (!BodyProperties.FromXmlNode(xmlNode5, out bodyProperties))
									{
										bodyPropertyMin = bodyProperties;
										Debug.FailedAssert("cannot read max body properties", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.Core\\BasicCharacterObject.cs", "Deserialize", 518);
									}
								}
								else if (xmlNode5.Name == "face_key_template")
								{
									MBBodyProperty bodyPropertyRange = objectManager.ReadObjectReferenceFromXml<MBBodyProperty>("value", xmlNode5);
									this.BodyPropertyRange = bodyPropertyRange;
								}
							}
							continue;
						}
					}
					if (xmlNode2.Name == "Resistances" || xmlNode2.Name == "resistances")
					{
						this.KnockbackResistance = XmlHelper.ReadFloat(xmlNode2, "knockback", 25f) * 0.01f;
						this.KnockbackResistance = MBMath.ClampFloat(this.KnockbackResistance, 0f, 1f);
						this.KnockdownResistance = XmlHelper.ReadFloat(xmlNode2, "knockdown", 50f) * 0.01f;
						this.KnockdownResistance = MBMath.ClampFloat(this.KnockdownResistance, 0f, 1f);
						this.DismountResistance = XmlHelper.ReadFloat(xmlNode2, "dismount", 50f) * 0.01f;
						this.DismountResistance = MBMath.ClampFloat(this.DismountResistance, 0f, 1f);
					}
				}
			}
			if (this.BodyPropertyRange == null)
			{
				this.BodyPropertyRange = MBObjectManager.Instance.RegisterPresumedObject<MBBodyProperty>(new MBBodyProperty(base.StringId));
				this.BodyPropertyRange.Init(bodyPropertyMin, bodyProperties);
			}
			this.IsFemale = false;
			this.DefaultFormationGroup = 0;
			XmlNode xmlNode9 = node.Attributes["is_female"];
			if (xmlNode9 != null)
			{
				this.IsFemale = Convert.ToBoolean(xmlNode9.InnerText);
			}
			this.Culture = objectManager.ReadObjectReferenceFromXml<BasicCultureObject>("culture", node);
			XmlNode xmlNode10 = node.Attributes["age"];
			this.Age = ((xmlNode10 == null) ? MathF.Max(20f, this.BodyPropertyRange.BodyPropertyMax.Age) : ((float)Convert.ToInt32(xmlNode10.InnerText)));
			XmlNode xmlNode11 = node.Attributes["level"];
			this.Level = ((xmlNode11 != null) ? Convert.ToInt32(xmlNode11.InnerText) : 1);
			XmlNode xmlNode12 = node.Attributes["default_group"];
			if (xmlNode12 != null)
			{
				this.DefaultFormationGroup = this.FetchDefaultFormationGroup(xmlNode12.InnerText);
			}
			this.DefaultFormationClass = (FormationClass)this.DefaultFormationGroup;
			this._isRanged = this.DefaultFormationClass.IsRanged();
			this._isMounted = this.DefaultFormationClass.IsMounted();
			XmlNode xmlNode13 = node.Attributes["formation_position_preference"];
			this.FormationPositionPreference = ((xmlNode13 != null) ? ((FormationPositionPreference)Enum.Parse(typeof(FormationPositionPreference), xmlNode13.InnerText)) : FormationPositionPreference.Middle);
			bool flag = !string.IsNullOrEmpty(text);
			bool flag2 = !string.IsNullOrEmpty(text2);
			bool flag3 = !string.IsNullOrEmpty(text3);
			if (flag || flag2 || flag3)
			{
				if (this.BodyPropertyRange.HairTags != text || this.BodyPropertyRange.BeardTags != text2 || this.BodyPropertyRange.TattooTags != text3)
				{
					this.BodyPropertyRange = MBBodyProperty.CreateFrom(this.BodyPropertyRange);
				}
				if (flag)
				{
					this.BodyPropertyRange.HairTags = text;
				}
				if (flag2)
				{
					this.BodyPropertyRange.BeardTags = text2;
				}
				if (flag3)
				{
					this.BodyPropertyRange.TattooTags = text3;
				}
			}
			XmlNode xmlNode14 = node.Attributes["default_equipment_set"];
			if (xmlNode14 != null)
			{
				this._equipmentRoster.InitializeDefaultEquipment(xmlNode14.Value);
			}
			MBEquipmentRoster equipmentRoster = this._equipmentRoster;
			if (equipmentRoster == null)
			{
				return;
			}
			equipmentRoster.OrderEquipments();
		}

		// Token: 0x0600015C RID: 348 RVA: 0x000064C4 File Offset: 0x000046C4
		protected void AddEquipment(MBEquipmentRoster equipmentRoster, Equipment.EquipmentType equipmentType)
		{
			this._equipmentRoster.AddEquipmentRoster(equipmentRoster, equipmentType);
		}

		// Token: 0x0600015D RID: 349 RVA: 0x000064D4 File Offset: 0x000046D4
		protected int FetchDefaultFormationGroup(string innerText)
		{
			FormationClass result;
			if (Enum.TryParse<FormationClass>(innerText, true, out result))
			{
				return (int)result;
			}
			return -1;
		}

		// Token: 0x0600015E RID: 350 RVA: 0x000064EF File Offset: 0x000046EF
		public virtual FormationClass GetFormationClass()
		{
			return this.DefaultFormationClass;
		}

		// Token: 0x0600015F RID: 351 RVA: 0x000064F7 File Offset: 0x000046F7
		internal static void AutoGeneratedStaticCollectObjectsBasicCharacterObject(object o, List<object> collectedObjects)
		{
			((BasicCharacterObject)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
		}

		// Token: 0x06000160 RID: 352 RVA: 0x00006505 File Offset: 0x00004705
		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
		{
			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
		}

		// Token: 0x04000132 RID: 306
		public static readonly int SkillAffectingMaxLevel = 32;

		// Token: 0x04000133 RID: 307
		public const float DefaultKnockbackResistance = 25f;

		// Token: 0x04000134 RID: 308
		public const float DefaultKnockdownResistance = 50f;

		// Token: 0x04000135 RID: 309
		public const float DefaultDismountResistance = 50f;

		// Token: 0x04000136 RID: 310
		public const int MaxBattleTier = 7;

		// Token: 0x04000137 RID: 311
		protected TextObject _basicName;

		// Token: 0x0400013B RID: 315
		private bool _isMounted;

		// Token: 0x0400013C RID: 316
		private bool _isRanged;

		// Token: 0x04000144 RID: 324
		private MBEquipmentRoster _equipmentRoster;

		// Token: 0x04000147 RID: 327
		private BasicCultureObject _culture;

		// Token: 0x04000148 RID: 328
		[CachedData]
		private float _age;

		// Token: 0x0400014A RID: 330
		[CachedData]
		private bool _isBasicHero;

		// Token: 0x0400014C RID: 332
		protected MBCharacterSkills DefaultCharacterSkills;
	}
}
