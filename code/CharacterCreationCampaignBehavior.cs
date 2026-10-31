using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x020003DC RID: 988
	public class CharacterCreationCampaignBehavior : CampaignBehaviorBase, ICharacterCreationContentHandler
	{
		// Token: 0x06003B9C RID: 15260 RVA: 0x000FB240 File Offset: 0x000F9440
		private string GetMotherEquipmentId(CharacterCreationManager characterCreationManager, string occupationType, string cultureId)
		{
			string str;
			characterCreationManager.CharacterCreationContent.TryGetEquipmentToUse(occupationType, out str);
			return "mother_char_creation_" + str + "_" + cultureId;
		}

		// Token: 0x06003B9D RID: 15261 RVA: 0x000FB270 File Offset: 0x000F9470
		private string GetFatherEquipmentId(CharacterCreationManager characterCreationManager, string occupationType, string cultureId)
		{
			string str;
			characterCreationManager.CharacterCreationContent.TryGetEquipmentToUse(occupationType, out str);
			return "father_char_creation_" + str + "_" + cultureId;
		}

		// Token: 0x06003B9E RID: 15262 RVA: 0x000FB2A0 File Offset: 0x000F94A0
		private string GetPlayerChildhoodAgeEquipmentId(CharacterCreationManager characterCreationManager, string parentOccupationType, string cultureId, bool isFemale)
		{
			string text;
			characterCreationManager.CharacterCreationContent.TryGetEquipmentToUse(parentOccupationType, out text);
			return string.Concat(new string[]
			{
				"player_char_creation_childhood_age_",
				cultureId,
				"_",
				text,
				"_",
				isFemale ? "f" : "m"
			});
		}

		// Token: 0x06003B9F RID: 15263 RVA: 0x000FB2FC File Offset: 0x000F94FC
		private string GetPlayerEducationAgeEquipmentId(CharacterCreationManager characterCreationManager, string parentOccupationType, string cultureId, bool isFemale)
		{
			string text;
			characterCreationManager.CharacterCreationContent.TryGetEquipmentToUse(parentOccupationType, out text);
			return string.Concat(new string[]
			{
				"player_char_creation_education_age_",
				cultureId,
				"_",
				text,
				"_",
				isFemale ? "f" : "m"
			});
		}

		// Token: 0x06003BA0 RID: 15264 RVA: 0x000FB358 File Offset: 0x000F9558
		private string GetPlayerEquipmentId(CharacterCreationManager characterCreationManager, string occupationType, string cultureId, bool isFemale)
		{
			string text;
			characterCreationManager.CharacterCreationContent.TryGetEquipmentToUse(occupationType, out text);
			return string.Concat(new string[]
			{
				"player_char_creation_",
				cultureId,
				"_",
				text,
				"_",
				isFemale ? "f" : "m"
			});
		}

		// Token: 0x06003BA1 RID: 15265 RVA: 0x000FB3B2 File Offset: 0x000F95B2
		public override void RegisterEvents()
		{
			CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(this, new Action<CharacterCreationManager>(this.OnCharacterCreationInitialized));
		}

		// Token: 0x06003BA2 RID: 15266 RVA: 0x000FB3CB File Offset: 0x000F95CB
		public override void SyncData(IDataStore dataStore)
		{
		}

		// Token: 0x06003BA3 RID: 15267 RVA: 0x000FB3D0 File Offset: 0x000F95D0
		private void OnCharacterCreationInitialized(CharacterCreationManager characterCreationManager)
		{
			this._focusToAdd = characterCreationManager.CharacterCreationContent.FocusToAdd;
			this._skillLevelToAdd = characterCreationManager.CharacterCreationContent.SkillLevelToAdd;
			this._attributeLevelToAdd = characterCreationManager.CharacterCreationContent.AttributeLevelToAdd;
			characterCreationManager.CharacterCreationContent.DefaultSelectedTitleType = "guard";
			characterCreationManager.RegisterCharacterCreationContentHandler(this, 800);
		}

		// Token: 0x06003BA4 RID: 15268 RVA: 0x000FB42C File Offset: 0x000F962C
		void ICharacterCreationContentHandler.InitializeContent(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.AddEquipmentToUseGetter(delegate(string occupationId, out string equipmentId)
			{
				return this._occupationToEquipmentMapping.TryGetValue(occupationId, out equipmentId);
			});
			this.InitializeCharacterCreationStages(characterCreationManager);
			this.InitializeCharacterCreationCultures(characterCreationManager);
			this.InitializeData(characterCreationManager);
		}

		// Token: 0x06003BA5 RID: 15269 RVA: 0x000FB45A File Offset: 0x000F965A
		void ICharacterCreationContentHandler.AfterInitializeContent(CharacterCreationManager characterCreationManager)
		{
		}

		// Token: 0x06003BA6 RID: 15270 RVA: 0x000FB45C File Offset: 0x000F965C
		void ICharacterCreationContentHandler.OnStageCompleted(CharacterCreationStageBase stage)
		{
			if (stage is CharacterCreationFaceGeneratorStage)
			{
				this.FaceGenUpdated();
			}
		}

		// Token: 0x06003BA7 RID: 15271 RVA: 0x000FB46C File Offset: 0x000F966C
		void ICharacterCreationContentHandler.OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
		{
		}

		// Token: 0x06003BA8 RID: 15272 RVA: 0x000FB470 File Offset: 0x000F9670
		public void InitializeCharacterCreationStages(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.AddStage(new CharacterCreationCultureStage());
			characterCreationManager.AddStage(new CharacterCreationFaceGeneratorStage());
			characterCreationManager.AddStage(new CharacterCreationNarrativeStage());
			characterCreationManager.AddStage(new CharacterCreationBannerEditorStage());
			characterCreationManager.AddStage(new CharacterCreationClanNamingStage());
			characterCreationManager.AddStage(new CharacterCreationReviewStage());
			characterCreationManager.AddStage(new CharacterCreationOptionsStage());
		}

		// Token: 0x06003BA9 RID: 15273 RVA: 0x000FB4CC File Offset: 0x000F96CC
		public void InitializeCharacterCreationCultures(CharacterCreationManager characterCreationManager)
		{
			foreach (CultureObject cultureObject in Game.Current.ObjectManager.GetObjectTypeList<CultureObject>())
			{
				if (cultureObject.StringId == "aserai" || cultureObject.StringId == "battania" || cultureObject.StringId == "empire" || cultureObject.StringId == "khuzait" || cultureObject.StringId == "sturgia" || cultureObject.StringId == "vlandia")
				{
					characterCreationManager.CharacterCreationContent.AddCharacterCreationCulture(cultureObject, 1, 10);
				}
			}
		}

		// Token: 0x06003BAA RID: 15274 RVA: 0x000FB5A4 File Offset: 0x000F97A4
		public void InitializeData(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.ChangeReviewPageDescription(new TextObject("{=W6pKpEoT}You prepare to set off for a grand adventure in Calradia! Here is your character. Continue if you are ready, or go back to make changes.", null));
			this.AddParentsMenu(characterCreationManager);
			this.AddChildhoodMenu(characterCreationManager);
			this.AddEducationMenu(characterCreationManager);
			this.AddYouthMenu(characterCreationManager);
			this.AddAdulthoodMenu(characterCreationManager);
			this.AddAgeSelectionMenu(characterCreationManager);
		}

		// Token: 0x06003BAB RID: 15275 RVA: 0x000FB5F4 File Offset: 0x000F97F4
		public void FaceGenUpdated()
		{
			CharacterCreationManager characterCreationManager = (GameStateManager.Current.ActiveState as CharacterCreationState).CharacterCreationManager;
			BodyProperties bodyProperties2;
			BodyProperties bodyProperties;
			FaceGen.GenerateParentKey(bodyProperties = (bodyProperties2 = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1)), CharacterObject.PlayerCharacter.Race, ref bodyProperties2, ref bodyProperties);
			bodyProperties2 = new BodyProperties(new DynamicBodyProperties(33f, 0.3f, 0.2f), bodyProperties2.StaticProperties);
			bodyProperties = new BodyProperties(new DynamicBodyProperties(33f, 0.5f, 0.5f), bodyProperties.StaticProperties);
			foreach (NarrativeMenu narrativeMenu in characterCreationManager.NarrativeMenus)
			{
				foreach (NarrativeMenuCharacter narrativeMenuCharacter in narrativeMenu.Characters)
				{
					if (narrativeMenuCharacter.StringId.Equals("mother_character"))
					{
						narrativeMenuCharacter.UpdateBodyProperties(bodyProperties2, CharacterObject.PlayerCharacter.Race, true);
					}
					if (narrativeMenuCharacter.StringId.Equals("father_character"))
					{
						narrativeMenuCharacter.UpdateBodyProperties(bodyProperties, CharacterObject.PlayerCharacter.Race, false);
					}
					if (narrativeMenuCharacter.StringId.Equals("player_childhood_character") || narrativeMenuCharacter.StringId.Equals("player_education_character") || narrativeMenuCharacter.StringId.Equals("player_youth_character") || narrativeMenuCharacter.StringId.Equals("player_adulthood_character") || narrativeMenuCharacter.StringId.Equals("player_age_selection_character"))
					{
						narrativeMenuCharacter.UpdateBodyProperties(CharacterObject.PlayerCharacter.GetBodyProperties(null, -1), CharacterObject.PlayerCharacter.Race, false);
					}
				}
			}
		}

		// Token: 0x06003BAC RID: 15276 RVA: 0x000FB7EC File Offset: 0x000F99EC
		private List<NarrativeMenuCharacterArgs> GetParentMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			return new List<NarrativeMenuCharacterArgs>
			{
				new NarrativeMenuCharacterArgs("mother_character", 33, "mother_char_creation_none_" + characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, "act_character_creation_female_default_standing", "spawnpoint_player_1", "", "", null, true, true),
				new NarrativeMenuCharacterArgs("father_character", 33, "father_char_creation_none_" + characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, "act_character_creation_male_default_standing", "spawnpoint_player_1", "", "", null, true, false)
			};
		}

		// Token: 0x06003BAD RID: 15277 RVA: 0x000FB884 File Offset: 0x000F9A84
		private void AddParentsMenu(CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			BodyProperties bodyProperties2;
			BodyProperties bodyProperties;
			FaceGen.GenerateParentKey(bodyProperties = (bodyProperties2 = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1)), CharacterObject.PlayerCharacter.Race, ref bodyProperties2, ref bodyProperties);
			bodyProperties2 = new BodyProperties(new DynamicBodyProperties(33f, 0.3f, 0.2f), bodyProperties2.StaticProperties);
			bodyProperties = new BodyProperties(new DynamicBodyProperties(33f, 0.5f, 0.5f), bodyProperties.StaticProperties);
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("mother_character", bodyProperties2, CharacterObject.PlayerCharacter.Race, true);
			list.Add(item);
			NarrativeMenuCharacter item2 = new NarrativeMenuCharacter("father_character", bodyProperties, CharacterObject.PlayerCharacter.Race, false);
			list.Add(item2);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_parent_menu", "start", "narrative_childhood_menu", new TextObject("{=b4lDDcli}Family", null), new TextObject("{=XgFU1pCx}You were born into a family of...", null), list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetParentMenuNarrativeMenuCharacterArgs));
			this.AddEmpireParentNarrativeMenuOptions(narrativeMenu);
			this.AddVlandianParentNarrativeMenuOptions(narrativeMenu);
			this.AddSturgianParentNarrativeMenuOptions(narrativeMenu);
			this.AddAseraiParentNarrativeMenuOptions(narrativeMenu);
			this.AddBattaniaNarrativeMenuOptions(narrativeMenu);
			this.AddKhuzaitNarrativeMenuOptions(narrativeMenu);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003BAE RID: 15278 RVA: 0x000FB9B8 File Offset: 0x000F9BB8
		private void AddEmpireParentNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("empire_lanlord_option", new TextObject("{=InN5ZZt3}A landlord's retainers", null), new TextObject("{=ivKl4mV2}Your father was a trusted lieutenant of the local landowning aristocrat. He rode with the lord's cavalry, fighting as an armored lancer.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireLandlordNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireLandlordNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireLandlordNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("empire_merchant_option", new TextObject("{=651FhzdR}Urban merchants", null), new TextObject("{=FQntPChs}Your family were merchants in one of the main cities of the Empire. They sometimes organized caravans to nearby towns, and discussed issues in the town council.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireUrbanNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireUrbanNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireUrbanNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("empire_farmer_option", new TextObject("{=sb4gg8Ak}Freeholders", null), new TextObject("{=09z8Q08f}Your family were small farmers with just enough land to feed themselves and make a small profit. People like them were the pillars of the imperial rural economy, as well as the backbone of the levy.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("empire_artisan_option", new TextObject("{=v48N6h1t}Urban artisans", null), new TextObject("{=ueCm5y1C}Your family owned their own workshop in a city, making goods from raw materials brought in from the countryside. Your father played an active if minor role in the town council, and also served in the militia.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireArtisanNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireArtisanNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireArtisanNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("empire_hunter_option", new TextObject("{=7eWmU2mF}Foresters", null), new TextObject("{=yRFSzSDZ}Your family lived in a village, but did not own their own land. Instead, your father supplemented paid jobs with long trips in the woods, hunting and trapping, always keeping a wary eye for the lord's game wardens.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireHunterNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireHunterNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireHunterNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("empire_vagabond_option", new TextObject("{=aEke8dSb}Urban vagabonds", null), new TextObject("{=Jvf6K7TZ}Your family numbered among the many poor migrants living in the slums that grow up outside the walls of imperial cities, making whatever money they could from a variety of odd jobs. Sometimes they did service for one of the Empire's many criminal gangs, and you had an early look at the dark side of life.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEmpireVagabondNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EmpireVagabondNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EmpireVagabondNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003BAF RID: 15279 RVA: 0x000FBB98 File Offset: 0x000F9D98
		private void GetEmpireLandlordNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003BB0 RID: 15280 RVA: 0x000FBBEC File Offset: 0x000F9DEC
		private bool EmpireLandlordNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BB1 RID: 15281 RVA: 0x000FBC08 File Offset: 0x000F9E08
		private void EmpireLandlordNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BB2 RID: 15282 RVA: 0x000FBCA8 File Offset: 0x000F9EA8
		private void GetEmpireUrbanNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003BB3 RID: 15283 RVA: 0x000FBCFC File Offset: 0x000F9EFC
		private bool EmpireUrbanNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BB4 RID: 15284 RVA: 0x000FBD18 File Offset: 0x000F9F18
		private void EmpireUrbanNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("merchant_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BB5 RID: 15285 RVA: 0x000FBDB8 File Offset: 0x000F9FB8
		private void GetEmpireFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003BB6 RID: 15286 RVA: 0x000FBE0C File Offset: 0x000FA00C
		private bool EmpireFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BB7 RID: 15287 RVA: 0x000FBE28 File Offset: 0x000FA028
		private void EmpireFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BB8 RID: 15288 RVA: 0x000FBEC8 File Offset: 0x000FA0C8
		private void GetEmpireArtisanNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crafting,
				DefaultSkills.Crossbow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003BB9 RID: 15289 RVA: 0x000FBF1C File Offset: 0x000FA11C
		private bool EmpireArtisanNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BBA RID: 15290 RVA: 0x000FBF38 File Offset: 0x000FA138
		private void EmpireArtisanNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("artisan_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BBB RID: 15291 RVA: 0x000FBFD8 File Offset: 0x000FA1D8
		private void GetEmpireHunterNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003BBC RID: 15292 RVA: 0x000FC02C File Offset: 0x000FA22C
		private bool EmpireHunterNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BBD RID: 15293 RVA: 0x000FC048 File Offset: 0x000FA248
		private void EmpireHunterNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("hunter");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BBE RID: 15294 RVA: 0x000FC0E8 File Offset: 0x000FA2E8
		private void GetEmpireVagabondNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003BBF RID: 15295 RVA: 0x000FC13C File Offset: 0x000FA33C
		private bool EmpireVagabondNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003BC0 RID: 15296 RVA: 0x000FC158 File Offset: 0x000FA358
		private void EmpireVagabondNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("vagabond_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BC1 RID: 15297 RVA: 0x000FC1F8 File Offset: 0x000FA3F8
		public void UpdateParentEquipment(CharacterCreationManager characterCreationManager, MBEquipmentRoster motherEquipment, MBEquipmentRoster fatherEquipment, string motherAnimation, string fatherAnimation)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId.Equals("mother_character"))
				{
					narrativeMenuCharacter.SetEquipment(motherEquipment);
					narrativeMenuCharacter.SetAnimationId(motherAnimation);
				}
				if (narrativeMenuCharacter.StringId.Equals("father_character"))
				{
					narrativeMenuCharacter.SetEquipment(fatherEquipment);
					narrativeMenuCharacter.SetAnimationId(fatherAnimation);
				}
			}
		}

		// Token: 0x06003BC2 RID: 15298 RVA: 0x000FC28C File Offset: 0x000FA48C
		private void AddVlandianParentNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("vlandia_retainer_option", new TextObject("{=2TptWc4m}A baron's retainers", null), new TextObject("{=0Suu1Q9q}Your father was a bailiff for a local feudal magnate. He looked after his liege's estates, resolved disputes in the village, and helped train the village levy. He rode with the lord's cavalry, fighting as an armored knight.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaRetainerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaRetainerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaRetainerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("vlandia_merchant_option", new TextObject("{=651FhzdR}Urban merchants", null), new TextObject("{=qNZFkxJb}Your family were merchants in one of the main cities of the kingdom. They organized caravans to nearby towns and were active in the local merchant's guild.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaMerchantNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaMerchantNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaMerchantNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("vlandia_farmer_option", new TextObject("{=RDfXuVxT}Yeomen", null), new TextObject("{=BLZ4mdhb}Your family were small farmers with just enough land to feed themselves and make a small profit. People like them were the pillars of the kingdom's economy, as well as the backbone of the levy.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("vlandia_blacksmith_option", new TextObject("{=p2KIhGbE}Urban blacksmith", null), new TextObject("{=btsMpRcA}Your family owned a smithy in a city. Your father played an active if minor role in the town council, and also served in the militia.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaBlacksmithNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaBlacksmithNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaBlacksmithNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("vlandia_hunter_option", new TextObject("{=YcnK0Thk}Hunters", null), new TextObject("{=yRFSzSDZ}Your family lived in a village, but did not own their own land. Instead, your father supplemented paid jobs with long trips in the woods, hunting and trapping, always keeping a wary eye for the lord's game wardens.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaHunterNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaHunterNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaHunterNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("vlandia_mercenary_option", new TextObject("{=ipQP6aVi}Mercenaries", null), new TextObject("{=yYhX6JQC}Your father joined one of Vlandia's many mercenary companies, composed of men who got such a taste for war in their lord's service that they never took well to peace. Their crossbowmen were much valued across Calradia. Your mother was a camp follower, taking you along in the wake of bloody campaigns.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetVlandiaMercenaryNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.VlandiaMercenaryNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.VlandiaMercenaryNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003BC3 RID: 15299 RVA: 0x000FC46C File Offset: 0x000FA66C
		private void GetVlandiaRetainerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003BC4 RID: 15300 RVA: 0x000FC4C0 File Offset: 0x000FA6C0
		private bool VlandiaRetainerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BC5 RID: 15301 RVA: 0x000FC4DC File Offset: 0x000FA6DC
		private void VlandiaRetainerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BC6 RID: 15302 RVA: 0x000FC57C File Offset: 0x000FA77C
		private void GetVlandiaMerchantNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003BC7 RID: 15303 RVA: 0x000FC5D0 File Offset: 0x000FA7D0
		private bool VlandiaMerchantNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BC8 RID: 15304 RVA: 0x000FC5EC File Offset: 0x000FA7EC
		private void VlandiaMerchantNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("merchant_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BC9 RID: 15305 RVA: 0x000FC68C File Offset: 0x000FA88C
		private void GetVlandiaFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Polearm,
				DefaultSkills.Crossbow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003BCA RID: 15306 RVA: 0x000FC6E0 File Offset: 0x000FA8E0
		private bool VlandiaFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BCB RID: 15307 RVA: 0x000FC6FC File Offset: 0x000FA8FC
		private void VlandiaFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BCC RID: 15308 RVA: 0x000FC79C File Offset: 0x000FA99C
		private void GetVlandiaBlacksmithNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crafting,
				DefaultSkills.TwoHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003BCD RID: 15309 RVA: 0x000FC7F0 File Offset: 0x000FA9F0
		private bool VlandiaBlacksmithNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BCE RID: 15310 RVA: 0x000FC80C File Offset: 0x000FAA0C
		private void VlandiaBlacksmithNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("artisan_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BCF RID: 15311 RVA: 0x000FC8AC File Offset: 0x000FAAAC
		private void GetVlandiaHunterNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Crossbow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003BD0 RID: 15312 RVA: 0x000FC900 File Offset: 0x000FAB00
		private bool VlandiaHunterNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BD1 RID: 15313 RVA: 0x000FC91C File Offset: 0x000FAB1C
		private void VlandiaHunterNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("hunter");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BD2 RID: 15314 RVA: 0x000FC9BC File Offset: 0x000FABBC
		private void GetVlandiaMercenaryNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Crossbow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003BD3 RID: 15315 RVA: 0x000FCA10 File Offset: 0x000FAC10
		private bool VlandiaMercenaryNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003BD4 RID: 15316 RVA: 0x000FCA2C File Offset: 0x000FAC2C
		private void VlandiaMercenaryNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("merchant_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BD5 RID: 15317 RVA: 0x000FCACC File Offset: 0x000FACCC
		private void AddSturgianParentNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("sturgia_companion_option", new TextObject("{=mc78FEbA}A boyar's companions", null), new TextObject("{=hob3WVkU}Your father was a member of a boyar's druzhina, the 'companions' that make up his retinue. He sat at his lord's table in the great hall, oversaw the boyar's estates, and stood by his side in the center of the shield wall in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaCompanionNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaCompanionNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaCompanionNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("sturgia_trader_option", new TextObject("{=HqzVBfpl}Urban traders", null), new TextObject("{=bjVMtW3W}Your family were merchants who lived in one of Sturgia's great river ports, organizing the shipment of the north's bounty of furs, honey and other goods to faraway lands.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaTraderNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaTraderNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaTraderNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("sturgia_farmer_option", new TextObject("{=zrpqSWSh}Free farmers", null), new TextObject("{=Mcd3ZyKq}Your family had just enough land to feed themselves and make a small profit. People like them were the pillars of the kingdom's economy, as well as the backbone of the levy.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("sturgia_artisan_option", new TextObject("{=v48N6h1t}Urban artisans", null), new TextObject("{=ueCm5y1C}Your family owned their own workshop in a city, making goods from raw materials brought in from the countryside. Your father played an active if minor role in the town council, and also served in the militia.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaArtisanNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaArtisanNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaArtisanNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("sturgia_hunter_option", new TextObject("{=YcnK0Thk}Hunters", null), new TextObject("{=WyZ2UtFF}Your family had no taste for the authority of the boyars. They made their living deep in the woods, slashing and burning fields which they tended for a year or two before moving on. They hunted and trapped fox, hare, ermine, and other fur-bearing animals.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaHunterNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaHunterNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaHunterNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("sturgia_vagabond_option", new TextObject("{=TPoK3GSj}Vagabonds", null), new TextObject("{=2SDWhGmQ}Your family numbered among the poor migrants living in the slums that grow up outside the walls of the river cities, making whatever money they could from a variety of odd jobs. Sometimes they did services for one of the region's many criminal gangs.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetSturgiaVagabondNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.SturgiaVagabondNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.SturgiaVagabondNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003BD6 RID: 15318 RVA: 0x000FCCAC File Offset: 0x000FAEAC
		private void GetSturgiaCompanionNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.TwoHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003BD7 RID: 15319 RVA: 0x000FCD00 File Offset: 0x000FAF00
		private bool SturgiaCompanionNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BD8 RID: 15320 RVA: 0x000FCD1C File Offset: 0x000FAF1C
		private void SturgiaCompanionNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BD9 RID: 15321 RVA: 0x000FCDBC File Offset: 0x000FAFBC
		private void GetSturgiaTraderNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003BDA RID: 15322 RVA: 0x000FCE10 File Offset: 0x000FB010
		private bool SturgiaTraderNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BDB RID: 15323 RVA: 0x000FCE2C File Offset: 0x000FB02C
		private void SturgiaTraderNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("merchant_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BDC RID: 15324 RVA: 0x000FCECC File Offset: 0x000FB0CC
		private void GetSturgiaFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003BDD RID: 15325 RVA: 0x000FCF20 File Offset: 0x000FB120
		private bool SturgiaFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BDE RID: 15326 RVA: 0x000FCF3C File Offset: 0x000FB13C
		private void SturgiaFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BDF RID: 15327 RVA: 0x000FCFDC File Offset: 0x000FB1DC
		private void GetSturgiaArtisanNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crafting,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003BE0 RID: 15328 RVA: 0x000FD030 File Offset: 0x000FB230
		private bool SturgiaArtisanNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BE1 RID: 15329 RVA: 0x000FD04C File Offset: 0x000FB24C
		private void SturgiaArtisanNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("artisan_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BE2 RID: 15330 RVA: 0x000FD0EC File Offset: 0x000FB2EC
		private void GetSturgiaHunterNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003BE3 RID: 15331 RVA: 0x000FD140 File Offset: 0x000FB340
		private bool SturgiaHunterNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BE4 RID: 15332 RVA: 0x000FD15C File Offset: 0x000FB35C
		private void SturgiaHunterNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("hunter");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BE5 RID: 15333 RVA: 0x000FD1FC File Offset: 0x000FB3FC
		private void GetSturgiaVagabondNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003BE6 RID: 15334 RVA: 0x000FD250 File Offset: 0x000FB450
		private bool SturgiaVagabondNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003BE7 RID: 15335 RVA: 0x000FD26C File Offset: 0x000FB46C
		private void SturgiaVagabondNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("vagabond_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BE8 RID: 15336 RVA: 0x000FD30C File Offset: 0x000FB50C
		private void AddAseraiParentNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("aserai_kinsfolk_option", new TextObject("{=Sw8OxnNr}Kinsfolk of an emir", null), new TextObject("{=MFrIHJZM}Your family was from a smaller offshoot of an emir's tribe. Your father's land gave him enough income to afford a horse but he was not quite wealthy enough to buy the armor needed to join the heavier cavalry. He fought as one of the light horsemen for which the desert is famous.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiKinsfolkNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiKinsfolkNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiKinsfolkNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("aserai_slave_option", new TextObject("{=ngFVgwDD}Warrior-slaves", null), new TextObject("{=GsPC2MgU}Your father was part of one of the slave-bodyguards maintained by the Aserai emirs. He fought by his master's side with tribe's armored cavalry, and was freed - perhaps for an act of valor, or perhaps he paid for his freedom with his share of the spoils of battle. He then married your mother.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiSlaveNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiSlaveNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiSlaveNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("aserai_physician_option", new TextObject("{=bgy8LVvY}Physician", null), new TextObject("{=BhQlmQoj}Your family were respected physicians in an oasis town. They set bones and cured the sick, and their skills were in much demand. They were respected in the higher echelons of society too.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiPhysicianNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiPhysicianNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiPhysicianNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("aserai_farmer_option", new TextObject("{=g31pXuqi}Oasis farmers", null), new TextObject("{=5P0KqBAw}Your family tilled the soil in one of the oases of the Nahasa and tended the palm orchards that produced the desert's famous dates. Your father was a member of the main foot levy of his tribe, fighting with his kinsmen under the emir's banner.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("aserai_herder_option", new TextObject("{=EEedqolz}Bedouin", null), new TextObject("{=PKhcPbBX}Your family were part of a nomadic clan, crisscrossing the wastes between wadi beds and wells to feed their herds of goats and camels on the scraggly scrubs of the Nahasa.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiHerderNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiHerderNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiHerderNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("aserai_artisan_option", new TextObject("{=tRIrbTvv}Urban back-alley thugs", null), new TextObject("{=6bUSbsKC}Your father worked for a fitiwi, one of the strongmen who keep order in the poorer quarters of the oasis towns. He resolved disputes over land, dice and insults, imposing his authority with the fitiwi's traditional staff.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAseraiArtisanNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AseraiArtisanNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AseraiArtisanNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003BE9 RID: 15337 RVA: 0x000FD4EC File Offset: 0x000FB6EC
		private void GetAseraiKinsfolkNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003BEA RID: 15338 RVA: 0x000FD540 File Offset: 0x000FB740
		private bool AseraiKinsfolkNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BEB RID: 15339 RVA: 0x000FD55C File Offset: 0x000FB75C
		private void AseraiKinsfolkNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BEC RID: 15340 RVA: 0x000FD5FC File Offset: 0x000FB7FC
		private void GetAseraiSlaveNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003BED RID: 15341 RVA: 0x000FD650 File Offset: 0x000FB850
		private bool AseraiSlaveNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BEE RID: 15342 RVA: 0x000FD66C File Offset: 0x000FB86C
		private void AseraiSlaveNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("mercenary_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BEF RID: 15343 RVA: 0x000FD70C File Offset: 0x000FB90C
		private void GetAseraiPhysicianNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Medicine,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003BF0 RID: 15344 RVA: 0x000FD760 File Offset: 0x000FB960
		private bool AseraiPhysicianNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BF1 RID: 15345 RVA: 0x000FD77C File Offset: 0x000FB97C
		private void AseraiPhysicianNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("physician_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BF2 RID: 15346 RVA: 0x000FD81C File Offset: 0x000FBA1C
		private void GetAseraiFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003BF3 RID: 15347 RVA: 0x000FD870 File Offset: 0x000FBA70
		private bool AseraiFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BF4 RID: 15348 RVA: 0x000FD88C File Offset: 0x000FBA8C
		private void AseraiFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BF5 RID: 15349 RVA: 0x000FD92C File Offset: 0x000FBB2C
		private void GetAseraiHerderNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003BF6 RID: 15350 RVA: 0x000FD980 File Offset: 0x000FBB80
		private bool AseraiHerderNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BF7 RID: 15351 RVA: 0x000FD99C File Offset: 0x000FBB9C
		private void AseraiHerderNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("herder");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BF8 RID: 15352 RVA: 0x000FDA3C File Offset: 0x000FBC3C
		private void GetAseraiArtisanNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003BF9 RID: 15353 RVA: 0x000FDA90 File Offset: 0x000FBC90
		private bool AseraiArtisanNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003BFA RID: 15354 RVA: 0x000FDAAC File Offset: 0x000FBCAC
		private void AseraiArtisanNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("artisan_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BFB RID: 15355 RVA: 0x000FDB4C File Offset: 0x000FBD4C
		private void AddBattaniaNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("battania_retainer_option", new TextObject("{=GeNKQlHR}Members of the chieftain's hearthguard", null), new TextObject("{=LpH8SYFL}Your family were the trusted kinfolk of a Battanian chieftain, and sat at his table in his great hall. Your father assisted his chief in running the affairs of the clan and trained with the traditional weapons of the Battanian elite, the two-handed sword or falx and the bow.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaRetainerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaRetainerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaRetainerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("battania_healer_option", new TextObject("{=AeBzTj6w}Healers", null), new TextObject("{=j6py5Rv5}Your parents were healers who gathered herbs and treated the sick. As a living reservoir of Battanian tradition, they were also asked to adjudicate many disputes between the clans.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaHealerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaHealerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaHealerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("battania_farmer_option", new TextObject("{=tGEStbxb}Tribespeople", null), new TextObject("{=WchH8bS2}Your family were middle-ranking members of a Battanian clan, who tilled their own land. Your father fought with the kern, the main body of his people's warriors, joining in the screaming charges for which the Battanians were famous.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("battania_artisan_option", new TextObject("{=BCU6RezA}Smiths", null), new TextObject("{=kg9YtrOg}Your family were smiths, a revered profession among the Battanians. They crafted everything from fine filigree jewelry in geometric designs to the well-balanced longswords favored by the Battanian aristocracy.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaArtisanNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaArtisanNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaArtisanNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("battania_hunter_option", new TextObject("{=7eWmU2mF}Foresters", null), new TextObject("{=7jBroUUQ}Your family had little land of their own, so they earned their living from the woods, hunting and trapping. They taught you from an early age that skills like finding game trails and killing an animal with one shot could make the difference between eating and starvation.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaHunterNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaHunterNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaHunterNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("battania_bard_option", new TextObject("{=SpJqhEEh}Bards", null), new TextObject("{=aVzcyhhy}Your father was a bard, drifting from chieftain's hall to chieftain's hall making his living singing the praises of one Battanian aristocrat and mocking his enemies, then going to his enemy's hall and doing the reverse. You learned from him that a clever tongue could spare you  from a life toiling in the fields, if you kept your wits about you.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetBattaniaBardNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.BattaniaBardNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.BattaniaBardNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003BFC RID: 15356 RVA: 0x000FDD2C File Offset: 0x000FBF2C
		private void GetBattaniaRetainerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.TwoHanded,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003BFD RID: 15357 RVA: 0x000FDD80 File Offset: 0x000FBF80
		private bool BattaniaRetainerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003BFE RID: 15358 RVA: 0x000FDD9C File Offset: 0x000FBF9C
		private void BattaniaRetainerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003BFF RID: 15359 RVA: 0x000FDE3C File Offset: 0x000FC03C
		private void GetBattaniaHealerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Medicine,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C00 RID: 15360 RVA: 0x000FDE90 File Offset: 0x000FC090
		private bool BattaniaHealerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C01 RID: 15361 RVA: 0x000FDEAC File Offset: 0x000FC0AC
		private void BattaniaHealerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("healer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C02 RID: 15362 RVA: 0x000FDF4C File Offset: 0x000FC14C
		private void GetBattaniaFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C03 RID: 15363 RVA: 0x000FDFA0 File Offset: 0x000FC1A0
		private bool BattaniaFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C04 RID: 15364 RVA: 0x000FDFBC File Offset: 0x000FC1BC
		private void BattaniaFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C05 RID: 15365 RVA: 0x000FE05C File Offset: 0x000FC25C
		private void GetBattaniaArtisanNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crafting,
				DefaultSkills.TwoHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C06 RID: 15366 RVA: 0x000FE0B0 File Offset: 0x000FC2B0
		private bool BattaniaArtisanNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C07 RID: 15367 RVA: 0x000FE0CC File Offset: 0x000FC2CC
		private void BattaniaArtisanNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("artisan_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C08 RID: 15368 RVA: 0x000FE16C File Offset: 0x000FC36C
		private void GetBattaniaHunterNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C09 RID: 15369 RVA: 0x000FE1C0 File Offset: 0x000FC3C0
		private bool BattaniaHunterNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C0A RID: 15370 RVA: 0x000FE1DC File Offset: 0x000FC3DC
		private void BattaniaHunterNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("hunter");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C0B RID: 15371 RVA: 0x000FE27C File Offset: 0x000FC47C
		private void GetBattaniaBardNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C0C RID: 15372 RVA: 0x000FE2D0 File Offset: 0x000FC4D0
		private bool BattaniaBardNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C0D RID: 15373 RVA: 0x000FE2EC File Offset: 0x000FC4EC
		private void BattaniaBardNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("bard_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C0E RID: 15374 RVA: 0x000FE38C File Offset: 0x000FC58C
		private void AddKhuzaitNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("khuzait_retainer_option", new TextObject("{=FVaRDe2a}A noyan's kinsfolk", null), new TextObject("{=jAs3kDXh}Your family were the trusted kinsfolk of a Khuzait noyan, and shared his meals in the chieftain's yurt. Your father assisted his chief in running the affairs of the clan and fought in the core of armored lancers in the center of the Khuzait battle line.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitRetainerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitRetainerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitRetainerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("khuzait_merhant_option", new TextObject("{=TkgLEDRM}Merchants", null), new TextObject("{=qPg3IDiq}Your family came from one of the merchant clans that dominated the cities in eastern Calradia before the Khuzait conquest. They adjusted quickly to their new masters, keeping the caravan routes running and ensuring that the tariff revenues that once went into imperial coffers now flowed to the khanate.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitMerchantNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitMerchantNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitMerchantNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("khuzait_mercenary_option", new TextObject("{=tGEStbxb}Tribespeople", null), new TextObject("{=URgZ4ai4}Your family were middle-ranking members of one of the Khuzait clans. He had some herds of his own, but was not rich. When the Khuzait horde was summoned to battle, he fought with the horse archers, shooting and wheeling and wearing down the enemy before the lancers delivered the final punch.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitHerderNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitHerderNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitHerderNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("khuzait_farmer_option", new TextObject("{=gQ2tAvCz}Farmers", null), new TextObject("{=5QSGoRFj}Your family tilled one of the small patches of arable land in the steppes for generations. When the Khuzaits came, they ceased paying taxes to the emperor and providing conscripts for his army, and served the khan instead.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitFarmerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitFarmerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitFarmerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("khuzait_healer_option", new TextObject("{=vfhVveLW}Shamans", null), new TextObject("{=WOKNhaG2}Your family were guardians of the sacred traditions of the Khuzaits, channelling the spirits of the wilderness and of the ancestors. They tended the sick and dispensed wisdom, resolving disputes and providing practical advice.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitHealerNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitHealerNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitHealerNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("khuzait_herder_option", new TextObject("{=Xqba1Obq}Nomads", null), new TextObject("{=9aoQYpZs}Your family's clan never pledged its loyalty to the khan and never settled down, preferring to live out in the deep steppe away from his authority. They remain some of the finest trackers and scouts in the grasslands, as the ability to spot an enemy coming and move quickly is often all that protects their herds from their neighbors' predations.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetKhuzaitNomadHerderNarrativeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.KhuzaitNomadHerderNarrativeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.KhuzaitNomadHerderNarrativeOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003C0F RID: 15375 RVA: 0x000FE56C File Offset: 0x000FC76C
		private void GetKhuzaitRetainerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C10 RID: 15376 RVA: 0x000FE5C0 File Offset: 0x000FC7C0
		private bool KhuzaitRetainerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C11 RID: 15377 RVA: 0x000FE5DC File Offset: 0x000FC7DC
		private void KhuzaitRetainerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("retainer_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_1";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_1";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C12 RID: 15378 RVA: 0x000FE67C File Offset: 0x000FC87C
		private void GetKhuzaitMerchantNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C13 RID: 15379 RVA: 0x000FE6D0 File Offset: 0x000FC8D0
		private bool KhuzaitMerchantNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C14 RID: 15380 RVA: 0x000FE6EC File Offset: 0x000FC8EC
		private void KhuzaitMerchantNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("merchant_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_mother_front";
			string fatherAnimation = "act_character_creation_male_default_mother_front";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C15 RID: 15381 RVA: 0x000FE78C File Offset: 0x000FC98C
		private void GetKhuzaitHerderNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Bow,
				DefaultSkills.Riding
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C16 RID: 15382 RVA: 0x000FE7E0 File Offset: 0x000FC9E0
		private bool KhuzaitHerderNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C17 RID: 15383 RVA: 0x000FE7FC File Offset: 0x000FC9FC
		private void KhuzaitHerderNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("herder");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_father_sitting";
			string fatherAnimation = "act_character_creation_male_default_father_sitting";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C18 RID: 15384 RVA: 0x000FE89C File Offset: 0x000FCA9C
		private void GetKhuzaitFarmerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Polearm,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003C19 RID: 15385 RVA: 0x000FE8F0 File Offset: 0x000FCAF0
		private bool KhuzaitFarmerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C1A RID: 15386 RVA: 0x000FE90C File Offset: 0x000FCB0C
		private void KhuzaitFarmerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("farmer");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_2";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_2";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C1B RID: 15387 RVA: 0x000FE9AC File Offset: 0x000FCBAC
		private void GetKhuzaitHealerNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Medicine,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C1C RID: 15388 RVA: 0x000FEA00 File Offset: 0x000FCC00
		private bool KhuzaitHealerNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C1D RID: 15389 RVA: 0x000FEA1C File Offset: 0x000FCC1C
		private void KhuzaitHealerNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("healer_urban");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_side_to_side_3";
			string fatherAnimation = "act_character_creation_male_default_side_to_side_3";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C1E RID: 15390 RVA: 0x000FEABC File Offset: 0x000FCCBC
		private void GetKhuzaitNomadHerderNarrativeOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Scouting,
				DefaultSkills.Riding
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C1F RID: 15391 RVA: 0x000FEB10 File Offset: 0x000FCD10
		private bool KhuzaitNomadHerderNarrativeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C20 RID: 15392 RVA: 0x000FEB2C File Offset: 0x000FCD2C
		private void KhuzaitNomadHerderNarrativeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SetParentOccupation("herder");
			string motherEquipmentId = this.GetMotherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			string fatherEquipmentId = this.GetFatherEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId);
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(motherEquipmentId);
			MBEquipmentRoster object2 = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(fatherEquipmentId);
			string motherAnimation = "act_character_creation_female_default_hugging";
			string fatherAnimation = "act_character_creation_male_default_hugging";
			this.UpdateParentEquipment(characterCreationManager, @object, object2, motherAnimation, fatherAnimation);
		}

		// Token: 0x06003C21 RID: 15393 RVA: 0x000FEBCC File Offset: 0x000FCDCC
		private List<NarrativeMenuCharacterArgs> GetChildhoodMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacterArgs> list = new List<NarrativeMenuCharacterArgs>();
			string playerChildhoodAgeEquipmentId = this.GetPlayerChildhoodAgeEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			list.Add(new NarrativeMenuCharacterArgs("player_childhood_character", 7, playerChildhoodAgeEquipmentId, "act_childhood_schooled", "spawnpoint_player_1", "", "", null, true, CharacterObject.PlayerCharacter.IsFemale));
			return list;
		}

		// Token: 0x06003C22 RID: 15394 RVA: 0x000FEC40 File Offset: 0x000FCE40
		private void AddChildhoodMenu(CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			BodyProperties bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1);
			bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, 7f);
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("player_childhood_character", bodyProperties, CharacterObject.PlayerCharacter.Race, CharacterObject.PlayerCharacter.IsFemale);
			list.Add(item);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_childhood_menu", "narrative_parent_menu", "narrative_education_menu", new TextObject("{=8Yiwt1z6}Early Childhood", null), new TextObject("{=character_creation_content_16}As a child you were noted for...", null), list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetChildhoodMenuNarrativeMenuCharacterArgs));
			this.AddChildhoodNarrativeMenuOptions(narrativeMenu);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003C23 RID: 15395 RVA: 0x000FECE4 File Offset: 0x000FCEE4
		private void AddChildhoodNarrativeMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("childhood_leadership_option", new TextObject("{=kmM68Qx4}your leadership skills.", null), new TextObject("{=FfNwXtii}If the wolf pup gang of your early childhood had an alpha, it was definitely you. All the other kids followed your lead as you decided what to play and where to play, and led them in games and mischief.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodLeadershipOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodLeadershipOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodLeadershipOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("childhood_brawn_option", new TextObject("{=5HXS8HEY}your brawn.", null), new TextObject("{=YKzuGc54}You were big, and other children looked to have you around in any scrap with children from a neighboring village. You pushed a plough and threw an axe like an adult.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodBrawnOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodBrawnOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodBrawnOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("childhood_detail_option", new TextObject("{=QrYjPUEf}your attention to detail.", null), new TextObject("{=JUSHAPnu}You were quick on your feet and attentive to what was going on around you. Usually you could run away from trouble, though you could give a good account of yourself in a fight with other children if cornered.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodDetailOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodDetailOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodDetailOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("childhood_smart_option", new TextObject("{=Y3UcaX74}your aptitude for numbers.", null), new TextObject("{=DFidSjIf}Most children around you had only the most rudimentary education, but you lingered after class to study letters and mathematics. You were fascinated by the marketplace - weights and measures, tallies and accounts, the chatter about profits and losses.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodSmartOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodSmartOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodSmartOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("childhood_leader_option", new TextObject("{=GEYzLuwb}your way with people.", null), new TextObject("{=w2TEQq26}You were always attentive to other people, good at guessing their motivations. You studied how individuals were swayed, and tried out what you learned from adults on your friends.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodLeaderOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodLeaderOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodLeaderOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("childhood_horse_option", new TextObject("{=MEgLE2kj}your skill with horses.", null), new TextObject("{=ngazFofr}You were always drawn to animals, and spent as much time as possible hanging out in the village stables. You could calm horses, and were sometimes called upon to break in new colts. You learned the basics of veterinary arts, much of which is applicable to humans as well.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetChildhoodHorseOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.ChildhoodHorseOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.ChildhoodHorseOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
		}

		// Token: 0x06003C24 RID: 15396 RVA: 0x000FEEC4 File Offset: 0x000FD0C4
		private void GetChildhoodLeadershipOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Leadership,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C25 RID: 15397 RVA: 0x000FEF18 File Offset: 0x000FD118
		private bool ChildhoodLeadershipOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C26 RID: 15398 RVA: 0x000FEF1C File Offset: 0x000FD11C
		private void ChildhoodLeadershipOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_leader");
				}
			}
		}

		// Token: 0x06003C27 RID: 15399 RVA: 0x000FEF8C File Offset: 0x000FD18C
		private void GetChildhoodBrawnOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.TwoHanded,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003C28 RID: 15400 RVA: 0x000FEFE0 File Offset: 0x000FD1E0
		private bool ChildhoodBrawnOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C29 RID: 15401 RVA: 0x000FEFE4 File Offset: 0x000FD1E4
		private void ChildhoodBrawnOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_athlete");
				}
			}
		}

		// Token: 0x06003C2A RID: 15402 RVA: 0x000FF054 File Offset: 0x000FD254
		private void GetChildhoodDetailOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C2B RID: 15403 RVA: 0x000FF0A8 File Offset: 0x000FD2A8
		private bool ChildhoodDetailOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C2C RID: 15404 RVA: 0x000FF0AC File Offset: 0x000FD2AC
		private void ChildhoodDetailOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_memory");
				}
			}
		}

		// Token: 0x06003C2D RID: 15405 RVA: 0x000FF11C File Offset: 0x000FD31C
		private void GetChildhoodSmartOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Engineering,
				DefaultSkills.Trade
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C2E RID: 15406 RVA: 0x000FF170 File Offset: 0x000FD370
		private bool ChildhoodSmartOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C2F RID: 15407 RVA: 0x000FF174 File Offset: 0x000FD374
		private void ChildhoodSmartOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_numbers");
				}
			}
		}

		// Token: 0x06003C30 RID: 15408 RVA: 0x000FF1E4 File Offset: 0x000FD3E4
		private void GetChildhoodLeaderOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Charm,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C31 RID: 15409 RVA: 0x000FF238 File Offset: 0x000FD438
		private bool ChildhoodLeaderOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C32 RID: 15410 RVA: 0x000FF23C File Offset: 0x000FD43C
		private void ChildhoodLeaderOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_manners");
				}
			}
		}

		// Token: 0x06003C33 RID: 15411 RVA: 0x000FF2AC File Offset: 0x000FD4AC
		private void GetChildhoodHorseOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Medicine
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C34 RID: 15412 RVA: 0x000FF300 File Offset: 0x000FD500
		private bool ChildhoodHorseOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C35 RID: 15413 RVA: 0x000FF304 File Offset: 0x000FD504
		private void ChildhoodHorseOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_childhood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_animals");
				}
			}
		}

		// Token: 0x06003C36 RID: 15414 RVA: 0x000FF374 File Offset: 0x000FD574
		private List<NarrativeMenuCharacterArgs> GetEducationMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacterArgs> list = new List<NarrativeMenuCharacterArgs>();
			string playerEducationAgeEquipmentId = this.GetPlayerEducationAgeEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedParentOccupation, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			list.Add(new NarrativeMenuCharacterArgs("player_education_character", 12, playerEducationAgeEquipmentId, "act_childhood_schooled", "spawnpoint_player_1", "", "", null, true, CharacterObject.PlayerCharacter.IsFemale));
			return list;
		}

		// Token: 0x06003C37 RID: 15415 RVA: 0x000FF3E8 File Offset: 0x000FD5E8
		public void AddEducationMenu(CharacterCreationManager characterCreationManager)
		{
			BodyProperties bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1);
			bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, 12f);
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("player_education_character", bodyProperties, CharacterObject.PlayerCharacter.Race, CharacterObject.PlayerCharacter.IsFemale);
			list.Add(item);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_education_menu", "narrative_childhood_menu", "narrative_youth_menu", new TextObject("{=rcoueCmk}Adolescence", null), new TextObject("{=WYvnWcXQ}Like all village children you helped out in the fields. You also...", null), list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetEducationMenuNarrativeMenuCharacterArgs));
			this.AddEducationMenuOptions(narrativeMenu);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003C38 RID: 15416 RVA: 0x000FF48C File Offset: 0x000FD68C
		private void AddEducationMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("education_herder_option", new TextObject("{=RKVNvimC}herded the sheep.", null), new TextObject("{=KfaqPpbK}You went with other fleet-footed youths to take the villages' sheep, goats or cattle to graze in pastures near the village. You were in charge of chasing down stray beasts, and always kept a big stone on hand to be hurled at lurking predators if necessary.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationHerderOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationHerderOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationHerderOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("education_smith_option", new TextObject("{=bTKiN0hr}worked in the village smithy.", null), new TextObject("{=y6j1bJTH}You were apprenticed to the local smith. You learned how to heat and forge metal, hammering for hours at a time until your muscles ached.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationSmithOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationSmithOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationSmithOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("education_engineer_option", new TextObject("{=tI8ZLtoA}repaired projects.", null), new TextObject("{=6LFj919J}You helped dig wells, rethatch houses, and fix broken plows. You learned about the basics of construction, as well as what it takes to keep a farming community prosperous.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationEngineerOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationEngineerOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationEngineerOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("education_doctor_option", new TextObject("{=TRwgSLD2}gathered herbs in the wild.", null), new TextObject("{=9ks4u5cH}You were sent by the village healer up into the hills to look for useful medicinal plants. You learned which herbs healed wounds or brought down a fever, and how to find them.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationDoctorOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationDoctorOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationDoctorOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("education_hunter_option", new TextObject("{=T7m7ReTq}hunted small game.", null), new TextObject("{=RuvSk3QT}You accompanied a local hunter as he went into the wilderness, helping him set up traps and catch small animals.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationHunterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationHunterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationHunterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("education_merchant_option", new TextObject("{=qAbMagWq}sold product at the market.", null), new TextObject("{=DIgsfYfz}You took your family's goods to the nearest town to sell your produce and buy supplies. It was hard work, but you enjoyed the hubbub of the marketplace.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationMerchantOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationMerchantOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationMerchantOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
			NarrativeMenuOption narrativeMenuOption7 = new NarrativeMenuOption("education_watcher_option", new TextObject("{=go7Yu7KS}watched the militia training.", null), new TextObject("{=qnqdEJOv}You watched the town's watch practice shooting and perfect their plans to defend the walls in case of a siege.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationWatcherOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationWatcherOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationWatcherOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption7);
			NarrativeMenuOption narrativeMenuOption8 = new NarrativeMenuOption("education_ganger_option", new TextObject("{=gAjvAGTa}hung out with the gangs in the alleys.", null), new TextObject("{=1SUTcF0J}The gang leaders who kept watch over the slums of Calradian cities were always in need of poor youth to run messages and back them up in turf wars, while thrill-seeking merchants' sons and daughters sometimes slummed it in their company as well.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationGangerOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationGangerOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationGangerOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption8);
			NarrativeMenuOption narrativeMenuOption9 = new NarrativeMenuOption("education_docker_option", new TextObject("{=QVVCgajg}helped at building sites.", null), new TextObject("{=bhdkegZ4}All towns had their share of projects that were constantly in need of both skilled and unskilled labor. You learned how hoists and scaffolds were constructed, how planks and stones were hewn and fitted, and other skills.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationDockerOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationDockerOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationDockerOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption9);
			NarrativeMenuOption narrativeMenuOption10 = new NarrativeMenuOption("education_marketer_option", new TextObject("{=JTsv6PFe}worked in the markets and caravanserais.", null), new TextObject("{=rmMcwSn8}You helped your family handle their business affairs, going down to the marketplace to make purchases and oversee the arrival of caravans.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationMarketerOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationMarketerOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationMarketerOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption10);
			NarrativeMenuOption narrativeMenuOption11 = new NarrativeMenuOption("education_tutor_option", new TextObject("{=EMVojYzW}studied with your private tutor.", null), new TextObject("{=hXl25avg}Your family arranged for a private tutor and you took full advantage, reading voraciously on history, mathematics, and philosophy and discussing what you read with your tutor and classmates.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationTutorOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationTutorOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationTutorOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption11);
			NarrativeMenuOption narrativeMenuOption12 = new NarrativeMenuOption("education_horser_option", new TextObject("{=hin3iA2D}cared for the horses.", null), new TextObject("{=Ghz90npw}Your family owned a few horses at the town stables and you took charge of their care. Many evenings you would take them out beyond the walls and gallup through the fields, racing other youth.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEducationPoorHorserOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EducationPoorHorserOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EducationPoorHorserOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption12);
		}

		// Token: 0x06003C39 RID: 15417 RVA: 0x000FF848 File Offset: 0x000FDA48
		private void GetEducationHerderOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C3A RID: 15418 RVA: 0x000FF89C File Offset: 0x000FDA9C
		private bool EducationHerderOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C3B RID: 15419 RVA: 0x000FF8B4 File Offset: 0x000FDAB4
		private void EducationHerderOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_streets");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("carry_bostaff_rogue1");
					break;
				}
			}
		}

		// Token: 0x06003C3C RID: 15420 RVA: 0x000FF93C File Offset: 0x000FDB3C
		private void GetEducationSmithOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.TwoHanded,
				DefaultSkills.Crafting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003C3D RID: 15421 RVA: 0x000FF990 File Offset: 0x000FDB90
		private bool EducationSmithOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C3E RID: 15422 RVA: 0x000FF9A8 File Offset: 0x000FDBA8
		private void EducationSmithOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_militia");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("peasant_hammer_1_t1");
					break;
				}
			}
		}

		// Token: 0x06003C3F RID: 15423 RVA: 0x000FFA30 File Offset: 0x000FDC30
		private void GetEducationEngineerOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crafting,
				DefaultSkills.Engineering
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C40 RID: 15424 RVA: 0x000FFA84 File Offset: 0x000FDC84
		private bool EducationEngineerOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C41 RID: 15425 RVA: 0x000FFA9C File Offset: 0x000FDC9C
		private void EducationEngineerOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_grit");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("carry_hammer");
					break;
				}
			}
		}

		// Token: 0x06003C42 RID: 15426 RVA: 0x000FFB24 File Offset: 0x000FDD24
		private void GetEducationDoctorOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Medicine,
				DefaultSkills.Scouting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C43 RID: 15427 RVA: 0x000FFB78 File Offset: 0x000FDD78
		private bool EducationDoctorOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C44 RID: 15428 RVA: 0x000FFB90 File Offset: 0x000FDD90
		private void EducationDoctorOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_peddlers");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("_to_carry_bd_basket_a");
					break;
				}
			}
		}

		// Token: 0x06003C45 RID: 15429 RVA: 0x000FFC18 File Offset: 0x000FDE18
		private void GetEducationHunterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Bow,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C46 RID: 15430 RVA: 0x000FFC6C File Offset: 0x000FDE6C
		private bool EducationHunterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C47 RID: 15431 RVA: 0x000FFC84 File Offset: 0x000FDE84
		private void EducationHunterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("composite_bow");
					break;
				}
			}
		}

		// Token: 0x06003C48 RID: 15432 RVA: 0x000FFD0C File Offset: 0x000FDF0C
		private void GetEducationMerchantOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C49 RID: 15433 RVA: 0x000FFD60 File Offset: 0x000FDF60
		private bool EducationMerchantOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C4A RID: 15434 RVA: 0x000FFD78 File Offset: 0x000FDF78
		private void EducationMerchantOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_peddlers_2");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("_to_carry_bd_fabric_c");
					break;
				}
			}
		}

		// Token: 0x06003C4B RID: 15435 RVA: 0x000FFE00 File Offset: 0x000FE000
		private void GetEducationWatcherOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Polearm,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C4C RID: 15436 RVA: 0x000FFE54 File Offset: 0x000FE054
		private bool EducationWatcherOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C4D RID: 15437 RVA: 0x000FFE68 File Offset: 0x000FE068
		private void EducationWatcherOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_fox");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("");
					break;
				}
			}
		}

		// Token: 0x06003C4E RID: 15438 RVA: 0x000FFEF0 File Offset: 0x000FE0F0
		private void GetEducationGangerOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C4F RID: 15439 RVA: 0x000FFF44 File Offset: 0x000FE144
		private bool EducationGangerOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C50 RID: 15440 RVA: 0x000FFF58 File Offset: 0x000FE158
		private void EducationGangerOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_athlete");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("");
					break;
				}
			}
		}

		// Token: 0x06003C51 RID: 15441 RVA: 0x000FFFE0 File Offset: 0x000FE1E0
		private void GetEducationDockerOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Crafting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003C52 RID: 15442 RVA: 0x00100034 File Offset: 0x000FE234
		private bool EducationDockerOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C53 RID: 15443 RVA: 0x00100048 File Offset: 0x000FE248
		private void EducationDockerOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_peddlers");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("_to_carry_bd_basket_a");
					break;
				}
			}
		}

		// Token: 0x06003C54 RID: 15444 RVA: 0x001000D0 File Offset: 0x000FE2D0
		private void GetEducationMarketerOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Charm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C55 RID: 15445 RVA: 0x00100124 File Offset: 0x000FE324
		private bool EducationMarketerOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C56 RID: 15446 RVA: 0x00100138 File Offset: 0x000FE338
		private void EducationMarketerOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_manners");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("");
					break;
				}
			}
		}

		// Token: 0x06003C57 RID: 15447 RVA: 0x001001C0 File Offset: 0x000FE3C0
		private void GetEducationTutorOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Engineering,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C58 RID: 15448 RVA: 0x00100214 File Offset: 0x000FE414
		private bool EducationTutorOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C59 RID: 15449 RVA: 0x00100228 File Offset: 0x000FE428
		private void EducationTutorOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_book");
					narrativeMenuCharacter.SetLeftHandItem("character_creation_notebook");
					narrativeMenuCharacter.SetRightHandItem("");
					break;
				}
			}
		}

		// Token: 0x06003C5A RID: 15450 RVA: 0x001002B0 File Offset: 0x000FE4B0
		private void GetEducationPoorHorserOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Steward
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C5B RID: 15451 RVA: 0x00100304 File Offset: 0x000FE504
		private bool EducationPoorHorserOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003C5C RID: 15452 RVA: 0x00100318 File Offset: 0x000FE518
		private void EducationPoorHorserOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_education_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_peddlers_2");
					narrativeMenuCharacter.SetLeftHandItem("");
					narrativeMenuCharacter.SetRightHandItem("_to_carry_bd_fabric_c");
					break;
				}
			}
		}

		// Token: 0x06003C5D RID: 15453 RVA: 0x001003A0 File Offset: 0x000FE5A0
		private List<NarrativeMenuCharacterArgs> GetYouthMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			if (string.IsNullOrEmpty(characterCreationManager.CharacterCreationContent.SelectedTitleType))
			{
				characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			}
			List<NarrativeMenuCharacterArgs> list = new List<NarrativeMenuCharacterArgs>();
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			list.Add(new NarrativeMenuCharacterArgs("player_youth_character", 17, playerEquipmentId, "act_childhood_schooled", "spawnpoint_player_1", "", "", null, true, CharacterObject.PlayerCharacter.IsFemale));
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
			ItemObject item = @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item;
			list.Add(new NarrativeMenuCharacterArgs("narrative_character_horse", -1, "", "act_inventory_idle_start", "spawnpoint_mount_1", @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item.StringId, @object.DefaultEquipment[EquipmentIndex.HorseHarness].Item.StringId, MountCreationKey.GetRandomMountKey(item, CharacterObject.PlayerCharacter.GetMountKeySeed()), false, false));
			return list;
		}

		// Token: 0x06003C5E RID: 15454 RVA: 0x001004C4 File Offset: 0x000FE6C4
		private void AddYouthMenu(CharacterCreationManager characterCreationManager)
		{
			TextObject description = CharacterObject.PlayerCharacter.IsFemale ? new TextObject("{=5kbeAC7k}In wartorn Calradia, especially in frontier or tribal areas, some women as well as men learn to fight from an early age. You...", null) : new TextObject("{=F7OO5SAa}As a youngster growing up in Calradia, war was never too far away. You...", null);
			BodyProperties bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1);
			bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, 17f);
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("player_youth_character", bodyProperties, CharacterObject.PlayerCharacter.Race, CharacterObject.PlayerCharacter.IsFemale);
			NarrativeMenuCharacter item2 = new NarrativeMenuCharacter("narrative_character_horse");
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			list.Add(item);
			list.Add(item2);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_youth_menu", "narrative_education_menu", "narrative_adulthood_menu", new TextObject("{=ok8lSW6M}Youth", null), description, list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetYouthMenuNarrativeMenuCharacterArgs));
			this.AddYouthMenuOptions(narrativeMenu);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003C5F RID: 15455 RVA: 0x0010059C File Offset: 0x000FE79C
		private void AddYouthMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("youth_staff_first_option", new TextObject("{=CITG915d}joined a commander's staff.", null), new TextObject("{=wNHqFlDL}You were chosen by your superior officer to serve an imperial strategos as a courier. You were not given major responsibilities - mostly carrying messages and tending to his horse - but it did give you a chance to see how campaigns were planned and men were deployed in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthStaffOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthStaffOneOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthStaffOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("youth_staff_second_option", new TextObject("{=CITG915d}joined a commander's staff.", null), new TextObject("{=ANbNblaH}You were picked as the courier of the commander of the local forces. You were not given major responsibilities - mostly carrying messages and tending to his horse - but it did give you a chance to see how campaigns were planned and men were deployed in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthStaffOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthStaffTwoOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthStaffOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("youth_groom_option", new TextObject("{=bhE2i6OU}served as a baron's groom.", null), new TextObject("{=i3k7YtA8}You were chosen by a knight to accompany a minor baron of the Vlandian kingdom. You were not given major responsibilities - mostly carrying messages and tending to his horse - but it did give you a chance to see how campaigns were planned and men were deployed in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthGroomOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthGroomOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthGroomOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("youth_servant_first_option", new TextObject("{=F2bgujPo}were a chieftain's servant.", null), new TextObject("{=AXWO4C69}Your were choosen among others to accompany a chieftain of your people. You were not given major responsibilities - mostly carrying messages and tending to his horse - but it did give you a chance to see how campaigns were planned and men were deployed in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthServantOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthServantOneOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthServantOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("youth_servant_second_option", new TextObject("{=F2bgujPo}were a chieftain's servant.", null), new TextObject("{=neMCgMZM}Local wise man picked you to become the messenger of a chieftain of your people. You were not given major responsibilities - mostly carrying messages and tending to his horse - but it did give you a chance to see how campaigns were planned and men were deployed in battle.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthServantOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthServantTwoOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthServantOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("youth_cavalry_option", new TextObject("{=h2KnarLL}trained with the cavalry.", null), new TextObject("{=7cHsIMLP}You could never have bought the equipment on your own, but you were a good enough rider so that the local lord lent you a horse and equipment. You joined the armored cavalry, training with the lance.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthCavalryOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthCavalryOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthCavalryOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
			NarrativeMenuOption narrativeMenuOption7 = new NarrativeMenuOption("youth_hearth_option", new TextObject("{=zsC2t5Hb}trained with the hearth guard.", null), new TextObject("{=RmbWW6Bm}You were a big and imposing enough youth that the chief's guard allowed you to train alongside them, in preparation to join them some day.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthHearthOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthHearthOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthHearthOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption7);
			NarrativeMenuOption narrativeMenuOption8 = new NarrativeMenuOption("youth_guard_high_register_option", new TextObject("{=aTncHUfL}stood guard with the garrisons.", null), new TextObject("{=63TAYbkx}Urban troops spend much of their time guarding the town walls. Most of their training was in missile weapons, especially useful during sieges.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthGuardHighRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthGuardHighRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthGuardHighRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption8);
			NarrativeMenuOption narrativeMenuOption9 = new NarrativeMenuOption("youth_guard_low_register_option", new TextObject("{=aTncHUfL}stood guard with the garrisons.", null), new TextObject("{=oR58iNDz}Urban troops spend much of their time guarding the town walls. Most of their training was in missile weapons.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthGuardLowRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthGuardLowRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthGuardLowRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption9);
			NarrativeMenuOption narrativeMenuOption10 = new NarrativeMenuOption("youth_guard_garrisons_register_option", new TextObject("{=aTncHUfL}stood guard with the garrisons.", null), new TextObject("{=e6lINjFg}The garrisons spent most of their time guarding the town walls, and their training focused largely on missile weapons.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthGuardGarrisonRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthGuardGarrisonRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthGuardGarrisonRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption10);
			NarrativeMenuOption narrativeMenuOption11 = new NarrativeMenuOption("youth_guard_empire_register_option", new TextObject("{=aTncHUfL}stood guard with the garrisons.", null), new TextObject("{=oR58iNDz}Urban troops spend much of their time guarding the town walls. Most of their training was in missile weapons.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthGuardEmpireRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthGuardEmpireRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthGuardEmpireRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption11);
			NarrativeMenuOption narrativeMenuOption12 = new NarrativeMenuOption("youth_rider_high_register_option", new TextObject("{=VlXOgIX6}rode with the scouts.", null), new TextObject("{=888lmJqs}All of Calradia's kingdoms recognize the value of good light cavalry and horse archers, and are sure to recruit nomads and borderers with the skills to fulfill those duties. You were a good enough rider that your neighbors pitched in to buy you a small pony and a good bow so that you could fulfill their levy obligations.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthRiderHighRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthRiderHighRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthRiderHighRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption12);
			NarrativeMenuOption narrativeMenuOption13 = new NarrativeMenuOption("youth_rider_low_register_option", new TextObject("{=VlXOgIX6}rode with the scouts.", null), new TextObject("{=sYuN6hPD}All of Calradia's kingdoms recognize the value of good light cavalry, and are sure to recruit nomads and borderers with the skills to fulfill those duties. You were a good enough rider that your neighbors pitched in to buy you a small pony and a sheaf of javelins so that you could fulfill their levy obligations.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthRiderLowRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthRiderLowRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthRiderLowRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption13);
			NarrativeMenuOption narrativeMenuOption14 = new NarrativeMenuOption("youth_infantry_option", new TextObject("{=a8arFSra}trained with the infantry.", null), new TextObject("{=afH90aNs}Levy armed with spear and shield, drawn from smallholding farmers, have always been the backbone of most armies of Calradia.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthInfantryOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthInfantryOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthInfantryOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption14);
			NarrativeMenuOption narrativeMenuOption15 = new NarrativeMenuOption("youth_skirmisher_option", new TextObject("{=oMbOIPc9}joined the skirmishers.", null), new TextObject("{=bXAg5w19}Younger recruits, or those of a slighter build, or those too poor to buy shield and armor tend to join the skirmishers. Fighting with bow and javelin, they try to stay out of reach of the main enemy forces.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthSkirmisherOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthSkirmisherOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthSkirmisherOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption15);
			NarrativeMenuOption narrativeMenuOption16 = new NarrativeMenuOption("youth_kern_option", new TextObject("{=cDWbwBwI}joined the kern.", null), new TextObject("{=tTb28jyU}Many Battanians fight as kern, versatile troops who could both harass the enemy line with their javelins or join in the final screaming charge once it weakened.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthKernOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthKernOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthKernOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption16);
			NarrativeMenuOption narrativeMenuOption17 = new NarrativeMenuOption("youth_camp_option", new TextObject("{=GFUggps8}marched with the camp followers.", null), new TextObject("{=64rWqBLN}You avoided service with one of the main forces of your realm's armies, but followed instead in the train - the troops' wives, lovers and servants, and those who make their living by caring for, entertaining, or cheating the soldiery.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetYouthCampOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.YouthCampOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.YouthCampOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption17);
			NarrativeMenuOption narrativeMenuOption18 = new NarrativeMenuOption("youth_envoys_guard_first_option", new TextObject("{=YmPlLGXb}served in an envoy's entourage", null), new TextObject("{=qPamcCkA}Your family arranged for you to accompany an envoy. You were not given major responsibilities - mostly carrying arms and trying to look imposing. - but it did give you a chance to travel a lot and socialise and see the world.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEnvoysGuardFirstOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EnvoysGuardFirstOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EnvoysGuardFirstOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption18);
			NarrativeMenuOption narrativeMenuOption19 = new NarrativeMenuOption("youth_envoys_guard_second_option", new TextObject("{=YmPlLGXb}served in an envoy's entourage", null), new TextObject("{=VYU1nEHP}Your family arranged for you to accompany an envoy. You were not given major responsibilities but it did give you a chance to travel and socialise and see a bit of the world.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetEnvoysGuardSecondOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.EnvoysGuardSecondOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.EnvoysGuardSecondOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption19);
		}

		// Token: 0x06003C60 RID: 15456 RVA: 0x00100B80 File Offset: 0x000FED80
		private void GetYouthStaffOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Steward,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C61 RID: 15457 RVA: 0x00100BD4 File Offset: 0x000FEDD4
		private bool YouthStaffOneOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003C62 RID: 15458 RVA: 0x00100BF0 File Offset: 0x000FEDF0
		private bool YouthStaffTwoOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003C63 RID: 15459 RVA: 0x00100C0C File Offset: 0x000FEE0C
		private void YouthStaffOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "retainer";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_decisive");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C64 RID: 15460 RVA: 0x00100CD0 File Offset: 0x000FEED0
		private void GetYouthGroomOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Charm,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C65 RID: 15461 RVA: 0x00100D24 File Offset: 0x000FEF24
		private bool YouthGroomOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003C66 RID: 15462 RVA: 0x00100D40 File Offset: 0x000FEF40
		private void YouthGroomOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "retainer";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C67 RID: 15463 RVA: 0x00100E04 File Offset: 0x000FF004
		private void GetYouthServantOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Steward,
				DefaultSkills.Tactics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C68 RID: 15464 RVA: 0x00100E58 File Offset: 0x000FF058
		private bool YouthServantOneOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C69 RID: 15465 RVA: 0x00100E74 File Offset: 0x000FF074
		private bool YouthServantTwoOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C6A RID: 15466 RVA: 0x00100E90 File Offset: 0x000FF090
		private void YouthServantOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "retainer";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_ready");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C6B RID: 15467 RVA: 0x00100F54 File Offset: 0x000FF154
		private void GetYouthCavalryOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C6C RID: 15468 RVA: 0x00100FA8 File Offset: 0x000FF1A8
		private bool YouthCavalryOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003C6D RID: 15469 RVA: 0x00100FC4 File Offset: 0x000FF1C4
		private void YouthCavalryOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "mercenary";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_apprentice");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C6E RID: 15470 RVA: 0x00101088 File Offset: 0x000FF288
		private void GetYouthHearthOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Polearm
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C6F RID: 15471 RVA: 0x001010DC File Offset: 0x000FF2DC
		private bool YouthHearthOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C70 RID: 15472 RVA: 0x00101118 File Offset: 0x000FF318
		private void YouthHearthOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "mercenary";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_athlete");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C71 RID: 15473 RVA: 0x001011DC File Offset: 0x000FF3DC
		private void GetYouthGuardHighRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crossbow,
				DefaultSkills.Engineering
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C72 RID: 15474 RVA: 0x00101230 File Offset: 0x000FF430
		private bool YouthGuardHighRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia";
		}

		// Token: 0x06003C73 RID: 15475 RVA: 0x0010124C File Offset: 0x000FF44C
		private void YouthGuardHighRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_vibrant");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C74 RID: 15476 RVA: 0x00101310 File Offset: 0x000FF510
		private void GetYouthGuardLowRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Bow,
				DefaultSkills.Engineering
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C75 RID: 15477 RVA: 0x00101364 File Offset: 0x000FF564
		private bool YouthGuardLowRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003C76 RID: 15478 RVA: 0x00101380 File Offset: 0x000FF580
		private void YouthGuardLowRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C77 RID: 15479 RVA: 0x00101444 File Offset: 0x000FF644
		private void GetYouthGuardGarrisonRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Bow,
				DefaultSkills.Engineering
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C78 RID: 15480 RVA: 0x00101498 File Offset: 0x000FF698
		private bool YouthGuardGarrisonRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003C79 RID: 15481 RVA: 0x001014FC File Offset: 0x000FF6FC
		private void YouthGuardGarrisonRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C7A RID: 15482 RVA: 0x001015C0 File Offset: 0x000FF7C0
		private void GetYouthGuardEmpireRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Crossbow,
				DefaultSkills.Engineering
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
		}

		// Token: 0x06003C7B RID: 15483 RVA: 0x00101614 File Offset: 0x000FF814
		private bool YouthGuardEmpireRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire";
		}

		// Token: 0x06003C7C RID: 15484 RVA: 0x00101630 File Offset: 0x000FF830
		private void YouthGuardEmpireRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C7D RID: 15485 RVA: 0x001016F4 File Offset: 0x000FF8F4
		private void GetYouthRiderHighRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C7E RID: 15486 RVA: 0x00101748 File Offset: 0x000FF948
		private bool YouthRiderHighRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C7F RID: 15487 RVA: 0x00101784 File Offset: 0x000FF984
		private void YouthRiderHighRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "hunter";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_sturgia_mp_warrior_axe");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C80 RID: 15488 RVA: 0x00101848 File Offset: 0x000FFA48
		private void GetYouthRiderLowRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Riding,
				DefaultSkills.Bow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
		}

		// Token: 0x06003C81 RID: 15489 RVA: 0x0010189C File Offset: 0x000FFA9C
		private bool YouthRiderLowRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003C82 RID: 15490 RVA: 0x001018D8 File Offset: 0x000FFAD8
		private void YouthRiderLowRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "hunter";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_sturgia_mp_huskarl_idle");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C83 RID: 15491 RVA: 0x0010199C File Offset: 0x000FFB9C
		private void GetYouthInfantryOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Polearm,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
		}

		// Token: 0x06003C84 RID: 15492 RVA: 0x001019F0 File Offset: 0x000FFBF0
		private bool YouthInfantryOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003C85 RID: 15493 RVA: 0x00101AA8 File Offset: 0x000FFCA8
		private void YouthInfantryOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "infantry";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_fierce");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C86 RID: 15494 RVA: 0x00101B6C File Offset: 0x000FFD6C
		private void GetYouthSkirmisherOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Throwing,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C87 RID: 15495 RVA: 0x00101BC0 File Offset: 0x000FFDC0
		private bool YouthSkirmisherOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003C88 RID: 15496 RVA: 0x00101C5C File Offset: 0x000FFE5C
		private void YouthSkirmisherOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "skirmisher";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_fox");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C89 RID: 15497 RVA: 0x00101D20 File Offset: 0x000FFF20
		private void GetYouthKernOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Throwing,
				DefaultSkills.OneHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
		}

		// Token: 0x06003C8A RID: 15498 RVA: 0x00101D74 File Offset: 0x000FFF74
		private bool YouthKernOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003C8B RID: 15499 RVA: 0x00101D90 File Offset: 0x000FFF90
		private void YouthKernOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "kern";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_apprentice");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C8C RID: 15500 RVA: 0x00101E54 File Offset: 0x00100054
		private void GetYouthCampOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Roguery,
				DefaultSkills.Throwing
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
		}

		// Token: 0x06003C8D RID: 15501 RVA: 0x00101EA8 File Offset: 0x001000A8
		private bool YouthCampOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia";
		}

		// Token: 0x06003C8E RID: 15502 RVA: 0x00101EE4 File Offset: 0x001000E4
		private void YouthCampOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "bard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_militia");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C8F RID: 15503 RVA: 0x00101FA8 File Offset: 0x001001A8
		private void GetEnvoysGuardFirstOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Charm,
				DefaultSkills.Scouting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C90 RID: 15504 RVA: 0x00101FFC File Offset: 0x001001FC
		private void GetEnvoysGuardSecondOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Charm,
				DefaultSkills.Scouting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
		}

		// Token: 0x06003C91 RID: 15505 RVA: 0x00102050 File Offset: 0x00100250
		private bool EnvoysGuardFirstOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait";
		}

		// Token: 0x06003C92 RID: 15506 RVA: 0x0010208A File Offset: 0x0010028A
		private bool EnvoysGuardSecondOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai";
		}

		// Token: 0x06003C93 RID: 15507 RVA: 0x001020C4 File Offset: 0x001002C4
		private void EnvoysGuardFirstOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C94 RID: 15508 RVA: 0x00102188 File Offset: 0x00100388
		private void EnvoysGuardSecondOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.SelectedTitleType = "guard";
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_youth_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.SetEquipment(Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId));
				}
			}
		}

		// Token: 0x06003C95 RID: 15509 RVA: 0x0010224C File Offset: 0x0010044C
		private List<NarrativeMenuCharacterArgs> GetAdultMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacterArgs> list = new List<NarrativeMenuCharacterArgs>();
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			list.Add(new NarrativeMenuCharacterArgs("player_adulthood_character", 20, playerEquipmentId, "act_childhood_schooled", "spawnpoint_player_1", "", "", null, true, CharacterObject.PlayerCharacter.IsFemale));
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
			ItemObject item = @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item;
			list.Add(new NarrativeMenuCharacterArgs("narrative_character_horse", -1, "", "act_horse_stand_1", "spawnpoint_mount_1", @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item.StringId, @object.DefaultEquipment[EquipmentIndex.HorseHarness].Item.StringId, MountCreationKey.GetRandomMountKey(item, CharacterObject.PlayerCharacter.GetMountKeySeed()), false, false));
			return list;
		}

		// Token: 0x06003C96 RID: 15510 RVA: 0x0010234C File Offset: 0x0010054C
		private void AddAdulthoodMenu(CharacterCreationManager characterCreationManager)
		{
			BodyProperties bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1);
			bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, 20f);
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("player_adulthood_character", bodyProperties, CharacterObject.PlayerCharacter.Race, CharacterObject.PlayerCharacter.IsFemale);
			NarrativeMenuCharacter item2 = new NarrativeMenuCharacter("narrative_character_horse");
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			list.Add(item);
			list.Add(item2);
			MBTextManager.SetTextVariable("EXP_VALUE", this._skillLevelToAdd);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_adulthood_menu", "narrative_youth_menu", "narrative_age_selection_menu", new TextObject("{=MafIe9yI}Young Adulthood", null), new TextObject("{=4WYY0X59}Before you set out for a life of adventure, your biggest achievement was...", null), list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetAdultMenuNarrativeMenuCharacterArgs));
			this.AddAdulthoodMenuOptions(narrativeMenu);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003C97 RID: 15511 RVA: 0x00102414 File Offset: 0x00100614
		private void AddAdulthoodMenuOptions(NarrativeMenu narrativeMenu)
		{
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("adulthood_defeated_enemy_option", new TextObject("{=8bwpVpgy}you defeated an enemy in battle.", null), new TextObject("{=1IEroJKs}Not everyone who musters for the levy marches to war, and not everyone who goes on campaign sees action. You did both, and you also took down an enemy warrior in direct one-to-one combat, in the full view of your comrades.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodDefeatedEnemyOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodDefeatedEnemyOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodDefeatedEnemyOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("adulthood_manhunt_option", new TextObject("{=mP3uFbcq}you led a successful manhunt.", null), new TextObject("{=4f5xwzX0}When your community needed to organize a posse to pursue horse thieves, you were the obvious choice. You hunted down the raiders, surrounded them and forced their surrender, and took back your stolen property.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodManhuntOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodManhuntOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodManhuntOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("adulthood_caravan_leader_option", new TextObject("{=wfbtS71d}you led a caravan.", null), new TextObject("{=joRHKCkm}Your family needed someone trustworthy to take a caravan to a neighboring town. You organized supplies, ensured a constant watch to keep away bandits, and brought it safely to its destination.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodCaravanLeaderOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodCaravanLeaderOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodCaravanLeaderOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("adulthood_saved_village_option", new TextObject("{=x1HTX5hq}you saved your village from a flood.", null), new TextObject("{=bWlmGDf3}When a sudden storm caused the local stream to rise suddenly, your neighbors needed quick-thinking leadership. You provided it, directing them to build levees to save their homes.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodSavedVillageOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodSavedVillageOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodSavedVillageOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			NarrativeMenuOption narrativeMenuOption5 = new NarrativeMenuOption("adulthood_saved_city_option", new TextObject("{=s8PNllPN}you saved your city quarter from a fire.", null), new TextObject("{=ZAGR6PYc}When a sudden blaze broke out in a back alley, your neighbors needed quick-thinking leadership and you provided it. You organized a bucket line to the nearest well, putting the fire out before any homes were lost.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodSavedCityOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodSavedCityOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodSavedCityOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption5);
			NarrativeMenuOption narrativeMenuOption6 = new NarrativeMenuOption("adulthood_workshop_option", new TextObject("{=xORjDTal}you invested some money in a workshop.", null), new TextObject("{=PyVqDLBu}Your parents didn't give you much money, but they did leave just enough for you to secure a loan against a larger amount to build a small workshop. You paid back what you borrowed, and sold your enterprise for a profit.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodWorkshopOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodWorkshopOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodWorkshopOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption6);
			NarrativeMenuOption narrativeMenuOption7 = new NarrativeMenuOption("adulthood_investor_option", new TextObject("{=xKXcqRJI}you invested some money in land.", null), new TextObject("{=cbF9jdQo}Your parents didn't give you much money, but they did leave just enough for you to purchase a plot of unused land at the edge of the village. You cleared away rocks and dug an irrigation ditch, raised a few seasons of crops, than sold it for a considerable profit.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodInvestorOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodInvestorOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodInvestorOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption7);
			NarrativeMenuOption narrativeMenuOption8 = new NarrativeMenuOption("adulthood_hunter_option", new TextObject("{=TbNRtUjb}you hunted a dangerous animal.", null), new TextObject("{=I3PcdaaL}Wolves, bears are a constant menace to the flocks of northern Calradia, while hyenas and leopards trouble the south. You went with a group of your fellow villagers and fired the missile that brought down the beast.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodHunterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodHunterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodHunterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption8);
			NarrativeMenuOption narrativeMenuOption9 = new NarrativeMenuOption("adulthood_siege_survivor_option", new TextObject("{=WbHfGCbd}you survived a siege.", null), new TextObject("{=FhZPjhli}Your hometown was briefly placed under siege, and you were called to defend the walls. Everyone did their part to repulse the enemy assault, and everyone is justly proud of what they endured.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodSiegeSurvivorOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodSiegeSurvivorOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodSiegeSurvivorOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption9);
			NarrativeMenuOption narrativeMenuOption10 = new NarrativeMenuOption("adulthood_escapade_high_register_option", new TextObject("{=kNXet6Um}you had a famous escapade in town.", null), new TextObject("{=DjeAJtix}Maybe it was a love affair, or maybe you cheated at dice, or maybe you just chose your words poorly when drinking with a dangerous crowd. Anyway, on one of your trips into town you got into the kind of trouble from which only a quick tongue or quick feet get you out alive.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodEscapadeHighRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodEscapadeHighRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodEscapadeHighRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption10);
			NarrativeMenuOption narrativeMenuOption11 = new NarrativeMenuOption("adulthood_escapade_low_register_option", new TextObject("{=qlOuiKXj}you had a famous escapade.", null), new TextObject("{=lD5Ob3R4}Maybe it was a love affair, or maybe you cheated at dice, or maybe you just chose your words poorly when drinking with a dangerous crowd. Anyway, you got into the kind of trouble from which only a quick tongue or quick feet get you out alive.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodEscapadeLowRegisterOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodEscapadeLowRegisterOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodEscapadeLowRegisterOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption11);
			NarrativeMenuOption narrativeMenuOption12 = new NarrativeMenuOption("adulthood_nice_person_option", new TextObject("{=Yqm0Dics}you treated people well.", null), new TextObject("{=dDmcqTzb}Yours wasn't the kind of reputation that local legends are made of, but it was the kind that wins you respect among those around you. You were consistently fair and honest in your business dealings and helpful to those in trouble. In doing so, you got a sense of what made people tick.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAdulthoodNicePersonOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AdulthoodNicePersonOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AdulthoodNicePersonOptionOnSelect), null);
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption12);
		}

		// Token: 0x06003C98 RID: 15512 RVA: 0x001027D0 File Offset: 0x001009D0
		private void GetAdulthoodDefeatedEnemyOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.OneHanded,
				DefaultSkills.TwoHanded
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Vigor, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Valor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(20);
		}

		// Token: 0x06003C99 RID: 15513 RVA: 0x00102849 File Offset: 0x00100A49
		private bool AdulthoodDefeatedEnemyOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003C9A RID: 15514 RVA: 0x0010284C File Offset: 0x00100A4C
		private void AdulthoodDefeatedEnemyOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_athlete");
				}
			}
		}

		// Token: 0x06003C9B RID: 15515 RVA: 0x001028BC File Offset: 0x00100ABC
		private void GetAdulthoodManhuntOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Tactics,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Calculating
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003C9C RID: 15516 RVA: 0x00102938 File Offset: 0x00100B38
		private bool AdulthoodManhuntOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation) && (characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait");
		}

		// Token: 0x06003C9D RID: 15517 RVA: 0x001029E8 File Offset: 0x00100BE8
		private void AdulthoodManhuntOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_battania_mp_clan_warrior_shieldperk_idle");
				}
			}
		}

		// Token: 0x06003C9E RID: 15518 RVA: 0x00102A58 File Offset: 0x00100C58
		private void GetAdulthoodCaravanLeaderOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Calculating
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003C9F RID: 15519 RVA: 0x00102AD4 File Offset: 0x00100CD4
		private bool AdulthoodCaravanLeaderOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation) && (characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "vlandia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "empire" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "aserai" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "khuzait" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "nord");
		}

		// Token: 0x06003CA0 RID: 15520 RVA: 0x00102BA4 File Offset: 0x00100DA4
		private void AdulthoodCaravanLeaderOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_ready_handshield");
				}
			}
		}

		// Token: 0x06003CA1 RID: 15521 RVA: 0x00102C14 File Offset: 0x00100E14
		private void GetAdulthoodSavedVillageOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Tactics,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Valor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003CA2 RID: 15522 RVA: 0x00102C90 File Offset: 0x00100E90
		private bool AdulthoodSavedVillageOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation) && (characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "sturgia" || characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "nord");
		}

		// Token: 0x06003CA3 RID: 15523 RVA: 0x00102CEC File Offset: 0x00100EEC
		private void AdulthoodSavedVillageOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_drafted_to_war_pose");
				}
			}
		}

		// Token: 0x06003CA4 RID: 15524 RVA: 0x00102D5C File Offset: 0x00100F5C
		private void GetAdulthoodSavedCityOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Tactics,
				DefaultSkills.Leadership
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Cunning, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Calculating
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003CA5 RID: 15525 RVA: 0x00102DD5 File Offset: 0x00100FD5
		private bool AdulthoodSavedCityOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation) && characterCreationManager.CharacterCreationContent.SelectedCulture.StringId == "battania";
		}

		// Token: 0x06003CA6 RID: 15526 RVA: 0x00102E08 File Offset: 0x00101008
		private void AdulthoodSavedCityOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_vibrant");
				}
			}
		}

		// Token: 0x06003CA7 RID: 15527 RVA: 0x00102E78 File Offset: 0x00101078
		private void GetAdulthoodWorkshopOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Crafting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Calculating
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003CA8 RID: 15528 RVA: 0x00102EF1 File Offset: 0x001010F1
		private bool AdulthoodWorkshopOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CA9 RID: 15529 RVA: 0x00102F04 File Offset: 0x00101104
		private void AdulthoodWorkshopOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_decisive");
				}
			}
		}

		// Token: 0x06003CAA RID: 15530 RVA: 0x00102F74 File Offset: 0x00101174
		private void GetAdulthoodInvestorOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Trade,
				DefaultSkills.Crafting
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Intelligence, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Calculating
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(10);
		}

		// Token: 0x06003CAB RID: 15531 RVA: 0x00102FED File Offset: 0x001011ED
		private bool AdulthoodInvestorOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CAC RID: 15532 RVA: 0x00103004 File Offset: 0x00101204
		private void AdulthoodInvestorOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_decisive");
				}
			}
		}

		// Token: 0x06003CAD RID: 15533 RVA: 0x00103074 File Offset: 0x00101274
		private void GetAdulthoodHunterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Polearm,
				DefaultSkills.Athletics
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Valor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(5);
		}

		// Token: 0x06003CAE RID: 15534 RVA: 0x001030EC File Offset: 0x001012EC
		private bool AdulthoodHunterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CAF RID: 15535 RVA: 0x00103104 File Offset: 0x00101304
		private void AdulthoodHunterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_tough");
				}
			}
		}

		// Token: 0x06003CB0 RID: 15536 RVA: 0x00103174 File Offset: 0x00101374
		private void GetAdulthoodSiegeSurvivorOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Bow,
				DefaultSkills.Crossbow
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Control, this._attributeLevelToAdd);
			args.SetRenownToAdd(5);
		}

		// Token: 0x06003CB1 RID: 15537 RVA: 0x001031CF File Offset: 0x001013CF
		private bool AdulthoodSiegeSurvivorOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CB2 RID: 15538 RVA: 0x001031E4 File Offset: 0x001013E4
		private void AdulthoodSiegeSurvivorOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_tough");
				}
			}
		}

		// Token: 0x06003CB3 RID: 15539 RVA: 0x00103254 File Offset: 0x00101454
		private void GetAdulthoodEscapadeHighRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Roguery
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Valor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(5);
		}

		// Token: 0x06003CB4 RID: 15540 RVA: 0x001032CC File Offset: 0x001014CC
		private bool AdulthoodEscapadeHighRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return !CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CB5 RID: 15541 RVA: 0x001032E4 File Offset: 0x001014E4
		private void AdulthoodEscapadeHighRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_clever");
				}
			}
		}

		// Token: 0x06003CB6 RID: 15542 RVA: 0x00103354 File Offset: 0x00101554
		private void GetAdulthoodEscapadeLowRegisterOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Athletics,
				DefaultSkills.Roguery
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Endurance, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Valor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(5);
		}

		// Token: 0x06003CB7 RID: 15543 RVA: 0x001033CC File Offset: 0x001015CC
		private bool AdulthoodEscapadeLowRegisterOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return CharacterCreationCampaignBehavior.CharacterOccupationTypes.IsUrbanOccupation(characterCreationManager.CharacterCreationContent.SelectedParentOccupation);
		}

		// Token: 0x06003CB8 RID: 15544 RVA: 0x001033E0 File Offset: 0x001015E0
		private void AdulthoodEscapadeLowRegisterOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_clever");
				}
			}
		}

		// Token: 0x06003CB9 RID: 15545 RVA: 0x00103450 File Offset: 0x00101650
		private void GetAdulthoodNicePersonOptionArgs(NarrativeMenuOptionArgs args)
		{
			SkillObject[] affectedSkills = new SkillObject[]
			{
				DefaultSkills.Charm,
				DefaultSkills.Steward
			};
			args.SetAffectedSkills(affectedSkills);
			args.SetFocusToSkills(this._focusToAdd);
			args.SetLevelToSkills(this._skillLevelToAdd);
			args.SetLevelToAttribute(DefaultCharacterAttributes.Social, this._attributeLevelToAdd);
			TraitObject[] affectedTraits = new TraitObject[]
			{
				DefaultTraits.Mercy,
				DefaultTraits.Generosity,
				DefaultTraits.Honor
			};
			args.SetAffectedTraits(affectedTraits);
			args.SetLevelToTraits(1);
			args.SetRenownToAdd(5);
		}

		// Token: 0x06003CBA RID: 15546 RVA: 0x001034D8 File Offset: 0x001016D8
		private bool AdulthoodNicePersonOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003CBB RID: 15547 RVA: 0x001034DC File Offset: 0x001016DC
		private void AdulthoodNicePersonOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_adulthood_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_manners");
				}
			}
		}

		// Token: 0x06003CBC RID: 15548 RVA: 0x0010354C File Offset: 0x0010174C
		private List<NarrativeMenuCharacterArgs> GetAgeSelectionMenuNarrativeMenuCharacterArgs(CultureObject culture, string occupationType, CharacterCreationManager characterCreationManager)
		{
			List<NarrativeMenuCharacterArgs> list = new List<NarrativeMenuCharacterArgs>();
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			list.Add(new NarrativeMenuCharacterArgs("player_age_selection_character", characterCreationManager.CharacterCreationContent.StartingAge, playerEquipmentId, "act_childhood_schooled", "spawnpoint_player_1", "", "", null, true, CharacterObject.PlayerCharacter.IsFemale));
			MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
			ItemObject item = @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item;
			list.Add(new NarrativeMenuCharacterArgs("narrative_character_horse", -1, "", "act_horse_stand_1", "spawnpoint_mount_1", @object.DefaultEquipment[EquipmentIndex.ArmorItemEndSlot].Item.StringId, @object.DefaultEquipment[EquipmentIndex.HorseHarness].Item.StringId, MountCreationKey.GetRandomMountKey(item, CharacterObject.PlayerCharacter.GetMountKeySeed()), false, false));
			return list;
		}

		// Token: 0x06003CBD RID: 15549 RVA: 0x00103654 File Offset: 0x00101854
		private void AddAgeSelectionMenu(CharacterCreationManager characterCreationManager)
		{
			MBTextManager.SetTextVariable("EXP_VALUE", this._skillLevelToAdd);
			BodyProperties bodyProperties = CharacterObject.PlayerCharacter.GetBodyProperties(CharacterObject.PlayerCharacter.Equipment, -1);
			bodyProperties = FaceGen.GetBodyPropertiesWithAge(ref bodyProperties, (float)characterCreationManager.CharacterCreationContent.StartingAge);
			NarrativeMenuCharacter item = new NarrativeMenuCharacter("player_age_selection_character", bodyProperties, CharacterObject.PlayerCharacter.Race, CharacterObject.PlayerCharacter.IsFemale);
			NarrativeMenuCharacter item2 = new NarrativeMenuCharacter("narrative_character_horse");
			List<NarrativeMenuCharacter> list = new List<NarrativeMenuCharacter>();
			list.Add(item);
			list.Add(item2);
			NarrativeMenu narrativeMenu = new NarrativeMenu("narrative_age_selection_menu", "narrative_adulthood_menu", "", new TextObject("{=HDFEAYDk}Starting Age", null), new TextObject("{=VlOGrGSn}Your character started off on the adventuring path at the age of...", null), list, new NarrativeMenu.GetNarrativeMenuCharacterArgsDelegate(this.GetAgeSelectionMenuNarrativeMenuCharacterArgs));
			NarrativeMenuOption narrativeMenuOption = new NarrativeMenuOption("age_selection_young_adult_option", new TextObject("{=!}20", null), new TextObject("{=2k7adlh7}While lacking experience a bit, you are full with youthful energy, you are fully eager, for the long years of adventuring ahead.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAgeSelectionYoungAdultAgeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AgeSelectionYoungAdultAgeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AgeSelectionYoungAdultAgeOptionOnSelect), new NarrativeMenuOptionOnConsequenceDelegate(this.AgeSelectionYoungAdultAgeOptionOnConsequence));
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption);
			NarrativeMenuOption narrativeMenuOption2 = new NarrativeMenuOption("age_selection_adult_option", new TextObject("{=!}30", null), new TextObject("{=NUlVFRtK}You are at your prime, You still have some youthful energy but also have a substantial amount of experience under your belt. ", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAgeSelectionAdultOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AgeSelectionAdultOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AgeSelectionAdultOptionOnSelect), new NarrativeMenuOptionOnConsequenceDelegate(this.AgeSelectionAdultOptionOnConsequence));
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption2);
			NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("age_selection_middle_age_option", new TextObject("{=!}40", null), new TextObject("{=5MxTYApM}This is the right age for starting off, you have years of experience, and you are old enough for people to respect you and gather under your banner.", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAgeSelectionMiddleAgeOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AgeSelectionMiddleAgeOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AgeSelectionMiddleAgeOptionOnSelect), new NarrativeMenuOptionOnConsequenceDelegate(this.AgeSelectionMiddleAgeOptionOnConsequence));
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption3);
			NarrativeMenuOption narrativeMenuOption4 = new NarrativeMenuOption("age_selection_elder_option", new TextObject("{=!}50", null), new TextObject("{=ePD5Afvy}While you are past your prime, there is still enough time to go on that last big adventure for you. And you have all the experience you need to overcome anything!", null), new GetNarrativeMenuOptionArgsDelegate(this.GetAgeSelectionElderOptionArgs), new NarrativeMenuOptionOnConditionDelegate(this.AgeSelectionElderOptionOnCondition), new NarrativeMenuOptionOnSelectDelegate(this.AgeSelectionElderOptionOnSelect), new NarrativeMenuOptionOnConsequenceDelegate(this.AgeSelectionElderOptionOnConsequence));
			narrativeMenu.AddNarrativeMenuOption(narrativeMenuOption4);
			characterCreationManager.AddNewMenu(narrativeMenu);
		}

		// Token: 0x06003CBE RID: 15550 RVA: 0x00103887 File Offset: 0x00101A87
		private void GetAgeSelectionYoungAdultAgeOptionArgs(NarrativeMenuOptionArgs args)
		{
			args.SetUnspentFocusToAdd(2);
			args.SetUnspentAttributeToAdd(1);
		}

		// Token: 0x06003CBF RID: 15551 RVA: 0x00103897 File Offset: 0x00101A97
		private bool AgeSelectionYoungAdultAgeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003CC0 RID: 15552 RVA: 0x0010389C File Offset: 0x00101A9C
		private void AgeSelectionYoungAdultAgeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_age_selection_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_focus");
					narrativeMenuCharacter.ChangeAge(20f);
					MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
					if (@object == null)
					{
						Debug.FailedAssert("character creation menu character equipment should not be null!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\CharacterCreationCampaignBehavior.cs", "AgeSelectionYoungAdultAgeOptionOnSelect", 4884);
						@object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");
					}
					narrativeMenuCharacter.SetEquipment(@object);
					break;
				}
			}
			characterCreationManager.CharacterCreationContent.StartingAge = 20;
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-20f));
		}

		// Token: 0x06003CC1 RID: 15553 RVA: 0x001039B4 File Offset: 0x00101BB4
		private void AgeSelectionYoungAdultAgeOptionOnConsequence(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.StartingAge = 20;
			this.ApplyMainHeroEquipment(characterCreationManager);
		}

		// Token: 0x06003CC2 RID: 15554 RVA: 0x001039CA File Offset: 0x00101BCA
		private void GetAgeSelectionAdultOptionArgs(NarrativeMenuOptionArgs args)
		{
			args.SetUnspentFocusToAdd(4);
			args.SetUnspentAttributeToAdd(2);
		}

		// Token: 0x06003CC3 RID: 15555 RVA: 0x001039DA File Offset: 0x00101BDA
		private bool AgeSelectionAdultOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003CC4 RID: 15556 RVA: 0x001039E0 File Offset: 0x00101BE0
		private void AgeSelectionAdultOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_age_selection_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_athlete");
					narrativeMenuCharacter.ChangeAge(30f);
					MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
					if (@object == null)
					{
						Debug.FailedAssert("character creation menu character equipment should not be null!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\CharacterCreationCampaignBehavior.cs", "AgeSelectionAdultOptionOnSelect", 4934);
						@object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");
					}
					narrativeMenuCharacter.SetEquipment(@object);
					break;
				}
			}
			characterCreationManager.CharacterCreationContent.StartingAge = 30;
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-30f));
		}

		// Token: 0x06003CC5 RID: 15557 RVA: 0x00103AF8 File Offset: 0x00101CF8
		private void AgeSelectionAdultOptionOnConsequence(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.StartingAge = 30;
			this.ApplyMainHeroEquipment(characterCreationManager);
		}

		// Token: 0x06003CC6 RID: 15558 RVA: 0x00103B0E File Offset: 0x00101D0E
		private void GetAgeSelectionMiddleAgeOptionArgs(NarrativeMenuOptionArgs args)
		{
			args.SetUnspentFocusToAdd(6);
			args.SetUnspentAttributeToAdd(3);
		}

		// Token: 0x06003CC7 RID: 15559 RVA: 0x00103B1E File Offset: 0x00101D1E
		private bool AgeSelectionMiddleAgeOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003CC8 RID: 15560 RVA: 0x00103B24 File Offset: 0x00101D24
		private void AgeSelectionMiddleAgeOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_age_selection_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_sharp");
					narrativeMenuCharacter.ChangeAge(40f);
					MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
					if (@object == null)
					{
						Debug.FailedAssert("character creation menu character equipment should not be null!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\CharacterCreationCampaignBehavior.cs", "AgeSelectionMiddleAgeOptionOnSelect", 4984);
						@object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");
					}
					narrativeMenuCharacter.SetEquipment(@object);
					break;
				}
			}
			characterCreationManager.CharacterCreationContent.StartingAge = 40;
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-40f));
		}

		// Token: 0x06003CC9 RID: 15561 RVA: 0x00103C3C File Offset: 0x00101E3C
		private void AgeSelectionMiddleAgeOptionOnConsequence(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.StartingAge = 40;
			this.ApplyMainHeroEquipment(characterCreationManager);
		}

		// Token: 0x06003CCA RID: 15562 RVA: 0x00103C52 File Offset: 0x00101E52
		private void GetAgeSelectionElderOptionArgs(NarrativeMenuOptionArgs args)
		{
			args.SetUnspentFocusToAdd(8);
			args.SetUnspentAttributeToAdd(4);
		}

		// Token: 0x06003CCB RID: 15563 RVA: 0x00103C62 File Offset: 0x00101E62
		private bool AgeSelectionElderOptionOnCondition(CharacterCreationManager characterCreationManager)
		{
			return true;
		}

		// Token: 0x06003CCC RID: 15564 RVA: 0x00103C68 File Offset: 0x00101E68
		private void AgeSelectionElderOptionOnSelect(CharacterCreationManager characterCreationManager)
		{
			string playerEquipmentId = this.GetPlayerEquipmentId(characterCreationManager, characterCreationManager.CharacterCreationContent.SelectedTitleType, characterCreationManager.CharacterCreationContent.SelectedCulture.StringId, Hero.MainHero.IsFemale);
			foreach (NarrativeMenuCharacter narrativeMenuCharacter in characterCreationManager.CurrentMenu.Characters)
			{
				if (narrativeMenuCharacter.StringId == "player_age_selection_character")
				{
					narrativeMenuCharacter.SetAnimationId("act_childhood_tough");
					narrativeMenuCharacter.ChangeAge(50f);
					MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(playerEquipmentId);
					if (@object == null)
					{
						Debug.FailedAssert("character creation menu character equipment should not be null!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CampaignBehaviors\\CharacterCreationCampaignBehavior.cs", "AgeSelectionElderOptionOnSelect", 5034);
						@object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");
					}
					narrativeMenuCharacter.SetEquipment(@object);
					break;
				}
			}
			characterCreationManager.CharacterCreationContent.StartingAge = 50;
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-50f));
		}

		// Token: 0x06003CCD RID: 15565 RVA: 0x00103D80 File Offset: 0x00101F80
		private void AgeSelectionElderOptionOnConsequence(CharacterCreationManager characterCreationManager)
		{
			characterCreationManager.CharacterCreationContent.StartingAge = 50;
			this.ApplyMainHeroEquipment(characterCreationManager);
		}

		// Token: 0x06003CCE RID: 15566 RVA: 0x00103D98 File Offset: 0x00101F98
		private void ApplyMainHeroEquipment(CharacterCreationManager characterCreationManager)
		{
			NarrativeMenu narrativeMenuWithId = characterCreationManager.GetNarrativeMenuWithId("narrative_age_selection_menu");
			NarrativeMenuCharacter narrativeMenuCharacter = null;
			foreach (NarrativeMenuCharacter narrativeMenuCharacter2 in narrativeMenuWithId.Characters)
			{
				if (narrativeMenuCharacter2.StringId.Equals("player_age_selection_character"))
				{
					narrativeMenuCharacter = narrativeMenuCharacter2;
					break;
				}
			}
			CharacterObject.PlayerCharacter.Equipment.FillFrom(narrativeMenuCharacter.Equipment.DefaultEquipment, true);
			CharacterObject.PlayerCharacter.FirstCivilianEquipment.FillFrom(narrativeMenuCharacter.Equipment.GetRandomCivilianEquipment(), true);
		}

		// Token: 0x06003CCF RID: 15567 RVA: 0x00103E3C File Offset: 0x0010203C
		public void SetHeroAge(float age)
		{
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-age));
		}

		// Token: 0x04001265 RID: 4709
		private readonly IReadOnlyDictionary<string, string> _occupationToEquipmentMapping = new Dictionary<string, string>
		{
			{
				"retainer",
				"retainer"
			},
			{
				"bard",
				"bard"
			},
			{
				"hunter",
				"hunter"
			},
			{
				"farmer",
				"farmer"
			},
			{
				"herder",
				"herder"
			},
			{
				"healer",
				"healer"
			},
			{
				"mercenary",
				"mercenary"
			},
			{
				"infantry",
				"infantry"
			},
			{
				"skirmisher",
				"skirmisher"
			},
			{
				"kern",
				"kern"
			},
			{
				"guard",
				"guard"
			},
			{
				"retainer_urban",
				"retainer"
			},
			{
				"mercenary_urban",
				"mercenary"
			},
			{
				"merchant_urban",
				"merchant"
			},
			{
				"vagabond_urban",
				"vagabond"
			},
			{
				"artisan_urban",
				"artisan"
			},
			{
				"physician_urban",
				"physician"
			},
			{
				"healer_urban",
				"healer"
			},
			{
				"bard_urban",
				"bard"
			}
		};

		// Token: 0x04001266 RID: 4710
		private const int ChildhoodAge = 7;

		// Token: 0x04001267 RID: 4711
		private const int EducationAge = 12;

		// Token: 0x04001268 RID: 4712
		private const int YouthAge = 17;

		// Token: 0x04001269 RID: 4713
		private const int AccomplishmentAge = 20;

		// Token: 0x0400126A RID: 4714
		private const int ParentAge = 33;

		// Token: 0x0400126B RID: 4715
		private const int YoungAdultAge = 20;

		// Token: 0x0400126C RID: 4716
		private const int AdultAge = 30;

		// Token: 0x0400126D RID: 4717
		private const int MiddleAge = 40;

		// Token: 0x0400126E RID: 4718
		private const int ElderAge = 50;

		// Token: 0x0400126F RID: 4719
		public const int FocusToAddYouthStart = 2;

		// Token: 0x04001270 RID: 4720
		public const int FocusToAddAdultStart = 4;

		// Token: 0x04001271 RID: 4721
		public const int FocusToAddMiddleAgedStart = 6;

		// Token: 0x04001272 RID: 4722
		public const int FocusToAddElderlyStart = 8;

		// Token: 0x04001273 RID: 4723
		public const int AttributeToAddYouthStart = 1;

		// Token: 0x04001274 RID: 4724
		public const int AttributeToAddAdultStart = 2;

		// Token: 0x04001275 RID: 4725
		public const int AttributeToAddMiddleAgedStart = 3;

		// Token: 0x04001276 RID: 4726
		public const int AttributeToAddElderlyStart = 4;

		// Token: 0x04001277 RID: 4727
		public const string MotherNarrativeCharacterStringId = "mother_character";

		// Token: 0x04001278 RID: 4728
		public const string FatherNarrativeCharacterStringId = "father_character";

		// Token: 0x04001279 RID: 4729
		public const string PlayerChildhoodCharacterStringId = "player_childhood_character";

		// Token: 0x0400127A RID: 4730
		public const string PlayerEducationCharacterStringId = "player_education_character";

		// Token: 0x0400127B RID: 4731
		public const string PlayerYouthCharacterStringId = "player_youth_character";

		// Token: 0x0400127C RID: 4732
		public const string PlayerAdulthoodCharacterStringId = "player_adulthood_character";

		// Token: 0x0400127D RID: 4733
		public const string PlayerAgeSelectionCharacterStringId = "player_age_selection_character";

		// Token: 0x0400127E RID: 4734
		public const string HorseNarrativeCharacterStringId = "narrative_character_horse";

		// Token: 0x0400127F RID: 4735
		private int _focusToAdd = 1;

		// Token: 0x04001280 RID: 4736
		private int _skillLevelToAdd = 10;

		// Token: 0x04001281 RID: 4737
		private int _attributeLevelToAdd = 1;

		// Token: 0x020007CC RID: 1996
		private static class CharacterOccupationTypes
		{
			// Token: 0x06006388 RID: 25480 RVA: 0x001C1E10 File Offset: 0x001C0010
			public static bool IsUrbanOccupation(string occupation)
			{
				return occupation == "retainer_urban" || occupation == "mercenary_urban" || occupation == "merchant_urban" || occupation == "vagabond_urban" || occupation == "artisan_urban" || occupation == "physician_urban" || occupation == "healer_urban" || occupation == "bard_urban";
			}

			// Token: 0x04001F70 RID: 8048
			public const string Retainer = "retainer";

			// Token: 0x04001F71 RID: 8049
			public const string Bard = "bard";

			// Token: 0x04001F72 RID: 8050
			public const string Hunter = "hunter";

			// Token: 0x04001F73 RID: 8051
			public const string Farmer = "farmer";

			// Token: 0x04001F74 RID: 8052
			public const string Herder = "herder";

			// Token: 0x04001F75 RID: 8053
			public const string Healer = "healer";

			// Token: 0x04001F76 RID: 8054
			public const string Mercenary = "mercenary";

			// Token: 0x04001F77 RID: 8055
			public const string Infantry = "infantry";

			// Token: 0x04001F78 RID: 8056
			public const string Skirmisher = "skirmisher";

			// Token: 0x04001F79 RID: 8057
			public const string Kern = "kern";

			// Token: 0x04001F7A RID: 8058
			public const string Guard = "guard";

			// Token: 0x04001F7B RID: 8059
			public const string RetainerUrban = "retainer_urban";

			// Token: 0x04001F7C RID: 8060
			public const string MercenaryUrban = "mercenary_urban";

			// Token: 0x04001F7D RID: 8061
			public const string MerchantUrban = "merchant_urban";

			// Token: 0x04001F7E RID: 8062
			public const string VagabondUrban = "vagabond_urban";

			// Token: 0x04001F7F RID: 8063
			public const string ArtisanUrban = "artisan_urban";

			// Token: 0x04001F80 RID: 8064
			public const string PhysicianUrban = "physician_urban";

			// Token: 0x04001F81 RID: 8065
			public const string HealerUrban = "healer_urban";

			// Token: 0x04001F82 RID: 8066
			public const string BardUrban = "bard_urban";
		}
	}
}
