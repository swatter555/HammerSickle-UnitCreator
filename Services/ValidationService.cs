using System;
using System.Collections.Generic;
using System.Linq;
using HammerAndSickle.Models;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// ValidationResult encapsulates the outcome of validation operations with detailed error and warning reporting.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
                Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
                Warnings.Add(warning);
        }

        public void AddErrors(IEnumerable<string> errors)
        {
            foreach (var error in errors.Where(e => !string.IsNullOrWhiteSpace(e)))
                Errors.Add(error);
        }

        public void AddWarnings(IEnumerable<string> warnings)
        {
            foreach (var warning in warnings.Where(w => !string.IsNullOrWhiteSpace(w)))
                Warnings.Add(warning);
        }

        public void Merge(ValidationResult other)
        {
            if (other != null)
            {
                AddErrors(other.Errors);
                AddWarnings(other.Warnings);
            }
        }
    }

    /// <summary>
    /// ValidationService provides comprehensive data integrity validation for all game objects
    /// and their relationships. Performs individual object validation, cross-reference checking,
    /// dependency validation, and export readiness verification.
    /// 
    /// Key responsibilities:
    /// - Individual object field validation with range checking
    /// - Cross-reference validation between related objects
    /// - Dependency validation for deletion operations
    /// - Complete data integrity verification for export operations
    /// - Detailed error and warning reporting for UI feedback
    /// </summary>
    public class ValidationService
    {
        private const string CLASS_NAME = nameof(ValidationService);
        private readonly DataService _dataService;

        // Validation constants from CUConstants
        private const int MIN_COMBAT_VALUE = 0;
        private const int MAX_COMBAT_VALUE = 25;
        private const float MIN_RANGE = 0f;
        private const float MAX_RANGE = 100f;
        private const float MIN_MOVEMENT_MODIFIER = 0.1f;
        private const float MAX_MOVEMENT_MODIFIER = 10f;
        private const int MIN_LEADER_NAME_LENGTH = 2;
        private const int MAX_LEADER_NAME_LENGTH = 50;

        public ValidationService(DataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        #region Leader Validation

        /// <summary>
        /// Validates a Leader object for completeness and consistency
        /// </summary>
        public ValidationResult ValidateLeader(Leader leader)
        {
            var result = new ValidationResult();

            if (leader == null)
            {
                result.AddError("Leader cannot be null");
                return result;
            }

            try
            {
                // Basic field validation
                ValidateLeaderBasicFields(leader, result);

                // Cross-reference validation
                ValidateLeaderReferences(leader, result);

                // Business logic validation
                ValidateLeaderBusinessRules(leader, result);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateLeader), e);
                result.AddError($"Validation failed with exception: {e.Message}");
            }

            return result;
        }

        private void ValidateLeaderBasicFields(Leader leader, ValidationResult result)
        {
            // Name validation
            if (string.IsNullOrWhiteSpace(leader.Name))
            {
                result.AddError("Leader name is required");
            }
            else if (leader.Name.Length < MIN_LEADER_NAME_LENGTH)
            {
                result.AddError($"Leader name must be at least {MIN_LEADER_NAME_LENGTH} characters");
            }
            else if (leader.Name.Length > MAX_LEADER_NAME_LENGTH)
            {
                result.AddError($"Leader name cannot exceed {MAX_LEADER_NAME_LENGTH} characters");
            }

            // LeaderID validation
            if (string.IsNullOrWhiteSpace(leader.LeaderID))
            {
                result.AddError("Leader ID is required");
            }

            // Enum validation
            if (!Enum.IsDefined(typeof(Side), leader.Side))
            {
                result.AddError($"Invalid side: {leader.Side}");
            }

            if (!Enum.IsDefined(typeof(Nationality), leader.Nationality))
            {
                result.AddError($"Invalid nationality: {leader.Nationality}");
            }

            if (!Enum.IsDefined(typeof(CommandGrade), leader.CommandGrade))
            {
                result.AddError($"Invalid command grade: {leader.CommandGrade}");
            }

            if (!Enum.IsDefined(typeof(CommandAbility), leader.CombatCommand))
            {
                result.AddError($"Invalid combat command ability: {leader.CombatCommand}");
            }

            // Reputation validation
            if (leader.ReputationPoints < 0)
            {
                result.AddError("Reputation points cannot be negative");
            }
            else if (leader.ReputationPoints > 10000) // Reasonable upper bound
            {
                result.AddWarning("Reputation points are unusually high (>10000)");
            }
        }

        private void ValidateLeaderReferences(Leader leader, ValidationResult result)
        {
            // Check for duplicate IDs
            var duplicateLeader = _dataService.Leaders.FirstOrDefault(l =>
                l.LeaderID == leader.LeaderID && l != leader);
            if (duplicateLeader != null)
            {
                result.AddError($"Leader ID '{leader.LeaderID}' is already in use by another leader");
            }

            // Check for duplicate names (warning only)
            var duplicateName = _dataService.Leaders.FirstOrDefault(l =>
                l.Name == leader.Name && l != leader);
            if (duplicateName != null)
            {
                result.AddWarning($"Leader name '{leader.Name}' is already in use by another leader");
            }
        }

        private void ValidateLeaderBusinessRules(Leader leader, ValidationResult result)
        {
            // Assignment consistency
            if (leader.IsAssigned && string.IsNullOrWhiteSpace(leader.UnitID))
            {
                result.AddError("Leader marked as assigned but has no unit ID");
            }
            else if (!leader.IsAssigned && !string.IsNullOrWhiteSpace(leader.UnitID))
            {
                result.AddError("Leader has unit ID but is not marked as assigned");
            }

            // Verify assignment consistency with actual units
            if (leader.IsAssigned && !string.IsNullOrWhiteSpace(leader.UnitID))
            {
                var assignedUnit = _dataService.CombatUnits.FirstOrDefault(u => u.UnitID == leader.UnitID);
                if (assignedUnit == null)
                {
                    result.AddError($"Leader assigned to unit '{leader.UnitID}' but unit does not exist");
                }
                else if (assignedUnit.CommandingOfficer?.LeaderID != leader.LeaderID)
                {
                    result.AddError($"Leader assignment to unit '{leader.UnitID}' is not bidirectional");
                }
            }
        }

        #endregion

        #region WeaponSystemProfile Validation

        /// <summary>
        /// Validates a WeaponSystemProfile for combat value ranges and consistency
        /// </summary>
        public ValidationResult ValidateWeaponProfile(WeaponSystemProfile profile)
        {
            var result = new ValidationResult();

            if (profile == null)
            {
                result.AddError("Weapon system profile cannot be null");
                return result;
            }

            try
            {
                ValidateWeaponProfileBasicFields(profile, result);
                ValidateWeaponProfileCombatValues(profile, result);
                ValidateWeaponProfileReferences(profile, result);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateWeaponProfile), e);
                result.AddError($"Validation failed with exception: {e.Message}");
            }

            return result;
        }

        private void ValidateWeaponProfileBasicFields(WeaponSystemProfile profile, ValidationResult result)
        {
            // Name validation
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                result.AddError("Weapon system profile name is required");
            }

            // WeaponSystemID validation
            if (string.IsNullOrWhiteSpace(profile.WeaponSystemID))
            {
                result.AddError("Weapon system ID is required");
            }

            // Enum validation
            if (!Enum.IsDefined(typeof(Nationality), profile.Nationality))
            {
                result.AddError($"Invalid nationality: {profile.Nationality}");
            }

            if (!Enum.IsDefined(typeof(WeaponSystems), profile.WeaponSystem))
            {
                result.AddError($"Invalid weapon system: {profile.WeaponSystem}");
            }

            // Range validation
            ValidateRange(profile.PrimaryRange, "Primary Range", result);
            ValidateRange(profile.IndirectRange, "Indirect Range", result);
            ValidateRange(profile.SpottingRange, "Spotting Range", result);

            // Movement modifier validation
            if (profile.MovementModifier < MIN_MOVEMENT_MODIFIER || profile.MovementModifier > MAX_MOVEMENT_MODIFIER)
            {
                result.AddError($"Movement modifier must be between {MIN_MOVEMENT_MODIFIER} and {MAX_MOVEMENT_MODIFIER}");
            }
        }

        private void ValidateWeaponProfileCombatValues(WeaponSystemProfile profile, ValidationResult result)
        {
            // Combat rating validation using CombatRating properties
            ValidateCombatRating(profile.GetLandHardAttack(), profile.GetLandHardDefense(), "Land Hard", result);
            ValidateCombatRating(profile.GetLandSoftAttack(), profile.GetLandSoftDefense(), "Land Soft", result);
            ValidateCombatRating(profile.GetLandAirAttack(), profile.GetLandAirDefense(), "Land Air", result);
            ValidateCombatRating(profile.GetAirAttack(), profile.GetAirDefense(), "Air", result);
            ValidateCombatRating(profile.GetAirGroundAttack(), profile.GetAirGroundDefense(), "Air Ground", result);

            // Single combat values
            ValidateCombatValue(profile.AirAvionics, "Air Avionics", result);
            ValidateCombatValue(profile.AirStrategicAttack, "Air Strategic Attack", result);

            // Warn about completely inactive profiles
            var totalCombatValue = profile.GetTotalCombatValue();
            if (totalCombatValue == 0)
            {
                result.AddWarning("Weapon system profile has no combat capability (all values are 0)");
            }
        }

        private void ValidateWeaponProfileReferences(WeaponSystemProfile profile, ValidationResult result)
        {
            // Check for duplicate weapon system IDs
            var duplicate = _dataService.WeaponProfiles.FirstOrDefault(w =>
                w.WeaponSystemID == profile.WeaponSystemID && w != profile);
            if (duplicate != null)
            {
                result.AddError($"Weapon system ID '{profile.WeaponSystemID}' is already in use");
            }

            // Validate enum consistency
            if (!profile.WeaponSystemID.Contains(profile.WeaponSystem.ToString()))
            {
                result.AddWarning($"Weapon system ID '{profile.WeaponSystemID}' doesn't match weapon system '{profile.WeaponSystem}'");
            }
        }

        private void ValidateCombatRating(int attack, int defense, string category, ValidationResult result)
        {
            ValidateCombatValue(attack, $"{category} Attack", result);
            ValidateCombatValue(defense, $"{category} Defense", result);
        }

        private void ValidateCombatValue(int value, string fieldName, ValidationResult result)
        {
            if (value < MIN_COMBAT_VALUE || value > MAX_COMBAT_VALUE)
            {
                result.AddError($"{fieldName} must be between {MIN_COMBAT_VALUE} and {MAX_COMBAT_VALUE}");
            }
        }

        private void ValidateRange(float value, string fieldName, ValidationResult result)
        {
            if (value < MIN_RANGE || value > MAX_RANGE)
            {
                result.AddError($"{fieldName} must be between {MIN_RANGE} and {MAX_RANGE}");
            }
        }

        #endregion

        #region UnitProfile Validation

        /// <summary>
        /// Validates a UnitProfile for equipment consistency and completeness
        /// </summary>
        public ValidationResult ValidateUnitProfile(UnitProfile profile)
        {
            var result = new ValidationResult();

            if (profile == null)
            {
                result.AddError("Unit profile cannot be null");
                return result;
            }

            try
            {
                ValidateUnitProfileBasicFields(profile, result);
                ValidateUnitProfileEquipment(profile, result);
                ValidateUnitProfileReferences(profile, result);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateUnitProfile), e);
                result.AddError($"Validation failed with exception: {e.Message}");
            }

            return result;
        }

        private void ValidateUnitProfileBasicFields(UnitProfile profile, ValidationResult result)
        {
            // Profile ID validation
            if (string.IsNullOrWhiteSpace(profile.UnitProfileID))
            {
                result.AddError("Unit profile ID is required");
            }

            // Nationality validation
            if (!Enum.IsDefined(typeof(Nationality), profile.Nationality))
            {
                result.AddError($"Invalid nationality: {profile.Nationality}");
            }
        }

        private void ValidateUnitProfileEquipment(UnitProfile profile, ValidationResult result)
        {
            var weaponSystems = profile.GetWeaponSystems().ToList();

            if (!weaponSystems.Any())
            {
                result.AddWarning("Unit profile has no equipment defined");
                return;
            }

            // Validate equipment quantities
            foreach (var weaponSystem in weaponSystems)
            {
                var count = profile.GetWeaponSystemMaxValue(weaponSystem);
                if (count <= 0)
                {
                    result.AddError($"Equipment '{weaponSystem}' has invalid quantity: {count}");
                }
                else if (count > 10000) // Reasonable upper bound
                {
                    result.AddWarning($"Equipment '{weaponSystem}' has unusually high quantity: {count}");
                }
            }

            // Check for logical equipment combinations
            ValidateEquipmentLogic(weaponSystems, result);
        }

        private void ValidateEquipmentLogic(List<WeaponSystems> weaponSystems, ValidationResult result)
        {
            // Check for conflicting equipment types (basic validation)
            var hasPersonnel = weaponSystems.Any(w => w.ToString().Contains("INF"));
            var hasVehicles = weaponSystems.Any(w => w.ToString().StartsWith("TANK") ||
                                                    w.ToString().StartsWith("IFV") ||
                                                    w.ToString().StartsWith("APC"));
            var hasAircraft = weaponSystems.Any(w => w.ToString().StartsWith("ASF") ||
                                                     w.ToString().StartsWith("MRF") ||
                                                     w.ToString().StartsWith("ATT") ||
                                                     w.ToString().StartsWith("HEL"));

            // Warn about mixed unit types that might be unusual
            if (hasPersonnel && hasAircraft)
            {
                result.AddWarning("Unit profile contains both personnel and aircraft - verify this is intended");
            }

            if (hasVehicles && hasAircraft)
            {
                result.AddWarning("Unit profile contains both ground vehicles and aircraft - verify this is intended");
            }
        }

        private void ValidateUnitProfileReferences(UnitProfile profile, ValidationResult result)
        {
            // Check for duplicate profile IDs
            var duplicate = _dataService.UnitProfiles.FirstOrDefault(u =>
                u.UnitProfileID == profile.UnitProfileID && u != profile);
            if (duplicate != null)
            {
                result.AddError($"Unit profile ID '{profile.UnitProfileID}' is already in use");
            }
        }

        #endregion

        #region CombatUnit Validation

        /// <summary>
        /// Validates a CombatUnit for profile assignments and configuration consistency
        /// </summary>
        public ValidationResult ValidateCombatUnit(CombatUnit unit)
        {
            var result = new ValidationResult();

            if (unit == null)
            {
                result.AddError("Combat unit cannot be null");
                return result;
            }

            try
            {
                ValidateCombatUnitBasicFields(unit, result);
                ValidateCombatUnitProfiles(unit, result);
                ValidateCombatUnitLeaderAssignment(unit, result);
                ValidateCombatUnitState(unit, result);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateCombatUnit), e);
                result.AddError($"Validation failed with exception: {e.Message}");
            }

            return result;
        }

        private void ValidateCombatUnitBasicFields(CombatUnit unit, ValidationResult result)
        {
            // Unit name validation
            if (string.IsNullOrWhiteSpace(unit.UnitName))
            {
                result.AddError("Unit name is required");
            }

            // Unit ID validation
            if (string.IsNullOrWhiteSpace(unit.UnitID))
            {
                result.AddError("Unit ID is required");
            }

            // Enum validation
            if (!Enum.IsDefined(typeof(UnitType), unit.UnitType))
            {
                result.AddError($"Invalid unit type: {unit.UnitType}");
            }

            if (!Enum.IsDefined(typeof(UnitClassification), unit.Classification))
            {
                result.AddError($"Invalid classification: {unit.Classification}");
            }

            if (!Enum.IsDefined(typeof(UnitRole), unit.Role))
            {
                result.AddError($"Invalid role: {unit.Role}");
            }

            if (!Enum.IsDefined(typeof(Side), unit.Side))
            {
                result.AddError($"Invalid side: {unit.Side}");
            }

            if (!Enum.IsDefined(typeof(Nationality), unit.Nationality))
            {
                result.AddError($"Invalid nationality: {unit.Nationality}");
            }
        }

        private void ValidateCombatUnitProfiles(CombatUnit unit, ValidationResult result)
        {
            // Deployed profile is required
            if (unit.DeployedProfile == null)
            {
                result.AddError("Deployed profile is required for all combat units");
            }

            // Unit profile is required
            if (unit.UnitProfile == null)
            {
                result.AddError("Unit profile is required for all combat units");
            }

            // Check nationality consistency
            if (unit.DeployedProfile != null && unit.DeployedProfile.Nationality != unit.Nationality)
            {
                result.AddError("Deployed profile nationality must match unit nationality");
            }

            if (unit.MountedProfile != null && unit.MountedProfile.Nationality != unit.Nationality)
            {
                result.AddError("Mounted profile nationality must match unit nationality");
            }

            if (unit.UnitProfile != null && unit.UnitProfile.Nationality != unit.Nationality)
            {
                result.AddError("Unit profile nationality must match unit nationality");
            }

            // Mounted profile validation
            if (unit.IsMounted && unit.MountedProfile == null)
            {
                result.AddWarning("Unit is mounted but has no mounted profile");
            }
        }

        private void ValidateCombatUnitLeaderAssignment(CombatUnit unit, ValidationResult result)
        {
            // Leader assignment consistency
            if (unit.IsLeaderAssigned && unit.CommandingOfficer == null)
            {
                result.AddError("Unit marked as having leader assigned but CommandingOfficer is null");
            }
            else if (!unit.IsLeaderAssigned && unit.CommandingOfficer != null)
            {
                result.AddError("Unit has CommandingOfficer but IsLeaderAssigned is false");
            }

            // Bidirectional leader assignment validation
            if (unit.CommandingOfficer != null)
            {
                if (!unit.CommandingOfficer.IsAssigned)
                {
                    result.AddError("Unit's commanding officer is not marked as assigned");
                }
                else if (unit.CommandingOfficer.UnitID != unit.UnitID)
                {
                    result.AddError("Unit's commanding officer is assigned to a different unit");
                }
            }
        }

        private void ValidateCombatUnitState(CombatUnit unit, ValidationResult result)
        {
            // State validation
            if (!Enum.IsDefined(typeof(ExperienceLevel), unit.ExperienceLevel))
            {
                result.AddError($"Invalid experience level: {unit.ExperienceLevel}");
            }

            if (!Enum.IsDefined(typeof(EfficiencyLevel), unit.EfficiencyLevel))
            {
                result.AddError($"Invalid efficiency level: {unit.EfficiencyLevel}");
            }

            if (!Enum.IsDefined(typeof(CombatState), unit.CombatState))
            {
                result.AddError($"Invalid combat state: {unit.CombatState}");
            }

            // Hit points validation
            if (unit.HitPoints.Max <= 0)
            {
                result.AddError("Maximum hit points must be greater than 0");
            }

            if (unit.HitPoints.Current < 0)
            {
                result.AddError("Current hit points cannot be negative");
            }

            if (unit.HitPoints.Current > unit.HitPoints.Max)
            {
                result.AddError("Current hit points cannot exceed maximum");
            }

            // Supply validation
            if (unit.DaysSupply.Max <= 0)
            {
                result.AddWarning("Unit has no supply capacity");
            }

            if (unit.DaysSupply.Current < 0)
            {
                result.AddError("Current supply cannot be negative");
            }
        }

        #endregion

        #region Comprehensive Validation

        /// <summary>
        /// Validates all data in the system for overall consistency and export readiness
        /// </summary>
        public ValidationResult ValidateAllData()
        {
            var result = new ValidationResult();

            try
            {
                AppService.CaptureUiMessage("Starting comprehensive data validation");

                // Validate all individual objects
                ValidateAllLeaders(result);
                ValidateAllWeaponProfiles(result);
                ValidateAllUnitProfiles(result);
                ValidateAllCombatUnits(result);

                // Cross-reference validation
                ValidateDataConsistency(result);

                // Export readiness validation
                ValidateExportReadiness(result);

                AppService.CaptureUiMessage($"Data validation complete: {result.Errors.Count} errors, {result.Warnings.Count} warnings");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateAllData), e);
                result.AddError($"Comprehensive validation failed: {e.Message}");
            }

            return result;
        }

        private void ValidateAllLeaders(ValidationResult result)
        {
            foreach (var leader in _dataService.Leaders)
            {
                var leaderResult = ValidateLeader(leader);
                result.Merge(leaderResult);
            }
        }

        private void ValidateAllWeaponProfiles(ValidationResult result)
        {
            foreach (var profile in _dataService.WeaponProfiles)
            {
                var profileResult = ValidateWeaponProfile(profile);
                result.Merge(profileResult);
            }
        }

        private void ValidateAllUnitProfiles(ValidationResult result)
        {
            foreach (var profile in _dataService.UnitProfiles)
            {
                var profileResult = ValidateUnitProfile(profile);
                result.Merge(profileResult);
            }
        }

        private void ValidateAllCombatUnits(ValidationResult result)
        {
            foreach (var unit in _dataService.CombatUnits)
            {
                var unitResult = ValidateCombatUnit(unit);
                result.Merge(unitResult);
            }
        }

        private void ValidateDataConsistency(ValidationResult result)
        {
            // Check for orphaned references
            ValidateOrphanedLeaderAssignments(result);
            ValidateOrphanedProfileReferences(result);

            // Check for missing critical data
            ValidateCriticalDataPresence(result);
        }

        private void ValidateOrphanedLeaderAssignments(ValidationResult result)
        {
            // Leaders assigned to non-existent units
            foreach (var leader in _dataService.Leaders.Where(l => l.IsAssigned))
            {
                if (string.IsNullOrEmpty(leader.UnitID) ||
                    !_dataService.CombatUnits.Any(u => u.UnitID == leader.UnitID))
                {
                    result.AddError($"Leader '{leader.Name}' is assigned to non-existent unit '{leader.UnitID}'");
                }
            }

            // Units with leaders that don't exist in leader collection
            foreach (var unit in _dataService.CombatUnits.Where(u => u.CommandingOfficer != null))
            {
                if (!_dataService.Leaders.Any(l => l.LeaderID == unit.CommandingOfficer.LeaderID))
                {
                    result.AddError($"Unit '{unit.UnitName}' has commander '{unit.CommandingOfficer.LeaderID}' that doesn't exist in leaders collection");
                }
            }
        }

        private void ValidateOrphanedProfileReferences(ValidationResult result)
        {
            // Units with profiles not in collections
            foreach (var unit in _dataService.CombatUnits)
            {
                if (unit.DeployedProfile != null &&
                    !_dataService.WeaponProfiles.Any(w => w.WeaponSystemID == unit.DeployedProfile.WeaponSystemID))
                {
                    result.AddWarning($"Unit '{unit.UnitName}' deployed profile not found in weapon profiles collection");
                }

                if (unit.MountedProfile != null &&
                    !_dataService.WeaponProfiles.Any(w => w.WeaponSystemID == unit.MountedProfile.WeaponSystemID))
                {
                    result.AddWarning($"Unit '{unit.UnitName}' mounted profile not found in weapon profiles collection");
                }

                if (unit.UnitProfile != null &&
                    !_dataService.UnitProfiles.Any(u => u.UnitProfileID == unit.UnitProfile.UnitProfileID))
                {
                    result.AddWarning($"Unit '{unit.UnitName}' unit profile not found in unit profiles collection");
                }
            }
        }

        private void ValidateCriticalDataPresence(ValidationResult result)
        {
            if (!_dataService.CombatUnits.Any())
            {
                result.AddWarning("No combat units defined - scenario will be empty");
            }

            if (!_dataService.WeaponProfiles.Any())
            {
                result.AddWarning("No weapon profiles defined - units will have no combat capability");
            }

            var unitsWithoutProfiles = _dataService.CombatUnits.Count(u => u.DeployedProfile == null);
            if (unitsWithoutProfiles > 0)
            {
                result.AddError($"{unitsWithoutProfiles} combat units have no deployed profile");
            }
        }

        private void ValidateExportReadiness(ValidationResult result)
        {
            // Check minimum requirements for scenario export
            if (_dataService.CombatUnits.Any() && !_dataService.WeaponProfiles.Any())
            {
                result.AddError("Cannot export: Combat units exist but no weapon profiles defined");
            }

            if (_dataService.CombatUnits.Any() && !_dataService.UnitProfiles.Any())
            {
                result.AddError("Cannot export: Combat units exist but no unit profiles defined");
            }

            // Check for units without required profiles
            var incompleteUnits = _dataService.CombatUnits.Where(u =>
                u.DeployedProfile == null || u.UnitProfile == null).Count();

            if (incompleteUnits > 0)
            {
                result.AddError($"Cannot export: {incompleteUnits} units missing required profiles");
            }
        }

        #endregion

        #region Dependency Validation

        /// <summary>
        /// Checks if a leader can be safely deleted without breaking references
        /// </summary>
        public ValidationResult ValidateLeaderDeletion(string leaderId)
        {
            var result = new ValidationResult();

            try
            {
                if (!_dataService.CanDeleteLeader(leaderId))
                {
                    var assignedUnits = _dataService.CombatUnits
                        .Where(u => u.CommandingOfficer?.LeaderID == leaderId)
                        .Select(u => u.UnitName)
                        .ToList();

                    result.AddError($"Cannot delete leader: assigned to {assignedUnits.Count} unit(s): {string.Join(", ", assignedUnits)}");
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateLeaderDeletion), e);
                result.AddError($"Leader deletion validation failed: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Checks if a weapon profile can be safely deleted without breaking references
        /// </summary>
        public ValidationResult ValidateWeaponProfileDeletion(WeaponSystemProfile profile)
        {
            var result = new ValidationResult();

            try
            {
                if (_dataService.IsWeaponProfileInUse(profile))
                {
                    var usingUnits = _dataService.CombatUnits
                        .Where(u => u.DeployedProfile?.WeaponSystemID == profile.WeaponSystemID ||
                                   u.MountedProfile?.WeaponSystemID == profile.WeaponSystemID)
                        .Select(u => u.UnitName)
                        .ToList();

                    result.AddError($"Cannot delete weapon profile: used by {usingUnits.Count} unit(s): {string.Join(", ", usingUnits)}");
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateWeaponProfileDeletion), e);
                result.AddError($"Weapon profile deletion validation failed: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Checks if a unit profile can be safely deleted without breaking references
        /// </summary>
        public ValidationResult ValidateUnitProfileDeletion(UnitProfile profile)
        {
            var result = new ValidationResult();

            try
            {
                if (_dataService.IsUnitProfileInUse(profile))
                {
                    var usingUnits = _dataService.CombatUnits
                        .Where(u => u.UnitProfile?.UnitProfileID == profile.UnitProfileID)
                        .Select(u => u.UnitName)
                        .ToList();

                    result.AddError($"Cannot delete unit profile: used by {usingUnits.Count} unit(s): {string.Join(", ", usingUnits)}");
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateUnitProfileDeletion), e);
                result.AddError($"Unit profile deletion validation failed: {e.Message}");
            }

            return result;
        }

        #endregion
    }
}