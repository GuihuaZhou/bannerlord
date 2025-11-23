using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem
{
	// Token: 0x0200008D RID: 141
	public static class HeroCreator
	{
		// Token: 0x0600123B RID: 4667 RVA: 0x000534B4 File Offset: 0x000516B4
		public static Hero CreateNotable(Occupation occupation, Settlement settlement = null)
		{
			CharacterObject randomTemplateByOccupation = Campaign.Current.Models.HeroCreationModel.GetRandomTemplateByOccupation(occupation, settlement);
			ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(randomTemplateByOccupation, true, -1);
			CampaignTime item = birthAndDeathDay.Item1;
			CampaignTime item2 = birthAndDeathDay.Item2;
			Hero hero = HeroCreator.CreateHero(randomTemplateByOccupation, true, item, item2);
			HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, false).SetGenerateFirstAndFullName(true);
			if (settlement != null)
			{
				heroInitializationArgs.SetBornSettlement(settlement);
			}
			heroInitializationArgs.SetAppearance(new StaticBodyProperties?(Campaign.Current.Models.HeroCreationModel.GetStaticBodyProperties(hero, false, 0f)), -1f, -1f, -1, -1, -1);
			HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
			return hero;
		}

		// Token: 0x0600123C RID: 4668 RVA: 0x00053568 File Offset: 0x00051768
		public static Hero CreateSpecialHero(CharacterObject template, Settlement bornSettlement = null, Clan faction = null, Clan supporterOfClan = null, int age = -1)
		{
			ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(template, true, age);
			CampaignTime item = birthAndDeathDay.Item1;
			CampaignTime item2 = birthAndDeathDay.Item2;
			Hero hero = HeroCreator.CreateHero(template, true, item, item2);
			HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, false).SetGenerateFirstAndFullName(true);
			if (bornSettlement != null)
			{
				heroInitializationArgs.SetBornSettlement(bornSettlement);
			}
			if (faction != null)
			{
				heroInitializationArgs.SetClan(faction);
			}
			if (supporterOfClan != null)
			{
				heroInitializationArgs.SetSupporterOf(supporterOfClan);
			}
			HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
			return hero;
		}

		// Token: 0x0600123D RID: 4669 RVA: 0x000535E0 File Offset: 0x000517E0
		public static Hero CreateChild(CharacterObject template, Settlement bornSettlement, Clan clan, int age)
		{
			ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(template, true, age);
			CampaignTime item = birthAndDeathDay.Item1;
			CampaignTime item2 = birthAndDeathDay.Item2;
			Hero hero = HeroCreator.CreateHero(template, true, item, item2);
			HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, false).SetGenerateFirstAndFullName(true).SetBornSettlement(bornSettlement).SetClan(clan).SetLevel(1);
			HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
			return hero;
		}

		// Token: 0x0600123E RID: 4670 RVA: 0x00053648 File Offset: 0x00051848
		public static Hero CreateRelativeNotableHero(Hero relative)
		{
			CharacterObject randomTemplateByOccupation = Campaign.Current.Models.HeroCreationModel.GetRandomTemplateByOccupation(relative.Occupation, relative.HomeSettlement);
			ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(randomTemplateByOccupation, true, -1);
			CampaignTime item = birthAndDeathDay.Item1;
			CampaignTime item2 = birthAndDeathDay.Item2;
			Hero hero = HeroCreator.CreateHero(randomTemplateByOccupation, true, item, item2);
			BodyProperties bodyPropertiesMin = relative.CharacterObject.GetBodyPropertiesMin(false);
			BodyProperties bodyPropertiesMin2 = randomTemplateByOccupation.GetBodyPropertiesMin(false);
			int defaultFaceSeed = relative.CharacterObject.GetDefaultFaceSeed(1);
			MBBodyProperty bodyPropertyRange = hero.CharacterObject.BodyPropertyRange;
			BodyProperties randomBodyProperties = BodyProperties.GetRandomBodyProperties(randomTemplateByOccupation.Race, randomTemplateByOccupation.IsFemale, bodyPropertiesMin, bodyPropertiesMin2, 1, defaultFaceSeed, bodyPropertyRange.HairTags, bodyPropertyRange.BeardTags, bodyPropertyRange.TattooTags, 0f);
			HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, false).SetBornSettlement(relative.HomeSettlement).SetCulture(relative.Culture).SetAppearance(new StaticBodyProperties?(randomBodyProperties.StaticProperties), -1f, -1f, -1, -1, -1).SetGenerateFirstAndFullName(true);
			HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
			return hero;
		}

		// Token: 0x0600123F RID: 4671 RVA: 0x00053758 File Offset: 0x00051958
		public static bool CreateBasicHero(string stringId, CharacterObject character, out Hero hero, bool isAlive = true)
		{
			hero = Campaign.Current.CampaignObjectManager.Find<Hero>(stringId);
			if (hero == null)
			{
				ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(character, isAlive, (int)character.Age);
				CampaignTime item = birthAndDeathDay.Item1;
				CampaignTime item2 = birthAndDeathDay.Item2;
				hero = HeroCreator.CreateHero(character, false, item, item2);
				HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, false);
				HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
				return true;
			}
			return false;
		}

		// Token: 0x06001240 RID: 4672 RVA: 0x000537C8 File Offset: 0x000519C8
		public static Hero DeliverOffSpring(Hero mother, Hero father, bool isOffspringFemale)
		{
			Debug.SilentAssert(mother.CharacterObject.Race == father.CharacterObject.Race, "", false, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\HeroCreator.cs", "DeliverOffSpring", 275);
			CharacterObject characterTemplateForOffspring = Campaign.Current.Models.HeroCreationModel.GetCharacterTemplateForOffspring(mother, father, isOffspringFemale);
			ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(characterTemplateForOffspring, true, 0);
			CampaignTime item = birthAndDeathDay.Item1;
			CampaignTime item2 = birthAndDeathDay.Item2;
			Hero hero = HeroCreator.CreateHero(characterTemplateForOffspring, true, item, item2);
			HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, true).SetMother(mother).SetFather(father).SetIsFemale(isOffspringFemale).SetOccupation(isOffspringFemale ? mother.Occupation : father.Occupation).SetLevel(1).SetGenerateFirstAndFullName(true);
			if (mother == Hero.MainHero || father == Hero.MainHero)
			{
				heroInitializationArgs.SetClan(Hero.MainHero.Clan).SetCulture(Hero.MainHero.Culture);
			}
			else
			{
				CultureObject culture = (MBRandom.RandomFloat < 0.5f) ? father.Culture : mother.Culture;
				heroInitializationArgs.SetClan(father.Clan).SetCulture(culture);
			}
			HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
			return hero;
		}

		// Token: 0x06001241 RID: 4673 RVA: 0x000538F8 File Offset: 0x00051AF8
		private static Hero CreateHero(CharacterObject character, bool useCharacterAsTemplate, CampaignTime birthDay, CampaignTime deathDay)
		{
			if (useCharacterAsTemplate)
			{
				Debug.Print("creating hero from template with id: " + character.StringId, 0, Debug.DebugColor.White, 17592186044416UL);
				character = CharacterObject.CreateFrom(character, null);
			}
			else
			{
				Debug.Print("creating hero for character with id: " + character.StringId, 0, Debug.DebugColor.White, 17592186044416UL);
			}
			return new Hero(character.StringId, character, birthDay, deathDay);
		}

		// Token: 0x06001242 RID: 4674 RVA: 0x0005396C File Offset: 0x00051B6C
		private static void InitializeHeroFromSettings(Hero hero, HeroCreator.HeroInitializationArgs initializationArgs)
		{
			hero.Mother = initializationArgs.Mother;
			hero.Father = initializationArgs.Father;
			hero.IsFemale = initializationArgs.IsFemale;
			hero.BornSettlement = (initializationArgs.HasBornSettlementBeenSet ? initializationArgs.BornSettlement : Campaign.Current.Models.HeroCreationModel.GetBornSettlement(hero));
			hero.PreferredUpgradeFormation = (initializationArgs.PreferredUpgradeFormation ?? Campaign.Current.Models.HeroCreationModel.GetPreferredUpgradeFormation(hero));
			hero.Clan = (initializationArgs.HasClanBeenSet ? initializationArgs.Clan : Campaign.Current.Models.HeroCreationModel.GetClan(hero));
			hero.Culture = (initializationArgs.Culture ?? Campaign.Current.Models.HeroCreationModel.GetCulture(hero, hero.BornSettlement, hero.Clan));
			hero.StaticBodyProperties = (initializationArgs.StaticBodyProperties ?? Campaign.Current.Models.HeroCreationModel.GetStaticBodyProperties(hero, initializationArgs.IsOffspring, 0.35f));
			hero.SupporterOf = initializationArgs.SupporterOf;
			hero.Level = initializationArgs.Level;
			hero.Weight = initializationArgs.Weight;
			hero.Build = initializationArgs.Build;
			if (initializationArgs.GenerateFirstAndFullName)
			{
				ValueTuple<TextObject, TextObject> valueTuple = Campaign.Current.Models.HeroCreationModel.GenerateFirstAndFullName(hero);
				TextObject item = valueTuple.Item1;
				TextObject item2 = valueTuple.Item2;
				hero.SetName(item2, item);
			}
			else
			{
				hero.SetName(initializationArgs.Name, initializationArgs.FirstName);
			}
			if (initializationArgs.Occupation != hero.Occupation)
			{
				hero.SetNewOccupation(initializationArgs.Occupation);
			}
			foreach (ValueTuple<TraitObject, int> valueTuple2 in Campaign.Current.Models.HeroCreationModel.GetTraitsForHero(hero))
			{
				TraitObject item3 = valueTuple2.Item1;
				int item4 = valueTuple2.Item2;
				hero.SetTraitLevel(item3, item4);
			}
			foreach (ValueTuple<SkillObject, int> valueTuple3 in Campaign.Current.Models.HeroCreationModel.GetDefaultSkillsForHero(hero))
			{
				SkillObject item5 = valueTuple3.Item1;
				int item6 = valueTuple3.Item2;
				hero.SetSkillValue(item5, item6);
			}
			if (initializationArgs.IsOffspring)
			{
				hero.HeroDeveloper.InitializeHeroDeveloper();
				hero.ClearTraits();
			}
			else if (hero.Age >= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge)
			{
				hero.HeroDeveloper.InitializeHeroDeveloper();
			}
			Equipment civilianEquipment = Campaign.Current.Models.HeroCreationModel.GetCivilianEquipment(hero);
			EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment);
			Equipment battleEquipment = Campaign.Current.Models.HeroCreationModel.GetBattleEquipment(hero);
			EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
			CampaignEventDispatcher.Instance.OnHeroCreated(initializationArgs.Hero, initializationArgs.IsOffspring);
		}

		// Token: 0x0200053D RID: 1341
		private class HeroInitializationArgs
		{
			// Token: 0x17000ED7 RID: 3799
			// (get) Token: 0x06004C05 RID: 19461 RVA: 0x00178706 File Offset: 0x00176906
			public Hero Hero { get; }

			// Token: 0x17000ED8 RID: 3800
			// (get) Token: 0x06004C06 RID: 19462 RVA: 0x0017870E File Offset: 0x0017690E
			// (set) Token: 0x06004C07 RID: 19463 RVA: 0x00178716 File Offset: 0x00176916
			public TextObject Name { get; private set; }

			// Token: 0x17000ED9 RID: 3801
			// (get) Token: 0x06004C08 RID: 19464 RVA: 0x0017871F File Offset: 0x0017691F
			// (set) Token: 0x06004C09 RID: 19465 RVA: 0x00178727 File Offset: 0x00176927
			public TextObject FirstName { get; private set; }

			// Token: 0x17000EDA RID: 3802
			// (get) Token: 0x06004C0A RID: 19466 RVA: 0x00178730 File Offset: 0x00176930
			// (set) Token: 0x06004C0B RID: 19467 RVA: 0x00178738 File Offset: 0x00176938
			public Hero Mother { get; private set; }

			// Token: 0x17000EDB RID: 3803
			// (get) Token: 0x06004C0C RID: 19468 RVA: 0x00178741 File Offset: 0x00176941
			// (set) Token: 0x06004C0D RID: 19469 RVA: 0x00178749 File Offset: 0x00176949
			public Hero Father { get; private set; }

			// Token: 0x17000EDC RID: 3804
			// (get) Token: 0x06004C0E RID: 19470 RVA: 0x00178752 File Offset: 0x00176952
			// (set) Token: 0x06004C0F RID: 19471 RVA: 0x0017875A File Offset: 0x0017695A
			public bool IsFemale { get; private set; }

			// Token: 0x17000EDD RID: 3805
			// (get) Token: 0x06004C10 RID: 19472 RVA: 0x00178763 File Offset: 0x00176963
			// (set) Token: 0x06004C11 RID: 19473 RVA: 0x0017876B File Offset: 0x0017696B
			public Settlement BornSettlement { get; private set; }

			// Token: 0x17000EDE RID: 3806
			// (get) Token: 0x06004C12 RID: 19474 RVA: 0x00178774 File Offset: 0x00176974
			// (set) Token: 0x06004C13 RID: 19475 RVA: 0x0017877C File Offset: 0x0017697C
			public int Level { get; private set; }

			// Token: 0x17000EDF RID: 3807
			// (get) Token: 0x06004C14 RID: 19476 RVA: 0x00178785 File Offset: 0x00176985
			// (set) Token: 0x06004C15 RID: 19477 RVA: 0x0017878D File Offset: 0x0017698D
			public float Weight { get; private set; }

			// Token: 0x17000EE0 RID: 3808
			// (get) Token: 0x06004C16 RID: 19478 RVA: 0x00178796 File Offset: 0x00176996
			// (set) Token: 0x06004C17 RID: 19479 RVA: 0x0017879E File Offset: 0x0017699E
			public float Build { get; private set; }

			// Token: 0x17000EE1 RID: 3809
			// (get) Token: 0x06004C18 RID: 19480 RVA: 0x001787A7 File Offset: 0x001769A7
			// (set) Token: 0x06004C19 RID: 19481 RVA: 0x001787AF File Offset: 0x001769AF
			public StaticBodyProperties? StaticBodyProperties { get; private set; }

			// Token: 0x17000EE2 RID: 3810
			// (get) Token: 0x06004C1A RID: 19482 RVA: 0x001787B8 File Offset: 0x001769B8
			// (set) Token: 0x06004C1B RID: 19483 RVA: 0x001787C0 File Offset: 0x001769C0
			public FormationClass? PreferredUpgradeFormation { get; private set; }

			// Token: 0x17000EE3 RID: 3811
			// (get) Token: 0x06004C1C RID: 19484 RVA: 0x001787C9 File Offset: 0x001769C9
			// (set) Token: 0x06004C1D RID: 19485 RVA: 0x001787D1 File Offset: 0x001769D1
			public Clan Clan { get; private set; }

			// Token: 0x17000EE4 RID: 3812
			// (get) Token: 0x06004C1E RID: 19486 RVA: 0x001787DA File Offset: 0x001769DA
			// (set) Token: 0x06004C1F RID: 19487 RVA: 0x001787E2 File Offset: 0x001769E2
			public CultureObject Culture { get; private set; }

			// Token: 0x17000EE5 RID: 3813
			// (get) Token: 0x06004C20 RID: 19488 RVA: 0x001787EB File Offset: 0x001769EB
			// (set) Token: 0x06004C21 RID: 19489 RVA: 0x001787F3 File Offset: 0x001769F3
			public Clan SupporterOf { get; private set; }

			// Token: 0x17000EE6 RID: 3814
			// (get) Token: 0x06004C22 RID: 19490 RVA: 0x001787FC File Offset: 0x001769FC
			// (set) Token: 0x06004C23 RID: 19491 RVA: 0x00178804 File Offset: 0x00176A04
			public Occupation Occupation { get; private set; }

			// Token: 0x17000EE7 RID: 3815
			// (get) Token: 0x06004C24 RID: 19492 RVA: 0x0017880D File Offset: 0x00176A0D
			// (set) Token: 0x06004C25 RID: 19493 RVA: 0x00178815 File Offset: 0x00176A15
			public bool IsOffspring { get; private set; }

			// Token: 0x17000EE8 RID: 3816
			// (get) Token: 0x06004C26 RID: 19494 RVA: 0x0017881E File Offset: 0x00176A1E
			// (set) Token: 0x06004C27 RID: 19495 RVA: 0x00178826 File Offset: 0x00176A26
			public bool GenerateFirstAndFullName { get; private set; }

			// Token: 0x17000EE9 RID: 3817
			// (get) Token: 0x06004C28 RID: 19496 RVA: 0x0017882F File Offset: 0x00176A2F
			// (set) Token: 0x06004C29 RID: 19497 RVA: 0x00178837 File Offset: 0x00176A37
			public bool HasBornSettlementBeenSet { get; private set; }

			// Token: 0x17000EEA RID: 3818
			// (get) Token: 0x06004C2A RID: 19498 RVA: 0x00178840 File Offset: 0x00176A40
			// (set) Token: 0x06004C2B RID: 19499 RVA: 0x00178848 File Offset: 0x00176A48
			public bool HasClanBeenSet { get; private set; }

			// Token: 0x06004C2C RID: 19500 RVA: 0x00178854 File Offset: 0x00176A54
			public HeroInitializationArgs(Hero hero, bool isOffspring)
			{
				DynamicBodyProperties dynamicBodyPropertiesBetweenMinMaxRange = CharacterHelper.GetDynamicBodyPropertiesBetweenMinMaxRange(hero.CharacterObject);
				this.Hero = hero;
				this.IsOffspring = isOffspring;
				this.Name = hero.Name;
				this.FirstName = hero.FirstName;
				this.Mother = hero.Mother;
				this.Father = hero.Father;
				this.IsFemale = hero.IsFemale;
				this.BornSettlement = null;
				this.Level = hero.Level;
				this.Weight = dynamicBodyPropertiesBetweenMinMaxRange.Weight;
				this.Build = dynamicBodyPropertiesBetweenMinMaxRange.Build;
				this.StaticBodyProperties = null;
				this.PreferredUpgradeFormation = null;
				this.Clan = null;
				this.SupporterOf = hero.SupporterOf;
				this.Occupation = hero.Occupation;
				this.Culture = null;
			}

			// Token: 0x06004C2D RID: 19501 RVA: 0x0017892C File Offset: 0x00176B2C
			public HeroCreator.HeroInitializationArgs SetGenerateFirstAndFullName(bool value)
			{
				this.GenerateFirstAndFullName = value;
				return this;
			}

			// Token: 0x06004C2E RID: 19502 RVA: 0x00178936 File Offset: 0x00176B36
			public HeroCreator.HeroInitializationArgs SetName(TextObject name)
			{
				this.Name = name;
				return this;
			}

			// Token: 0x06004C2F RID: 19503 RVA: 0x00178940 File Offset: 0x00176B40
			public HeroCreator.HeroInitializationArgs SetFirstName(TextObject firstName)
			{
				this.FirstName = firstName;
				return this;
			}

			// Token: 0x06004C30 RID: 19504 RVA: 0x0017894A File Offset: 0x00176B4A
			public HeroCreator.HeroInitializationArgs SetMother(Hero mother)
			{
				this.Mother = mother;
				return this;
			}

			// Token: 0x06004C31 RID: 19505 RVA: 0x00178954 File Offset: 0x00176B54
			public HeroCreator.HeroInitializationArgs SetFather(Hero father)
			{
				this.Father = father;
				return this;
			}

			// Token: 0x06004C32 RID: 19506 RVA: 0x0017895E File Offset: 0x00176B5E
			public HeroCreator.HeroInitializationArgs SetIsFemale(bool isFemale)
			{
				this.IsFemale = isFemale;
				return this;
			}

			// Token: 0x06004C33 RID: 19507 RVA: 0x00178968 File Offset: 0x00176B68
			public HeroCreator.HeroInitializationArgs SetBornSettlement(Settlement bornSettlement)
			{
				this.BornSettlement = bornSettlement;
				this.HasBornSettlementBeenSet = true;
				return this;
			}

			// Token: 0x06004C34 RID: 19508 RVA: 0x00178979 File Offset: 0x00176B79
			public HeroCreator.HeroInitializationArgs SetLevel(int level)
			{
				this.Level = level;
				return this;
			}

			// Token: 0x06004C35 RID: 19509 RVA: 0x00178984 File Offset: 0x00176B84
			public HeroCreator.HeroInitializationArgs SetAppearance(StaticBodyProperties? staticBodyProperties, float weight = -1f, float build = -1f, int hair = -1, int beard = -1, int tattoo = -1)
			{
				if (weight > 0f)
				{
					this.Weight = weight;
				}
				if (build > 0f)
				{
					this.Build = build;
				}
				BodyProperties bodyProperties = new BodyProperties(new DynamicBodyProperties(this.Hero.Age, this.Weight, this.Build), staticBodyProperties ?? default(StaticBodyProperties));
				FaceGen.SetHair(ref bodyProperties, hair, beard, tattoo);
				this.StaticBodyProperties = new StaticBodyProperties?(bodyProperties.StaticProperties);
				return this;
			}

			// Token: 0x06004C36 RID: 19510 RVA: 0x00178A0F File Offset: 0x00176C0F
			public HeroCreator.HeroInitializationArgs SetPreferredUpgradeFormation(FormationClass preferredUpgradeFormation)
			{
				this.PreferredUpgradeFormation = new FormationClass?(preferredUpgradeFormation);
				return this;
			}

			// Token: 0x06004C37 RID: 19511 RVA: 0x00178A1E File Offset: 0x00176C1E
			public HeroCreator.HeroInitializationArgs SetClan(Clan clan)
			{
				this.Clan = clan;
				this.HasClanBeenSet = true;
				return this;
			}

			// Token: 0x06004C38 RID: 19512 RVA: 0x00178A2F File Offset: 0x00176C2F
			public HeroCreator.HeroInitializationArgs SetCulture(CultureObject culture)
			{
				this.Culture = culture;
				return this;
			}

			// Token: 0x06004C39 RID: 19513 RVA: 0x00178A39 File Offset: 0x00176C39
			public HeroCreator.HeroInitializationArgs SetSupporterOf(Clan supporterOf)
			{
				this.SupporterOf = supporterOf;
				return this;
			}

			// Token: 0x06004C3A RID: 19514 RVA: 0x00178A43 File Offset: 0x00176C43
			public HeroCreator.HeroInitializationArgs SetOccupation(Occupation occupation)
			{
				this.Occupation = occupation;
				return this;
			}
		}
	}
}
