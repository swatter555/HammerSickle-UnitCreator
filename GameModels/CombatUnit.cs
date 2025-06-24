﻿using HammerAndSickle.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

/*───────────────────────────────────────────────────────────────────────────────
 CombatUnit  —  universal runtime model for every maneuver element or base
 ────────────────────────────────────────────────────────────────────────────────
 Overview
 ════════
 A **CombatUnit** instance represents a single controllable entity on the
 Hammer & Sickle hex map: anything from a tank battalion to an airbase or depot.
 It stores *mutable* per-unit state, while pointing to shared, **immutable**
 template objects (WeaponSystemProfile, UnitProfile, Leader, FacilityManager).

 Major Responsibilities
 ══════════════════════
 • Identification & metadata
     - UnitID, name, side, nationality, type, classification, role
 • Unified action-economy management
     - Single SpendAction() interface for all action types with variable costs
     - Move / Combat / Deployment / Opportunity / Intel tokens + MP pool
 • Combat-posture state machine
     - Mobile ←→ Deployed ←→ HastyDefense ←→ Entrenched ←→ Fortified
 • Mounted-vs-Dismounted profile switching
 • Experience & leadership systems
     - XP progression, tiered multipliers, leader skills & reputation
 • Damage, repair, supply & efficiency tracking
 • Position & movement on a pointy-top, odd-r hex grid (future pathfinder hook)
 • Persistence with two-phase loading and reference resolution
 • Cloning for unit templates

 Design Highlights
 ═════════════════
 • **Unified Action Interface**: SpendAction() handles all action types with 
   flexible movement cost parameter for future pathfinding integration.
 • **Interface-Focused Architecture**: Public methods organized into logical
   groups (Generic, Experience, Leader, Action, CombatState, Position).
 • All max/current pairs use **StatsMaxCurrent** for clamping & % queries.
 • Unity-free data model – no MonoBehaviours serialized.
 • All exceptions funnel through *AppService.HandleException*; important user
   feedback goes through *AppService.CaptureUiMessage*.

 Public-Method Reference
 ═══════════════════════
   ── Generic Interface ────────────────────────────────────────────────────────
   RefreshAllActions()                Reset all action pools to max (turn start).
   RefreshMovementPoints()            Restore MP to max (turn start).
   SetSpottedLevel(level)             Update visibility level for opposing sides.
   GetActiveWeaponSystemProfile()     Returns mounted or deployed profile.
   GetCurrentCombatStrength()         Applies all modifiers to active profile.
   TakeDamage(amount)                 Reduces hit points and updates unit profile.
   Repair(amount)                     Restores hit points up to maximum.
   ConsumeSupplies(amount)            Deducts supply days, returns success.
   ReceiveSupplies(amount)            Adds supplies up to capacity, returns actual.
   IsDestroyed()                      Checks if hit points <= 0.
   CanMove()                          Validates movement based on damage/supply.
   GetSupplyStatus()                  Returns supply percentage (0.0-1.0).
   SetEfficiencyLevel(level)          Sets the efficiency level for the unit.
   DecreaseEfficiencyLevelBy1()       Lowers efficiency level by 1 (if possible).
   IncreaseEfficiencyLevelBy1()       Raises efficiency level by 1 (if possible).

   ── Experience System ────────────────────────────────────────────────────────
   AddExperience(points)              Adds XP and handles level-up notifications.
   SetExperience(points)              Direct setter for load/dev tools.
   GetPointsToNextLevel()             XP needed to advance (0 if Elite).
   GetExperienceProgress()            Progress bar value (0.0-1.0) to next level.

   ── Leader Assignment ───────────────────────────────────────────────────────
   AssignLeader(leader)               Assigns commander with full validation.
   RemoveLeader()                     Removes commander with proper cleanup.
   GetLeaderBonuses()                 Dictionary of all active skill bonuses.
   HasLeaderCapability(type)          Checks for specific bonus availability.
   GetLeaderBonus(type)               Returns specific bonus value or 0.
   HasLeader()                        Returns true if commander assigned.
   GetLeaderName()                    Leader name or empty string.
   GetLeaderGrade()                   Command grade or JuniorGrade default.
   GetLeaderReputation()              Reputation points or 0.
   GetLeaderRank()                    Formatted rank string or empty.
   GetLeaderCommandAbility()          Combat command ability or Average.
   HasLeaderSkill(skill)              Checks if specific skill unlocked.
   AwardLeaderReputation(action)      Awards reputation for unit actions.
   AwardLeaderReputation(points)      Direct reputation point award.

   ── Action Economy ──────────────────────────────────────────────────────────
   SpendAction(type, movementCost)    Unified action consumption with MP cost.
   GetAvailableActions()              Dictionary of truly available actions.

   ── Combat State Management ─────────────────────────────────────────────────
   SetCombatState(state)              Changes state with validation & costs.
   UpOneState()                       Moves up to next state (if possible).
   DownOneState()                     Moves down to previous state (if possible).
   CanChangeToState(state)            Validates transition rules & resources.
   BeginEntrenchment()                Convenience method: Deployed → HastyDefense.
   CanEntrench()                      Checks if entrenchment is possible.
   GetValidStateTransitions()         List of legal target states.

   ── Positioning & Movement ──────────────────────────────────────────────────
   SetPosition(pos)                   Places unit at map coordinates.
   CanMoveTo(pos)                     Future: validates movement legality.
   GetDistanceTo(pos|unit)            Future: pathfinder distance calculation.

   ── Development Support ─────────────────────────────────────────────────────
   DebugSetCombatState(state)         Direct state change (no validation/cost).

 Action System Design
 ════════════════════
 The unified SpendAction() method handles all action types with intelligent
 movement point deduction:

   • **Move Actions**: Variable cost (default 0) - ready for pathfinding
   • **Combat Actions**: Fixed cost (25% max MP) + action token
   • **Deployment Actions**: Fixed cost (50% max MP) + action token  
   • **Opportunity Actions**: No movement cost, token only
   • **Intelligence Actions**: Fixed cost (10% max MP) + token (bases exempt)

 Combat vs Mounted State
 ═══════════════════════
 CombatState controls tactical posture; *IsMounted* controls transport usage.
 Transition rules:

   • To **Mobile**: Uses MountedProfile if available, else adds movement bonus
   • From **Mobile**: Always uses DeployedProfile, removes any movement bonus

 Persistence Architecture
 ════════════════════════
 • ISerializable with two-phase loading pattern
 • Reference resolution for shared objects (profiles, leaders)
 • FacilityManager integration with proper parent relationships
 • Comprehensive consistency validation and error recovery

 ───────────────────────────────────────────────────────────────────────────────
 KEEP THIS COMMENT BLOCK IN SYNC WITH PUBLIC API CHANGES!
 ───────────────────────────────────────────────────────────────────────────── */
namespace HammerAndSickle.Models
{
    [Serializable]
    public class CombatUnit : ICloneable, ISerializable, IResolvableReferences
    {
        #region Constants

        private const string CLASS_NAME = nameof(CombatUnit);

        #endregion

        #region Fields

        // Temporary fields for deserialization reference resolution
        private string unresolvedDeployedProfileID = "";
        private string unresolvedMountedProfileID = "";
        private string unresolvedUnitProfileID = "";
        private string unresolvedLeaderID = "";
        private bool unresolvedFacilityManagerData = false;

        #endregion // Fields


        #region Properties

        // Identification and metadata
        public string UnitName { get; set; }
        public string UnitID { get; private set; }
        public UnitType UnitType { get; private set; }
        public UnitClassification Classification { get; private set; }
        public UnitRole Role { get; private set; }
        public Side Side { get; private set; }
        public Nationality Nationality { get; private set; }
        public bool IsTransportable { get; private set; }
        public bool IsBase { get; private set; }

        // Profiles contain unit stats and capabilities.
        public WeaponSystemProfile DeployedProfile { get; private set; }
        public WeaponSystemProfile MountedProfile { get; private set; }
        public UnitProfile UnitProfile { get; private set; }
        public FacilityManager FacilityManager { get; internal set; }

        // The unit's leader.
        public bool IsLeaderAssigned = false;
        public Leader CommandingOfficer { get; internal set; }

        // Action counts using StatsMaxCurrent
        public StatsMaxCurrent MoveActions { get; private set; }
        public StatsMaxCurrent CombatActions { get; private set; }
        public StatsMaxCurrent DeploymentActions { get; private set; }
        public StatsMaxCurrent OpportunityActions { get; private set; }
        public StatsMaxCurrent IntelActions { get; private set; }

        // State data using StatsMaxCurrent where appropriate
        public int ExperiencePoints { get; internal set; }
        public ExperienceLevel ExperienceLevel { get; internal set; }
        public EfficiencyLevel EfficiencyLevel { get; internal set; }
        public bool IsMounted { get; internal set; }
        public CombatState CombatState { get; internal set; }
        public StatsMaxCurrent HitPoints { get; private set; }
        public StatsMaxCurrent DaysSupply { get; private set; }
        public StatsMaxCurrent MovementPoints { get; private set; }
        public Coordinate2D MapPos { get; internal set; }
        public SpottedLevel SpottedLevel { get; private set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new CombatUnit with the specified core properties.
        /// </summary>
        /// <param name="unitName">Display name of the unit</param>
        /// <param name="unitType">Type of unit (land, air, naval)</param>
        /// <param name="classification">Unit classification (tank, infantry, etc.)</param>
        /// <param name="role">Primary role of the unit</param>
        /// <param name="side">Which side controls this unit</param>
        /// <param name="nationality">National affiliation</param>
        /// <param name="deployedProfile">Combat profile when deployed</param>
        /// <param name="mountedProfile">Combat profile when mounted (can be null)</param>
        /// <param name="unitProfile">Organizational profile for tracking losses</param>
        /// <param name="isTransportable">Whether this unit can be transported</param>
        /// <param name="isBase">Whether this unit is a land-based facility</param>
        public CombatUnit(
        string unitName,
        UnitType unitType,
        UnitClassification classification,
        UnitRole role,
        Side side,
        Nationality nationality,
        WeaponSystemProfile deployedProfile,
        WeaponSystemProfile? mountedProfile,
        UnitProfile unitProfile,
        bool isTransportable,
        bool isBase = false,
        DepotCategory category = DepotCategory.Secondary,
        DepotSize size = DepotSize.Small)
        {
            const string METHOD = "+ctor"; // for AppService logging

            try
            {
                // 1 – Validate mandatory params (catch null *and* empty for strings).
                if (string.IsNullOrWhiteSpace(unitName))
                    throw new ArgumentException("Unit name cannot be null or empty", nameof(unitName));
                if (deployedProfile is null)
                    throw new ArgumentNullException(nameof(deployedProfile));
                if (unitProfile is null)
                    throw new ArgumentNullException(nameof(unitProfile));

                // 2 – Simple assignments (no UnityEngine GUIDs; pure .NET Guid).
                UnitName = unitName;
                UnitID = Guid.NewGuid().ToString();
                UnitType = unitType;
                Classification = classification;
                Role = role;
                Side = side;
                Nationality = nationality;
                IsTransportable = isTransportable;
                IsLeaderAssigned = false;

                // 3 – Profiles (mounted can be null – editor handles it gracefully).
                DeployedProfile = deployedProfile;
                MountedProfile = mountedProfile; // nullable OK
                UnitProfile = unitProfile;

                // 4 – Facilities for base units.
                IsBase = isBase;
                if (IsBase)
                {
                    FacilityManager = new FacilityManager();
                    switch (classification)
                    {
                        case UnitClassification.HQ:
                            FacilityManager.SetupHQ(this);
                            break;
                        case UnitClassification.DEPOT:
                            FacilityManager.SetupSupplyDepot(this, category, size);
                            break;
                        case UnitClassification.AIRB:
                            FacilityManager.SetupAirbase(this);
                            break;
                        default:
                            FacilityManager.SetParent(this);
                            break;
                    }
                }
                else
                {
                    FacilityManager = new FacilityManager();
                    FacilityManager.SetParent(this);
                }

                // 5 – Leader is null until assigned (explicit).
                CommandingOfficer = null;

                // 6 – Action counters & state.
                InitializeActionCounts();
                ExperiencePoints = 0;
                ExperienceLevel = ExperienceLevel.Raw;
                EfficiencyLevel = EfficiencyLevel.FullyOperational;
                IsMounted = false;
                CombatState = CombatState.Deployed;
                SpottedLevel = SpottedLevel.Level1;

                // 7 – Stats containers.
                HitPoints = new StatsMaxCurrent(CUConstants.MAX_HP);
                DaysSupply = new StatsMaxCurrent(CUConstants.MaxDaysSupplyUnit);
                InitializeMovementPoints();

                // 8 – Initial map position.
                MapPos = Coordinate2D.Zero;
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, METHOD, ex);
                throw; // preserve stack trace for upstream callers
            }
        }

        /// <summary>
        /// Deserialization constructor for loading CombatUnit from saved data.
        /// </summary>
        /// <param name="info">Serialization info containing saved data</param>
        /// <param name="context">Streaming context for deserialization</param>
        protected CombatUnit(SerializationInfo info, StreamingContext context)
        {
            try
            {
                // Load basic properties
                UnitName = info.GetString(nameof(UnitName));
                UnitID = info.GetString(nameof(UnitID));
                UnitType = (UnitType)info.GetValue(nameof(UnitType), typeof(UnitType));
                Classification = (UnitClassification)info.GetValue(nameof(Classification), typeof(UnitClassification));
                Role = (UnitRole)info.GetValue(nameof(Role), typeof(UnitRole));
                Side = (Side)info.GetValue(nameof(Side), typeof(Side));
                Nationality = (Nationality)info.GetValue(nameof(Nationality), typeof(Nationality));
                IsTransportable = info.GetBoolean(nameof(IsTransportable));
                IsBase = info.GetBoolean(nameof(IsBase));
                IsLeaderAssigned = info.GetBoolean(nameof(IsLeaderAssigned));
                SpottedLevel = (SpottedLevel)info.GetValue(nameof(SpottedLevel), typeof(SpottedLevel));

                // Store profile IDs for later resolution (don't resolve objects yet)
                unresolvedDeployedProfileID = info.GetString("DeployedProfileID");
                unresolvedMountedProfileID = info.GetString("MountedProfileID");
                unresolvedUnitProfileID = info.GetString("UnitProfileID");
                unresolvedLeaderID = info.GetString("LeaderID");

                // Deserialize owned StatsMaxCurrent objects
                HitPoints = new StatsMaxCurrent(
                    info.GetSingle("HitPoints_Max"),
                    info.GetSingle("HitPoints_Current")
                );

                DaysSupply = new StatsMaxCurrent(
                    info.GetSingle("DaysSupply_Max"),
                    info.GetSingle("DaysSupply_Current")
                );

                MovementPoints = new StatsMaxCurrent(
                    info.GetSingle("MovementPoints_Max"),
                    info.GetSingle("MovementPoints_Current")
                );

                MoveActions = new StatsMaxCurrent(
                    info.GetSingle("MoveActions_Max"),
                    info.GetSingle("MoveActions_Current")
                );

                CombatActions = new StatsMaxCurrent(
                    info.GetSingle("CombatActions_Max"),
                    info.GetSingle("CombatActions_Current")
                );

                DeploymentActions = new StatsMaxCurrent(
                    info.GetSingle("DeploymentActions_Max"),
                    info.GetSingle("DeploymentActions_Current")
                );

                OpportunityActions = new StatsMaxCurrent(
                    info.GetSingle("OpportunityActions_Max"),
                    info.GetSingle("OpportunityActions_Current")
                );

                IntelActions = new StatsMaxCurrent(
                    info.GetSingle("IntelActions_Max"),
                    info.GetSingle("IntelActions_Current")
                );

                // Load simple properties
                ExperiencePoints = info.GetInt32(nameof(ExperiencePoints));
                ExperienceLevel = (ExperienceLevel)info.GetValue(nameof(ExperienceLevel), typeof(ExperienceLevel));
                EfficiencyLevel = (EfficiencyLevel)info.GetValue(nameof(EfficiencyLevel), typeof(EfficiencyLevel));
                IsMounted = info.GetBoolean(nameof(IsMounted));
                CombatState = (CombatState)info.GetValue(nameof(CombatState), typeof(CombatState));
                MapPos = (Coordinate2D)info.GetValue(nameof(MapPos), typeof(Coordinate2D));

                // Load FacilityManager data
                unresolvedFacilityManagerData = info.GetBoolean("HasFacilityManager");
                if (unresolvedFacilityManagerData)
                {
                    // Reconstruct FacilityManager from the same serialization info
                    // FacilityManager uses FM_ prefixed names so no conflicts
                    FacilityManager = new FacilityManager(info, context);
                }
                else
                {
                    FacilityManager = new FacilityManager();
                }

                // Leave all object references null - they will be resolved later
                DeployedProfile = null;
                MountedProfile = null;
                UnitProfile = null;
                CommandingOfficer = null;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(CombatUnit), e);
                throw;
            }
        }

        #endregion


        #region Generic Interface Methods

        /// <summary>
        /// Refreshes all action counts to their maximum values.
        /// Called at the start of each turn.
        /// </summary>
        public void RefreshAllActions()
        {
            MoveActions.ResetToMax();
            CombatActions.ResetToMax();
            DeploymentActions.ResetToMax();
            OpportunityActions.ResetToMax();
            IntelActions.ResetToMax();
        }

        /// <summary>
        /// Refreshes movement points to maximum.
        /// Called at the start of each turn.
        /// </summary>
        public void RefreshMovementPoints()
        {
            MovementPoints.ResetToMax();
        }

        /// <summary>
        /// Sets the spotted level for the current instance.
        /// </summary>
        /// <param name="spottedLevel">The new spotted level to assign.</param>
        public void SetSpottedLevel(SpottedLevel spottedLevel)
        {
            SpottedLevel = spottedLevel;
        }

        /// <summary>
        /// Retrieves the active weapon system profile based on the current mounted state.
        /// </summary>
        /// <returns>The active <see cref="WeaponSystemProfile"/>. Returns the <see cref="MountedProfile"/> if the system is
        /// mounted; otherwise, returns the <see cref="DeployedProfile"/>.</returns>
        public WeaponSystemProfile GetActiveWeaponSystemProfile()
        {
            // Return the active profile based on mounted state
            return IsMounted ? MountedProfile : DeployedProfile;
        }

        /// <summary>
        /// Calculates the adjusted combat strength for the active weapon‑system profile,
        /// applying every relevant modifier (HP, combat‑state, efficiency, experience, etc.).
        /// </summary>
        /// <remarks>
        /// This method is UI‑agnostic and safe to call from both the Unity runtime and the
        /// Avalonia editor.  All exceptions are routed through <see cref="AppService"/> for
        /// consistent error reporting.
        /// </remarks>
        /// <returns>
        /// A deep‑cloned <see cref="WeaponSystemProfile"/> containing **modified** combat
        /// ratings, or <c>null</c> if an unrecoverable fault occurs (already logged).
        /// </returns>
        public WeaponSystemProfile GetCurrentCombatStrength()
        {
            const string METHOD_NAME = nameof(GetCurrentCombatStrength);

            try
            {
                // 1) Retrieve and validate the active profile only once.
                WeaponSystemProfile activeProfile = GetActiveWeaponSystemProfile();
                if (activeProfile is null)
                    throw new InvalidOperationException("Active weapon‑system profile is null.");

                // 2) Clone so that downstream callers cannot mutate shared templates.
                WeaponSystemProfile combatProfile = activeProfile.Clone();

                // 3) Aggregate every applicable modifier *once* and cache the result.
                float finalModifier = GetFinalCombatRatingModifier();

                // 4) Apply the modifier uniformly across every combat domain.
                ApplyModifier(combatProfile.LandHard, finalModifier);
                ApplyModifier(combatProfile.LandSoft, finalModifier);
                ApplyModifier(combatProfile.LandAir, finalModifier);
                ApplyModifier(combatProfile.Air, finalModifier);
                ApplyModifier(combatProfile.AirGround, finalModifier);

                // 5) Return a fully‑prepared profile for caller use.
                return combatProfile;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"{METHOD_NAME}: { e.ToString()} ");
            }
        }

        /// <summary>
        /// Rounds *up* the product of <paramref name="modifier"/> and the supplied
        /// <see cref="CombatRating"/>'s attack/defense values, then writes the results back
        /// to the rating using its validated setters.
        /// </summary>
        private static void ApplyModifier(CombatRating rating, float modifier)
        {
            if (rating is null) return; // Defensive guard – should never fire.

            rating.SetAttack(ToCeilInt(rating.Attack * modifier));
            rating.SetDefense(ToCeilInt(rating.Defense * modifier));
        }

        /// <summary>
        /// Helper that performs a ceiling‑round and converts the result to <c>int</c> using
        /// <see cref="MathF.Ceiling(float)"/> to keep this assembly free of Unity‑specific
        /// dependencies (i.e., no <c>MathF</c> usage).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToCeilInt(float value) => (int)MathF.Ceiling(value);


        /// <summary>
        /// Applies damage to the unit, reducing hit points and updating combat effectiveness.
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        public void TakeDamage(float damage)
        {
            try
            {
                if (damage < 0f)
                {
                    throw new ArgumentException("Damage cannot be negative", nameof(damage));
                }

                if (damage == 0f)
                {
                    return; // No damage to apply
                }

                // Apply damage to hit points
                float newHitPoints = MathF.Max(0f, HitPoints.Current - damage);
                HitPoints.SetCurrent(newHitPoints);

                // Update unit profile to reflect current strength
                if (UnitProfile != null)
                {
                    UnitProfile.UpdateCurrentHP(HitPoints.Current);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "TakeDamage", e);
                throw;
            }
        }

        /// <summary>
        /// Repairs damage to the unit, restoring hit points.
        /// </summary>
        /// <param name="repairAmount">Amount of damage to repair</param>
        public void Repair(float repairAmount)
        {
            try
            {
                if (repairAmount < 0f)
                {
                    throw new ArgumentException("Repair amount cannot be negative", nameof(repairAmount));
                }

                if (repairAmount == 0f)
                {
                    return; // No repair to apply
                }

                // Apply repair to hit points (clamped to maximum)
                float newHitPoints = MathF.Min(HitPoints.Max, HitPoints.Current + repairAmount);
                HitPoints.SetCurrent(newHitPoints);

                // Update unit profile to reflect current strength
                if (UnitProfile != null)
                {
                    UnitProfile.UpdateCurrentHP(HitPoints.Current);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "Repair", e);
                throw;
            }
        }

        /// <summary>
        /// Consumes supplies for unit operations.
        /// </summary>
        /// <param name="amount">Amount of supplies to consume</param>
        /// <returns>True if supplies were consumed, false if insufficient</returns>
        public bool ConsumeSupplies(float amount)
        {
            try
            {
                if (amount < 0f)
                {
                    throw new ArgumentException("Supply amount cannot be negative", nameof(amount));
                }

                if (amount == 0f)
                {
                    return true; // No supplies to consume
                }

                if (DaysSupply.Current >= amount)
                {
                    DaysSupply.SetCurrent(DaysSupply.Current - amount);
                    return true;
                }

                return false; // Insufficient supplies
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ConsumeSupplies", e);
                return false;
            }
        }

        /// <summary>
        /// Receives supplies from external source (depot, transport, etc.).
        /// </summary>
        /// <param name="amount">Amount of supplies offered</param>
        /// <returns>Actual amount of supplies received (may be less than offered due to capacity)</returns>
        public float ReceiveSupplies(float amount)
        {
            try
            {
                if (amount < 0f)
                {
                    throw new ArgumentException("Supply amount cannot be negative", nameof(amount));
                }

                if (amount == 0f)
                {
                    return 0f; // No supplies offered
                }

                // Calculate how much we can actually receive
                float availableCapacity = DaysSupply.Max - DaysSupply.Current;
                float actualAmount = MathF.Min(amount, availableCapacity);

                // Add supplies
                DaysSupply.SetCurrent(DaysSupply.Current + actualAmount);

                return actualAmount;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ReceiveSupplies", e);
                return 0f;
            }
        }

        /// <summary>
        /// Checks if the unit is destroyed (no hit points remaining).
        /// </summary>
        /// <returns>True if the unit is destroyed</returns>
        public bool IsDestroyed()
        {
            return HitPoints.Current <= 0f;
        }

        /// <summary>
        /// Checks if the unit can move based on various factors.
        /// </summary>
        /// <returns></returns>
        public bool CanMove()
        {
            try
            {
                // Must have hit points to operate
                if (IsDestroyed())
                {
                    return false;
                }

                // Must have some supplies for most operations.
                if (DaysSupply.Current < 1f)
                {
                    return false;
                }

                // Low efficiency level units cannot move.
                if (EfficiencyLevel == EfficiencyLevel.StaticOperations)
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "CanMove", e);
                return false;
            }
        }

        /// <summary>
        /// Gets the supply status as a percentage of maximum capacity.
        /// </summary>
        /// <returns>Supply status from 0.0 to 1.0</returns>
        public float GetSupplyStatus()
        {
            try
            {
                if (DaysSupply.Max <= 0f)
                {
                    return 0f;
                }

                return DaysSupply.Current / DaysSupply.Max;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetSupplyStatus", e);
                return 0f;
            }
        }

        /// <summary>
        /// Sets the efficiency level for the application.
        /// </summary>
        /// <remarks>This method updates the application's efficiency level to the specified value. 
        /// Ensure that the provided <paramref name="level"/> is a valid enumeration value to avoid
        /// exceptions.</remarks>
        /// <param name="level">The desired efficiency level to set. Must be a valid value of the <see cref="EfficiencyLevel"/> enumeration.</param>
        public void SetEfficiencyLevel(EfficiencyLevel level)
        {
            try
            {
                // Validate the new level
                if (!Enum.IsDefined(typeof(EfficiencyLevel), level))
                {
                    throw new ArgumentOutOfRangeException(nameof(level), "Invalid efficiency level");
                }
                // Set the new efficiency level
                EfficiencyLevel = level;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetEfficiencyLevel", e);
            }
        }

        /// <summary>
        /// Decreases the efficiency level by 1, if possible.
        /// </summary>
        public void DecreaseEfficiencyLevelBy1()
        {
            try
            {
                if (EfficiencyLevel > EfficiencyLevel.StaticOperations)
                    EfficiencyLevel--;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "DecreaseEfficiencyLevel", e);
            }
        }

        /// <summary>
        /// Increases the efficiency level by 1, if possible.
        /// </summary>
        public void IncreaseEfficiencyLevelBy1()
        {
            try
            {
                if (EfficiencyLevel < EfficiencyLevel.PeakOperational)
                    EfficiencyLevel++;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "IncreaseEfficiencyLevel", e);
            }
        }

        #endregion // Generic Interface Methods


        #region Experience System Interface Methods

        /// <summary>
        /// Adds experience points to the unit and returns true if successful.
        /// </summary>
        /// <param name="points">Experience points to add</param>
        /// <returns>Returns true if successful</returns>
        public bool AddExperience(int points)
        {
            try
            {
                // Units cannot gain negative experience.
                if (points <= 0)
                    return false;

                // Validate points do not exceed maximum gain per action.
                if (points > CUConstants.MAX_EXP_GAIN_PER_ACTION)
                {
                    points = CUConstants.MAX_EXP_GAIN_PER_ACTION;
                }
                    
                // Add experience points to total.
                ExperiencePoints += points;

                // Cap at Elite level.
                if (ExperiencePoints > (int)ExperiencePointLevels.Elite)
                {
                    ExperiencePoints = (int)ExperiencePointLevels.Elite;
                }

                // Store the previous level for comparison.
                var previousLevel = ExperienceLevel;

                // Get the new experience level based on updated points.
                var newLevel = CalculateExperienceLevel(ExperiencePoints);

                // If the level has changed, update and notify.
                if (newLevel != previousLevel)
                {
                    ExperienceLevel = newLevel;
                    OnExperienceLevelChanged(previousLevel, newLevel);
                }

                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AddExperience", e);
                return false;
            }
        }

        /// <summary>
        /// Sets the unit's experience points directly and updates the level accordingly.
        /// Used for loading saved games or manual experience setting.
        /// </summary>
        /// <param name="points">Total experience points</param>
        public void SetExperience(int points)
        {
            try
            {
                if (points < 0)
                    points = 0;

                ExperiencePoints = Math.Min(points, (int)ExperiencePointLevels.Elite);
                ExperienceLevel = CalculateExperienceLevel(points);

            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetExperience", e);
            }
        }

        /// <summary>
        /// Gets the experience points required for the next level.
        /// Returns 0 if already at maximum level (Elite).
        /// </summary>
        /// <returns>Points needed for next level, or 0 if at max level</returns>
        public int GetPointsToNextLevel()
        {
            return ExperienceLevel switch
            {
                ExperienceLevel.Raw => (int)ExperiencePointLevels.Green - ExperiencePoints,
                ExperienceLevel.Green => (int)ExperiencePointLevels.Trained - ExperiencePoints,
                ExperienceLevel.Trained => (int)ExperiencePointLevels.Experienced - ExperiencePoints,
                ExperienceLevel.Experienced => (int)ExperiencePointLevels.Veteran - ExperiencePoints,
                ExperienceLevel.Veteran => (int)ExperiencePointLevels.Elite - ExperiencePoints,
                ExperienceLevel.Elite => 0,// Already at max level
                _ => 0,
            };
        }

        /// <summary>
        /// Gets the experience progress as a percentage towards the next level (0.0 to 1.0).
        /// Returns 1.0 if at maximum level.
        /// </summary>
        /// <returns>Progress percentage towards next level</returns>
        public float GetExperienceProgress()
        {
            // Elite is already the top tier – always report 100 %.
            if (ExperienceLevel == ExperienceLevel.Elite)
                return 1.0f;

            int currentLevelMin = GetMinPointsForLevel(ExperienceLevel);
            int nextLevelMin = GetMinPointsForLevel(GetNextLevel(ExperienceLevel));

            // Defensive guard – malformed table would yield a divide‑by‑zero.
            if (nextLevelMin == currentLevelMin)
                return 1.0f;

            float progress = (float)(ExperiencePoints - currentLevelMin) / (nextLevelMin - currentLevelMin);

            // System.MathF has no Clamp01 helper; replicate Unity’s behaviour manually.
            return MathF.Min(1.0f, MathF.Max(0.0f, progress));
        }


        #endregion // Experience System Interface Methods


        #region Leader System Interface Methods

        /// <summary>
        /// Assigns a leader to command this unit.
        /// Handles unassigning any current leader and updating all necessary state.
        /// Validates that the leader is available and manages bidirectional assignment.
        /// </summary>
        /// <param name="leader">The leader to assign to this unit</param>
        /// <returns>True if assignment was successful, false otherwise</returns>
        public bool AssignLeader(Leader? leader)
        {
            const string METHOD = nameof(AssignLeader);

            try
            {
                // 1 — Parameter validation
                if (leader is null)
                {
                    AppService.CaptureUiMessage("Cannot assign commander: No leader specified.");
                    return false;
                }

                // 2 — Prevent redundant re‑assignment
                if (CommandingOfficer?.LeaderID == leader.LeaderID)
                {
                    AppService.CaptureUiMessage($"{leader.FormattedRank} {leader.Name} already commands {UnitName}.");
                    return false;
                }

                // 3 — Leader must be unassigned
                if (leader.IsAssigned)
                {
                    AppService.CaptureUiMessage($"Cannot assign {leader.Name}: Already assigned to another unit.");
                    return false;
                }

                // 4 — Release current CO (if any). The editor can undo via UI if needed.
                CommandingOfficer?.UnassignFromUnit();

                // 5 — Assign the new leader
                leader.AssignToUnit(UnitID);
                CommandingOfficer = leader;
                IsLeaderAssigned = true;

                AppService.CaptureUiMessage($"{leader.FormattedRank} {leader.Name} now commands {UnitName}.");
                return true;
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, METHOD, ex);
                return false;
            }
        }

        /// <summary>
        /// Removes the commanding officer from this unit.
        /// Handles proper state management and cleanup for both unit and leader.
        /// </summary>
        /// <returns>True if removal was successful, false if no leader was assigned or removal failed</returns>
        public bool RemoveLeader()
        {
            const string METHOD = nameof(RemoveLeader);

            try
            {
                // 1 — Nothing to do?
                if (CommandingOfficer is null || !IsLeaderAssigned)
                {
                    AppService.CaptureUiMessage($"{UnitName} has no commanding officer to remove.");
                    return false;
                }

                // 2 — Defensive consistency check (logging only; we still proceed).
                if (CommandingOfficer.UnitID != UnitID)
                {
                    AppService.HandleException(
                        CLASS_NAME, METHOD,
                        new InvalidOperationException($"Leader/unit ID mismatch: {CommandingOfficer.UnitID} vs {UnitID}"));
                }

                // 3 — Unassign.
                string rank = CommandingOfficer.FormattedRank;
                string name = CommandingOfficer.Name;

                CommandingOfficer.UnassignFromUnit();
                CommandingOfficer = null;
                IsLeaderAssigned = false;

                // 4 — Notify UI.
                AppService.CaptureUiMessage($"{rank} {name} relieved of command of {UnitName} and returned to leader pool.");
                return true;
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, METHOD, ex);
                return false;
            }
        }

        /// <summary>
        /// Gets all bonuses provided by the commanding officer's skills.
        /// Returns an empty dictionary if no leader is assigned.
        /// </summary>
        /// <returns>Dictionary mapping skill bonus types to their values</returns>
        public Dictionary<SkillBonusType, float> GetLeaderBonuses()
        {
            var bonuses = new Dictionary<SkillBonusType, float>();

            try
            {
                // Return empty dictionary if no leader assigned
                if (CommandingOfficer == null)
                {
                    return bonuses;
                }

                // Iterate through all skill bonus types and get non-zero values
                foreach (SkillBonusType bonusType in (SkillBonusType[])Enum.GetValues(typeof(SkillBonusType)))
                {
                    if (bonusType == SkillBonusType.None) continue;

                    float bonusValue = CommandingOfficer.GetBonusValue(bonusType);
                    if (bonusValue != 0f)
                    {
                        bonuses[bonusType] = bonusValue;
                    }
                }

                return bonuses;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetLeaderBonuses", e);
                return bonuses; // Return empty dictionary on error
            }
        }

        /// <summary>
        /// Checks if the unit has a specific leader capability/bonus.
        /// </summary>
        /// <param name="bonusType">The bonus type to check for</param>
        /// <returns>True if the leader provides this capability</returns>
        public bool HasLeaderCapability(SkillBonusType bonusType) =>
            CommandingOfficer != null && CommandingOfficer.HasCapability(bonusType);

        /// <summary>
        /// Gets a specific leader bonus value.
        /// Returns 0 if no leader assigned or bonus not present.
        /// </summary>
        /// <param name="bonusType">The type of bonus to retrieve</param>
        /// <returns>The bonus value, or 0 if not present</returns>
        public float GetLeaderBonus(SkillBonusType bonusType) =>
            CommandingOfficer != null && bonusType != SkillBonusType.None
            ? CommandingOfficer.GetBonusValue(bonusType)
            : 0f;

        /// <summary>
        /// Checks if a leader is currently assigned to this unit.
        /// </summary>
        /// <returns>True if a leader is assigned</returns>
        public bool HasLeader()
        {
            return CommandingOfficer != null;
        }

        /// <summary>
        /// Gets the leader's name for display purposes.
        /// Returns empty string if no leader assigned.
        /// </summary>
        /// <returns>Leader name or empty string</returns>
        public string GetLeaderName() => CommandingOfficer?.Name ?? string.Empty;

        /// <summary>
        /// Gets the leader's command grade for display and bonus calculations.
        /// Returns JuniorGrade if no leader assigned.
        /// </summary>
        /// <returns>Leader's command grade</returns>
        public CommandGrade GetLeaderGrade() => CommandingOfficer?.CommandGrade ?? CommandGrade.JuniorGrade;

        /// <summary>
        /// Gets the leader's reputation points for display purposes.
        /// Returns 0 if no leader assigned.
        /// </summary>
        /// <returns>Leader's reputation points</returns>
        public int GetLeaderReputation() => CommandingOfficer?.ReputationPoints ?? 0;

        /// <summary>
        /// Gets the leader's formatted rank based on nationality.
        /// Returns empty string if no leader assigned.
        /// </summary>
        /// <returns>Formatted rank string</returns>
        public string GetLeaderRank() => CommandingOfficer?.FormattedRank ?? "";

        /// <summary>
        /// Gets the leader's combat command ability modifier.
        /// Returns Average if no leader assigned.
        /// </summary>
        /// <returns>Leader's combat command ability</returns>
        public CommandAbility GetLeaderCommandAbility() =>
            CommandingOfficer?.CombatCommand ?? CommandAbility.Average;

        /// <summary>
        /// Checks if the leader has unlocked a specific skill.
        /// Returns false if no leader assigned.
        /// </summary>
        /// <param name="skillEnum">The skill to check</param>
        /// <returns>True if the skill is unlocked</returns>
        public bool HasLeaderSkill(Enum skill) =>
            CommandingOfficer != null && CommandingOfficer.IsSkillUnlocked(skill);

        /// <summary>
        /// Awards reputation to the leader for unit actions.
        /// Does nothing if no leader assigned.
        /// </summary>
        /// <param name="actionType">Type of action performed</param>
        /// <param name="contextMultiplier">Context-based multiplier (default 1.0)</param>
        public void AwardLeaderReputation(CUConstants.ReputationAction actionType, float contextMultiplier = 1.0f)
        {
            try
            {
                if (CommandingOfficer == null)
                {
                    return;
                }

                CommandingOfficer.AwardReputationForAction(actionType, contextMultiplier);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AwardLeaderReputation", e);
            }
        }

        /// <summary>
        /// Awards reputation points directly to the leader.
        /// Does nothing if no leader assigned.
        /// </summary>
        /// <param name="amount">Amount of reputation to award</param>
        public void AwardLeaderReputation(int amount)
        {
            try
            {
                if (CommandingOfficer == null || amount <= 0)
                {
                    return;
                }

                CommandingOfficer.AwardReputation(amount);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AwardLeaderReputation", e);
            }
        }

        #endregion // Leader System Interface Methods


        #region Action System Interface Methods

        /// <summary>
        /// Spend an action of the specified type, consuming movement points if necessary.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="movtCost"></param>
        /// <returns></returns>
        public bool SpendAction(ActionTypes type, float movtCost = 0)
        {
            try
            {
                switch (type)
                {
                    case ActionTypes.MoveAction:

                        // Check if we have at least one move action available
                        if (MoveActions.Current < 1)
                        {
                            AppService.CaptureUiMessage($"{UnitName} has no move actions available.");
                            return false;
                        }

                        // Check if we have sufficient movement points and consume them.
                        if (!ConsumeMovementPoints(movtCost))
                        {
                            AppService.CaptureUiMessage($"{UnitName} does not have enough movement points to perform a move action.");
                            return false;
                        }

                        // Deduct one move action.
                        MoveActions.DecrementCurrent();
                        return true;

                    case ActionTypes.CombatAction:
                        // Check if we have at least one combat action available
                        if (CombatActions.Current < 1)
                        {
                            AppService.CaptureUiMessage($"{UnitName} has no combat actions available.");
                            return false;
                        }

                        // Calculate and consume movement points first
                        float movementCost = GetCombatActionMovementCost();
                        if (!ConsumeMovementPoints(movementCost))
                        {
                            AppService.CaptureUiMessage($"{UnitName} does not have enough movement points to perform a combat action.");
                            return false;
                        }

                        // Deduct one combat action.
                        CombatActions.DecrementCurrent();
                        return true;

                    case ActionTypes.DeployAction:

                        // Check if we have at least one deployment action available
                        if (DeploymentActions.Current < 1)
                        {
                            AppService.CaptureUiMessage($"{UnitName} has no deployment actions available.");
                            return false;
                        }

                        // Check if we have sufficient movement points for deployment
                        float deployMovementCost = GetDeploymentActionMovementCost();
                        if (!ConsumeMovementPoints(deployMovementCost))
                        {
                            AppService.CaptureUiMessage($"{UnitName} does not have enough movement points to perform a deployment action.");
                            return false;
                        }

                        // Deduct one deployment action.
                        DeploymentActions.DecrementCurrent();
                        return true;

                    case ActionTypes.OpportunityAction:

                        // Check if we have at least one opportunity action available
                        if (OpportunityActions.Current < 1)
                        {
                            AppService.CaptureUiMessage($"{UnitName} has no opportunity actions available.");
                            return false;
                        }

                        // Consume one opportunity action
                        OpportunityActions.DecrementCurrent();
                        return true;

                    case ActionTypes.IntelAction:
                        // Check if we have at least one intel action available
                        if (IntelActions.Current < 1)
                        {
                            AppService.CaptureUiMessage($"{UnitName} has no intelligence actions available.");
                            return false;
                        }

                        // If it's not a base, gathering intel cost movement points.
                        if (!IsBase)
                        {
                            // Get intel action movement cost.
                            float intelMovementCost = GetIntelActionMovementCost();
                            if (!ConsumeMovementPoints(intelMovementCost))
                            {
                                AppService.CaptureUiMessage($"{UnitName} does not have enough movement points to perform an intelligence action.");
                                return false;
                            }
                        }

                        // Consume one intel action
                        IntelActions.DecrementCurrent();
                        return true;
                }

                // Let user know the action was not successful
                AppService.CaptureUiMessage($"{UnitName} could not perform the action: {type}.");

                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SpendAction", e);
                return false;
            }
        }

        /// <summary>
        /// Returns a dictionary mapping each action type to the number of **truly** available tokens
        /// after validating both action counters and movement‑point prerequisites.
        /// </summary>
        public Dictionary<string, float> GetAvailableActions()
        {
            // Move – must have a token and at least 1 movement point remaining.
            float moveAvailable = (CanConsumeMoveAction() && MovementPoints.Current >= 0f)
                ? MoveActions.Current : 0f;

            // Combat – existing validation already checks movement cost.
            float combatAvailable = CanConsumeCombatAction() ? CombatActions.Current : 0f;

            // Deployment – needs token **and** 50 % of max movement (unless immobile base).
            float deployMpCost = MovementPoints.Max * CUConstants.DEPLOYMENT_ACTION_MOVEMENT_COST;
            bool canDeploy = MovementPoints.Max == 0f || MovementPoints.Current >= deployMpCost;
            float deploymentAvailable = (CanConsumeDeploymentAction() && canDeploy)
                ? DeploymentActions.Current : 0f;

            // Opportunity – purely reactive, no validation.
            float opportunityAvailable = OpportunityActions.Current;

            // Intel – existing validation already handles base / movement logic.
            float intelAvailable = CanConsumeIntelAction() ? IntelActions.Current : 0f;

            return new Dictionary<string, float>
            {
                ["Move"] = moveAvailable,
                ["Combat"] = combatAvailable,
                ["Deployment"] = deploymentAvailable,
                ["Opportunity"] = opportunityAvailable,
                ["Intelligence"] = intelAvailable,
                ["MovementPoints"] = MovementPoints.Current
            };
        }

        #endregion // Action System Interface Methods


        #region CombatState Interface Methods

        /// <summary>
        /// Moves the unit up one combat state (toward more mobile/less defensive posture).
        /// Progression: Fortified → Entrenched → HastyDefense → Deployed → Mobile
        /// </summary>
        /// <returns>True if state change was successful, false if already at maximum mobility or transition invalid</returns>
        public bool UpOneState()
        {
            try
            {
                // Define the state order for navigation
                var stateOrder = new Dictionary<CombatState, int>
                {
                    { CombatState.Mobile, 0 },
                    { CombatState.Deployed, 1 },
                    { CombatState.HastyDefense, 2 },
                    { CombatState.Entrenched, 3 },
                    { CombatState.Fortified, 4 }
                };

                // Check if current state is valid
                if (!stateOrder.ContainsKey(CombatState))
                {
                    AppService.CaptureUiMessage($"Cannot move up from unknown combat state: {CombatState}");
                    return false;
                }

                int currentIndex = stateOrder[CombatState];

                // Check if already at maximum mobility (Mobile state)
                if (currentIndex == 0)
                {
                    AppService.CaptureUiMessage($"{UnitName} is already in Mobile state - cannot move to higher mobility.");
                    return false;
                }

                // Calculate target state (one step toward Mobile)
                int targetIndex = currentIndex - 1;
                var targetState = stateOrder.First(kvp => kvp.Value == targetIndex).Key;

                // Use existing validation and state change logic
                return SetCombatState(targetState);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "UpOneState", e);
                return false;
            }
        }

        /// <summary>
        /// Moves the unit down one combat state (toward more defensive/less mobile posture).
        /// Progression: Mobile → Deployed → HastyDefense → Entrenched → Fortified
        /// </summary>
        /// <returns>True if state change was successful, false if already at maximum defensive posture or transition invalid</returns>
        public bool DownOneState()
        {
            try
            {
                // Define the state order for navigation
                var stateOrder = new Dictionary<CombatState, int>
                {
                    { CombatState.Mobile, 0 },
                    { CombatState.Deployed, 1 },
                    { CombatState.HastyDefense, 2 },
                    { CombatState.Entrenched, 3 },
                    { CombatState.Fortified, 4 }
                };

                // Check if current state is valid
                if (!stateOrder.ContainsKey(CombatState))
                {
                    AppService.CaptureUiMessage($"Cannot move down from unknown combat state: {CombatState}");
                    return false;
                }

                int currentIndex = stateOrder[CombatState];

                // Check if already at maximum defensive posture (Fortified state)
                if (currentIndex == 4)
                {
                    AppService.CaptureUiMessage($"{UnitName} is already in Fortified state - cannot move to higher defensive posture.");
                    return false;
                }

                // Calculate target state (one step toward Fortified)
                int targetIndex = currentIndex + 1;
                var targetState = stateOrder.First(kvp => kvp.Value == targetIndex).Key;

                // Use existing validation and state change logic
                return SetCombatState(targetState);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "DownOneState", e);
                return false;
            }
        }

        /// <summary>
        /// Attempts to transition this unit to the specified <paramref name="newState" />.
        /// </summary>
        /// <param name="newState">The combat state the unit should enter.</param>
        /// <returns>
        /// <c>true</c> if the transition succeeds; otherwise <c>false</c>. A return value of
        /// <c>false</c> indicates either <see cref="CanChangeToState(CombatState)" /> rejected the
        /// request or an unexpected internal fault was captured and logged.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown (in debug builds) when validation has already passed but a required resource
        /// —deployment action token, movement points, or supplies—cannot be spent, signalling a
        /// logic error elsewhere in the game loop. The exception is caught, logged via
        /// <see cref="AppService.HandleException(string,string,System.Exception)" />, and the
        /// method returns <c>false</c>.
        /// </exception>
        /// <remarks>
        /// <para>This method performs the state transition atomically:</para>
        /// <list type="number">
        ///   <item><description>Verifies the change is still legal by calling <see cref="CanChangeToState" />.</description></item>
        ///   <item><description>Deducts the fixed supply cost and spends a deployment action token.</description></item>
        ///   <item><description>Commits <see cref="CombatState" /> and refreshes related profiles via <see cref="UpdateStateAndProfiles" />.</description></item>
        /// </list>
        /// Because resource deductions occur inside the same critical section, the unit is never left in
        /// a “half‑paid” state: if any step fails, all changes are rolled back and the method
        /// returns <c>false</c>.</remarks>
        public bool SetCombatState(CombatState newState)
        {
            try
            {
                if (!CanChangeToState(newState))
                    return false;

                // Spend supplies first – no side-effects if it fails
                float cost = CUConstants.COMBAT_STATE_SUPPLY_TRANSITION_COST;
                if (!ConsumeSupplies(cost))
                    throw new InvalidOperationException($"{UnitName}: bug – supplies suddenly insufficient.");

                #if DEBUG
                // Deployment token should always be present after validation
                if (!SpendAction(ActionTypes.DeployAction))
                    throw new InvalidOperationException($"{UnitName}: bug – deployment token vanished.");
                #else
                if (!SpendAction(ActionTypes.DeployAction))
                {
                // Roll back supplies in release builds and bail gracefully
                ReceiveSupplies(cost);
                AppService.CaptureUiMessage($"{UnitName} could not change state due to an internal error.");
                return false;
                }
                #endif

                var previousState = CombatState;
                CombatState = newState;
                UpdateStateAndProfiles(newState, previousState);
                return true;
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, "SetCombatState", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if the unit can transition to the specified combat state.
        /// Validates unit type restrictions, adjacency rules, and resource requirements.
        /// </summary>
        /// <param name="targetState">The desired combat state</param>
        /// <returns>True if transition is allowed</returns>
        public bool CanChangeToState(CombatState targetState)
        {
            try
            {
                // Capture the UI message if needed.
                string errorMessage = $"Cannot change from {CombatState} to {targetState}: ";

                // Same state - no change needed
                if (CombatState == targetState)
                {
                    errorMessage += "Already in target state.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // Check if the unit is destroyed
                if (IsDestroyed())
                    return false;

                // Air units and bases cannot change states.
                if (!CanUnitTypeChangeStates())
                {
                    errorMessage += "Unit type cannot change combat states.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // Check if transition is adjacent
                if (!IsAdjacentStateTransition(CombatState, targetState))
                {
                    errorMessage += $"Transition from {CombatState} to {targetState} is not adjacent.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // Make sure you have a deployment action to spend.
                if (!CanConsumeDeploymentAction())
                {
                    errorMessage += "No deployment actions available for state change.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // You need sufficient movement points for deployment actions.
                if (!HasSufficientMovementForDeployment())
                {
                    errorMessage += "Insufficient movement points for deployment action.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // Check if the unit has critical supply levels
                if (DaysSupply.Current <= CUConstants.CRITICAL_SUPPLY_THRESHOLD)
                {
                    errorMessage += "Cannot change state with critical supply levels.";
                    AppService.CaptureUiMessage(errorMessage);
                    return false;
                }

                // Only limited CombatState transitions are allowed based on efficiency level.
                if (EfficiencyLevel == EfficiencyLevel.StaticOperations)
                {
                    if (CombatState == CombatState.Fortified || CombatState == CombatState.Entrenched || CombatState == CombatState.HastyDefense)
                    {
                        errorMessage += "Cannot change from defensive states in Static Operations.";
                        AppService.CaptureUiMessage(errorMessage);
                        return false; // Cannot change from defensive states in static operations
                    }

                    if (targetState == CombatState.Mobile)
                    {
                        errorMessage += "Cannot change to Mobile state in Static Operations.";
                        AppService.CaptureUiMessage(errorMessage);
                        return false; // Cannot change to Mobile state in static operations
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "CanChangeToState", e);
                return false;
            }
        }

        /// <summary>
        /// Begins entrenchment process by transitioning to HastyDefense.
        /// Convenience method for defensive positioning.
        /// </summary>
        /// <returns>True if entrenchment began successfully</returns>
        public bool BeginEntrenchment()
        {
            try
            {
                if (CombatState != CombatState.Deployed)
                {
                    return false; // Can only start entrenchment from Deployed
                }

                return SetCombatState(CombatState.HastyDefense);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "BeginEntrenchment", e);
                return false;
            }
        }

        /// <summary>
        /// Checks if the unit can begin entrenchment (transition to defensive states).
        /// </summary>
        /// <returns>True if entrenchment is possible</returns>
        public bool CanEntrench()
        {
            try
            {
                return CanChangeToState(CombatState.HastyDefense);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "CanEntrench", e);
                return false;
            }
        }

        /// <summary>
        /// Gets all valid combat states this unit can transition to from current state.
        /// </summary>
        /// <returns>List of valid target states</returns>
        public List<CombatState> GetValidStateTransitions()
        {
            var validStates = new List<CombatState>();

            try
            {
                if (!CanUnitTypeChangeStates())
                {
                    return validStates; // Return empty list
                }

                // Check each possible state
                foreach (CombatState state in (CombatState[])Enum.GetValues(typeof(CombatState)))
                {
                    if (CanChangeToState(state))
                    {
                        validStates.Add(state);
                    }
                }

                return validStates;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetValidStateTransitions", e);
                return validStates;
            }
        }

        #endregion // CombatState Interface Methods


        #region Position and Movement Interface Methods

        /// <summary>
        /// Sets the unit's position on the map.
        /// </summary>
        /// <param name="newPos">The new position coordinates</param>
        public void SetPosition(Coordinate2D newPos)
        {
            try
            {
                MapPos = newPos; // Direct assignment instead of reflection
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetPosition", e);
                throw;
            }
        }

        /// <summary>
        /// Checks if the unit can move to the specified position.
        /// This is a basic validation - full movement rules will be implemented later.
        /// </summary>
        /// <param name="targetPos">The target position to validate</param>
        /// <returns>True if movement appears valid</returns>
        public bool CanMoveTo(Coordinate2D targetPos)
        {
            try
            {
                throw new NotImplementedException("Movement validation logic not implemented yet.");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "CanMoveTo", e);
                return false;
            }
        }

        /// <summary>
        /// Gets the distance between this unit and a target position in Unity units.
        /// </summary>
        /// <param name="targetPos">The target position</param>
        /// <returns>Distance in Unity units</returns>
        public float GetDistanceTo(Coordinate2D targetPos)
        {
            try
            {
                throw new NotImplementedException("Distance calculation logic not implemented yet.");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetDistanceTo", e);
                return 0f;
            }
        }

        /// <summary>
        /// Gets the distance between this unit and another unit.
        /// </summary>
        /// <param name="otherUnit">The other unit</param>
        /// <returns>Distance in Unity units</returns>
        public float GetDistanceTo(CombatUnit otherUnit)
        {
            try
            {
                throw new NotImplementedException("Distance calculation logic not implemented yet.");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetDistanceTo", e);
                return 0f;
            }
        }

        #endregion // Position and Movement Interface Methods


        #region Debugging Methods

        /// <summary>
        /// Direct change of combat state for debugging purposes.
        /// </summary>
        /// <param name="newState"></param>
        public void DebugSetCombatState(CombatState newState)
        {
            CombatState = newState;
        }

        /// <summary>
        /// Sets the mounted state of the object for debugging purposes.
        /// </summary>
        /// <param name="isMounted">A value indicating whether the object should be marked as mounted.  <see langword="true"/> to mark the
        /// object as mounted; otherwise, <see langword="false"/>.</param>
        public void DebugSetMounted(bool isMounted)
        {
            IsMounted = isMounted;
        }   

        #endregion // Debugging Methods


        #region Generic Interface Helper Methods

        /// <summary>
        /// Initializes action counts based on unit type and classification.
        /// Most units get standard action counts, with variations for special cases.
        /// </summary>
        private void InitializeActionCounts()
        {
            // Standard action counts for most units
            int moveActions = CUConstants.DEFAULT_MOVE_ACTIONS;
            int combatActions = CUConstants.DEFAULT_COMBAT_ACTIONS;
            int deploymentActions = CUConstants.DEFAULT_DEPLOYMENT_ACTIONS;
            int opportunityActions = CUConstants.DEFAULT_OPPORTUNITY_ACTIONS;
            int intelActions = CUConstants.DEFAULT_INTEL_ACTIONS;


            switch (Classification)
            {
                case UnitClassification.TANK:
                case UnitClassification.MECH:
                case UnitClassification.MOT:
                case UnitClassification.AB:
                case UnitClassification.MAB:
                case UnitClassification.MAR:
                case UnitClassification.MMAR:
                case UnitClassification.AT:
                case UnitClassification.INF:
                case UnitClassification.ART:
                case UnitClassification.SPA:
                case UnitClassification.ROC:
                case UnitClassification.BM:
                case UnitClassification.ENG:
                case UnitClassification.HELO:
                    break;

                case UnitClassification.RECON:
                    moveActions += 1;
                    break;

                case UnitClassification.AM:
                case UnitClassification.MAM:
                    deploymentActions += 1;
                    break;

                case UnitClassification.SPECF:
                case UnitClassification.SPECM:
                case UnitClassification.SPECH:
                    intelActions += 1;
                    break;

                case UnitClassification.SAM:
                case UnitClassification.SPSAM:
                case UnitClassification.AAA:
                case UnitClassification.SPAAA:
                    opportunityActions += 1;
                    break;

                case UnitClassification.ASF:
                case UnitClassification.MRF:
                case UnitClassification.ATT:
                case UnitClassification.BMB:
                case UnitClassification.RECONA:
                    moveActions += 2;
                    combatActions = 0;
                    deploymentActions = 0;
                    intelActions = 0;
                    break;

                case UnitClassification.HQ:
                    intelActions += 1;
                    break;

                case UnitClassification.DEPOT:
                case UnitClassification.AIRB:
                    moveActions = 0;
                    combatActions = 0;
                    deploymentActions = 0;
                    opportunityActions = 0;
                    intelActions = 0;
                    break;

                default:
                    break;
            }

            // Create StatsMaxCurrent instances
            MoveActions = new StatsMaxCurrent(moveActions);
            CombatActions = new StatsMaxCurrent(combatActions);
            DeploymentActions = new StatsMaxCurrent(deploymentActions);
            OpportunityActions = new StatsMaxCurrent(opportunityActions);
            IntelActions = new StatsMaxCurrent(intelActions);
        }

        /// <summary>
        /// Initializes movement points based on unit classification.
        /// </summary>
        private void InitializeMovementPoints()
        {
            var maxMovement = Classification switch
            {
                UnitClassification.TANK or
                UnitClassification.MECH or
                UnitClassification.RECON or
                UnitClassification.MAB or
                UnitClassification.MAM or
                UnitClassification.MMAR or
                UnitClassification.SPECM or
                UnitClassification.SPA or
                UnitClassification.SPAAA or
                UnitClassification.SPSAM => CUConstants.MECH_MOV,

                UnitClassification.AT or
                UnitClassification.MOT or
                UnitClassification.ROC => CUConstants.MOT_MOV,

                UnitClassification.INF or
                UnitClassification.AB or
                UnitClassification.AM or
                UnitClassification.MAR or
                UnitClassification.ART or
                UnitClassification.SAM or
                UnitClassification.AAA or
                UnitClassification.SPECF or
                UnitClassification.ENG => CUConstants.FOOT_MOV,

                UnitClassification.ASF or
                UnitClassification.MRF or
                UnitClassification.ATT or
                UnitClassification.BMB or
                UnitClassification.RECONA => CUConstants.FIXEDWING_MOV,

                UnitClassification.HELO or
                UnitClassification.SPECH => CUConstants.HELO_MOV,

                UnitClassification.HQ or
                UnitClassification.DEPOT or
                UnitClassification.AIRB => 0,// Bases don't move

                _ => CUConstants.FOOT_MOV,// Default to foot movement
            };
            MovementPoints = new StatsMaxCurrent(maxMovement);
        }

        /// <summary>
        /// Calculates the final combat rating modifier by combining various contributing factors.
        /// </summary>
        /// <remarks>This method aggregates multiple modifiers, including strength, combat state,
        /// efficiency, and experience, to compute the final combat rating modifier. If an error occurs during
        /// calculation, the method returns a default neutral modifier of 1.0.</remarks>
        /// <returns>A <see cref="float"/> representing the final combat rating modifier. The value is the product of all
        /// contributing modifiers, or 1.0 if an error occurs.</returns>
        private float GetFinalCombatRatingModifier()
        {
            try
            {
                // Calculate the final combat rating modifier based on all factors.
                float strengthModifier = GetStrengthModifier();
                float combatStateModifier = GetCombatStateModifier();
                float efficiencyModifier = GetEfficiencyModifier();
                float experienceModifier = GetExperienceMultiplier();

                // Combine all modifiers
                return strengthModifier * combatStateModifier * efficiencyModifier * experienceModifier;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetFinalCombatRatingModifier", e);
                return 1.0f; // Default to neutral modifier on error
            }
        }

        /// <summary>
        /// Gets the combat effectiveness as a percentage based on current hit points.
        /// </summary>
        /// <returns>Combat effectiveness from 0.0 to 1.0</returns>
        private float GetStrengthModifier()
        {
            try
            {
                // Compute the combat strength multiplier based on hit points.
                if (HitPoints.Current >= (HitPoints.Max * CUConstants.FULL_STRENGTH_FLOOR))
                {
                    return CUConstants.STRENGTH_MOD_FULL;
                }
                else if (HitPoints.Current >= (HitPoints.Max * CUConstants.DEPLETED_STRENGTH_FLOOR))
                {
                    return CUConstants.STRENGTH_MOD_DEPLETED;
                }
                else
                {
                    return CUConstants.STRENGTH_MOD_LOW;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetStrengthModifier", e);
                return CUConstants.STRENGTH_MOD_LOW;
            }
        }

        /// <summary>
        /// Calculates the combat state modifier based on the current combat state.
        /// </summary>
        /// <returns>A <see cref="float"/> representing the combat state multiplier. The value corresponds to the current combat
        /// state, with predefined modifiers for specific states. Returns <c>1.0f</c> if the combat state is not
        /// recognized.</returns>
        private float GetCombatStateModifier()
        {
            // Returns the combat state multiplier based on current combat state.
            return CombatState switch
            {
                CombatState.Mobile => CUConstants.COMBAT_MOD_MOBILE,
                CombatState.Deployed => CUConstants.COMBAT_MOD_DEPLOYED,
                CombatState.HastyDefense => CUConstants.COMBAT_MOD_HASTY_DEFENSE,
                CombatState.Entrenched => CUConstants.COMBAT_MOD_ENTRENCHED,
                CombatState.Fortified => CUConstants.COMBAT_MOD_FORTIFIED,
                _ => 1.0f, // Default multiplier for other states
            };
        }

        /// <summary>
        /// Calculates the efficiency modifier based on the current efficiency level.
        /// </summary>
        /// <remarks>The returned modifier is determined by the current <c>EfficiencyLevel</c> and maps to
        /// specific  constants defined in <c>CUConstants</c>. If the efficiency level is unrecognized, a default 
        /// static modifier is returned.</remarks>
        /// <returns>A <see cref="float"/> representing the efficiency modifier. The value corresponds to the current 
        /// operational state, with predefined constants for each efficiency level.</returns>
        private float GetEfficiencyModifier()
        {
            // Returns the efficiency modifier based on current efficiency level.
            return EfficiencyLevel switch
            {
                EfficiencyLevel.PeakOperational => CUConstants.EFFICIENCY_MOD_PEAK,
                EfficiencyLevel.FullyOperational => CUConstants.EFFICIENCY_MOD_FULL,
                EfficiencyLevel.Operational => CUConstants.EFFICIENCY_MOD_OPERATIONAL,
                EfficiencyLevel.DegradedOperations => CUConstants.EFFICIENCY_MOD_DEGRADED,
                _ => CUConstants.EFFICIENCY_MOD_STATIC, // Default multiplier for other states
            };
        }

        /// <summary>
        /// Used to re-establish the parent relationship of the FacilityManager.
        /// </summary>
        private void ReestablishFacilityManagerParent()
        {
            try
            {
                if (FacilityManager == null) return;

                if (!IsBase)
                {
                    // For non-base units, just set the parent directly
                    FacilityManager.SetParent(this);
                    return;
                }

                // For base units, re-establish the parent relationship based on the unit classification
                switch (Classification)
                {
                    case UnitClassification.HQ:
                        FacilityManager.SetupHQ(this);
                        break;
                    case UnitClassification.DEPOT:
                        // We need to preserve the original depot settings
                        var category = FacilityManager.DepotCategory;
                        var size = FacilityManager.DepotSize;
                        FacilityManager.SetupSupplyDepot(this, category, size);
                        break;
                    case UnitClassification.AIRB:
                        FacilityManager.SetupAirbase(this);
                        break;
                    default:
                        // For other classifications, just set the parent
                        FacilityManager.SetParent(this);
                        break;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ReestablishFacilityManagerParent", e);
            }
        }

        #endregion // Generic Interface Helper Methods


        #region Experience System Helper Methods

        /// <summary>
        /// Calculates the experience level based on total experience points.
        /// </summary>
        /// <param name="totalPoints">Total experience points</param>
        /// <returns>The appropriate experience level</returns>
        private ExperienceLevel CalculateExperienceLevel(int totalPoints)
        {
            if (totalPoints >= (int)ExperiencePointLevels.Elite)
                return ExperienceLevel.Elite;
            else if (totalPoints >= (int)ExperiencePointLevels.Veteran)
                return ExperienceLevel.Veteran;
            else if (totalPoints >= (int)ExperiencePointLevels.Experienced)
                return ExperienceLevel.Experienced;
            else if (totalPoints >= (int)ExperiencePointLevels.Trained)
                return ExperienceLevel.Trained;
            else if (totalPoints >= (int)ExperiencePointLevels.Green)
                return ExperienceLevel.Green;
            else
                return ExperienceLevel.Raw;
        }

        /// <summary>
        /// Gets the minimum experience points required for a specific level.
        /// </summary>
        /// <param name="level">The experience level</param>
        /// <returns>Minimum points required for that level</returns>
        private int GetMinPointsForLevel(ExperienceLevel level)
        {
            return level switch
            {
                ExperienceLevel.Raw => (int)ExperiencePointLevels.Raw,
                ExperienceLevel.Green => (int)ExperiencePointLevels.Green,
                ExperienceLevel.Trained => (int)ExperiencePointLevels.Trained,
                ExperienceLevel.Experienced => (int)ExperiencePointLevels.Experienced,
                ExperienceLevel.Veteran => (int)ExperiencePointLevels.Veteran,
                ExperienceLevel.Elite => (int)ExperiencePointLevels.Elite,
                _ => 0,
            };
        }

        /// <summary>
        /// Gets the next experience level after the specified level.
        /// Returns Elite if already at Elite.
        /// </summary>
        /// <param name="currentLevel">Current experience level</param>
        /// <returns>Next experience level</returns>
        private ExperienceLevel GetNextLevel(ExperienceLevel currentLevel)
        {
            return currentLevel switch
            {
                ExperienceLevel.Raw => ExperienceLevel.Green,
                ExperienceLevel.Green => ExperienceLevel.Trained,
                ExperienceLevel.Trained => ExperienceLevel.Experienced,
                ExperienceLevel.Experienced => ExperienceLevel.Veteran,
                ExperienceLevel.Veteran => ExperienceLevel.Elite,
                ExperienceLevel.Elite => ExperienceLevel.Elite,// Already at max
                _ => ExperienceLevel.Green,
            };
        }

        /// <summary>
        /// Called when the unit's experience level changes.
        /// Can be overridden or used to trigger events/notifications.
        /// </summary>
        /// <param name="previousLevel">The previous experience level</param>
        /// <param name="newLevel">The new experience level</param>
        protected virtual void OnExperienceLevelChanged(ExperienceLevel previousLevel, ExperienceLevel newLevel)
        {
            AppService.CaptureUiMessage($"{UnitName} has advanced from {previousLevel} to {newLevel}!");
        }

        /// <summary>
        /// Gets the combat effectiveness multiplier based on experience level.
        /// Used to modify combat values based on unit experience.
        /// </summary>
        /// <returns>Multiplier for combat effectiveness (1.0 = normal)</returns>
        private float GetExperienceMultiplier()
        {
            return ExperienceLevel switch
            {
                ExperienceLevel.Raw => CUConstants.RAW_XP_MODIFIER,                // -20% effectiveness
                ExperienceLevel.Green => CUConstants.GREEN_XP_MODIFIER,            // -10% effectiveness
                ExperienceLevel.Trained => CUConstants.TRAINED_XP_MODIFIER,        // Normal effectiveness
                ExperienceLevel.Experienced => CUConstants.EXPERIENCED_XP_MODIFIER,// +10% effectiveness
                ExperienceLevel.Veteran => CUConstants.VETERAN_XP_MODIFIER,        // +20% effectiveness
                ExperienceLevel.Elite => CUConstants.ELITE_XP_MODIFIER,            // +30% effectiveness
                _ => 1.0f,
            };
        }

        #endregion // Experience System Helper Methods


        #region Leader System Helper Methods

        /// <summary>
        /// Validates that IsLeaderAssigned flag is consistent with CommandingOfficer state.
        /// </summary>
        /// <returns>True if consistent, false if there's a mismatch</returns>
        private bool ValidateLeaderAssignmentConsistency()
        {
            return (CommandingOfficer == null && !IsLeaderAssigned) ||
                   (CommandingOfficer != null && IsLeaderAssigned &&
                    CommandingOfficer.IsAssigned && CommandingOfficer.UnitID == UnitID);
        }

        /// <summary>
        /// Fixes any inconsistency between IsLeaderAssigned flag and CommandingOfficer state.
        /// </summary>
        private void FixLeaderAssignmentConsistency()
        {
            bool hasLeader = CommandingOfficer != null;
            if (IsLeaderAssigned != hasLeader)
            {
                AppService.HandleException(CLASS_NAME, "FixLeaderAssignmentConsistency", new Exception());
                IsLeaderAssigned = hasLeader;
            }
        }

        /// <summary>
        /// Gets the display name of a unit by its ID, with fallback handling.
        /// </summary>
        /// <param name="unitId">The unit ID to look up</param>
        /// <returns>The unit's display name or a fallback string</returns>
        private string GetUnitDisplayName(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return "Unknown Unit";
            }

            try
            {
                var unit = GameDataManager.Instance.GetCombatUnit(unitId);
                return unit?.UnitName ?? $"Unit {unitId}";
            }
            catch (Exception e)
            {
                // Log the query failure but return fallback name
                AppService.HandleException(CLASS_NAME, "GetUnitDisplayName", new Exception());
                return $"Unit {unitId}";
            }
        }

        #endregion


        #region Action System Helper Methods

        /// <summary>
        /// Consumes movement points if available.
        /// </summary>
        /// <param name="points">Number of movement points to consume</param>
        /// <returns>True if movement points were consumed, false if insufficient</returns>
        private bool ConsumeMovementPoints(float points)
        {
            try
            {
                if (points <= 0f)
                {
                    return true; // No points to consume
                }

                if (MovementPoints.Current >= points)
                {
                    MovementPoints.SetCurrent(MovementPoints.Current - points);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ConsumeMovementPoints", e);
                return false;
            }
        }

        /// <summary>
        /// Calculates the movement point cost for a deployment action.
        /// </summary>
        /// <returns>Movement points required (50% of max)</returns>
        private float GetDeploymentActionMovementCost()
        {
            return MovementPoints.Max * CUConstants.DEPLOYMENT_ACTION_MOVEMENT_COST;
        }

        /// <summary>
        /// Calculates the movement‑point cost for a combat action.
        /// Immobile units (Max == 0) pay nothing.
        /// </summary>
        private float GetCombatActionMovementCost()
        {
            if (MovementPoints.Max <= 0f) return 0f;
            return MovementPoints.Max * CUConstants.COMBAT_ACTION_MOVEMENT_COST;
        }

        /// <summary>
        /// Calculates the movement‑point cost for an intelligence action.
        /// Immobile units (Max == 0) pay nothing.
        /// </summary>
        private float GetIntelActionMovementCost()
        {
            if (MovementPoints.Max <= 0f) return 0f;
            return MovementPoints.Max * CUConstants.INTEL_ACTION_MOVEMENT_COST;
        }

        /// <summary>
        /// Checks if the unit can consume a move action.
        /// </summary>
        /// <returns>True if at least one move action is available</returns>
        private bool CanConsumeMoveAction()
        {
            return MoveActions.Current >= 1f;
        }

        /// <summary>
        /// Checks if the unit can consume a combat action and has sufficient movement points.
        /// </summary>
        /// <returns>True if at least one combat action and sufficient movement are available</returns>
        private bool CanConsumeCombatAction()
        {
            // Check if we have a combat action available
            if (CombatActions.Current < 1f)
            {
                return false;
            }

            // Check if we have sufficient movement points
            float requiredMovement = GetCombatActionMovementCost();
            if (MovementPoints.Current < requiredMovement)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the unit can consume a deployment action.
        /// </summary>
        /// <returns>True if at least one deployment action is available</returns>
        private bool CanConsumeDeploymentAction()
        {
            return DeploymentActions.Current >= 1f;
        }

        /// <summary>
        /// Checks if the unit can consume an intelligence action and has sufficient movement points.
        /// Bases don't require movement points for intel actions.
        /// </summary>
        /// <returns>True if at least one intelligence action and sufficient movement are available</returns>
        private bool CanConsumeIntelAction()
        {
            // Check if we have an intel action available
            if (IntelActions.Current < 1f)
            {
                return false;
            }

            // Bases don't need movement points for intel gathering
            if (IsBase)
            {
                return true;
            }

            // Check if we have sufficient movement points
            float requiredMovement = GetIntelActionMovementCost();
            if (MovementPoints.Current < requiredMovement)
            {
                return false;
            }

            return true;
        }

        #endregion // Action System Helper Methods


        #region CombatState Interface Helper Methods

        /// <summary>
        /// Checks if this unit classification can change combat states.
        /// Fixed-wing aircraft and bases cannot change states.
        /// </summary>
        /// <returns>True if unit can change states</returns>
        private bool CanUnitTypeChangeStates()
        {
            // Fixed-wing aircraft cannot change states
            if (Classification == UnitClassification.ASF ||
                Classification == UnitClassification.MRF ||
                Classification == UnitClassification.ATT ||
                Classification == UnitClassification.BMB ||
                Classification == UnitClassification.RECONA)
            {
                return false;
            }

            // Bases cannot change states
            if (Classification == UnitClassification.HQ ||
                Classification == UnitClassification.DEPOT ||
                Classification == UnitClassification.AIRB)
            {
                return false;
            }

            // All other units (including helicopters) can change states
            return true;
        }

        /// <summary>
        /// Checks if the transition between two states is adjacent (one step).
        /// </summary>
        /// <param name="currentState">Current combat state</param>
        /// <param name="targetState">Target combat state</param>
        /// <returns>True if transition is adjacent</returns>
        private bool IsAdjacentStateTransition(CombatState currentState, CombatState targetState)
        {
            // Define the state order: Mobile ← Deployed → HastyDefense → Entrenched → Fortified
            var stateOrder = new Dictionary<CombatState, int>
            {
                { CombatState.Mobile, 0 },
                { CombatState.Deployed, 1 },
                { CombatState.HastyDefense, 2 },
                { CombatState.Entrenched, 3 },
                { CombatState.Fortified, 4 }
            };

            if (!stateOrder.ContainsKey(currentState) || !stateOrder.ContainsKey(targetState))
            {
                return false;
            }

            int currentIndex = stateOrder[currentState];
            int targetIndex = stateOrder[targetState];

            // Adjacent means difference of exactly 1
            return Math.Abs(currentIndex - targetIndex) == 1;
        }

        /// <summary>
        /// Checks if unit has sufficient movement points for a deployment action.
        /// Deployment actions cost 50% of max movement points.
        /// </summary>
        /// <returns>True if sufficient movement points available</returns>
        private bool HasSufficientMovementForDeployment()
        {
            float requiredMovement = GetDeploymentActionMovementCost();
            return MovementPoints.Current >= requiredMovement;
        }

        /// <summary>
        /// Updates the unit's mounted state and movement points based on combat state transitions.
        /// </summary>
        /// <remarks>
        /// This method handles profile switching and movement point adjustments during combat state changes.
        /// Movement points are only modified when transitioning TO or FROM Mobile state to add/remove movement bonuses.
        /// For transitions between non-Mobile states, movement points are left unchanged to preserve the 
        /// movement point consumption from SpendAction.
        private void UpdateStateAndProfiles(CombatState newState, CombatState previousState)
        {
            try
            {
                // Validate BEFORE making any changes
                if (DeployedProfile == null)
                    throw new InvalidOperationException("Cannot update state: DeployedProfile is null");

                if (newState == CombatState.Mobile && IsMounted && MountedProfile == null)
                    throw new InvalidOperationException("Cannot update state: Unit is mounted but MountedProfile is null");

                // Handle transition TO Mobile state
                if (newState == CombatState.Mobile)
                {
                    // Check for a mounted profile first
                    if (MountedProfile != null)
                    {
                        // Unit can physically mount - set mounted state
                        if (!IsMounted) IsMounted = true;
                    }
                    else
                    {
                        // No MountedProfile available - add movement bonus to current movement
                        float movementBonus = CUConstants.MOBILE_MOVEMENT_BONUS;
                        float newMaxMovement = MovementPoints.Max + movementBonus;

                        // Update max movement while preserving current movement points
                        float currentMovement = MovementPoints.Current;
                        MovementPoints.SetMax(newMaxMovement);
                        MovementPoints.SetCurrent(currentMovement + movementBonus);

                        // Unit is not mounted but gets movement bonus
                        IsMounted = false;
                    }
                }
                // Handle transition FROM Mobile state
                else if (previousState == CombatState.Mobile)
                {
                    // If coming FROM Mobile state, we need to remove any movement bonuses

                    // If unit was mounted, just dismount
                    if (IsMounted)
                    {
                        IsMounted = false;
                    }
                    // If unit had movement bonus (wasn't mounted but was Mobile), remove the bonus
                    else if (MountedProfile == null)
                    {
                        // Remove the movement bonus while preserving consumed movement points
                        float movementBonus = CUConstants.MOBILE_MOVEMENT_BONUS;
                        float newMaxMovement = MovementPoints.Max - movementBonus;
                        float currentMovement = MovementPoints.Current - movementBonus;

                        // Ensure current movement doesn't go below 0
                        currentMovement = MathF.Max(0f, currentMovement);

                        MovementPoints.SetMax(newMaxMovement);
                        MovementPoints.SetCurrent(currentMovement);
                    }
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "UpdateStateAndProfiles", e);
            }
        }

        #endregion // CombatState Interface Helper Methods


        #region ICloneable Implementation

        public object Clone()
        {
            try
            {
                // Create new unit using constructor with same core properties
                // This ensures proper initialization and generates a new UnitID
                var clone = new CombatUnit(
                    this.UnitName,
                    this.UnitType,
                    this.Classification,
                    this.Role,
                    this.Side,
                    this.Nationality,
                    this.DeployedProfile,      // Shared reference
                    this.MountedProfile,       // Shared reference  
                    this.UnitProfile,          // Shared reference
                    this.IsTransportable,
                    this.IsBase
                );

                // Deep copy all StatsMaxCurrent objects by reconstructing them
                clone.HitPoints = new StatsMaxCurrent(this.HitPoints.Max, this.HitPoints.Current);
                clone.DaysSupply = new StatsMaxCurrent(this.DaysSupply.Max, this.DaysSupply.Current);
                clone.MovementPoints = new StatsMaxCurrent(this.MovementPoints.Max, this.MovementPoints.Current);
                clone.MoveActions = new StatsMaxCurrent(this.MoveActions.Max, this.MoveActions.Current);
                clone.CombatActions = new StatsMaxCurrent(this.CombatActions.Max, this.CombatActions.Current);
                clone.DeploymentActions = new StatsMaxCurrent(this.DeploymentActions.Max, this.DeploymentActions.Current);
                clone.OpportunityActions = new StatsMaxCurrent(this.OpportunityActions.Max, this.OpportunityActions.Current);
                clone.IntelActions = new StatsMaxCurrent(this.IntelActions.Max, this.IntelActions.Current);

                // Copy per-unit state data
                clone.SetExperience(this.ExperiencePoints);
                clone.EfficiencyLevel = this.EfficiencyLevel;
                clone.IsMounted = this.IsMounted;
                clone.CombatState = this.CombatState;
                clone.MapPos = this.MapPos;
                clone.SpottedLevel = this.SpottedLevel;

                // NOTE: CommandingOfficer and IsLeaderAssigned are NOT copied
                // Templates should never have leaders assigned
                // Leaders must be assigned manually after cloning

                // Clone FacilityManager for base units
                if (this.FacilityManager != null)
                {
                    var clonedFacilityManager = this.FacilityManager.Clone();

                    // Re-establish the parent relationship for the cloned facility
                    if (this.IsBase)
                    {
                        switch (this.Classification)
                        {
                            case UnitClassification.HQ:
                                clonedFacilityManager.SetupHQ(clone);
                                break;
                            case UnitClassification.DEPOT:
                                clonedFacilityManager.SetupSupplyDepot(clone, this.FacilityManager.DepotCategory, this.FacilityManager.DepotSize);
                                break;
                            case UnitClassification.AIRB:
                                clonedFacilityManager.SetupAirbase(clone);
                                // Air unit attachments are never cloned - templates should be empty
                                break;
                            default:
                                clonedFacilityManager.SetParent(clone);
                                break;
                        }
                    }
                    else
                    {
                        clonedFacilityManager.SetParent(clone);
                    }

                    clone.FacilityManager = clonedFacilityManager;
                }

                // Copy properties with private setters using reflection
                var cloneType = typeof(CombatUnit);

                // Copy unresolved reference fields (should be empty in normal cloning scenarios)
                cloneType.GetField("unresolvedDeployedProfileID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(clone, this.unresolvedDeployedProfileID);
                cloneType.GetField("unresolvedMountedProfileID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(clone, this.unresolvedMountedProfileID);
                cloneType.GetField("unresolvedUnitProfileID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(clone, this.unresolvedUnitProfileID);

                return clone;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "Clone", e);
                throw;
            }
        }

        #endregion // ICloneable Implementation


        #region ISerializable Implementation

        /// <summary>
        /// Serializes CombatUnit data for saving to file.
        /// </summary>
        /// <param name="info">Serialization info to store data</param>
        /// <param name="context">Streaming context for serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            try
            {
                // Serialize basic properties
                info.AddValue(nameof(UnitName), UnitName);
                info.AddValue(nameof(UnitID), UnitID);
                info.AddValue(nameof(UnitType), UnitType);
                info.AddValue(nameof(Classification), Classification);
                info.AddValue(nameof(Role), Role);
                info.AddValue(nameof(Side), Side);
                info.AddValue(nameof(Nationality), Nationality);
                info.AddValue(nameof(IsTransportable), IsTransportable);
                info.AddValue(nameof(IsBase), IsBase);
                info.AddValue(nameof(IsLeaderAssigned), IsLeaderAssigned);
                info.AddValue(nameof(SpottedLevel), SpottedLevel);

                // Serialize profile references as IDs/names (not the objects themselves)
                info.AddValue("DeployedProfileID", DeployedProfile?.WeaponSystemID ?? "");
                info.AddValue("MountedProfileID", MountedProfile?.WeaponSystemID ?? "");
                info.AddValue("UnitProfileID", UnitProfile?.UnitProfileID ?? "");
                info.AddValue("LeaderID", CommandingOfficer?.LeaderID ?? "");

                // Serialize owned StatsMaxCurrent objects as Max/Current pairs
                info.AddValue("HitPoints_Max", HitPoints.Max);
                info.AddValue("HitPoints_Current", HitPoints.Current);
                info.AddValue("DaysSupply_Max", DaysSupply.Max);
                info.AddValue("DaysSupply_Current", DaysSupply.Current);
                info.AddValue("MovementPoints_Max", MovementPoints.Max);
                info.AddValue("MovementPoints_Current", MovementPoints.Current);
                info.AddValue("MoveActions_Max", MoveActions.Max);
                info.AddValue("MoveActions_Current", MoveActions.Current);
                info.AddValue("CombatActions_Max", CombatActions.Max);
                info.AddValue("CombatActions_Current", CombatActions.Current);
                info.AddValue("DeploymentActions_Max", DeploymentActions.Max);
                info.AddValue("DeploymentActions_Current", DeploymentActions.Current);
                info.AddValue("OpportunityActions_Max", OpportunityActions.Max);
                info.AddValue("OpportunityActions_Current", OpportunityActions.Current);
                info.AddValue("IntelActions_Max", IntelActions.Max);
                info.AddValue("IntelActions_Current", IntelActions.Current);

                // Serialize simple properties
                info.AddValue(nameof(ExperiencePoints), ExperiencePoints);
                info.AddValue(nameof(ExperienceLevel), ExperienceLevel);
                info.AddValue(nameof(EfficiencyLevel), EfficiencyLevel);
                info.AddValue(nameof(IsMounted), IsMounted);
                info.AddValue(nameof(CombatState), CombatState);
                info.AddValue(nameof(MapPos), MapPos);

                // Serialize FacilityManager
                bool hasFacilityManager = FacilityManager != null;
                info.AddValue("HasFacilityManager", hasFacilityManager);

                if (hasFacilityManager)
                {
                    // Let FacilityManager serialize itself with FM_ prefix to avoid naming conflicts
                    FacilityManager.GetObjectData(info, context);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetObjectData), e);
                throw;
            }
        }

        /// <summary>
        /// Checks if there are unresolved references that need to be resolved.
        /// </summary>
        /// <returns>True if any resolution methods need to be called</returns>
        public bool HasUnresolvedReferences()
        {
            bool hasUnresolved = !string.IsNullOrEmpty(unresolvedDeployedProfileID) ||
                !string.IsNullOrEmpty(unresolvedMountedProfileID) ||
                !string.IsNullOrEmpty(unresolvedUnitProfileID) ||
                !string.IsNullOrEmpty(unresolvedLeaderID) ||
                unresolvedFacilityManagerData;

            // Also check if FacilityManager has unresolved references
            if (FacilityManager != null && FacilityManager.HasUnresolvedReferences())
            {
                hasUnresolved = true;
            }

            return hasUnresolved;
        }

        #endregion // ISerializable Implementation


        #region IResolvableReferences

        /// <summary>
        /// Gets the list of unresolved reference IDs that need to be resolved.
        /// </summary>
        /// <returns>Collection of object IDs that this object references</returns>
        public IReadOnlyList<string> GetUnresolvedReferenceIDs()
        {
            var unresolvedIDs = new List<string>();

            if (!string.IsNullOrEmpty(unresolvedDeployedProfileID))
                unresolvedIDs.Add($"DeployedProfile:{unresolvedDeployedProfileID}");

            if (!string.IsNullOrEmpty(unresolvedMountedProfileID))
                unresolvedIDs.Add($"MountedProfile:{unresolvedMountedProfileID}");

            if (!string.IsNullOrEmpty(unresolvedUnitProfileID))
                unresolvedIDs.Add($"UnitProfile:{unresolvedUnitProfileID}");

            if (!string.IsNullOrEmpty(unresolvedLeaderID))
                unresolvedIDs.Add($"Leader:{unresolvedLeaderID}");

            if (unresolvedFacilityManagerData)
                unresolvedIDs.Add("FacilityManager:SelfReferencedData");

            // Include FacilityManager's unresolved references
            if (FacilityManager != null && FacilityManager.HasUnresolvedReferences())
            {
                var facilityRefs = FacilityManager.GetUnresolvedReferenceIDs();
                unresolvedIDs.AddRange(facilityRefs);
            }

            return unresolvedIDs.AsReadOnly();
        }

        /// <summary>
        /// Resolves object references using the provided data manager.
        /// Called after all objects have been deserialized.
        /// </summary>
        /// <param name="manager">Game data manager containing all loaded objects</param>
        public void ResolveReferences(GameDataManager manager)
        {
            try
            {
                // Resolve WeaponSystemProfile references
                if (!string.IsNullOrEmpty(unresolvedDeployedProfileID))
                {
                    if (Enum.TryParse<WeaponSystems>(unresolvedDeployedProfileID, out WeaponSystems deployedWeapon))
                    {
                        var deployedProfile = manager.GetWeaponProfile(deployedWeapon, Nationality);
                        if (deployedProfile != null)
                        {
                            DeployedProfile = deployedProfile;
                            unresolvedDeployedProfileID = "";
                        }
                        else
                        {
                            AppService.HandleException(CLASS_NAME, "ResolveReferences",
                                new KeyNotFoundException($"Deployed profile {deployedWeapon}_{Nationality} not found"));
                        }
                    }
                    else
                    {
                        AppService.HandleException(CLASS_NAME, "ResolveReferences",
                            new ArgumentException($"Invalid weapon system ID: {unresolvedDeployedProfileID}"));
                    }
                }

                if (!string.IsNullOrEmpty(unresolvedMountedProfileID))
                {
                    if (Enum.TryParse<WeaponSystems>(unresolvedMountedProfileID, out WeaponSystems mountedWeapon))
                    {
                        var mountedProfile = manager.GetWeaponProfile(mountedWeapon, Nationality);
                        if (mountedProfile != null)
                        {
                            MountedProfile = mountedProfile;
                            unresolvedMountedProfileID = "";
                        }
                        else
                        {
                            AppService.HandleException(CLASS_NAME, "ResolveReferences",
                                new KeyNotFoundException($"Mounted profile {mountedWeapon}_{Nationality} not found"));
                        }
                    }
                    else
                    {
                        AppService.HandleException(CLASS_NAME, "ResolveReferences",
                            new ArgumentException($"Invalid weapon system ID: {unresolvedMountedProfileID}"));
                    }
                }

                // Resolve UnitProfile reference
                if (!string.IsNullOrEmpty(unresolvedUnitProfileID))
                {
                    var unitProfile = manager.GetUnitProfile(unresolvedUnitProfileID, Nationality);
                    if (unitProfile != null)
                    {
                        UnitProfile = unitProfile;
                        unresolvedUnitProfileID = "";
                    }
                    else
                    {
                        AppService.HandleException(CLASS_NAME, "ResolveReferences",
                            new KeyNotFoundException($"Unit profile {unresolvedUnitProfileID}_{Nationality} not found"));
                    }
                }

                // Resolve Leader reference
                if (!string.IsNullOrEmpty(unresolvedLeaderID))
                {
                    var leader = manager.GetLeader(unresolvedLeaderID);
                    if (leader != null)
                    {
                        CommandingOfficer = leader;
                        unresolvedLeaderID = "";

                        // ADD THIS CONSISTENCY CHECK:
                        // Ensure IsLeaderAssigned is consistent with resolved leader
                        if (!IsLeaderAssigned)
                        {
                            AppService.HandleException(CLASS_NAME, "ResolveReferences",new Exception());
                            IsLeaderAssigned = true; // Fix the inconsistency
                        }
                    }
                    else
                    {
                        AppService.HandleException(CLASS_NAME, "ResolveReferences",
                            new KeyNotFoundException($"Leader {unresolvedLeaderID} not found"));

                        // ADD THIS CLEANUP:
                        // If leader couldn't be resolved, ensure flag is cleared
                        IsLeaderAssigned = false;
                    }
                }
                else if (IsLeaderAssigned)
                {
                    // ADD THIS CONSISTENCY CHECK:
                    // Flag says we have a leader but no leader ID was saved
                    AppService.HandleException(CLASS_NAME, "ResolveReferences", new Exception());
                    IsLeaderAssigned = false; // Fix the inconsistency
                }

                // Re-establish FacilityManager parent relationship after both objects are loaded
                if (unresolvedFacilityManagerData && FacilityManager != null)
                {
                    ReestablishFacilityManagerParent();
                    unresolvedFacilityManagerData = false;
                }

                // Resolve FacilityManager's references
                if (FacilityManager != null && FacilityManager.HasUnresolvedReferences())
                {
                    FacilityManager.ResolveReferences(manager);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ResolveReferences", e);
                throw;
            }
        }

        #endregion // IResolvableReferences
    }
}