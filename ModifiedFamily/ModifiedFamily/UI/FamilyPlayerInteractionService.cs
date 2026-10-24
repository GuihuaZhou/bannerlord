// Player inbox workflow for accepting, rejecting, or countering incoming proposals.
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Campaign;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using ModifiedFamily.Core;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;

namespace ModifiedFamily.UI
{
    public static class FamilyPlayerInteractionService
    {
        public static void ShowFamilyActions()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement("marriage", new TextObject("{=MF_UI_ArrangeMarriage}安排家族联姻").ToString(), null, true, string.Empty),
                new InquiryElement("companion", new TextObject("{=MF_UI_AdoptCompanion}收养本家族同伴").ToString(), null, true, string.Empty),
                new InquiryElement("external", new TextObject("{=MF_UI_RequestAdoption}向其他家族提出收养").ToString(), null, true, string.Empty),
                new InquiryElement("inbox", new TextObject("{=MF_UI_OpenInbox}查看待处理提议").ToString(), null, true, string.Empty)
            };
            ShowSelection("{=MF_UI_FamilyActions}家族事务", "{=MF_UI_FamilyActionsDescription}请选择需要办理的家族事务。", options, selected =>
            {
                string id = selected?.FirstOrDefault()?.Identifier as string;
                if (id == "marriage") SelectOwnMarriageHero(); else if (id == "companion") SelectCompanion();
                else if (id == "external") SelectExternalAdoptionClan(); else if (id == "inbox") ShowInbox();
            });
        }

        // Opens the marriage workflow directly from the clan screen shortcut.
        public static void ShowMarriageActions() => SelectOwnMarriageHero();

        // Continues marriage negotiation for the member selected on the clan page.
        public static void ShowMarriageActionsFor(Hero hero)
        {
            if (hero != null && hero.Clan == CampaignClan.PlayerClan) SelectMarriageTargetClan(hero);
        }

        // Continues from the visual clan/hero selection screen to terms.
        public static void ShowMarriageTermsFor(Hero own, Hero other)
        {
            if (own != null && other != null) SelectMarriageTerms(own, other);
        }

        // Offers the two supported adoption sources without mixing them into marriage choices.
        public static void ShowAdoptionActions()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement("companion", new TextObject("{=MF_UI_AdoptCompanion}收养本家族同伴").ToString(), null, true, string.Empty),
                new InquiryElement("external", new TextObject("{=MF_UI_RequestAdoption}向其他家族提出收养").ToString(), null, true, string.Empty)
            };
            ShowSelection("{=MF_UI_AdoptionActionsShort}办理收养", "{=MF_UI_AdoptionActionsDescription}请选择被收养人的来源。", options, selected =>
            {
                string id = selected?.FirstOrDefault()?.Identifier as string;
                if (id == "companion") SelectCompanion(); else if (id == "external") SelectExternalAdoptionClan();
            });
        }

        // Continues companion adoption for the companion selected on the clan page.
        public static void ShowAdoptionActionsFor(Hero hero)
        {
            if (hero != null && hero.CompanionOf == CampaignClan.PlayerClan)
                ShowAdoptionResult(Service?.TryCreateAdoptionProposal(new AdoptionProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = null, AdopterHero = Hero.MainHero, AdopteeHero = hero, DestinationClan = CampaignClan.PlayerClan }));
        }

        // Opens the persistent proposal inbox from the clan screen shortcut.
        public static void ShowProposalInbox() => ShowInbox();

        private static void SelectOwnMarriageHero()
        {
            var candidates = CampaignClan.PlayerClan.Heroes.Where(h => h != Hero.MainHero && MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed)
                .Select(h => HeroElement(h)).ToList();
            ShowSelection("{=MF_UI_SelectOwnHero}选择本家族联姻成员", "{=MF_UI_SelectOwnHeroDesc}请选择需要安排婚姻的家族成员。", candidates,
                selected => { Hero hero = selected?.FirstOrDefault()?.Identifier as Hero; if (hero != null) SelectMarriageTargetClan(hero); });
        }

        private static void SelectMarriageTargetClan(Hero own)
        {
            var clans = CampaignClan.All.Where(c => c != CampaignClan.PlayerClan && MarriageEligibilityModel.CanUseClan(c).IsAllowed &&
                c.Heroes.Any(h => h.IsFemale != own.IsFemale && MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed && HasValidMarriageDirection(own, h)))
                .OrderBy(c => c.Name.ToString()).Select(c => new InquiryElement(c, c.Name.ToString(), null, true, c.Kingdom?.Name.ToString() ?? string.Empty)).ToList();
            ShowSelection("{=MF_UI_SelectTargetClan}选择联姻家族", "{=MF_UI_SelectTargetClanDesc}请选择准备提出联姻的目标家族。", clans,
                selected => { CampaignClan clan = selected?.FirstOrDefault()?.Identifier as CampaignClan; if (clan != null) SelectMarriageTargetHero(own, clan); });
        }

        private static void SelectMarriageTargetHero(Hero own, CampaignClan targetClan)
        {
            var candidates = targetClan.Heroes.Where(h => h.IsFemale != own.IsFemale && MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed && HasValidMarriageDirection(own, h))
                .Select(HeroElement).ToList();
            ShowSelection("{=MF_UI_SelectTargetHero}选择联姻对象", "{=MF_UI_SelectTargetHeroDesc}请选择目标家族中的联姻对象。", candidates,
                selected => { Hero other = selected?.FirstOrDefault()?.Identifier as Hero; if (other != null) SelectMarriageTerms(own, other); });
        }

        private static void SelectMarriageTerms(Hero own, Hero other)
        {
            var options = new List<InquiryElement>();
            foreach (MarriageDirection direction in new[] { MarriageDirection.FemaleJoinsMaleClan, MarriageDirection.MaleJoinsFemaleClan })
            {
                options.Add(new InquiryElement(new MarriageTerms(direction, 0), TermLabel(direction, 0), null, true, string.Empty));
                options.Add(new InquiryElement(new MarriageTerms(direction, 1), TermLabel(direction, 1), null, true, string.Empty));
                options.Add(new InquiryElement(new MarriageTerms(direction, 2), TermLabel(direction, 2), null, true, string.Empty));
            }
            ShowSelection("{=MF_UI_SelectMarriageTerms}设置联姻条件", "{=MF_UI_SelectMarriageTermsDesc}请选择婚后归属及礼金方向。礼金金额可在下一步填写。", options,
                selected => { var terms = selected?.FirstOrDefault()?.Identifier as MarriageTerms; if (terms != null) CompleteMarriageTerms(own, other, terms); });
        }

        private static void CompleteMarriageTerms(Hero own, Hero other, MarriageTerms terms)
        {
            if (terms.GiftMode == 0) { SubmitMarriage(own, other, terms, 0); return; }
            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=MF_UI_GiftAmount}礼金金额").ToString(), "0", true, true,
                new TextObject("{=MF_UI_Confirm}确认").ToString(), new TextObject("{=MF_UI_Close}关闭").ToString(),
                value => SubmitMarriage(own, other, terms, int.Parse(value)), null, false,
                value => { int amount; bool ok = int.TryParse(value, out amount) && amount >= 0 && (terms.GiftMode != 1 || Hero.MainHero.Gold >= amount); return new System.Tuple<bool, string>(ok, ok ? string.Empty : new TextObject("{=MF_UI_InvalidGift}请输入有效的非负金额；由玩家支付时还必须保证余额充足。").ToString()); }, string.Empty, string.Empty), false, false);
        }

        private static void SubmitMarriage(Hero own, Hero other, MarriageTerms terms, int gift)
        {
            CampaignClan payer = terms.GiftMode == 1 ? CampaignClan.PlayerClan : terms.GiftMode == 2 ? other.Clan : null;
            FamilyResult result = Service?.TryCreateMarriageProposal(new MarriageProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = other.Clan, HeroA = own, HeroB = other, Direction = terms.Direction, GoldPayerClan = payer, GiftAmount = gift });
            ShowResult(result);
        }

        private static void SelectCompanion()
        {
            var items = CampaignClan.PlayerClan.Companions.Select(HeroElement).ToList();
            ShowSelection("{=MF_UI_SelectCompanion}选择需要收养的同伴", "{=MF_UI_SelectCompanionDesc}该同伴将退出同伴名册并成为正式家族成员。", items,
                selected => { Hero companion = selected?.FirstOrDefault()?.Identifier as Hero; if (companion != null) SelectAdopter(companion, null); });
        }

        private static void SelectExternalAdoptionClan()
        {
            var clans = CampaignClan.All.Where(c => c != CampaignClan.PlayerClan && !c.IsEliminated && !c.IsBanditFaction && !c.IsRebelClan)
                .OrderBy(c => c.Name.ToString()).Select(c => new InquiryElement(c, c.Name.ToString(), null, true, c.Kingdom?.Name.ToString() ?? string.Empty)).ToList();
            ShowSelection("{=MF_UI_SelectAdoptionClan}选择目标家族", "{=MF_UI_SelectAdoptionClanDesc}请选择被收养人当前所属的家族。", clans,
                selected => { CampaignClan clan = selected?.FirstOrDefault()?.Identifier as CampaignClan; if (clan != null) SelectExternalAdoptee(clan); });
        }

        private static void SelectExternalAdoptee(CampaignClan source)
        {
            var heroes = source.Heroes.Where(h => !h.IsDead && h != source.Leader).Select(HeroElement).ToList();
            ShowSelection("{=MF_UI_SelectAdoptee}选择被收养人", "{=MF_UI_SelectAdopteeDesc}请选择希望纳入玩家家族的成员。", heroes,
                selected => { Hero hero = selected?.FirstOrDefault()?.Identifier as Hero; if (hero != null) SelectAdopter(hero, source); });
        }

        private static void SelectAdopter(Hero adoptee, CampaignClan receiver)
        {
            var adopters = CampaignClan.PlayerClan.Heroes.Where(h => !h.IsDead).Select(HeroElement).ToList();
            ShowSelection("{=MF_UI_SelectAdopter}选择收养人", "{=MF_UI_SelectAdopterDesc}请选择负责建立法定收养关系的家族成员。", adopters,
                selected => { Hero adopter = selected?.FirstOrDefault()?.Identifier as Hero; if (adopter != null) ShowResult(Service?.TryCreateAdoptionProposal(new AdoptionProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = receiver, AdopterHero = adopter, AdopteeHero = adoptee, DestinationClan = CampaignClan.PlayerClan })); });
        }

        private static void ShowInbox()
        {
            var incoming = Service?.GetIncoming(CampaignClan.PlayerClan) ?? new List<FamilyProposal>();
            var items = incoming.Select(p => new InquiryElement(p, p.ProposalType + " - " + (p.ProposerClan?.Name.ToString() ?? "-"), null, true, p.ProposalId)).ToList();
            ShowSelection("{=MF_UI_OpenInbox}查看待处理提议", "{=MF_UI_InboxDesc}请选择需要查看的提议。", items,
                selected => { FamilyProposal p = selected?.FirstOrDefault()?.Identifier as FamilyProposal; if (p != null) ShowIncomingProposal(p.ProposalId); });
        }

        private static FamilyProposalService Service => TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
        private static bool HasValidMarriageDirection(Hero own, Hero other)
        {
            FamilyProposalService service = Service;
            return service != null && (service.CanCreateMarriageProposal(new MarriageProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = other.Clan, HeroA = own, HeroB = other, Direction = MarriageDirection.FemaleJoinsMaleClan }).IsAllowed
                || service.CanCreateMarriageProposal(new MarriageProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = other.Clan, HeroA = own, HeroB = other, Direction = MarriageDirection.MaleJoinsFemaleClan }).IsAllowed);
        }
        private static InquiryElement HeroElement(Hero hero) => new InquiryElement(hero, hero.Name.ToString(), null, true, hero.Clan?.Name.ToString() ?? string.Empty);
        private static void ShowSelection(string title, string description, List<InquiryElement> items, System.Action<List<InquiryElement>> done)
        {
            if (items == null || items.Count == 0) { ShowMessage("{=MF_UI_NoCandidates}当前没有符合条件的对象。"); return; }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(new TextObject(title).ToString(), new TextObject(description).ToString(), items, true, 1, 1,
                new TextObject("{=MF_UI_Confirm}确认").ToString(), new TextObject("{=MF_UI_Close}关闭").ToString(), done, null, string.Empty, false), false, false);
        }
        private static string TermLabel(MarriageDirection direction, int giftMode)
        {
            string directionText = direction == MarriageDirection.FemaleJoinsMaleClan ? new TextObject("{=MF_UI_DirectionNormal}女方加入男方家族").ToString() : new TextObject("{=MF_UI_DirectionMatrilocal}男方加入女方家族").ToString();
            string giftText = giftMode == 0 ? new TextObject("{=MF_UI_NoGift}不设礼金").ToString() : giftMode == 1 ? new TextObject("{=MF_UI_PlayerPays}玩家家族支付礼金").ToString() : new TextObject("{=MF_UI_OtherPays}要求对方家族支付礼金").ToString();
            return directionText + "；" + giftText;
        }
        private static void ShowResult(FamilyResult result) => ShowMessage(result != null && result.IsAllowed ? "{=MF_UI_RequestCreated}提议已成功提交。" : "{=MF_UI_RequestFailed}提议未能提交，请检查人物状态、家族关系、冷却或礼金条件。");
        private static void ShowAdoptionResult(FamilyResult result) => ShowMessage(result != null && result.IsAllowed ? "{=MF_UI_AdoptionRegistered}收养手续已经登记，将在下次家族事务处理时完成身份转换。" : "{=MF_UI_AdoptionFailed}收养未能办理，请检查人物状态、亲属关系或是否已有其他待处理事务。");
        private static void ShowMessage(string text) => InformationManager.DisplayMessage(new InformationMessage(new TextObject(text).ToString()));

        private sealed class MarriageTerms { internal MarriageTerms(MarriageDirection direction, int giftMode) { Direction = direction; GiftMode = giftMode; } internal MarriageDirection Direction { get; } internal int GiftMode { get; } }

        public static void ShowIncomingProposal(string proposalId)
        {
            FamilyCampaignBehavior behavior = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>();
            FamilyProposal proposal = behavior?.Service?.GetIncoming(CampaignClan.PlayerClan)
                ?.FirstOrDefault(p => p.ProposalId == proposalId);
            if (proposal == null) { ShowUnavailable(); return; }

            var choices = new List<InquiryElement>
            {
                new InquiryElement("accept", new TextObject("{=MF_UI_Accept}接受提议").ToString(), null, true, string.Empty),
                new InquiryElement("reject", new TextObject("{=MF_UI_Reject}拒绝提议").ToString(), null, true, string.Empty)
            };
            if (proposal.ProposalType == FamilyProposalType.Marriage && proposal.CounterCount == 0)
                choices.Add(new InquiryElement("counter", new TextObject("{=MF_UI_Counter}提出反提议").ToString(), null, true, string.Empty));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=MF_UI_IncomingTitle}收到家族提议").ToString(), Describe(proposal), choices, true, 1, 1,
                new TextObject("{=MF_UI_Confirm}确认").ToString(), new TextObject("{=MF_UI_Close}关闭").ToString(),
                selected => HandleDecision(proposal, selected), null, string.Empty, false), false, false);
        }

        private static void HandleDecision(FamilyProposal proposal, List<InquiryElement> selected)
        {
            string action = selected?.FirstOrDefault()?.Identifier as string;
            if (action == "accept") TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service.TryAccept(proposal.ProposalId, CampaignClan.PlayerClan);
            else if (action == "reject") TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service.TryReject(proposal.ProposalId, CampaignClan.PlayerClan, FamilyReasonCode.PlayerDeclined);
            else if (action == "counter") ShowCounterOptions(proposal);
        }

        private static void ShowCounterOptions(FamilyProposal proposal)
        {
            var options = new List<InquiryElement>();
            AddCounterOption(options, proposal, MarriageDirection.FemaleJoinsMaleClan, null, 0, "{=MF_UI_CounterNormal}女方加入男方家族，不设礼金");
            AddCounterOption(options, proposal, MarriageDirection.MaleJoinsFemaleClan, null, 0, "{=MF_UI_CounterMatrilocal}男方加入女方家族，不设礼金");
            AddGiftOptions(options, proposal, MarriageDirection.FemaleJoinsMaleClan);
            AddGiftOptions(options, proposal, MarriageDirection.MaleJoinsFemaleClan);
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=MF_UI_CounterTitle}提出反提议").ToString(),
                new TextObject("{=MF_UI_CounterDescription}请选择婚后归属与礼金条件。每条谈判链仅允许一次反提议。").ToString(),
                options, true, 1, 1, new TextObject("{=MF_UI_Confirm}确认").ToString(), new TextObject("{=MF_UI_Close}关闭").ToString(),
                selected => SubmitCounter(proposal, selected), null, string.Empty, false), false, false);
        }

        private static void AddGiftOptions(List<InquiryElement> options, FamilyProposal p, MarriageDirection direction)
        {
            AddCounterOption(options, p, direction, CampaignClan.PlayerClan, 5000, "{=MF_UI_CounterPayGift}支付5,000第纳尔礼金");
            CampaignClan other = p.ProposerClan == CampaignClan.PlayerClan ? p.ReceiverClan : p.ProposerClan;
            AddCounterOption(options, p, direction, other, 5000, "{=MF_UI_CounterRequestGift}要求对方支付5,000第纳尔礼金");
        }

        private static void AddCounterOption(List<InquiryElement> options, FamilyProposal p, MarriageDirection direction, CampaignClan payer, int gift, string label)
        {
            options.Add(new InquiryElement(new CounterChoice(direction, payer, gift), new TextObject(label).ToString(), null,
                payer != CampaignClan.PlayerClan || payer.Leader?.Gold >= gift, string.Empty));
        }

        private static void SubmitCounter(FamilyProposal proposal, List<InquiryElement> selected)
        {
            var choice = selected?.FirstOrDefault()?.Identifier as CounterChoice;
            if (choice == null) return;
            TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service.TryCounter(
                proposal.ProposalId, CampaignClan.PlayerClan, choice.Direction, choice.Payer, choice.Gift);
        }

        private static string Describe(FamilyProposal p)
        {
            if (p.ProposalType == FamilyProposalType.Adoption)
            {
                var adoption = new TextObject("{=MF_UI_AdoptionDescription}{PROPOSER}提议由{ADOPTER}收养{ADOPTEE}，并将其纳入{DESTINATION}。回应期限剩余约{DAYS}天。");
                adoption.SetTextVariable("PROPOSER", p.ProposerClan?.Name ?? TextObject.GetEmpty());
                adoption.SetTextVariable("ADOPTER", p.AdopterHero?.Name ?? TextObject.GetEmpty()); adoption.SetTextVariable("ADOPTEE", p.AdopteeHero?.Name ?? TextObject.GetEmpty());
                adoption.SetTextVariable("DESTINATION", p.DestinationClan?.Name ?? TextObject.GetEmpty());
                adoption.SetTextVariable("DAYS", System.Math.Max(0, (int)System.Math.Ceiling(-p.ReplyDeadline.ElapsedDaysUntilNow))); return adoption.ToString();
            }
            var text = new TextObject("{=MF_UI_MarriageDescription}{PROPOSER}提议由{HERO_A}与{HERO_B}成婚，婚后加入{DESTINATION}。礼金：{GOLD}第纳尔。回应期限剩余约{DAYS}天。");
            text.SetTextVariable("PROPOSER", p.ProposerClan?.Name ?? TextObject.GetEmpty());
            text.SetTextVariable("HERO_A", p.HeroA?.Name ?? TextObject.GetEmpty()); text.SetTextVariable("HERO_B", p.HeroB?.Name ?? TextObject.GetEmpty());
            text.SetTextVariable("DESTINATION", p.DestinationClan?.Name ?? TextObject.GetEmpty()); text.SetTextVariable("GOLD", p.GiftAmount);
            text.SetTextVariable("DAYS", System.Math.Max(0, (int)System.Math.Ceiling(-p.ReplyDeadline.ElapsedDaysUntilNow))); return text.ToString();
        }

        private static void ShowUnavailable()
        {
            InformationManager.ShowInquiry(new InquiryData(new TextObject("{=MF_UI_UnavailableTitle}提议已不可用").ToString(),
                new TextObject("{=MF_UI_UnavailableDescription}该提议已经处理、失效或超过回应期限。").ToString(), true, false,
                new TextObject("{=MF_UI_Close}关闭").ToString(), string.Empty, null, null, string.Empty, 0f, null, null, null), false, false);
        }

        private sealed class CounterChoice
        {
            internal CounterChoice(MarriageDirection direction, CampaignClan payer, int gift) { Direction = direction; Payer = payer; Gift = gift; }
            internal MarriageDirection Direction { get; }
            internal CampaignClan Payer { get; }
            internal int Gift { get; }
        }
    }
}
