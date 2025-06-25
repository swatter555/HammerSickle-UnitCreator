using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls.Templates;
using ReactiveUI;
using HammerAndSickle.Models;
using HammerAndSickle.Services;
using HammerSickle.UnitCreator.Services;
using HammerSickle.UnitCreator.ViewModels.Base;

namespace HammerSickle.UnitCreator.ViewModels.Tabs
{
    /// <summary>
    /// LeadersTabViewModel manages the Leaders tab interface, providing master-detail editing
    /// capabilities for Leader objects with CRUD operations, filtering, and validation.
    /// 
    /// Key responsibilities:
    /// - Leader creation, editing, deletion, and cloning operations
    /// - Filtering by name, nationality, rank, and assignment status
    /// - Cross-reference validation for leader assignments
    /// - Integration with DataService for persistence operations
    /// - Real-time validation feedback and error reporting
    /// - Cultural name generation using NameGen service
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

        public LeadersTabViewModel() : this(new DataService(), new ValidationService(new DataService()))
        {
        }

        public LeadersTabViewModel(DataService dataService, ValidationService validationService)
            : base(dataService, validationService)
        {
            // Initialize nationality collection - get all values and filter out any that shouldn't be shown
            // Instead of assuming 'None' exists, we'll filter based on actual enum structure
            _availableNationalities = GetValidNationalities().ToList();
            _availableCommandGrades = GetValidCommandGrades().ToList();

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
        /// Determines if a nationality is valid for display/selection
        /// </summary>
        private bool IsValidNationality(Nationality nationality)
        {
            // Filter out any values that shouldn't be selectable
            // Check for common sentinel values without assuming they exist
            var name = nationality.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)nationality >= 0; // Assuming negative values are invalid
        }

        /// <summary>
        /// Determines if a command grade is valid for display/selection
        /// </summary>
        private bool IsValidCommandGrade(CommandGrade grade)
        {
            // Filter out any values that shouldn't be selectable
            var name = grade.ToString();
            return !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   (int)grade >= 0; // Assuming negative values are invalid
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

        public bool ShowFilters
        {
            get => _showFilters;
            set => this.RaiseAndSetIfChanged(ref _showFilters, value);
        }

        public List<Nationality> AvailableNationalities => _availableNationalities;
        public List<CommandGrade> AvailableCommandGrades => _availableCommandGrades;

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
                // Fallback to default naming pattern
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
                leader.SetOfficerCommandAbility(CommandAbility.BelowAverage);

                // Generate and set culturally appropriate name
                var generatedName = GenerateLeaderName(nationality);
                if (!leader.SetOfficerName(generatedName))
                {
                    // If SetOfficerName fails, try a simpler fallback
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
                // Use default nationality or fallback to USSR
                var defaultNationality = Nationality.USSR;

                // Create new leader using default constructor
                var newLeader = new Leader();
                ConfigureNewLeader(newLeader, defaultNationality);

                if (_dataService.AddLeader(newLeader))
                {
                    RefreshItemsCollection();

                    // Select the new leader
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
                // Validate deletion is allowed
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

                    // Clear selection if deleted leader was selected
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
                // Clone the leader
                var clonedLeader = originalLeader.Clone() as Leader;
                if (clonedLeader != null)
                {
                    // Generate a completely new name for the clone (no "Clone" suffix)
                    ConfigureNewLeader(clonedLeader, originalLeader.Nationality);

                    // Ensure the cloned leader has a unique ID and a new name that aligns with nationality.
                    clonedLeader.SetOfficerName(GenerateLeaderName(originalLeader.Nationality));

                    if (_dataService.AddLeader(clonedLeader))
                    {
                        RefreshItemsCollection();

                        // Select the cloned leader
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

                // Apply text filter
                if (!string.IsNullOrWhiteSpace(FilterText))
                {
                    var filterLower = FilterText.ToLowerInvariant();
                    filteredLeaders = filteredLeaders.Where(l =>
                        l.Name.ToLowerInvariant().Contains(filterLower) ||
                        l.LeaderID.ToLowerInvariant().Contains(filterLower) ||
                        l.GetFormattedRank().ToLowerInvariant().Contains(filterLower));
                }

                // Apply nationality filter
                if (SelectedNationalityFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.Nationality == SelectedNationalityFilter.Value);
                }

                // Apply command grade filter
                if (SelectedCommandGradeFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.CommandGrade == SelectedCommandGradeFilter.Value);
                }

                // Apply assignment status filter
                if (IsAssignedFilter.HasValue)
                {
                    filteredLeaders = filteredLeaders.Where(l => l.IsAssigned == IsAssignedFilter.Value);
                }

                // Update Items collection
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

        #region Validation

        protected override ValidationResult ValidateSelectedItem()
        {
            if (SelectedItem is Leader leader)
            {
                return _validationService.ValidateLeader(leader);
            }

            return new ValidationResult(); // Valid if no selection
        }

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
                // Use available nationalities instead of hardcoded list
                var availableNats = _availableNationalities.Where(n => IsValidNationality(n)).ToArray();
                if (!availableNats.Any())
                {
                    // Fallback to known values if available nationalities is empty
                    availableNats = new[] { Nationality.USSR };
                }

                var randomNationality = availableNats[new Random().Next(availableNats.Length)];

                // Create new leader using default constructor
                var newLeader = new Leader();
                ConfigureNewLeader(newLeader, randomNationality);

                if (_dataService.AddLeader(newLeader))
                {
                    RefreshItemsCollection();

                    // Select the new leader
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