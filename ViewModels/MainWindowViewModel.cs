using System.Reactive;
using ReactiveUI;
using HammerSickle.UnitCreator.ViewModels.Base;
using HammerSickle.UnitCreator.ViewModels.Tabs;

namespace HammerSickle.UnitCreator.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private int _selectedTabIndex;
        private string _statusMessage = "Ready";

        public MainWindowViewModel()
        {
            // Initialize tab ViewModels
            LeadersTab = new LeadersTabViewModel();
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

        // Command handlers (placeholder implementations)
        private void OnNew()
        {
            StatusMessage = "New project created";
        }

        private void OnOpen()
        {
            StatusMessage = "Open file dialog - not implemented yet";
        }

        private void OnSave()
        {
            StatusMessage = "Save - not implemented yet";
        }

        private void OnSaveAs()
        {
            StatusMessage = "Save As dialog - not implemented yet";
        }

        private void OnExport()
        {
            StatusMessage = "Export to .sce - not implemented yet";
        }

        private void OnExit()
        {
            StatusMessage = "Exit requested";
            // Application exit logic will be handled later
        }
    }
}