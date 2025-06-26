using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Templates;
using ReactiveUI;
using HammerAndSickle.Models;
using HammerAndSickle.Services;
using HammerSickle.UnitCreator.Services;
using HammerSickle.UnitCreator.ViewModels.Base;
using HammerSickle.UnitCreator.Models;

namespace HammerSickle.UnitCreator.ViewModels.Tabs
{
    /// <summary>
    /// LeadersTabViewModel manages the Leaders tab interface with full ITabViewModel integration,
    /// providing master-detail editing capabilities for Leader objects with CRUD operations,
    /// filtering, validation, and comprehensive tab lifecycle management.
    /// 
    /// Key responsibilities:
    /// - Leader creation, editing, deletion, and cloning operations
    /// - Filtering by name, nationality, rank, and assignment status  
    /// - Cross-reference validation for leader assignments
    /// - Integration with DataService for persistence operations
    /// - Real-time validation feedback and error reporting
    /// - Cultural name generation using NameGen service
    /// - Full ITabViewModel lifecycle support for coordinated file operations
    /// </summary>
    public class LeadersTabViewModel : MasterDetailViewModelBase<Leader>
    {
        private const string CLASS_NAME = nameof(LeadersTabViewModel);

        // Filter properties
        private Nationality? _selectedNationalityFilter;
        private CommandGrade? _selectedCommandGradeFilter;
        private bool? _isAssignedFilter;
        private bool _showFilters;

        // Collections for dropdowns - properly filtered without assuming None values
        private readonly List<Nationality> _availableNationalities;
        private readonly List<CommandGrade> _availableCommandGrades;
        private readonly List<Side> _availableSides;
        private readonly List<CommandAbility> _availableCommandAbilities;

        public LeadersTabViewModel() : this(new DataService(), new ValidationService(new DataService()))
        {
        }

        public LeadersTabViewModel(DataService dataService, ValidationService validationService)
            : base(dataService, validationService)
        {
            // Initialize dropdown collections - get all values and filter out any that shouldn't be shown
            _availableNationalities = GetValidNationalities().ToList();
            _availableCommandGrades = GetValidCommandGrades().ToList();
            _availableSides = GetValidSides().ToList();
            _availableCommandAbilities = GetValidCommandAbilities().ToList();

            // Initialize filter commands
            ClearFiltersCommand = ReactiveCommand.Create(ExecuteClearFilters);
            ToggleFiltersCommand = ReactiveCommand.Create(ExecuteToggleFilters);
            RandomGenerateCommand = ReactiveCommand.Create(ExecuteRandomGenerate);

            // Set up reactive property subscriptions for filters
            this.WhenAnyValue(
                x => x.SelectedNationalityFilter,
                x => x.SelectedCommandGradeFilter,
                x => x.IsAssignedFilter)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyFilter());

            // Load initial data
            OnRefresh();
        }

        #region ITabViewModel Implementation

        public override string TabName => "Leaders";

        public override bool HasUnsavedChanges
        {
            get
            {
                try
                {
                    // Check DataService for unsaved changes
                    if (_dataService.HasUnsavedChanges)
                        return true;

                    // Check if there are any leaders in memory that might not be saved
                    // This is a simple heuristic - in a real application you might track individual object changes
                    return Items.Count > 0 && (_dataService.TotalObjectCount == 0 || base.HasUnsavedChanges);
                }
                catch (Exception e)
                {
                    HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(HasUnsavedChanges), e);
                    return false;
                }
            }
        }

        public override string StatusSummary
        {
            get
            {
                try
                {
                    var assignedCount = Items.OfType<Leader>().Count(l => l.IsAssigned);
                    var availableCount = Items.Count - assignedCount;
                    var validationStatus = IsInValidState ? "Valid" : "Invalid";
                    var changesStatus = HasUnsavedChanges ? "Modified" : "Saved";

                    return $"Leaders: {Items.Count} total ({assignedCount} assigned, {availableCount} available), {validationStatus}, {changesStatus}";
                }
                catch (Exception e)
                {
                    HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(StatusSummary), e);
                    return "Leaders: Status unavailable";
                }
            }
        }

        #endregion

        #region Enhanced Tab Lifecycle Operations

        public override async Task<OperationResult> ValidateAsync()
        {
            try
            {
                IsBusy = true;

                var result = new ValidationResult();
                int validatedCount = 0;

                // Validate all leaders in the collection
                foreach (var leader in Items.OfType<Leader>())
                {
                    var leaderResult = _validationService.ValidateLeader(leader);
                    result.Merge(leaderResult);
                    validatedCount++;
                }

                // Check for cross-reference issues
                await ValidateCrossReferences(result);

                var operationResult = result.IsValid
                    ? OperationResult.Successful($"Leaders validation passed: {validatedCount} leaders validated")
                    : OperationResult.ValidationFailed(result.Errors);

                LastValidationResult = operationResult;

                this.RaisePropertyChanged(nameof(IsInValidState));
                this.RaisePropertyChanged(nameof(StatusSummary));

                return operationResult;
            }
            catch (Exception e)
            {
                var errorResult = OperationResult.FromException(e, "Leaders validation failed");
                LastValidationResult = errorResult;
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ValidateAsync), e);
                return errorResult;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task<OperationResult> PrepareForSaveAsync()
        {
            try
            {
                IsBusy = true;

                // Validate all leaders before save
                var validationResult = await ValidateAsync();
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // Ensure all leaders are properly synced with DataService
                await SyncLeadersToDataService();

                return OperationResult.Successful($"Leaders prepared for save: {Items.Count} leaders ready");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(PrepareForSaveAsync), e);
                return OperationResult.FromException(e, "Leaders save preparation failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task<OperationResult> RefreshFromDataAsync()
        {
            try
            {
                IsBusy = true;

                // Sync collections from DataService
                _dataService.SyncCollectionsFromManager();

                // Refresh the UI collection
                OnRefresh();

                this.RaisePropertyChanged(nameof(StatusSummary));
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));

                return OperationResult.Successful($"Leaders refreshed: {Items.Count} leaders loaded from data service");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(RefreshFromDataAsync), e);
                return OperationResult.FromException(e, "Leaders refresh failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task<OperationResult> ClearAllDataAsync()
        {
            try
            {
                IsBusy = true;

                var originalCount = Items.Count;

                // Clear UI collections
                Items.Clear();
                ValidationSummaryItems.Clear();
                ShowValidationSummary = false;
                SelectedItem = null;
                LastValidationResult = null;

                // Clear filters
                SelectedNationalityFilter = null;
                SelectedCommandGradeFilter = null;
                IsAssignedFilter = null;
                FilterText = string.Empty;

                this.RaisePropertyChanged(nameof(StatusSummary));
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));

                return OperationResult.Successful($"Leaders cleared: {originalCount} leaders removed");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ClearAllDataAsync), e);
                return OperationResult.FromException(e, "Leaders clear failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Enhanced Validation

        protected override ValidationResult ValidateItem(Leader item)
        {
            return _validationService.ValidateLeader(item);
        }

        protected override ValidationResult ValidateSelectedItem()
        {
            if (SelectedItem is Leader leader)
            {
                return _validationService.ValidateLeader(leader);
            }
            return new ValidationResult(); // Valid if no selection
        }

        private async Task ValidateCrossReferences(ValidationResult result)
        {
            try
            {
                await Task.Delay(1); // Make it async

                // Check for assignment consistency across the entire dataset
                var assignmentIssues = new List<string>();

                foreach (var leader in Items.OfType<Leader>().Where(l => l.IsAssigned))
                {
                    if (string.IsNullOrEmpty(leader.UnitID))
                    {
                        assignmentIssues.Add($"Leader '{leader.Name}' marked as assigned but has no unit ID");
                    }
                    else
                    {
                        // Check if the unit exists (this would require access to units collection)
                        var unit = _dataService.CombatUnits.FirstOrDefault(u => u.UnitID == leader.UnitID);
                        if (unit == null)
                        {
                            assignmentIssues.Add($"Leader '{leader.Name}' assigned to non-existent unit '{leader.UnitID}'");
                        }
                        else if (unit.CommandingOfficer?.LeaderID != leader.LeaderID)
                        {
                            assignmentIssues.Add($"Leader '{leader.Name}' assignment to unit '{leader.UnitID}' is not bidirectional");
                        }
                    }
                }

                result.AddErrors(assignmentIssues);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ValidateCrossReferences), e);
                result.AddError($"Cross-reference validation failed: {e.Message}");
            }
        }

        private async Task SyncLeadersToDataService()
        {
            try
            {
                await Task.Delay(1); // Make it async

                // Ensure all leaders in the UI are properly registered with DataService
                foreach (var leader in Items.OfType<Leader>())
                {
                    if (!_dataService.Leaders.Contains(leader))
                    {
                        _dataService.UpdateLeader(leader);
                    }
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(SyncLeadersToDataService), e);
                throw;
            }
        }

        #endregion

        #region Enum Filtering Methods

        /// <summary>
        /// Gets valid nationalities, filtering out any sentinel/invalid values
        /// </summary>
        private IEnumerable<Nationality> GetValidNationalities()
        {
            return Enum.GetValues<Nationality>()
                .Where(IsValidNationality)
                .OrderBy(n => n.ToString());
        }

        /// <summary>
        /// Gets valid command grades, filtering out any sentinel/invalid values
        /// </summary>
        private IEnumerable<CommandGrade> GetValidCommandGrades()
        {
            return Enum.GetValues<CommandGrade>()
                .Where(IsValidCommandGrade)
                .OrderBy(g => g.ToString());
        }

        /// <summary>
        /// Gets valid sides, filtering out any sentinel/invalid values
        /// </summary>
        private IEnumerable<Side> GetValidSides()
        {
            return Enum.GetValues<Side>()
                .Where(IsValidSide)
                .OrderBy(s => s.ToString());
        }

        /// <summary>
        /// Gets valid command abilities, filtering out any sentinel/invalid values
        /// </summary>
        private IEnumerable<CommandAbility> GetValidCommandAbilities()
        {
            return Enum.GetValues<CommandAbility>()
                .Where(IsValidCommandAbility)
                .OrderBy(c => c.ToString());
        }

        /// <summary>
        /// Determines if a nationality is valid for display/selection
        /// </summary>
        private bool IsValidNationality(Nationality nationality)
        {
            var name = nationality.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)nationality >= 0;
        }

        /// <summary>
        /// Determines if a command grade is valid for display/selection
        /// </summary>
        private bool IsValidCommandGrade(CommandGrade grade)
        {
            var name = grade.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)grade >= 0;
        }

        /// <summary>
        /// Determines if a side is valid for display/selection
        /// </summary>
        private bool IsValidSide(Side side)
        {
            var name = side.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)side >= 0;
        }

        /// <summary>
        /// Determines if a command ability is valid for display/selection
        /// </summary>
        private bool IsValidCommandAbility(CommandAbility ability)
        {
            var name = ability.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)ability >= 0;
        }

        #endregion

        #region Filter Properties

        public Nationality? SelectedNationalityFilter
        {
            get => _selectedNationalityFilter;
            set => this.RaiseAndSetIfChanged(ref _selectedNationalityFilter, value);
        }

        public CommandGrade? SelectedCommandGradeFilter
        {
            get => _selectedCommandGradeFilter;
            set => this.RaiseAndSetIfChanged(ref _selectedCommandGradeFilter, value);
        }

        public bool? IsAssignedFilter
        {
            get => _isAssignedFilter;
            set => this.RaiseAndSetIfChanged(ref _isAssignedFilter, value);
        }

        /// <summary>
        /// Index-based assignment filter for ComboBox binding (0=All, 1=Assigned, 2=Available)
        /// </summary>
        public int AssignmentFilterIndex
        {
            get => _isAssignedFilter switch
            {
                null => 0,    // All Leaders
                true => 1,    // Assigned
                false => 2    // Available
            };
            set
            {
                var newValue = value switch
                {
                    0 => (bool?)null,  // All Leaders
                    1 => true,         // Assigned
                    2 => false,        // Available
                    _ => null          // Default to All
                };
                this.RaiseAndSetIfChanged(ref _isAssignedFilter, newValue, nameof(IsAssignedFilter));
                this.RaisePropertyChanged(); // For AssignmentFilterIndex itself
            }
        }

        public bool ShowFilters
        {
            get => _showFilters;
            set => this.RaiseAndSetIfChanged(ref _showFilters, value);
        }

        // Dropdown collections for UI binding
        public List<Nationality> AvailableNationalities => _availableNationalities;
        public List<CommandGrade> AvailableCommandGrades => _availableCommandGrades;
        public List<Side> AvailableSides => _availableSides;
        public List<CommandAbility> AvailableCommandAbilities => _availableCommandAbilities;

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> RandomGenerateCommand { get; }

        #endregion

        #region Abstract Implementation

        public override string FilterWatermark => "Filter by name...";
        public override string DetailTitle => "Leader Details";
        public override string NoSelectionMessage => "Select a leader to view and edit details";
        public override string AddButtonText => "Add Leader";
        public override string AddToolTip => "Create a new leader";
        public override string DeleteToolTip => "Delete selected leader (must be unassigned)";
        public override string CloneToolTip => "Clone selected leader with new ID";

        public override IDataTemplate? DetailTemplate => null; // Will be set by View

        #endregion

        #region Leader Name Generation

        /// <summary>
        /// Generates a culturally appropriate name for a leader based on nationality
        /// </summary>
        private string GenerateLeaderName(Nationality nationality)
        {
            try
            {
                return NameGen.MaleName(nationality);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(GenerateLeaderName), e);
                return $"Officer {DateTime.Now:HHmmss}";
            }
        }

        /// <summary>
        /// Sets up a newly created leader with nationality and appropriate name
        /// </summary>
        private void ConfigureNewLeader(Leader leader, Nationality nationality)
        {
            if (leader == null)
                return;

            try
            {
                // Set nationality first
                leader.SetNationality(nationality);
                leader.SetSide(Side.Player);
                leader.SetCommandGrade(CommandGrade.JuniorGrade);
                leader.SetOfficerCommandAbility(CommandAbility.Average);

                // Generate and set culturally appropriate name
                var generatedName = GenerateLeaderName(nationality);
                if (!leader.SetOfficerName(generatedName))
                {
                    leader.SetOfficerName($"Officer {DateTime.Now:mmss}");
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ConfigureNewLeader), e);
            }
        }

        #endregion

        #region CRUD Operations

        protected override void OnAdd()
        {
            try
            {
                var defaultNationality = Nationality.USSR;

                var newLeader = new Leader();
                ConfigureNewLeader(newLeader, defaultNationality);

                if (_dataService.AddLeader(newLeader))
                {
                    RefreshItemsCollection();

                    var addedLeader = Items.OfType<Leader>().FirstOrDefault(l => l.LeaderID == newLeader.LeaderID);
                    if (addedLeader != null)
                    {
                        SelectedItem = addedLeader;
                    }

                    HammerAndSickle.Services.AppService.CaptureUiMessage($"Leader '{newLeader.Name}' created successfully");
                }
                else
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage("Failed to create new leader");
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnAdd), e);
            }
        }

        protected override void OnDelete(object? item)
        {
            if (item is not Leader leader)
                return;

            try
            {
                var validationResult = _validationService.ValidateLeaderDeletion(leader.LeaderID);
                if (!validationResult.IsValid)
                {
                    UpdateValidationSummary(validationResult);
                    HammerAndSickle.Services.AppService.CaptureUiMessage($"Cannot delete leader: {validationResult.Errors.FirstOrDefault()}");
                    return;
                }

                if (_dataService.DeleteLeader(leader.LeaderID))
                {
                    RefreshItemsCollection();

                    if (SelectedItem == leader)
                    {
                        ClearSelection();
                    }

                    HammerAndSickle.Services.AppService.CaptureUiMessage($"Leader '{leader.Name}' deleted successfully");
                }
                else
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage($"Failed to delete leader '{leader.Name}'");
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnDelete), e);
            }
        }

        protected override void OnClone(object? item)
        {
            if (item is not Leader originalLeader)
                return;

            try
            {
                var clonedLeader = originalLeader.Clone() as Leader;
                if (clonedLeader != null)
                {
                    ConfigureNewLeader(clonedLeader, originalLeader.Nationality);
                    clonedLeader.SetOfficerName(GenerateLeaderName(originalLeader.Nationality));

                    if (_dataService.AddLeader(clonedLeader))
                    {
                        RefreshItemsCollection();

                        var addedLeader = Items.OfType<Leader>().FirstOrDefault(l => l.LeaderID == clonedLeader.LeaderID);
                        if (addedLeader != null)
                        {
                            SelectedItem = addedLeader;
                        }

                        HammerAndSickle.Services.AppService.CaptureUiMessage($"Leader '{clonedLeader.Name}' cloned successfully");
                    }
                    else
                    {
                        HammerAndSickle.Services.AppService.CaptureUiMessage("Failed to add cloned leader");
                    }
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnClone), e);
            }
        }

        protected override void OnRefresh()
        {
            try
            {
                _dataService.SyncCollectionsFromManager();
                ApplyFilter();
                HammerAndSickle.Services.AppService.CaptureUiMessage("Leaders refreshed from data");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnRefresh), e);
            }
        }

        #endregion

        #region Filtering

        protected override void ApplyFilter()
        {
            try
            {
                var filteredLeaders = _dataService.Leaders.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(FilterText))
                {
                    var filterLower = FilterText.ToLowerInvariant();
                    filteredLeaders = filteredLeaders.Where(l =>
                        l.Name.ToLowerInvariant().Contains(filterLower) ||
                        l.LeaderID.ToLowerInvariant().Contains(filterLower) ||
                        l.GetFormattedRank().ToLowerInvariant().Contains(filterLower));
                }

                if (SelectedNationalityFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.Nationality == SelectedNationalityFilter.Value);
                }

                if (SelectedCommandGradeFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.CommandGrade == SelectedCommandGradeFilter.Value);
                }

                if (IsAssignedFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.IsAssigned == IsAssignedFilter.Value);
                }

                Items.Clear();
                foreach (var leader in filteredLeaders.OrderBy(l => l.Name))
                {
                    Items.Add(leader);
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ApplyFilter), e);
            }
        }

        #endregion

        #region Enhanced Validation

        public override bool CanDelete => base.CanDelete && ValidateCanDelete(SelectedItem);

        protected override bool ValidateCanDelete(object? item)
        {
            if (item is not Leader leader)
                return false;

            try
            {
                return _dataService.CanDeleteLeader(leader.LeaderID);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ValidateCanDelete), e);
                return false;
            }
        }

        #endregion

        #region Command Handlers

        private void ExecuteClearFilters()
        {
            try
            {
                SelectedNationalityFilter = null;
                SelectedCommandGradeFilter = null;
                IsAssignedFilter = null;
                FilterText = string.Empty;

                HammerAndSickle.Services.AppService.CaptureUiMessage("Filters cleared");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteClearFilters), e);
            }
        }

        private void ExecuteToggleFilters()
        {
            ShowFilters = !ShowFilters;
        }

        private void ExecuteRandomGenerate()
        {
            try
            {
                var availableNats = _availableNationalities.Where(n => IsValidNationality(n)).ToArray();
                if (!availableNats.Any())
                {
                    availableNats = new[] { Nationality.USSR };
                }

                var randomNationality = availableNats[new Random().Next(availableNats.Length)];

                var newLeader = new Leader();
                ConfigureNewLeader(newLeader, randomNationality);

                if (_dataService.AddLeader(newLeader))
                {
                    RefreshItemsCollection();

                    var addedLeader = Items.OfType<Leader>().FirstOrDefault(l => l.LeaderID == newLeader.LeaderID);
                    if (addedLeader != null)
                    {
                        SelectedItem = addedLeader;
                    }

                    HammerAndSickle.Services.AppService.CaptureUiMessage($"Random leader '{newLeader.Name}' ({randomNationality}) generated");
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteRandomGenerate), e);
            }
        }

        #endregion

        #region Helper Properties

        /// <summary>
        /// Gets display text for the currently selected leader
        /// </summary>
        public string SelectedLeaderDisplayText
        {
            get
            {
                if (SelectedItem is Leader leader)
                {
                    var assignmentText = leader.IsAssigned ? $"Assigned to {leader.UnitID}" : "Unassigned";
                    return $"{leader.GetFormattedRank()} {leader.Name} ({leader.Nationality}) - {assignmentText}";
                }
                return "No leader selected";
            }
        }

        /// <summary>
        /// Gets count text with assignment breakdown
        /// </summary>
        public string LeaderCountText
        {
            get
            {
                var total = Items.Count;
                var assigned = Items.OfType<Leader>().Count(l => l.IsAssigned);
                var unassigned = total - assigned;
                return $"{total} total ({assigned} assigned, {unassigned} available)";
            }
        }

        /// <summary>
        /// Gets filter summary text
        /// </summary>
        public string FilterSummaryText
        {
            get
            {
                var filters = new List<string>();

                if (SelectedNationalityFilter.HasValue)
                    filters.Add($"Nationality: {SelectedNationalityFilter}");

                if (SelectedCommandGradeFilter.HasValue)
                    filters.Add($"Rank: {SelectedCommandGradeFilter}");

                if (IsAssignedFilter.HasValue)
                    filters.Add($"Status: {(IsAssignedFilter.Value ? "Assigned" : "Available")}");

                if (!string.IsNullOrWhiteSpace(FilterText))
                    filters.Add($"Text: '{FilterText}'");

                return filters.Any() ? $"Filtered by: {string.Join(", ", filters)}" : "No filters applied";
            }
        }

        #endregion
    }
}