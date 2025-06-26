﻿using HammerAndSickle.Services;
using ReactiveUI;
using System;
using System.Runtime.Serialization;

namespace HammerAndSickle.Models
{
    /*───────────────────────────────────────────────────────────────────────────────
    Leader  —  military officer model for unit command and skill progression
    ────────────────────────────────────────────────────────────────────────────────
    Overview
    ════════
    A **Leader** instance represents a military officer who can command units in
    Hammer & Sickle. Leaders provide combat bonuses, skill-based capabilities, and
    progression through reputation earned in combat. Each leader manages their own
    skill tree, unit assignment status, and nationality-specific rank progression.

    Major Responsibilities
    ══════════════════════
    • Officer generation & identification
        - Random name generation based on nationality
        - Unique LeaderID assignment and command ability generation
    • Reputation & progression system
        - Reputation point accumulation through combat actions
        - Command grade advancement (Junior → Senior → Top)
        - Skill tree progression and unlocking
    • Unit assignment management
        - Bidirectional unit-leader relationship tracking
        - Assignment validation and state consistency
    • Skill tree interface
        - Encapsulated skill system without exposing implementation
        - Branch availability, skill unlocking, and bonus calculation
    • Nationality-specific features
        - Localized rank formatting per nation
        - Cultural name generation integration
    • Persistence & cloning
        - Full serialization support for save/load
        - Deep cloning for leader templates

    Design Highlights
    ═════════════════
    • **Event-Driven Architecture**: UI notifications for reputation changes,
      promotions, skill unlocks, and assignment status updates.
    • **Encapsulated Skill System**: Internal LeaderSkillTree management with
      clean public interface - no skill tree implementation details exposed.
    • **Nationality Integration**: Rank formatting and name generation respects
      cultural conventions (USSR, NATO, FRG, FRA, MJ variants).
    • **Robust Validation**: All inputs validated with proper error handling
      and fallback behaviors for edge cases.
    • **Action-Based Reputation**: Contextual reputation awards based on specific
      combat actions with difficulty multipliers.

    Public-Method Reference
    ═══════════════════════
      ── Basic Officer Management ────────────────────────────────────────────────
      SetOfficerCommandAbility(command)     Sets combat command ability directly.
      RandomlyGenerateMe(nationality)       Regenerates name and abilities randomly.
      SetOfficerName(name)                  Updates name with length validation.
      GetFormattedRank()                    Returns nationality-specific rank string.
      SetCommandGrade(grade)                Manually sets command grade level.

      ── Reputation & Progression ────────────────────────────────────────────────
      AwardReputation(amount)               Adds reputation points directly.
      AwardReputationForAction(action, mult) Awards reputation for specific actions.

      ── Skill Tree Interface ────────────────────────────────────────────────────
      CanUnlockSkill(skillEnum)             Checks if skill prerequisites are met.
      UnlockSkill(skillEnum)                Attempts to unlock skill with validation.
      IsSkillUnlocked(skillEnum)            Returns true if skill is already unlocked.
      HasCapability(bonusType)              Checks for specific bonus availability.
      GetBonusValue(bonusType)              Returns cumulative bonus value for type.
      IsBranchAvailable(branch)             Checks if skill branch can be started.
      ResetSkills()                         Resets all skills except leadership.

      ── Unit Assignment Management ──────────────────────────────────────────────
      AssignToUnit(unitID)                  Assigns leader to specified unit.
      UnassignFromUnit()                    Removes leader from current assignment.

      ── Persistence & Cloning ───────────────────────────────────────────────────
      GetObjectData(info, context)         ISerializable save implementation.
      Clone()                               Creates deep copy with new LeaderID.

    Event System
    ════════════
    Leaders broadcast state changes through events for UI integration:

      • **OnReputationChanged(change, newTotal)**: Fired when reputation awarded
      • **OnGradeChanged(newGrade)**: Fired when command grade advances  
      • **OnSkillUnlocked(skillEnum, skillName)**: Fired when new skill acquired
      • **OnUnitAssigned(unitID)**: Fired when assigned to unit
      • **OnUnitUnassigned()**: Fired when removed from unit

    Reputation Action System
    ════════════════════════
    Leaders earn reputation through specific combat actions with context modifiers:

      • **Move Actions**: Base reputation per movement (low value)
      • **Mount/Dismount**: Tactical positioning reputation
      • **Intelligence Gathering**: Reconnaissance and spotting rewards  
      • **Combat Actions**: Primary reputation source from engagement
      • **Airborne Operations**: High-risk jump operation bonuses
      • **Tactical Success**: Forcing enemy retreats and unit destruction

    Context multipliers (0.5x - 2.0x) adjust reputation based on:
      - Enemy unit experience level and strength
      - Tactical difficulty and environmental factors
      - Mission objectives and strategic importance

    Command Ability Generation
    ═══════════════════════════
    New leaders receive randomized command abilities using configurable dice:
      - **Base Roll**: Multiple dice (default 3d6) for ability determination
      - **Modifier**: Constant bonus applied to raw roll
      - **Clamping**: Results bounded to valid CommandAbility enum range
      - **Distribution**: Produces realistic bell curve of officer competence

    Nationality-Specific Ranks
    ═══════════════════════════
    Rank formatting adapts to cultural military traditions:

      • **USSR**: Lieutenant Colonel → Colonel → Major General
      • **NATO (USA/UK/IQ/IR/SAUD)**: Lieutenant Colonel → Colonel → Brigadier General  
      • **FRG**: Oberst → Generalmajor → Generalleutnant
      • **FRA**: Colonel → Général de Brigade → Général de Division
      • **MJ**: Amir al-Fawj → Amir al-Mintaqa → Amir al-Jihad

    Skill Tree Integration
    ══════════════════════
    Leaders internally manage LeaderSkillTree instances but expose only essential
    interface methods. The skill system supports:
      - **Branch Prerequisites**: Leadership skills unlock advanced branches
      - **Reputation Costs**: Skills require accumulated reputation to unlock
      - **Cumulative Bonuses**: Multiple skills stack for enhanced capabilities
      - **Respec Functionality**: Reset all skills except core leadership

    Assignment Consistency
    ══════════════════════
    Unit assignment maintains bidirectional integrity:
      - Leader tracks UnitID and IsAssigned status
      - Events notify systems of assignment changes
      - Validation prevents invalid assignment states
      - Cleanup ensures proper state on unassignment

    ───────────────────────────────────────────────────────────────────────────────
    KEEP THIS COMMENT BLOCK IN SYNC WITH PUBLIC API CHANGES!
    ───────────────────────────────────────────────────────────────────────────── */
    [Serializable]
    public class Leader : ReactiveObject, ISerializable, ICloneable
    {
        #region Constants

        private const string CLASS_NAME = nameof(Leader);

        #endregion // Constants


        #region Fields

        private LeaderSkillTree skillTree;
        private string _name = string.Empty;
        private Side _side = Side.Player;
        private Nationality _nationality = Nationality.USSR;
        private CommandGrade _commandGrade = CommandGrade.JuniorGrade;
        private int _reputationPoints = 0;
        private CommandAbility _combatCommand = CommandAbility.Average;
        private bool _isAssigned = false;
        private string? _unitID = null;

        #endregion // Fields


        #region Properties

        public string LeaderID { get; private set; }                               // Unique identifier for the officer

        public string Name
        {
            get => _name;
            private set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public Side Side
        {
            get => _side;
            private set => this.RaiseAndSetIfChanged(ref _side, value);
        }

        public Nationality Nationality
        {
            get => _nationality;
            private set => this.RaiseAndSetIfChanged(ref _nationality, value);
        }

        public CommandGrade CommandGrade
        {
            get => _commandGrade;
            private set => this.RaiseAndSetIfChanged(ref _commandGrade, value);
        }

        public int ReputationPoints
        {
            get => _reputationPoints;
            private set => this.RaiseAndSetIfChanged(ref _reputationPoints, value);
        }

        public string FormattedRank { get { return GetFormattedRank(); } }   // Real-world rank of the officer

        public CommandAbility CombatCommand
        {
            get => _combatCommand;
            private set => this.RaiseAndSetIfChanged(ref _combatCommand, value);
        }

        public bool IsAssigned
        {
            get => _isAssigned;
            private set => this.RaiseAndSetIfChanged(ref _isAssigned, value);
        }

        public string? UnitID
        {
            get => _unitID;
            private set => this.RaiseAndSetIfChanged(ref _unitID, value);
        }

        #endregion // Properties


        #region Events

        // Events for UI and system notifications
        public event Action<int, int> OnReputationChanged;                    // (changeAmount, newTotal)
        public event Action<CommandGrade> OnGradeChanged;                     // (newGrade)
        public event Action<Enum, string> OnSkillUnlocked;                   // (skillEnum, skillName)
        public event Action<string> OnUnitAssigned;                          // (unitID)
        public event Action OnUnitUnassigned;                                // ()

        #endregion // Events


        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public Leader()
        {
            LeaderID = GenerateUniqueID();
            Nationality = Nationality.USSR; // Default
            Side = Side.Player; // Default side
            Name = "Default Officer"; // Placeholder name
            CommandGrade = CommandGrade.JuniorGrade; // Default command grade
            ReputationPoints = 0; // Start with no reputation
            IsAssigned = false; // Not assigned to any unit
            UnitID = null; // No unit assigned

            InitializeSkillTree();
        }

        /// <summary>
        /// Creates a new leader with random generation based on nationality
        /// </summary>
        /// <param name="side">Player or AI side</param>
        /// <param name="nationality">Nation of origin for name generation and rank formatting</param>
        public Leader(Side side, Nationality nationality)
        {
            try
            {
                InitializeCommonProperties(side, nationality);
                RandomlyGenerateMe(nationality);
                InitializeSkillTree();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "Constructor", e);
                throw;
            }
        }

        /// <summary>
        /// Creates a new leader with specified name and command ability
        /// </summary>
        /// <param name="name">Leader's name</param>
        /// <param name="side">Player or AI side</param>
        /// <param name="nationality">Nation of origin</param>
        /// <param name="command">Command ability level</param>
        public Leader(string name, Side side, Nationality nationality, CommandAbility command)
        {
            try
            {
                InitializeCommonProperties(side, nationality);

                // Validate and set name
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Length < CUConstants.MIN_LEADER_NAME_LENGTH ||
                    name.Length > CUConstants.MAX_LEADER_NAME_LENGTH)
                {
                    throw new ArgumentException($"Leader name must be between {CUConstants.MIN_LEADER_NAME_LENGTH} and {CUConstants.MAX_LEADER_NAME_LENGTH} characters");
                }

                Name = name.Trim();

                // Validate command ability
                if (!Enum.IsDefined(typeof(CommandAbility), command))
                {
                    throw new ArgumentException($"Invalid command ability: {command}");
                }

                CombatCommand = command;
                InitializeSkillTree();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "Constructor", e);
                throw;
            }
        }

        /// <summary>
        /// Deserialization constructor for loading from save data
        /// </summary>
        /// <param name="info">Serialization info containing saved data</param>
        /// <param name="context">Streaming context</param>
        protected Leader(SerializationInfo info, StreamingContext context)
        {
            try
            {
                // Load basic properties
                LeaderID = info.GetString(nameof(LeaderID));
                Name = info.GetString(nameof(Name));
                Side = (Side)info.GetValue(nameof(Side), typeof(Side));
                Nationality = (Nationality)info.GetValue(nameof(Nationality), typeof(Nationality));
                CommandGrade = (CommandGrade)info.GetValue(nameof(CommandGrade), typeof(CommandGrade));
                ReputationPoints = info.GetInt32(nameof(ReputationPoints));
                CombatCommand = (CommandAbility)info.GetValue(nameof(CombatCommand), typeof(CommandAbility));
                IsAssigned = info.GetBoolean(nameof(IsAssigned));

                // Handle optional UnitID (might be null)
                try
                {
                    UnitID = info.GetString(nameof(UnitID));
                }
                catch (SerializationException)
                {
                    UnitID = null; // Not assigned to any unit
                }

                // Load skill tree data
                var skillTreeData = (LeaderSkillTreeData)info.GetValue("SkillTreeData", typeof(LeaderSkillTreeData));
                skillTree = new LeaderSkillTree();
                skillTree.FromSerializableData(skillTreeData);

                // Wire up skill tree events to our events
                WireSkillTreeEvents();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "DeserializationConstructor", e);
                throw;
            }
        }

        #endregion // Constructors


        #region Initialization Helpers

        /// <summary>
        /// Initialize common properties shared by all constructors
        /// </summary>
        private void InitializeCommonProperties(Side side, Nationality nationality)
        {
            LeaderID = GenerateUniqueID();
            Side = side;
            Nationality = nationality;
            CommandGrade = CommandGrade.JuniorGrade;
            ReputationPoints = 0;
            IsAssigned = false;
            UnitID = null;
        }

        /// <summary>
        /// Initialize the skill tree and wire up events
        /// </summary>
        private void InitializeSkillTree()
        {
            skillTree = new LeaderSkillTree(ReputationPoints);
            WireSkillTreeEvents();
        }

        /// <summary>
        /// Wire skill tree events to our public events
        /// </summary>
        private void WireSkillTreeEvents()
        {
            skillTree.OnGradeChanged += (grade) =>
            {
                CommandGrade = grade;
                OnGradeChanged?.Invoke(grade);
            };

            skillTree.OnReputationChanged += (change, newTotal) =>
            {
                ReputationPoints = newTotal;
                OnReputationChanged?.Invoke(change, newTotal);
            };

            skillTree.OnSkillUnlocked += (skillEnum, skillName, description) =>
            {
                OnSkillUnlocked?.Invoke(skillEnum, skillName);
            };
        }

        /// <summary>
        /// Generate a unique LeaderID for the leader
        /// </summary>
        private string GenerateUniqueID()
        {
            string baseID = CUConstants.LEADER_ID_PREFIX;
            string randomPart = Guid.NewGuid().ToString("N")[..5].ToUpper();
            return $"{baseID}{randomPart}";
        }

        #endregion // Initialization Helpers


        #region Public Methods

        /// <summary>
        /// Manually set the officer's command ability (for testing or special cases)
        /// </summary>
        /// <param name="command">New command ability level</param>
        public void SetOfficerCommandAbility(CommandAbility command)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CommandAbility), command))
                {
                    throw new ArgumentException($"Invalid command ability: {command}");
                }

                CombatCommand = command;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetOfficerCommandAbility", e);
                throw;
            }
        }

        /// <summary>
        /// Randomly generate leader properties based on nationality
        /// </summary>
        /// <param name="nationality">Nation to base generation on</param>
        public void RandomlyGenerateMe(Nationality nationality)
        {
            try
            {
                var random = new System.Random();

                // Check if NameGenService is available
                if (NameGenService.Instance == null)
                {
                    throw new InvalidOperationException("NameGenService is not available");
                }

                // Update the officer's nationality
                Nationality = nationality;

                // Generate a random name based on nationality
                var generatedName = NameGenService.Instance.GenerateMaleName(nationality);

                // Ensure name is valid
                if (string.IsNullOrEmpty(generatedName))
                {
                    generatedName = $"Officer-{Guid.NewGuid().ToString()[..8]}";
                }

                Name = generatedName;

                // Generate command ability using constants from CUConstants
                int commandValue = 0;
                for (int i = 0; i < CUConstants.COMMAND_DICE_COUNT; i++)
                {
                    commandValue += random.Next(1, CUConstants.COMMAND_DICE_SIDES + 1);
                }
                commandValue += CUConstants.COMMAND_DICE_MODIFIER;

                // Clamp to valid range
                commandValue = Math.Clamp(commandValue, CUConstants.COMMAND_CLAMP_MIN, CUConstants.COMMAND_CLAMP_MAX);

                CombatCommand = (CommandAbility)commandValue;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "RandomlyGenerateMe", e);
                throw;
            }
        }

        /// <summary>
        /// Set the officer's name with validation
        /// </summary>
        /// <param name="name">New name for the officer</param>
        /// <returns>True if name was successfully set</returns>
        public bool SetOfficerName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Length < CUConstants.MIN_LEADER_NAME_LENGTH ||
                    name.Length > CUConstants.MAX_LEADER_NAME_LENGTH)
                {
                    return false;
                }

                Name = name.Trim();
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetOfficerName", e);
                return false;
            }
        }

        /// <summary>
        /// Get formatted rank based on nationality and command grade
        /// </summary>
        /// <returns>Localized rank string</returns>
        public string GetFormattedRank()
        {
            try
            {
                return Nationality switch
                {
                    Nationality.USSR => CommandGrade switch
                    {
                        CommandGrade.JuniorGrade => "Lieutenant Colonel",
                        CommandGrade.SeniorGrade => "Colonel",
                        CommandGrade.TopGrade => "Major General",
                        _ => "Officer",
                    },
                    Nationality.USA or Nationality.UK or Nationality.IQ or Nationality.IR or Nationality.SAUD => CommandGrade switch
                    {
                        CommandGrade.JuniorGrade => "Lieutenant Colonel",
                        CommandGrade.SeniorGrade => "Colonel",
                        CommandGrade.TopGrade => "Brigadier General",
                        _ => "Officer",
                    },
                    Nationality.FRG => CommandGrade switch
                    {
                        CommandGrade.JuniorGrade => "Oberst",
                        CommandGrade.SeniorGrade => "Generalmajor",
                        CommandGrade.TopGrade => "Generalleutnant",
                        _ => "Offizier",
                    },
                    Nationality.FRA => CommandGrade switch
                    {
                        CommandGrade.JuniorGrade => "Colonel",
                        CommandGrade.SeniorGrade => "Général de Brigade",
                        CommandGrade.TopGrade => "Général de Division",
                        _ => "Officier",
                    },
                    Nationality.MJ => CommandGrade switch
                    {
                        CommandGrade.JuniorGrade => "Amir al-Fawj",
                        CommandGrade.SeniorGrade => "Amir al-Mintaqa",
                        CommandGrade.TopGrade => "Amir al-Jihad",
                        _ => "Commander",
                    },
                    _ => CommandGrade.ToString(),
                };
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetFormattedRank", e);
                return "Officer";
            }
        }

        /// <summary>
        /// Set the command grade of the officer.
        /// </summary>
        /// <param name="grade"></param>
        public void SetCommandGrade(CommandGrade grade)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CommandGrade), grade))
                {
                    throw new ArgumentException($"Invalid command grade: {grade}");
                }
                CommandGrade = grade;
                OnGradeChanged?.Invoke(grade);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetCommandGrade", e);
                throw;
            }
        }

        /// <summary>
        /// Sets the side of the object to the specified value.
        /// </summary>
        /// <param name="side">The side to set. Must be a valid value of the <see cref="Side"/> enumeration.</param>
        public void SetSide(Side side)
        {
            try
            {
                if (!Enum.IsDefined(typeof(Side), side))
                {
                    throw new ArgumentException($"Invalid side: {side}");
                }
                Side = side;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetSide", e);
                throw;
            }
        }

        /// <summary>
        /// Set the nationality of the officer.
        /// </summary>
        /// <param name="nationality"></param>
        public void SetNationality(Nationality nationality)
        {
            try
            {
                if (!Enum.IsDefined(typeof(Nationality), nationality))
                {
                    throw new ArgumentException($"Invalid nationality: {nationality}");
                }
                Nationality = nationality;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "SetNationality", e);
                throw;
            }
        }

        #endregion // Public Methods


        #region Reputation Management

        /// <summary>
        /// Award reputation points to the leader
        /// </summary>
        /// <param name="amount">Amount of reputation to award</param>
        public void AwardReputation(int amount)
        {
            try
            {
                if (amount <= 0) return;

                ReputationPoints += amount;
                skillTree.AddReputation(amount);

                OnReputationChanged?.Invoke(amount, ReputationPoints);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AwardReputation", e);
                throw;
            }
        }

        /// <summary>
        /// Award reputation based on specific action type with context modifiers
        /// </summary>
        /// <param name="actionType">Type of action performed</param>
        /// <param name="contextMultiplier">Additional multiplier based on context (e.g., difficulty, unit experience)</param>
        public void AwardReputationForAction(CUConstants.ReputationAction actionType, float contextMultiplier = 1.0f)
        {
            try
            {
                int baseREP = actionType switch
                {
                    CUConstants.ReputationAction.Move => CUConstants.REP_PER_MOVE_ACTION,
                    CUConstants.ReputationAction.MountDismount => CUConstants.REP_PER_MOUNT_DISMOUNT,
                    CUConstants.ReputationAction.IntelGather => CUConstants.REP_PER_INTEL_GATHER,
                    CUConstants.ReputationAction.Combat => CUConstants.REP_PER_COMBAT_ACTION,
                    CUConstants.ReputationAction.AirborneJump => CUConstants.REP_PER_AIRBORNE_JUMP,
                    CUConstants.ReputationAction.ForcedRetreat => CUConstants.REP_PER_FORCED_RETREAT,
                    CUConstants.ReputationAction.UnitDestroyed => CUConstants.REP_PER_UNIT_DESTROYED,
                    _ => 0
                };

                // Validate multiplier bounds
                contextMultiplier = Math.Clamp(contextMultiplier, CUConstants.MIN_REP_MULTIPLIER, CUConstants.MAX_REP_MULTIPLIER);

                int finalREP = (int)MathF.Round(baseREP * contextMultiplier, MidpointRounding.AwayFromZero);

                if (finalREP > 0)
                {
                    AwardReputation(finalREP);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AwardReputationForAction", e);
                throw;
            }
        }

        #endregion // Reputation Management


        #region Skill Tree Interface

        /// <summary>
        /// Check if a skill can be unlocked
        /// </summary>
        /// <param name="skillEnum">Skill to check</param>
        /// <returns>True if skill can be unlocked</returns>
        public bool CanUnlockSkill(Enum skillEnum)
        {
            try
            {
                return skillTree?.CanUnlockSkill(skillEnum) ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "CanUnlockSkill", e);
                return false;
            }
        }

        /// <summary>
        /// Attempt to unlock a skill
        /// </summary>
        /// <param name="skillEnum">Skill to unlock</param>
        /// <returns>True if skill was successfully unlocked</returns>
        public bool UnlockSkill(Enum skillEnum)
        {
            try
            {
                return skillTree?.UnlockSkill(skillEnum) ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "UnlockSkill", e);
                return false;
            }
        }

        /// <summary>
        /// Check if a specific skill is unlocked
        /// </summary>
        /// <param name="skillEnum">Skill to check</param>
        /// <returns>True if skill is unlocked</returns>
        public bool IsSkillUnlocked(Enum skillEnum)
        {
            try
            {
                return skillTree?.IsSkillUnlocked(skillEnum) ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "IsSkillUnlocked", e);
                return false;
            }
        }

        /// <summary>
        /// Check if leader has a specific capability
        /// </summary>
        /// <param name="bonusType">Capability to check for</param>
        /// <returns>True if leader has this capability</returns>
        public bool HasCapability(SkillBonusType bonusType)
        {
            try
            {
                return skillTree?.HasCapability(bonusType) ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "HasCapability", e);
                return false;
            }
        }

        /// <summary>
        /// Get the total bonus value for a specific bonus type
        /// </summary>
        /// <param name="bonusType">Type of bonus to calculate</param>
        /// <returns>Total bonus value</returns>
        public float GetBonusValue(SkillBonusType bonusType)
        {
            try
            {
                return skillTree?.GetBonusValue(bonusType) ?? 0f;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetBonusValue", e);
                return 0f;
            }
        }

        /// <summary>
        /// Check if a skill branch is available to start
        /// </summary>
        /// <param name="branch">Branch to check</param>
        /// <returns>True if branch can be started</returns>
        public bool IsBranchAvailable(SkillBranch branch)
        {
            try
            {
                return skillTree?.IsBranchAvailable(branch) ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "IsBranchAvailable", e);
                return false;
            }
        }

        /// <summary>
        /// Reset all skills except leadership (respec functionality)
        /// </summary>
        /// <returns>True if any skills were reset</returns>
        public bool ResetSkills()
        {
            try
            {
                return skillTree?.ResetAllSkillsExceptLeadership() ?? false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "ResetSkills", e);
                return false;
            }
        }

        #endregion // Skill Tree Interface


        #region Unit Assignment

        /// <summary>
        /// Assign this leader to a unit
        /// </summary>
        /// <param name="unitID">LeaderID of the unit to assign to</param>
        public void AssignToUnit(string unitID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(unitID))
                {
                    throw new ArgumentException("Unit ID cannot be null or empty");
                }

                UnitID = unitID;
                IsAssigned = true;
                OnUnitAssigned?.Invoke(unitID);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "AssignToUnit", e);
                throw;
            }
        }

        /// <summary>
        /// Unassign this leader from their current unit
        /// </summary>
        public void UnassignFromUnit()
        {
            try
            {
                UnitID = null;
                IsAssigned = false;
                OnUnitUnassigned?.Invoke();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "UnassignFromUnit", e);
                throw;
            }
        }

        #endregion // Unit Assignment


        #region ISerializable Implementation

        /// <summary>
        /// Serialize the leader for save data
        /// </summary>
        /// <param name="info">Serialization info to populate</param>
        /// <param name="context">Streaming context</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            try
            {
                // Save basic properties
                info.AddValue(nameof(LeaderID), LeaderID);
                info.AddValue(nameof(Name), Name);
                info.AddValue(nameof(Side), Side);
                info.AddValue(nameof(Nationality), Nationality);
                info.AddValue(nameof(CommandGrade), CommandGrade);
                info.AddValue(nameof(ReputationPoints), ReputationPoints);
                info.AddValue(nameof(CombatCommand), CombatCommand);
                info.AddValue(nameof(IsAssigned), IsAssigned);
                info.AddValue(nameof(UnitID), UnitID);

                // Save skill tree data
                var skillTreeData = skillTree?.ToSerializableData();
                info.AddValue("SkillTreeData", skillTreeData);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "GetObjectData", e);
                throw;
            }
        }

        #endregion // ISerializable Implementation


        #region ICloneable Implementation

        /// <summary>
        /// Create a deep copy of this leader
        /// </summary>
        /// <returns>Cloned leader instance</returns>
        public object Clone()
        {
            try
            {
                // Create new leader with same basic properties
                var clone = new Leader(Name, Side, Nationality, CombatCommand);

                // Copy additional state
                clone.ReputationPoints = this.ReputationPoints;
                clone.CommandGrade = this.CommandGrade;

                // Copy assignment state
                if (IsAssigned)
                {
                    clone.AssignToUnit(UnitID);
                }

                // Copy skill tree state
                if (skillTree != null)
                {
                    var skillTreeData = skillTree.ToSerializableData();
                    clone.skillTree.FromSerializableData(skillTreeData);
                }

                return clone;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, "Clone", e);
                throw;
            }
        }

        #endregion // ICloneable Implementation
    }
}