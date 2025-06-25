using System;
using System.Reactive;
using ReactiveUI;
using HammerSickle.UnitCreator.ViewModels.Base;
using HammerSickle.UnitCreator.ViewModels.Tabs;
using HammerSickle.UnitCreator.Services;

namespace HammerSickle.UnitCreator.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private int _selectedTabIndex;
        private string _statusMessage = "Ready";
        private readonly DataService _dataService;
        private readonly ValidationService _validationService;

        public MainWindowViewModel()
        {
            try
            {
                // Initialize services
                _dataService = new DataService();
                _validationService = new ValidationService(_dataService);

                // Initialize tab ViewModels with services
                LeadersTab = new LeadersTabViewModel(_dataService, _validationService);
                WeaponSystemsTab = new WeaponSystemsTabViewModel();
                UnitProfilesTab = new UnitProfilesTabViewModel();
                CombatUnitsTab = new CombatUnitsTabViewModel();

                // Initialize commands
                NewCommand = ReactiveCommand.Create(OnNew);
                OpenCommand = ReactiveCommand.Create(OnOpen);
                SaveCommand = ReactiveCommand.Create(OnSave);
                SaveAsCommand = ReactiveCommand.Create(OnSaveAs);
                ExportCommand = ReactiveCommand.Create(OnExport);
                ExitCommand = ReactiveCommand.Create(OnExit);

                StatusMessage = "Application initialized successfully";
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(nameof(MainWindowViewModel), nameof(MainWindowViewModel), e);
                StatusMessage = $"Initialization error: {e.Message}";
            }
        }

        // Tab ViewModels
        public LeadersTabViewModel LeadersTab { get; }
        public WeaponSystemsTabViewModel WeaponSystemsTab { get; }
        public UnitProfilesTabViewModel UnitProfilesTab { get; }
        public CombatUnitsTabViewModel CombatUnitsTab { get; }

        // Properties
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

        // Commands
        public ReactiveCommand<Unit, Unit> NewCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportCommand { get; }
        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        // Command handlers
        private void OnNew()
        {
            try
            {
                // Clear all data to start fresh
                _dataService.ClearAll();
                StatusMessage = "New project created - all data cleared";
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(nameof(MainWindowViewModel), nameof(OnNew), e);
                StatusMessage = $"Failed to create new project: {e.Message}";
            }
        }

        private void OnOpen()
        {
            StatusMessage = "Open file dialog - not implemented yet (requires file dialogs)";
        }

        private void OnSave()
        {
            try
            {
                var validationResult = _validationService.ValidateAllData();
                if (validationResult.Errors.Count > 0)
                {
                    StatusMessage = $"Cannot save: {validationResult.Errors.Count} validation errors exist";
                    return;
                }

                StatusMessage = "Save operation - not fully implemented yet (requires file dialogs)";
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(nameof(MainWindowViewModel), nameof(OnSave), e);
                StatusMessage = $"Save failed: {e.Message}";
            }
        }

        private void OnSaveAs()
        {
            StatusMessage = "Save As dialog - not implemented yet (requires file dialogs)";
        }

        private void OnExport()
        {
            try
            {
                var validationResult = _validationService.ValidateAllData();
                if (!validationResult.IsValid)
                {
                    StatusMessage = $"Cannot export: {validationResult.Errors.Count} validation errors must be fixed";
                    return;
                }

                StatusMessage = "Export to .oob - not fully implemented yet (requires file dialogs)";
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(nameof(MainWindowViewModel), nameof(OnExport), e);
                StatusMessage = $"Export failed: {e.Message}";
            }
        }

        private void OnExit()
        {
            try
            {
                if (_dataService.HasUnsavedChanges)
                {
                    StatusMessage = "Unsaved changes detected - exit confirmation needed";
                    // TODO: Show confirmation dialog
                }
                else
                {
                    StatusMessage = "Exit requested";
                    // TODO: Application exit logic
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(nameof(MainWindowViewModel), nameof(OnExit), e);
                StatusMessage = $"Exit processing error: {e.Message}";
            }
        }
    }
}