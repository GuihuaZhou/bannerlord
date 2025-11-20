using ModifiedArmy.Patches;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Saveable type definer for Fief Squad system.
    /// Ensures proper serialization of settlement and roster lists.
    /// </summary>
    public class FiefSaveDefiner : SaveableTypeDefiner
    {
        public FiefSaveDefiner() : base(20251116) { }

        protected override void DefineClassTypes() { }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<Settlement>));
            ConstructContainerDefinition(typeof(List<TroopRoster>));
        }
    }

    /// <summary>
    /// Represents the result of filling a fief squad with troops.
    /// Tracks how many of each troop type were added.
    /// </summary>
    public class FillResult
    {
        public Dictionary<CharacterObject, int> RetinueAdded { get; } = new();
        public Dictionary<CharacterObject, int> SergeantsAdded { get; } = new();
        public Dictionary<CharacterObject, int> MilitiaAdded { get; } = new();

        /// <summary>
        /// Total number of troops added across all categories.
        /// </summary>
        public int TotalAdded => RetinueAdded.Values.Sum() + SergeantsAdded.Values.Sum() + MilitiaAdded.Values.Sum();
    }

    /// <summary>
    /// Defines the composition rules for a fief squad.
    /// Each squad has three troop types: Retinue (nobles), Sergeants (professionals), and Militia (levies).
    /// Total capacity is fixed at 10 troops.
    /// </summary>
    public static class FiefSquadComposition
    {
        public static class Retinue { public const int Min = 1; public const int Max = 2; }
        public static class Sergeant { public const int Min = 3; public const int Max = 4; }
        public static class Militia { public const int Min = 4; public const int Max = 6; }

        public const int TotalCapacity = 10;

        /// <summary>
        /// Validates that the composition rules are logically consistent:
        /// - Minimum total ≤ capacity
        /// - Maximum total ≥ capacity
        /// </summary>
        public static bool IsValid()
        {
            int minTotal = Retinue.Min + Sergeant.Min + Militia.Min;
            int maxTotal = Retinue.Max + Sergeant.Max + Militia.Max;
            return minTotal <= TotalCapacity && maxTotal >= TotalCapacity;
        }
    }

    /// <summary>
    /// Represents a fief squad, designed to be culturally agnostic and compatible with Western European,
    /// Byzantine, Islamic, Slavic, Steppe, and other in-game cultures.
    /// 
    /// The composition is governed by <see cref="FiefSquadComposition"/>, which enforces:
    /// - Minimum and maximum counts per troop type (Retinue, Sergeant, Militia)
    /// - A fixed total capacity of 10 troops per squad.
    /// 
    /// Core behavioral rules:
    /// - Once conscripted (<see cref="_isConscripted"/> = true), the squad cannot accept new troops.
    /// - A fully filled squad (10 troops) cannot be filled further.
    /// - An empty squad (0 troops) cannot be conscripted.
    /// - Disbanding (via <see cref="ResetForRefill"/>) resets the conscription flag, allowing refilling.
    /// 
    /// Filling logic:
    /// 1. **Guaranteed minimum phase**: Each troop type is filled up to its defined minimum count
    ///    (in order: Retinue → Sergeant → Militia), using available candidates without randomness.
    /// 2. **Random expansion phase**: After minima are satisfied, the squad attempts to fill remaining slots
    ///    (up to total capacity of 10) by iterating through types in the same order (Retinue → Sergeant → Militia).
    ///    For each potential addition:
    ///    - A random check via <see cref="ShouldContinueAddingTroop"/> is performed (70% chance to continue).
    ///    - If the check fails, filling stops immediately.
    ///    - Filling also stops if any type reaches its maximum or the squad hits 10 total troops.
    /// 
    /// Cooldown mechanism (post-disband protection):
    /// - When a squad is refilled after being disbanded (<paramref name="isRefillAfterDisband"/> = true),
    ///   and at least one troop is added, it enters a cooldown period: <see cref="_waitCycle"/> is set to 2.
    /// - During cooldown (<see cref="_waitCycle"/> > 0):
    ///     • The squad **can still be filled** (e.g., to reach full strength).
    ///     • The squad **cannot be conscripted** (player cannot recruit from it).
    /// - <see cref="DecrementWaitCycle"/> must be called periodically (e.g., weekly) to reduce the cooldown.
    /// - Once <see cref="_waitCycle"/> reaches 0, the squad becomes eligible for conscription again.
    /// </summary>
    public class FiefSquad
    {
        private bool _isConscripted = false;
        private int _waitCycle = 0;

        private readonly List<CharacterObject> _retinueTroops = new();
        private readonly List<CharacterObject> _sergeantTroops = new();
        private readonly List<CharacterObject> _militiaTroops = new();

        // ========== Public Read-Only Interfaces ==========
        public IReadOnlyList<CharacterObject> RetinueTroops => _retinueTroops;
        public IReadOnlyList<CharacterObject> SergeantTroops => _sergeantTroops;
        public IReadOnlyList<CharacterObject> MilitiaTroops => _militiaTroops;

        // ========== Derived Properties ==========
        /// <summary>
        /// Total number of troops in this squad.
        /// </summary>
        public int TotalTroopCount => _retinueTroops.Count + _sergeantTroops.Count + _militiaTroops.Count;

        /// <summary>
        /// Indicates whether this squad can accept new troops.
        /// Conditions:
        /// - Not yet conscripted (<see cref="_isConscripted"/> is false)
        /// - Total troop count is less than <see cref="FiefSquadComposition.TotalCapacity"/> (10)
        /// Note: Being in a wait cycle (<see cref="_waitCycle"/> > 0) does NOT prevent filling.
        /// </summary>
        public bool CanBeFilled => !_isConscripted && TotalTroopCount < FiefSquadComposition.TotalCapacity;

        /// <summary>
        /// Indicates whether this squad can be conscripted into a mobile party.
        /// Conditions:
        /// - Contains at least one troop (<see cref="TotalTroopCount"/> > 0)
        /// - Not in post-disband cooldown (<see cref="_waitCycle"/> == 0)
        /// Note: Once conscripted, <see cref="_isConscripted"/> becomes true and blocks further filling.
        /// </summary>
        public bool CanBeConscripted => TotalTroopCount > 0 && _waitCycle == 0;

        /// <summary>
        /// True if the squad is in the cooldown period after being refilled following a disband operation.
        /// During this time, conscription is blocked to prevent immediate re-recruitment.
        /// </summary>
        public bool IsInWaitCycle => _waitCycle > 0;

        /// <summary>
        /// Initializes an empty fief squad.
        /// </summary>
        public FiefSquad() { }

        /// <summary>
        /// Initializes a fief squad with pre-defined troops.
        /// </summary>
        /// <param name="retinue">Initial retinue troops.</param>
        /// <param name="sergeants">Initial sergeant troops.</param>
        /// <param name="militia">Initial militia troops.</param>
        public FiefSquad(List<CharacterObject> retinue, List<CharacterObject> sergeants, List<CharacterObject> militia)
        {
            FillSquad(retinue, sergeants, militia, isRefillAfterDisband: false);
        }

        /// <summary>
        /// Fills the squad using available troop pools.
        /// First ensures minimum counts per type, then randomly fills up to capacity.
        /// </summary>
        /// <param name="availableRetinue">Available retinue candidates.</param>
        /// <param name="availableSergeants">Available sergeant candidates.</param>
        /// <param name="availableMilitia">Available militia candidates.</param>
        /// <param name="isRefillAfterDisband">
        /// If true and any troops are added, activates a 2-cycle cooldown (<see cref="_waitCycle"/> = 2)
        /// to prevent immediate re-conscription.
        /// </param>
        /// <returns>A <see cref="FillResult"/> detailing which troops were added.</returns>
        public FillResult FillSquad(
            List<CharacterObject> availableRetinue,
            List<CharacterObject> availableSergeants,
            List<CharacterObject> availableMilitia,
            bool isRefillAfterDisband = false)
        {
            var result = new FillResult();
            if (!CanBeFilled) return result;

            var addedDicts = new Dictionary<List<CharacterObject>, Dictionary<CharacterObject, int>>
        {
            { _retinueTroops, result.RetinueAdded },
            { _sergeantTroops, result.SergeantsAdded },
            { _militiaTroops, result.MilitiaAdded }
        };

            var sources = new (List<CharacterObject> Available, List<CharacterObject> Current, int Min, int Max)[]
            {
            (availableRetinue, _retinueTroops, FiefSquadComposition.Retinue.Min, FiefSquadComposition.Retinue.Max),
            (availableSergeants, _sergeantTroops, FiefSquadComposition.Sergeant.Min, FiefSquadComposition.Sergeant.Max),
            (availableMilitia, _militiaTroops, FiefSquadComposition.Militia.Min, FiefSquadComposition.Militia.Max)
            };

            // Step 1: Ensure minimum required troops per category
            foreach (var (available, current, min, _) in sources)
            {
                if (available == null || current.Count >= min) continue;
                int need = Math.Min(min - current.Count, available.Count);
                for (int i = 0; i < need && TotalTroopCount < FiefSquadComposition.TotalCapacity; i++)
                {
                    var troop = available.FirstOrDefault(t => t != null && !t.IsPlayerCharacter);
                    if (troop == null) break;
                    current.Add(troop);
                    available.Remove(troop);
                    var dict = addedDicts[current];
                    if (dict.ContainsKey(troop)) dict[troop]++;
                    else dict[troop] = 1;
                }
            }

            // Step 2: Randomly fill remaining slots (order: Retinue → Sergeant → Militia)
            while (TotalTroopCount < FiefSquadComposition.TotalCapacity)
            {
                bool added = false;
                foreach (var (available, current, _, max) in sources)
                {
                    if (current.Count >= max || available == null || available.Count == 0) continue;
                    if (!ShouldContinueAddingTroop()) continue;
                    var troop = available.FirstOrDefault(t => t != null && !t.IsPlayerCharacter);
                    if (troop != null)
                    {
                        current.Add(troop);
                        available.Remove(troop);
                        var dict = addedDicts[current];
                        if (dict.ContainsKey(troop)) dict[troop]++;
                        else dict[troop] = 1;
                        added = true;
                        break;
                    }
                }
                if (!added) break;
            }

            // Set cooldown if this is a post-disband refill and troops were added
            if (TotalTroopCount > 0 && isRefillAfterDisband)
            {
                _waitCycle = 2;
            }

            return result;
        }

        private static bool ShouldContinueAddingTroop() => MBRandom.RandomFloat < 0.7f;

        /// <summary>
        /// Attempts to remove a single troop of the specified type from the squad.
        /// Marks the squad as conscripted upon first successful removal.
        /// </summary>
        /// <param name="type">The troop type to remove.</param>
        /// <returns>True if a troop was removed; otherwise, false.</returns>
        public bool TryRemoveTroop(FiefTroopType type)
        {
            if (!CanBeConscripted) return false;
            _isConscripted = true;
            var list = type switch
            {
                FiefTroopType.Fief_Retinue => _retinueTroops,
                FiefTroopType.Fief_Sergeant => _sergeantTroops,
                FiefTroopType.Fief_Militia => _militiaTroops,
                _ => null
            };
            if (list?.Count > 0)
            {
                list.RemoveAt(0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all troops from the squad.
        /// </summary>
        public void ClearSquad()
        {
            _retinueTroops.Clear();
            _sergeantTroops.Clear();
            _militiaTroops.Clear();
        }

        /// <summary>
        /// Resets the conscription flag without clearing troops.
        /// Used during disband operations to allow refilling.
        /// </summary>
        public void ResetConscription() => _isConscripted = false;

        /// <summary>
        /// Fully resets the squad for refilling: clears troops and resets conscription status.
        /// Does not affect <see cref="_waitCycle"/>; that is managed separately.
        /// </summary>
        internal void ResetForRefill()
        {
            ClearSquad();
            ResetConscription();
        }

        /// <summary>
        /// Decrements the post-disband wait cycle by 1 (minimum 0).
        /// Should be called once per game week to allow eventual re-conscription.
        /// </summary>
        public void DecrementWaitCycle()
        {
            if (_waitCycle > 0) _waitCycle--;
        }

        /// <summary>
        /// Enumerates all troops in the squad (retinue, sergeants, militia).
        /// </summary>
        public IEnumerable<CharacterObject> AllTroops
        {
            get
            {
                foreach (var t in _retinueTroops) yield return t;
                foreach (var t in _sergeantTroops) yield return t;
                foreach (var t in _militiaTroops) yield return t;
            }
        }

        /// <summary>
        /// Returns a list of recruitable troops grouped by character and count.
        /// Only returns troops if the squad can be conscripted (<see cref="CanBeConscripted"/> is true).
        /// </summary>
        public List<(CharacterObject troop, int count)> GetRecruitableTroops()
        {
            return CanBeConscripted ? AllTroops.GroupBy(t => t).Select(g => (g.Key, g.Count())).ToList() : new List<(CharacterObject, int)>();
        }
    }

    /// <summary>
    /// Manages all fief squads for a single settlement.
    /// Handles weekly updates, recruitment, and disbanding with troop recovery.
    /// </summary>
    public class FiefSettlementData
    {
        private Settlement _settlement;
        private TroopRoster _fiefSquadTroopRoster;
        [NonSerialized] private List<FiefSquad> _squads;

        // Pre-categorized candidate pools (initialized once per settlement)
        private List<CharacterWeightPair> _retinueCandidates;
        private List<CharacterWeightPair> _sergeantCandidates;
        private List<CharacterWeightPair> _militiaCandidates;

        /// <summary>
        /// Maximum number of fief squads based on settlement type:
        /// - Village: 0 (villages do not host independent fief squads)
        /// - Castle: 10 + (3 per bound village)
        /// - Town:   20 + (3 per bound village)
        /// </summary>
        public int MaxSquadCount
        {
            get
            {
                if (_settlement == null)
                    return 0;

                // Villages themselves cannot host fief squads
                if (_settlement.IsVillage)
                    return 0;

                int baseCount = _settlement.IsCastle ? 10 : _settlement.IsTown ? 20 : 0;

                // Add 3 squads per bound village
                int villageBonus = _settlement.BoundVillages.Count * 3;

                return baseCount + villageBonus;
            }
        }

        public Settlement GetSettlement() => _settlement;
        public List<FiefSquad> GetFiefSquads() => _squads;

        /// <summary>
        /// Default constructor for deserialization.
        /// </summary>
        public FiefSettlementData()
        {
            _fiefSquadTroopRoster = TroopRoster.CreateDummyTroopRoster();
        }

        /// <summary>
        /// Initializes fief data for a given settlement.
        /// Pre-filters volunteer candidates by troop type and creates empty squads.
        /// </summary>
        public FiefSettlementData(Settlement settlement)
        {
            _settlement = settlement;
            _fiefSquadTroopRoster = TroopRoster.CreateDummyTroopRoster();
            var allVolunteers = CultureVolunteerGroupsCache.Instance.GetVolunteerCandidateCache(settlement.Culture);
            _retinueCandidates = allVolunteers?.Where(c => c.Type == FiefTroopType.Fief_Retinue).ToList() ?? new List<CharacterWeightPair>();
            _sergeantCandidates = allVolunteers?.Where(c => c.Type == FiefTroopType.Fief_Sergeant).ToList() ?? new List<CharacterWeightPair>();
            _militiaCandidates = allVolunteers?.Where(c => c.Type == FiefTroopType.Fief_Militia).ToList() ?? new List<CharacterWeightPair>();
            _squads = new List<FiefSquad>();
            for (int i = 0; i < MaxSquadCount; i++)
            {
                _squads.Add(new FiefSquad());
            }
        }

        /// <summary>
        /// Converts all squads into a single TroopRoster for saving or display.
        /// Excludes player characters.
        /// </summary>
        public TroopRoster GetAsTroopRoster()
        {
            _fiefSquadTroopRoster.Clear();
            foreach (var squad in _squads)
            {
                foreach (var troop in squad.AllTroops)
                {
                    if (troop.IsPlayerCharacter) continue;
                    _fiefSquadTroopRoster.AddToCounts(troop, 1, false, 0, 0, true, -1);
                }
            }
            return _fiefSquadTroopRoster;
        }

        private CharacterObject WeightedRandomSelectFromCharacterWeightPairs(List<CharacterWeightPair> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;
            float totalWeight = candidates.Sum(c => c.Weight);
            if (totalWeight <= 0) return candidates[0].Character;
            float rand = MBRandom.RandomFloat * totalWeight;
            float sum = 0;
            foreach (var candidate in candidates)
            {
                sum += candidate.Weight;
                if (rand < sum) return candidate.Character;
            }
            return candidates[candidates.Count - 1].Character;
        }

        private (List<CharacterObject> retinue, List<CharacterObject> sergeant, List<CharacterObject> militia) GenerateMaxCandidateLists()
        {
            var retinue = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadComposition.Retinue.Max; i++)
            {
                var t = WeightedRandomSelectFromCharacterWeightPairs(_retinueCandidates);
                if (t != null) retinue.Add(t);
            }

            var sergeant = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadComposition.Sergeant.Max; i++)
            {
                var t = WeightedRandomSelectFromCharacterWeightPairs(_sergeantCandidates);
                if (t != null) sergeant.Add(t);
            }

            var militia = new List<CharacterObject>();
            for (int i = 0; i < FiefSquadComposition.Militia.Max; i++)
            {
                var t = WeightedRandomSelectFromCharacterWeightPairs(_militiaCandidates);
                if (t != null) militia.Add(t);
            }
            return (retinue, sergeant, militia);
        }

        /// <summary>
        /// Performs daily/weekly update:
        /// - Fills incomplete squads using culture-specific candidates
        /// - Decrements wait cycles
        /// Skips update if no candidates exist.
        /// </summary>
        public void WeeklyUpdate()
        {
            if (_retinueCandidates.Count == 0 && _sergeantCandidates.Count == 0 && _militiaCandidates.Count == 0)
            {
                ModLogger.Error($"[Fief] No candidates of any type for {_settlement?.Name}. Skipping update.");
                return;
            }

            int filledCount = 0;
            foreach (var squad in _squads)
            {
                if (squad.CanBeFilled)
                {
                    var (retinue, sergeant, militia) = GenerateMaxCandidateLists();
                    squad.FillSquad(retinue, sergeant, militia, isRefillAfterDisband: false);
                    filledCount++;
                }
            }

            foreach (var squad in _squads)
            {
                squad.DecrementWaitCycle();
            }

            if (filledCount > 0)
            {
                ModLogger.Debug($"[Fief] Filled {filledCount} squads in {_settlement?.Name}.");
            }
        }

        /// <summary>
        /// Restores squads from a saved TroopRoster (used during game load).
        /// Distributes troops into squads respecting composition limits.
        /// </summary>
        public void FillFromTroopRoster(TroopRoster savedRoster)
        {
            if (savedRoster == null || savedRoster.Count == 0)
            {
                ModLogger.Debug($"[Fief] No saved roster for {_settlement?.Name}. Skipping squad restoration.");
                return;
            }

            var retinueList = new List<CharacterObject>();
            var sergeantList = new List<CharacterObject>();
            var militiaList = new List<CharacterObject>();

            // Step 1: Classify all troops from the saved roster
            foreach (var element in savedRoster.GetTroopRoster())
            {
                if (element.Character == null) continue;
                int totalCount = element.Number + element.WoundedNumber;
                if (totalCount <= 0) continue;

                var troop = element.Character;
                var type = SoldierTypeClassifier.GetSoldierType(troop);

                for (int i = 0; i < totalCount; i++)
                {
                    switch (type)
                    {
                        case FiefTroopType.Fief_Retinue:
                            retinueList.Add(troop);
                            break;
                        case FiefTroopType.Fief_Sergeant:
                            sergeantList.Add(troop);
                            break;
                        case FiefTroopType.Fief_Militia:
                            militiaList.Add(troop);
                            break;
                        default:
                            ModLogger.Warn($"[Fief] Unrecognized troop type for {troop.Name} in {_settlement?.Name}.");
                            break;
                    }
                }
            }

            ModLogger.Debug(
                $"[Fief] Loaded {retinueList.Count} retinue, {sergeantList.Count} sergeants, " +
                $"{militiaList.Count} militia from saved roster in {_settlement?.Name}.");

            int squadsFilled = 0;
            int totalAssigned = 0;
            bool hasSkippedSquad = false;

            // Step 2: Distribute troops into squads
            foreach (var squad in _squads)
            {
                if (!squad.CanBeFilled)
                {
                    hasSkippedSquad = true;
                    continue;
                }

                var retinueToAdd = new List<CharacterObject>();
                if (retinueList.Count > 0 && retinueToAdd.Count < FiefSquadComposition.Retinue.Max)
                {
                    retinueToAdd.Add(retinueList[0]);
                    retinueList.RemoveAt(0);
                }

                var sergeantsToAdd = new List<CharacterObject>();
                while (sergeantsToAdd.Count < FiefSquadComposition.Sergeant.Max && sergeantList.Count > 0)
                {
                    sergeantsToAdd.Add(sergeantList[0]);
                    sergeantList.RemoveAt(0);
                }

                var militiaToAdd = new List<CharacterObject>();
                while (militiaToAdd.Count < FiefSquadComposition.Militia.Max && militiaList.Count > 0)
                {
                    militiaToAdd.Add(militiaList[0]);
                    militiaList.RemoveAt(0);
                }

                int assignedThisSquad = retinueToAdd.Count + sergeantsToAdd.Count + militiaToAdd.Count;
                if (assignedThisSquad == 0)
                {
                    // No more troops to assign — stop early
                    break;
                }

                squad.FillSquad(retinueToAdd, sergeantsToAdd, militiaToAdd, isRefillAfterDisband: false);
                squadsFilled++;
                totalAssigned += assignedThisSquad;
            }

            // Final summary log
            string summary = $"[Fief] Restored {squadsFilled} squads with {totalAssigned} troops in {_settlement?.Name}.";
            if (hasSkippedSquad)
            {
                summary += " (Some squads were skipped because they were full or already conscripted.)";
            }

            ModLogger.Info(summary);

            // Log unassigned leftovers only if any remain (useful for debugging imbalance)
            if (retinueList.Count > 0 || sergeantList.Count > 0 || militiaList.Count > 0)
            {
                ModLogger.Debug(
                    $"[Fief] Unassigned troops remaining after restoration in {_settlement?.Name}: " +
                    $"{retinueList.Count} retinue, {sergeantList.Count} sergeants, {militiaList.Count} militia.");
            }
        }

        /// <summary>
        /// Recruits all available (non-cooldown, non-empty) squads into the target party.
        /// Recruits entire squads only (no partial recruitment).
        /// Stops if party capacity is exceeded.
        /// </summary>
        public void RecruitAvailableSquads(MobileParty targetParty)
        {
            if (targetParty == null || _settlement == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: target party or settlement is null.");
                return;
            }

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int recruitedSquads = 0;
            int totalRecruited = 0;

            foreach (var squad in _squads)
            {
                if (!squad.CanBeConscripted || squad.TotalTroopCount == 0) continue;
                int squadSize = squad.TotalTroopCount;

                if (currentMembers >= partySizeLimit)
                {
                    ModLogger.Warn($"[Fief] Party is full ({currentMembers}/{partySizeLimit}). Stopped recruitment.");
                    break;
                }

                if (currentMembers + squadSize > partySizeLimit)
                {
                    ModLogger.Warn($"[Fief] Insufficient space for squad (need {squadSize}, available {partySizeLimit - currentMembers}). Stopped recruitment.");
                    break;
                }

                var troopsToRecruit = squad.GetRecruitableTroops();
                foreach (var (troop, count) in troopsToRecruit)
                {
                    if (troop != null && count > 0)
                    {
                        targetParty.AddElementToMemberRoster(troop, count, false);
                    }
                }

                squad.ResetForRefill();
                currentMembers += squadSize;
                recruitedSquads++;
                totalRecruited += squadSize;
            }

            if (recruitedSquads > 0)
            {
                ModLogger.Info($"[Fief] Recruited {recruitedSquads} squads ({totalRecruited} troops) into {targetParty.Name}.");
            }
            else
            {
                ModLogger.Info("[Fief] No squads available for recruitment (already conscripted, empty, or insufficient party space).");
            }
        }

        private void RemoveTroopsFromParty(MobileParty party, CharacterObject troop, int countToRemove)
        {
            if (party == null || troop == null || countToRemove <= 0) return;
            var roster = party.MemberRoster;
            int currentCount = roster.GetElementNumber(troop);
            if (currentCount <= 0) return;
            int actualRemove = Math.Min(countToRemove, currentCount);
            if (actualRemove > 0)
            {
                roster.RemoveTroop(troop, actualRemove, default(UniqueTroopDescriptor), 0);
            }
        }

        /// <summary>
        /// Disbands all squads and attempts to refill them from a source party.
        /// - Resets conscription status for all squads
        /// - If sourceParty is provided, recovers eligible troops and refills incomplete squads
        /// - Sets a 2-cycle cooldown on refilled squads
        /// </summary>
        public void DisbandAndRefillFromParty(MobileParty sourceParty)
        {
            foreach (var squad in _squads)
            {
                squad.ResetConscription();
            }

            if (sourceParty == null)
            {
                ModLogger.Info($"[Fief] Disbanded all squads in {_settlement.Name} (no source party for recovery).");
                return;
            }

            var recoverableRetinue = new List<CharacterObject>();
            var recoverableSergeants = new List<CharacterObject>();
            var recoverableMilitia = new List<CharacterObject>();

            foreach (var element in sourceParty.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.Occupation != Occupation.Soldier) continue;
                int totalCount = element.Number + element.WoundedNumber;
                if (totalCount <= 0) continue;
                var troop = element.Character;
                var type = SoldierTypeClassifier.GetSoldierType(troop);
                for (int i = 0; i < totalCount; i++)
                {
                    switch (type)
                    {
                        case FiefTroopType.Fief_Retinue: recoverableRetinue.Add(troop); break;
                        case FiefTroopType.Fief_Sergeant: recoverableSergeants.Add(troop); break;
                        case FiefTroopType.Fief_Militia: recoverableMilitia.Add(troop); break;
                    }
                }
            }

            ModLogger.Debug($"[Fief] Recoverable troops - Retinue: {recoverableRetinue.Count}, Sergeants: {recoverableSergeants.Count}, Militia: {recoverableMilitia.Count}");

            var totalRetinueAdded = new Dictionary<CharacterObject, int>();
            var totalSergeantsAdded = new Dictionary<CharacterObject, int>();
            var totalMilitiaAdded = new Dictionary<CharacterObject, int>();

            foreach (var squad in _squads)
            {
                if (!squad.CanBeFilled) continue;

                var localRetinue = new List<CharacterObject>(recoverableRetinue);
                var localSergeants = new List<CharacterObject>(recoverableSergeants);
                var localMilitia = new List<CharacterObject>(recoverableMilitia);

                FillResult result = squad.FillSquad(localRetinue, localSergeants, localMilitia, isRefillAfterDisband: true);

                MergeDictionary(totalRetinueAdded, result.RetinueAdded);
                MergeDictionary(totalSergeantsAdded, result.SergeantsAdded);
                MergeDictionary(totalMilitiaAdded, result.MilitiaAdded);

                RemoveFilledTroops(recoverableRetinue, result.RetinueAdded);
                RemoveFilledTroops(recoverableSergeants, result.SergeantsAdded);
                RemoveFilledTroops(recoverableMilitia, result.MilitiaAdded);
            }

            foreach (var kvp in totalRetinueAdded) RemoveTroopsFromParty(sourceParty, kvp.Key, kvp.Value);
            foreach (var kvp in totalSergeantsAdded) RemoveTroopsFromParty(sourceParty, kvp.Key, kvp.Value);
            foreach (var kvp in totalMilitiaAdded) RemoveTroopsFromParty(sourceParty, kvp.Key, kvp.Value);

            int totalAdded = totalRetinueAdded.Values.Sum() + totalSergeantsAdded.Values.Sum() + totalMilitiaAdded.Values.Sum();
            if (totalAdded > 0)
            {
                ModLogger.Info($"[Fief] Refilled {totalAdded} troops from {sourceParty.Name} into {_settlement.Name}'s squads.");
            }
        }

        private void MergeDictionary(Dictionary<CharacterObject, int> target, Dictionary<CharacterObject, int> source)
        {
            foreach (var kvp in source)
            {
                if (target.ContainsKey(kvp.Key)) target[kvp.Key] += kvp.Value;
                else target[kvp.Key] = kvp.Value;
            }
        }

        private void RemoveFilledTroops(List<CharacterObject> availableList, Dictionary<CharacterObject, int> usedTroops)
        {
            foreach (var kvp in usedTroops)
            {
                var troop = kvp.Key;
                int count = kvp.Value;
                for (int i = 0; i < count; i++)
                {
                    if (!availableList.Remove(troop)) break;
                }
            }
        }
    }

    /// <summary>
    /// Campaign behavior that manages fief squads across all player-owned settlements.
    /// Handles initialization, daily updates, saving/loading, and public APIs for recruitment/disbanding.
    /// </summary>
    public class FiefSquadManager : CampaignBehaviorBase
    {
        private Dictionary<Settlement, FiefSettlementData> _fiefDataMap = new Dictionary<Settlement, FiefSettlementData>();
        private List<Settlement> _savedSettlements = new();
        private List<TroopRoster> _savedRosters = new();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _savedSettlements.Clear();
                _savedRosters.Clear();
                foreach (var kvp in _fiefDataMap)
                {
                    var settlement = kvp.Key;
                    var data = kvp.Value;
                    if (settlement.OwnerClan != Clan.PlayerClan) continue;
                    var roster = data.GetAsTroopRoster();
                    if (roster.Count > 0)
                    {
                        _savedSettlements.Add(settlement);
                        _savedRosters.Add(roster);
                    }
                }
                int total = _savedRosters.Sum(r => r?.Count ?? 0);
                ModLogger.Info($"[Fief Squad Mod] Saving {_savedSettlements.Count} settlements with {total} unique troop types.");
            }

            dataStore.SyncData("_savedSettlements", ref _savedSettlements);
            dataStore.SyncData("_savedRosters", ref _savedRosters);

            if (!dataStore.IsSaving)
            {
                int total = _savedRosters.Sum(r => r?.Count ?? 0);
                ModLogger.Info($"[Fief Squad Mod] Loaded raw save data: {_savedSettlements?.Count ?? 0} settlements, {total} unique troop types.");
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SoldierTypeClassifier.InitializeAll();
            InitializeFiefData();
        }

        /// <summary>
        /// Initializes fief data for all player-owned villages, castles, and towns.
        /// Also restores saved squad compositions from loaded data.
        /// </summary>
        public void InitializeFiefData()
        {
            _fiefDataMap.Clear();
            foreach (var settlement in Settlement.All)
            {
                if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (settlement.IsCastle || settlement.IsTown)
                {
                    _fiefDataMap[settlement] = new FiefSettlementData(settlement);
                }
            }

            if (_savedSettlements != null && _savedRosters != null && _savedSettlements.Count == _savedRosters.Count)
            {
                for (int i = 0; i < _savedSettlements.Count; i++)
                {
                    var settlement = _savedSettlements[i];
                    var roster = _savedRosters[i];
                    if (settlement != null && roster != null && _fiefDataMap.TryGetValue(settlement, out var data))
                    {
                        data.FillFromTroopRoster(roster);
                    }
                }
            }

            int total = _fiefDataMap.Values.Sum(d => d.GetAsTroopRoster().Count);
            ModLogger.Info($"[Fief Squad Mod] Initialized {_fiefDataMap.Count} fiefs with {total} total troops.");
        }

        private void OnDailyTick()
        {
            foreach (var data in _fiefDataMap.Values)
            {
                data?.WeeklyUpdate();
            }
            ModLogger.Debug("[Fief Squad Mod] Daily reinforcement update completed.");
        }

        /// <summary>
        /// Retrieves fief data for a specific settlement.
        /// </summary>
        public FiefSettlementData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        /// <summary>
        /// Gets all fief data mappings.
        /// </summary>
        public Dictionary<Settlement, FiefSettlementData> GetAllFiefData()
        {
            return _fiefDataMap;
        }

        /// <summary>
        /// Returns all fief troops in a settlement as a flat list.
        /// </summary>
        public List<CharacterObject> GetAllFiefTroopsForSettlement(Settlement settlement)
        {
            if (settlement == null) return new List<CharacterObject>();
            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData)) return new List<CharacterObject>();
            var allTroops = new List<CharacterObject>();
            foreach (var squad in fiefData.GetFiefSquads())
            {
                allTroops.AddRange(squad.AllTroops);
            }
            return allTroops;
        }

        /// <summary>
        /// Returns all fief squads in a settlement as a list of troop lists.
        /// Each inner list represents one squad.
        /// </summary>
        public List<List<CharacterObject>> GetAllFiefSquads(Settlement settlement)
        {
            if (settlement == null) return new List<List<CharacterObject>>();
            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData)) return new List<List<CharacterObject>>();
            var allFiefSquads = new List<List<CharacterObject>>();
            foreach (var squad in fiefData.GetFiefSquads())
            {
                allFiefSquads.Add(squad.AllTroops.ToList());
            }
            return allFiefSquads;
        }

        /// <summary>
        /// Returns the combined troop roster of all fief squads in a settlement.
        /// </summary>
        public TroopRoster GetFiefTroopRoster(Settlement settlement)
        {
            if (settlement == null) return TroopRoster.CreateDummyTroopRoster();
            if (_fiefDataMap.TryGetValue(settlement, out FiefSettlementData data) && data != null)
            {
                return data.GetAsTroopRoster();
            }
            return TroopRoster.CreateDummyTroopRoster();
        }

        /// <summary>
        /// Recruits all available fief squads from a settlement into the target mobile party.
        /// </summary>
        public void RecruitFiefSquadsFromSettlement(Settlement settlement, MobileParty targetParty)
        {
            if (settlement == null || targetParty == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: invalid parameters.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                ModLogger.Info($"[Fief] Starting recruitment from {settlement.Name}...");
                data.RecruitAvailableSquads(targetParty);
            }
            else
            {
                ModLogger.Warn($"[Fief] No fief data found for {settlement.Name} (likely not player-owned).");
            }
        }

        /// <summary>
        /// Disbands all fief squads in a settlement.
        /// Optionally recovers troops from a mobile party to refill squads.
        /// </summary>
        public void DisbandFiefSquadsFromSettlement(Settlement settlement, MobileParty mobileParty = null)
        {
            if (settlement == null)
            {
                ModLogger.Error("[Fief] Disband failed: settlement is null.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                ModLogger.Info($"[Fief] Disbanding squads in {settlement.Name}...");
                data.DisbandAndRefillFromParty(mobileParty);
            }
            else
            {
                ModLogger.Warn($"[Fief] No fief data found for {settlement.Name} (likely not player-owned).");
            }
        }

        /// <summary>
        /// Gets the total number of fief squads in the given settlement.
        /// Returns 0 if the settlement is null, not player-owned, or has no fief data.
        /// </summary>
        public int GetTotalFiefSquadCount(Settlement settlement)
        {
            if (settlement == null || !_fiefDataMap.TryGetValue(settlement, out var data))
                return 0;
            return data.GetFiefSquads().Count;
        }

        /// <summary>
        /// Gets the number of fief squads that can currently be conscripted (i.e., non-empty and not in cooldown).
        /// </summary>
        public int GetRecruitableFiefSquadCount(Settlement settlement)
        {
            if (settlement == null || !_fiefDataMap.TryGetValue(settlement, out var data))
                return 0;
            return data.GetFiefSquads().Count(squad => squad.CanBeConscripted && squad.TotalTroopCount > 0);
        }

    }
}