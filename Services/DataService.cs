using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HammerAndSickle.Models;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// DataService provides UI-friendly access to game data through ObservableCollections
    /// and CRUD operations. Acts as a bridge between the UI layer and GameDataManager,
    /// maintaining synchronized collections for data binding while ensuring data integrity
    /// through the underlying persistence layer.
    /// 
    /// Key responsibilities:
    /// - Maintain ObservableCollections synchronized with GameDataManager
    /// - Provide CRUD operations with automatic collection updates
    /// - Handle cross-reference queries for UI dropdowns and validation
    /// - Change notification management for reactive UI binding
    /// - Error handling integration with AppService
    /// </summary>
    public class DataService
    {
        private const string CLASS_NAME = nameof(DataService);
        private readonly GameDataManager _gameDataManager;
        private bool _isInitialized;

        public DataService()
        {
            try
            {
                _gameDataManager = GameDataManager.Instance;
                InitializeCollections();
                _isInitialized = true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(DataService), e);
                throw;
            }
        }

        #region Collections for UI Binding

        public ObservableCollection<Leader> Leaders { get; private set; } = new();
        public ObservableCollection<WeaponSystemProfile> WeaponProfiles { get; private set; } = new();
        public ObservableCollection<UnitProfile> UnitProfiles { get; private set; } = new();
        public ObservableCollection<CombatUnit> CombatUnits { get; private set; } = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes collections from GameDataManager data
        /// </summary>
        private void InitializeCollections()
        {
            try
            {
                SyncCollectionsFromManager();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(InitializeCollections), e);
                throw;
            }
        }

        /// <summary>
        /// Synchronizes all collections with current GameDataManager state
        /// </summary>
        public void SyncCollectionsFromManager()
        {
            try
            {
                // Clear existing collections
                Leaders.Clear();
                WeaponProfiles.Clear();
                UnitProfiles.Clear();
                CombatUnits.Clear();

                // Sync from GameDataManager
                foreach (var leader in _gameDataManager.GetAllLeaders())
                {
                    Leaders.Add(leader);
                }

                foreach (var unit in _gameDataManager.GetAllCombatUnits())
                {
                    CombatUnits.Add(unit);
                }

                // Note: WeaponProfiles and UnitProfiles don't have GetAll methods in GameDataManager
                // They use compound key lookups. We'll need to maintain these separately or
                // extend GameDataManager with GetAll methods for profiles.
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SyncCollectionsFromManager), e);
                throw;
            }
        }

        #endregion

        #region Leader Operations

        /// <summary>
        /// Adds a new leader to both the manager and UI collection
        /// </summary>
        public bool AddLeader(Leader leader)
        {
            if (leader == null)
                return false;

            try
            {
                if (_gameDataManager.RegisterLeader(leader))
                {
                    Leaders.Add(leader);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(AddLeader), e);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing leader's properties
        /// </summary>
        public bool UpdateLeader(Leader leader)
        {
            if (leader == null)
                return false;

            try
            {
                // GameDataManager doesn't have explicit update - objects are updated in place
                // Just ensure the leader exists in our collection
                var existingLeader = Leaders.FirstOrDefault(l => l.LeaderID == leader.LeaderID);
                if (existingLeader == null)
                {
                    return AddLeader(leader);
                }

                // Leader object is updated in place, collection binding will reflect changes
                _gameDataManager.MarkDirty(leader.LeaderID);
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(UpdateLeader), e);
                return false;
            }
        }

        /// <summary>
        /// Removes a leader if not assigned to any units
        /// </summary>
        public bool DeleteLeader(string leaderId)
        {
            if (string.IsNullOrEmpty(leaderId))
                return false;

            try
            {
                // Check if leader is assigned to any units
                var assignedUnits = CombatUnits.Where(u => u.CommandingOfficer?.LeaderID == leaderId).ToList();
                if (assignedUnits.Any())
                {
                    AppService.CaptureUiMessage($"Cannot delete leader {leaderId}: assigned to {assignedUnits.Count} unit(s)");
                    return false;
                }

                var leader = Leaders.FirstOrDefault(l => l.LeaderID == leaderId);
                if (leader != null)
                {
                    Leaders.Remove(leader);
                    // Note: GameDataManager doesn't have explicit delete methods
                    // This is a limitation we'll need to address
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(DeleteLeader), e);
                return false;
            }
        }

        /// <summary>
        /// Gets leaders available for assignment (not currently assigned)
        /// </summary>
        public IEnumerable<Leader> GetAvailableLeaders()
        {
            try
            {
                var assignedLeaderIds = CombatUnits
                    .Where(u => u.CommandingOfficer != null)
                    .Select(u => u.CommandingOfficer.LeaderID)
                    .ToHashSet();

                return Leaders.Where(l => !assignedLeaderIds.Contains(l.LeaderID));
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetAvailableLeaders), e);
                return Enumerable.Empty<Leader>();
            }
        }

        #endregion

        #region WeaponSystemProfile Operations

        /// <summary>
        /// Adds a new weapon system profile
        /// </summary>
        public bool AddWeaponProfile(WeaponSystemProfile profile)
        {
            if (profile == null)
                return false;

            try
            {
                if (_gameDataManager.RegisterWeaponProfile(profile))
                {
                    WeaponProfiles.Add(profile);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(AddWeaponProfile), e);
                return false;
            }
        }

        /// <summary>
        /// Gets weapon profiles filtered by nationality
        /// </summary>
        public IEnumerable<WeaponSystemProfile> GetWeaponProfilesForNationality(Nationality nationality)
        {
            try
            {
                return WeaponProfiles.Where(w => w.Nationality == nationality);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetWeaponProfilesForNationality), e);
                return Enumerable.Empty<WeaponSystemProfile>();
            }
        }

        /// <summary>
        /// Gets weapon profiles filtered by weapon system type
        /// </summary>
        public IEnumerable<WeaponSystemProfile> GetWeaponProfilesForType(WeaponSystems weaponSystem)
        {
            try
            {
                return WeaponProfiles.Where(w => w.WeaponSystem == weaponSystem);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetWeaponProfilesForType), e);
                return Enumerable.Empty<WeaponSystemProfile>();
            }
        }

        #endregion

        #region UnitProfile Operations

        /// <summary>
        /// Adds a new unit profile
        /// </summary>
        public bool AddUnitProfile(UnitProfile profile)
        {
            if (profile == null)
                return false;

            try
            {
                if (_gameDataManager.RegisterUnitProfile(profile))
                {
                    UnitProfiles.Add(profile);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(AddUnitProfile), e);
                return false;
            }
        }

        /// <summary>
        /// Gets unit profiles filtered by nationality
        /// </summary>
        public IEnumerable<UnitProfile> GetUnitProfilesForNationality(Nationality nationality)
        {
            try
            {
                return UnitProfiles.Where(u => u.Nationality == nationality);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetUnitProfilesForNationality), e);
                return Enumerable.Empty<UnitProfile>();
            }
        }

        #endregion

        #region CombatUnit Operations

        /// <summary>
        /// Adds a new combat unit
        /// </summary>
        public bool AddCombatUnit(CombatUnit unit)
        {
            if (unit == null)
                return false;

            try
            {
                if (_gameDataManager.RegisterCombatUnit(unit))
                {
                    CombatUnits.Add(unit);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(AddCombatUnit), e);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing combat unit
        /// </summary>
        public bool UpdateCombatUnit(CombatUnit unit)
        {
            if (unit == null)
                return false;

            try
            {
                var existingUnit = CombatUnits.FirstOrDefault(u => u.UnitID == unit.UnitID);
                if (existingUnit == null)
                {
                    return AddCombatUnit(unit);
                }

                _gameDataManager.MarkDirty(unit.UnitID);
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(UpdateCombatUnit), e);
                return false;
            }
        }

        /// <summary>
        /// Removes a combat unit after unassigning its leader
        /// </summary>
        public bool DeleteCombatUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return false;

            try
            {
                var unit = CombatUnits.FirstOrDefault(u => u.UnitID == unitId);
                if (unit == null)
                    return false;

                // Unassign leader if present
                if (unit.CommandingOfficer != null)
                {
                    unit.RemoveLeader();
                }

                CombatUnits.Remove(unit);
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(DeleteCombatUnit), e);
                return false;
            }
        }

        /// <summary>
        /// Gets combat units filtered by nationality
        /// </summary>
        public IEnumerable<CombatUnit> GetCombatUnitsForNationality(Nationality nationality)
        {
            try
            {
                return CombatUnits.Where(u => u.Nationality == nationality);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetCombatUnitsForNationality), e);
                return Enumerable.Empty<CombatUnit>();
            }
        }

        /// <summary>
        /// Gets combat units filtered by side
        /// </summary>
        public IEnumerable<CombatUnit> GetCombatUnitsForSide(Side side)
        {
            try
            {
                return CombatUnits.Where(u => u.Side == side);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetCombatUnitsForSide), e);
                return Enumerable.Empty<CombatUnit>();
            }
        }

        #endregion

        #region Cross-Reference Validation

        /// <summary>
        /// Checks if a leader can be deleted (not assigned to any units)
        /// </summary>
        public bool CanDeleteLeader(string leaderId)
        {
            try
            {
                return !CombatUnits.Any(u => u.CommandingOfficer?.LeaderID == leaderId);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(CanDeleteLeader), e);
                return false;
            }
        }

        /// <summary>
        /// Checks if a weapon profile is in use by any units
        /// </summary>
        public bool IsWeaponProfileInUse(WeaponSystemProfile profile)
        {
            if (profile == null)
                return false;

            try
            {
                return CombatUnits.Any(u =>
                    u.DeployedProfile?.WeaponSystemID == profile.WeaponSystemID ||
                    u.MountedProfile?.WeaponSystemID == profile.WeaponSystemID);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(IsWeaponProfileInUse), e);
                return false;
            }
        }

        /// <summary>
        /// Checks if a unit profile is in use by any units
        /// </summary>
        public bool IsUnitProfileInUse(UnitProfile profile)
        {
            if (profile == null)
                return false;

            try
            {
                return CombatUnits.Any(u => u.UnitProfile?.UnitProfileID == profile.UnitProfileID);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(IsUnitProfileInUse), e);
                return false;
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Clears all collections and resets state
        /// </summary>
        public void ClearAll()
        {
            try
            {
                Leaders.Clear();
                WeaponProfiles.Clear();
                UnitProfiles.Clear();
                CombatUnits.Clear();
                _gameDataManager.ClearAll();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ClearAll), e);
            }
        }

        /// <summary>
        /// Gets total count of all managed objects
        /// </summary>
        public int TotalObjectCount => Leaders.Count + WeaponProfiles.Count + UnitProfiles.Count + CombatUnits.Count;

        /// <summary>
        /// Checks if there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges => _gameDataManager.HasUnsavedChanges;

        #endregion
    }
}