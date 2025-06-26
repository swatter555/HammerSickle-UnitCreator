using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using HammerSickle.UnitCreator.ViewModels.Base;
using HammerSickle.UnitCreator.ViewModels.Tabs;
using HammerSickle.UnitCreator.Services;
using HammerSickle.UnitCreator.Models;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.ViewModels
{
    /// <summary>
    /// MainWindowViewModel coordinates the entire Unit Creator application, managing file operations,
    /// tab coordination, window state, and user interface interactions.
    /// 
    /// Key responsibilities:
    /// - File operations (New, Open, Save, Save As, Export) with full user workflow support
    /// - Tab management and coordination through TabManagerService
    /// - Window title and status management with reactive updates
    /// - Unsaved changes detection and user confirmation workflows
    /// - Error handling and user feedback through integrated services
    /// - Application lifecycle management and graceful shutdown
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private const string CLASS_NAME = nameof(MainWindowViewModel);
        private readonly CompositeDisposable _subscriptions = new();

        // Services
        private readonly FileDialogService _fileDialogService;
        private readonly ProjectStateService _projectStateService;
        private readonly ExportService _exportService;
        private readonly ValidationService _validationService;
        private readonly TabManagerService _tabManagerService;
        private readonly DataService _dataService;

        // UI State
        private int _selectedTabIndex;
        private string _statusMessage = "Ready";
        private string _windowTitle = "Hammer & Sickle Unit Creator";
        private bool _isOperationInProgress;

        public MainWindowViewModel(
            FileDialogService fileDialogService,
            ProjectStateService projectStateService,
            ExportService exportService,
            ValidationService validationService,
            TabManagerService tabManagerService,
            DataService dataService)
        {
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _projectStateService = projectStateService ?? throw new ArgumentNullException(nameof(projectStateService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _tabManagerService = tabManagerService ?? throw new ArgumentNullException(nameof(tabManagerService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            try
            {
                InitializeTabViewModels();
                InitializeCommands();
                SetupReactiveSubscriptions();
                UpdateWindowTitle();

                StatusMessage = "Application initialized successfully";
                AppService.CaptureUiMessage("MainWindowViewModel initialized with full service integration");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(MainWindowViewModel), e);
                StatusMessage = $"Initialization error: {e.Message}";
                throw;
            }
        }

        #region Tab ViewModels

        public LeadersTabViewModel LeadersTab { get; private set; } = null!;
        public WeaponSystemsTabViewModel WeaponSystemsTab { get; private set; } = null!;
        public UnitProfilesTabViewModel UnitProfilesTab { get; private set; } = null!;
        public CombatUnitsTabViewModel CombatUnitsTab { get; private set; } = null!;

        #endregion

        #region Properties

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            private set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
        }

        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            private set => this.RaiseAndSetIfChanged(ref _isOperationInProgress, value);
        }

        /// <summary>
        /// Gets whether any tabs have unsaved changes (for UI binding)
        /// </summary>
        public bool HasUnsavedChanges => _projectStateService.HasUnsavedChanges || _tabManagerService.HasAnyUnsavedChanges();

        /// <summary>
        /// Gets the current project status for display
        /// </summary>
        public string ProjectStatus => _projectStateService.GetProjectStatusSummary();

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> NewCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> SaveCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> SaveAsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> ExportCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> ExitCommand { get; private set; } = null!;

        #endregion

        #region Initialization

        private void InitializeTabViewModels()
        {
            try
            {
                // Initialize tab ViewModels with services
                LeadersTab = new LeadersTabViewModel(_dataService, _validationService);
                WeaponSystemsTab = new WeaponSystemsTabViewModel();
                UnitProfilesTab = new UnitProfilesTabViewModel();
                CombatUnitsTab = new CombatUnitsTabViewModel();

                // Register tabs with TabManagerService (only LeadersTab implements ITabViewModel for now)
                if (LeadersTab is ITabViewModel leadersTabViewModel)
                {
                    _tabManagerService.RegisterTab(leadersTabViewModel);
                }

                AppService.CaptureUiMessage("Tab ViewModels initialized and registered");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(InitializeTabViewModels), e);
                throw;
            }
        }

        private void InitializeCommands()
        {
            try
            {
                // Create commands with operation in progress checks
                var canExecuteWhenNotBusy = this.WhenAnyValue(x => x.IsOperationInProgress).Select(busy => !busy);

                NewCommand = ReactiveCommand.CreateFromTask(OnNewAsync, canExecuteWhenNotBusy);
                OpenCommand = ReactiveCommand.CreateFromTask(OnOpenAsync, canExecuteWhenNotBusy);
                SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync, canExecuteWhenNotBusy);
                SaveAsCommand = ReactiveCommand.CreateFromTask(OnSaveAsAsync, canExecuteWhenNotBusy);
                ExportCommand = ReactiveCommand.CreateFromTask(OnExportAsync, canExecuteWhenNotBusy);
                ExitCommand = ReactiveCommand.CreateFromTask(OnExitAsync, canExecuteWhenNotBusy);

                AppService.CaptureUiMessage("Commands initialized with async execution");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(InitializeCommands), e);
                throw;
            }
        }

        private void SetupReactiveSubscriptions()
        {
            try
            {
                // Subscribe to project state changes for window title updates
                _projectStateService.CurrentProjectPathChanges
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => UpdateWindowTitle())
                    .DisposeWith(_subscriptions);

                _projectStateService.HasUnsavedChangesChanges
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        UpdateWindowTitle();
                        this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                        this.RaisePropertyChanged(nameof(ProjectStatus));
                    })
                    .DisposeWith(_subscriptions);

                _projectStateService.IsNewProjectChanges
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        UpdateWindowTitle();
                        this.RaisePropertyChanged(nameof(ProjectStatus));
                    })
                    .DisposeWith(_subscriptions);

                // Monitor tab changes for status updates
                Observable.Interval(TimeSpan.FromSeconds(2))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => this.RaisePropertyChanged(nameof(HasUnsavedChanges)))
                    .DisposeWith(_subscriptions);

                AppService.CaptureUiMessage("Reactive subscriptions established for UI updates");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SetupReactiveSubscriptions), e);
                throw;
            }
        }

        #endregion

        #region Window Title Management

        private void UpdateWindowTitle()
        {
            try
            {
                var projectName = _projectStateService.CurrentProjectName;
                var unsavedIndicator = _projectStateService.HasUnsavedChanges ? "*" : "";
                WindowTitle = $"Hammer & Sickle Unit Creator - {projectName}{unsavedIndicator}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(UpdateWindowTitle), e);
                WindowTitle = "Hammer & Sickle Unit Creator";
            }
        }

        #endregion

        #region Command Handlers

        private async Task OnNewAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Creating new project...";

                // Check for unsaved changes
                if (HasUnsavedChanges)
                {
                    var unsavedResult = await _fileDialogService.ShowUnsavedChangesDialogAsync(
                        GetMainWindow(), _projectStateService.CurrentProjectName);

                    switch (unsavedResult)
                    {
                        case UnsavedChangesResult.Save:
                            var saveResult = await PerformSaveOperationAsync();
                            if (!saveResult)
                            {
                                StatusMessage = "New project cancelled - save failed";
                                return;
                            }
                            break;
                        case UnsavedChangesResult.Cancel:
                            StatusMessage = "New project cancelled by user";
                            return;
                        case UnsavedChangesResult.DontSave:
                            // Continue with new project
                            break;
                    }
                }

                // Notify tabs that new project is starting
                await _tabManagerService.NotifyAllTabsNewProjectAsync();

                // Clear all data
                var clearResult = await _tabManagerService.ClearAllTabsAsync();
                if (!clearResult)
                {
                    StatusMessage = $"New project warning: {clearResult.Message}";
                }

                // Create new project state
                var newProjectResult = await _exportService.NewProjectAsync();
                if (newProjectResult)
                {
                    _projectStateService.MarkAsNewProject();
                    StatusMessage = "New project created successfully";
                }
                else
                {
                    StatusMessage = $"New project failed: {newProjectResult.Message}";
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnNewAsync), e);
                StatusMessage = $"New project failed: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task OnOpenAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Opening project...";

                // Check for unsaved changes
                if (HasUnsavedChanges)
                {
                    var unsavedResult = await _fileDialogService.ShowUnsavedChangesDialogAsync(
                        GetMainWindow(), _projectStateService.CurrentProjectName);

                    switch (unsavedResult)
                    {
                        case UnsavedChangesResult.Save:
                            var saveResult = await PerformSaveOperationAsync();
                            if (!saveResult)
                            {
                                StatusMessage = "Open cancelled - save failed";
                                return;
                            }
                            break;
                        case UnsavedChangesResult.Cancel:
                            StatusMessage = "Open cancelled by user";
                            return;
                        case UnsavedChangesResult.DontSave:
                            // Continue with open
                            break;
                    }
                }

                // Show open file dialog
                var filePath = await _fileDialogService.ShowOpenFileDialogAsync(GetMainWindow());
                if (string.IsNullOrEmpty(filePath))
                {
                    StatusMessage = "Open cancelled - no file selected";
                    return;
                }

                // Notify tabs that load is starting
                await _tabManagerService.NotifyAllTabsLoadStartingAsync();

                // Load the project
                var loadResult = await _exportService.LoadProjectAsync(filePath);
                if (loadResult)
                {
                    // Refresh all tabs with loaded data
                    var refreshResult = await _tabManagerService.RefreshAllTabsAsync();

                    // Update project state
                    _projectStateService.SetCurrentProject(filePath);

                    // Notify tabs that load completed
                    await _tabManagerService.NotifyAllTabsLoadCompletedAsync(true);

                    StatusMessage = refreshResult.Success
                        ? $"Project loaded successfully: {loadResult.Message}"
                        : $"Project loaded with warnings: {refreshResult.Message}";
                }
                else
                {
                    await _tabManagerService.NotifyAllTabsLoadCompletedAsync(false);
                    StatusMessage = $"Load failed: {loadResult.Message}";
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnOpenAsync), e);
                await _tabManagerService.NotifyAllTabsLoadCompletedAsync(false);
                StatusMessage = $"Load failed: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task OnSaveAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Saving project...";

                var success = await PerformSaveOperationAsync();
                StatusMessage = success
                    ? "Project saved successfully"
                    : "Save operation failed";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnSaveAsync), e);
                StatusMessage = $"Save failed: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task OnSaveAsAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Saving project as...";

                var success = await PerformSaveAsOperationAsync();
                StatusMessage = success
                    ? "Project saved successfully"
                    : "Save As operation failed";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnSaveAsAsync), e);
                StatusMessage = $"Save As failed: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task OnExportAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Exporting scenario...";

                // Validate all tabs before export
                var validationResult = await _tabManagerService.ValidateAllTabsAsync();
                if (!validationResult)
                {
                    StatusMessage = $"Export failed: {validationResult.Message}";
                    return;
                }

                // Show export file dialog
                var defaultName = _exportService.GenerateDefaultScenarioName();
                var filePath = await _fileDialogService.ShowExportFileDialogAsync(GetMainWindow(), defaultName);
                if (string.IsNullOrEmpty(filePath))
                {
                    StatusMessage = "Export cancelled - no file selected";
                    return;
                }

                // Prepare tabs for export
                var prepareResult = await _tabManagerService.PrepareAllTabsForSaveAsync();
                if (!prepareResult)
                {
                    StatusMessage = $"Export failed: {prepareResult.Message}";
                    return;
                }

                // Export the scenario
                var exportResult = await _exportService.ExportScenarioAsync(filePath);
                StatusMessage = exportResult.Success
                    ? $"Scenario exported successfully: {exportResult.Message}"
                    : $"Export failed: {exportResult.Message}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnExportAsync), e);
                StatusMessage = $"Export failed: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task OnExitAsync()
        {
            if (IsOperationInProgress)
                return;

            try
            {
                IsOperationInProgress = true;
                StatusMessage = "Preparing to exit...";

                var canClose = await HandleWindowClosingAsync();
                if (canClose)
                {
                    // The actual application exit will be handled by the View
                    StatusMessage = "Application closing...";
                }
                else
                {
                    StatusMessage = "Exit cancelled";
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnExitAsync), e);
                StatusMessage = $"Exit error: {e.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        #endregion

        #region Save Operations

        private async Task<bool> PerformSaveOperationAsync()
        {
            try
            {
                // Determine if we can save directly or need Save As
                if (_projectStateService.CanSaveDirectly)
                {
                    return await SaveToCurrentPathAsync();
                }
                else
                {
                    return await PerformSaveAsOperationAsync();
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(PerformSaveOperationAsync), e);
                return false;
            }
        }

        private async Task<bool> PerformSaveAsOperationAsync()
        {
            try
            {
                // Show save file dialog
                var defaultName = _projectStateService.GenerateSuggestedFileName();
                var filePath = await _fileDialogService.ShowSaveFileDialogAsync(GetMainWindow(), defaultName);
                if (string.IsNullOrEmpty(filePath))
                {
                    return false; // User cancelled
                }

                return await SaveToPathAsync(filePath);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(PerformSaveAsOperationAsync), e);
                return false;
            }
        }

        private async Task<bool> SaveToCurrentPathAsync()
        {
            if (string.IsNullOrEmpty(_projectStateService.CurrentProjectPath))
                return false;

            return await SaveToPathAsync(_projectStateService.CurrentProjectPath);
        }

        private async Task<bool> SaveToPathAsync(string filePath)
        {
            try
            {
                // Validate all tabs
                var validationResult = await _tabManagerService.ValidateAllTabsAsync();
                if (!validationResult.Success)
                {
                    AppService.CaptureUiMessage($"Save warning: {validationResult.Message}");
                    // Continue with save but warn user
                }

                // Prepare tabs for save
                var prepareResult = await _tabManagerService.PrepareAllTabsForSaveAsync();
                if (!prepareResult)
                {
                    return false;
                }

                // Notify tabs that save is starting
                await _tabManagerService.NotifyAllTabsSaveStartingAsync();

                // Perform the save
                var saveResult = await _exportService.SaveProjectAsync(filePath);

                if (saveResult)
                {
                    // Update project state
                    _projectStateService.SetCurrentProject(filePath);

                    // Notify tabs that save completed
                    await _tabManagerService.NotifyAllTabsSaveCompletedAsync(true);

                    return true;
                }
                else
                {
                    await _tabManagerService.NotifyAllTabsSaveCompletedAsync(false);
                    return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SaveToPathAsync), e);
                await _tabManagerService.NotifyAllTabsSaveCompletedAsync(false);
                return false;
            }
        }

        #endregion

        #region Window Closing Support

        /// <summary>
        /// Handles window closing with unsaved changes check
        /// Called by MainWindow.OnClosing override
        /// </summary>
        public async Task<bool> HandleWindowClosingAsync()
        {
            try
            {
                // Check if any tabs have unsaved changes
                var prepareResult = await _tabManagerService.PrepareAllTabsForCloseAsync();
                if (prepareResult.Success)
                {
                    return true; // Can close safely
                }

                // Show unsaved changes dialog
                var unsavedResult = await _fileDialogService.ShowUnsavedChangesDialogAsync(
                    GetMainWindow(), _projectStateService.CurrentProjectName);

                switch (unsavedResult)
                {
                    case UnsavedChangesResult.Save:
                        return await PerformSaveOperationAsync();
                    case UnsavedChangesResult.DontSave:
                        return true;
                    case UnsavedChangesResult.Cancel:
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(HandleWindowClosingAsync), e);
                // In case of error, allow closing to prevent application hang
                return true;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the main window for dialog parent (placeholder implementation)
        /// This will need to be provided by the View layer
        /// </summary>
        private Avalonia.Controls.Window? GetMainWindow()
        {
            // TODO: This should be injected or provided by the View
            // For now, return null - FileDialogService handles this gracefully
            return null;
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _subscriptions?.Dispose();
                        _projectStateService?.Dispose();
                        _tabManagerService?.Dispose();

                        AppService.CaptureUiMessage("MainWindowViewModel disposed successfully");
                    }
                    catch (Exception e)
                    {
                        AppService.HandleException(CLASS_NAME, nameof(Dispose), e);
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
}