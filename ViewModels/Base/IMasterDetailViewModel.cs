using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace HammerSickle.UnitCreator.ViewModels.Base
{
    public interface IMasterDetailViewModel
    {
        // Core data binding properties
        ObservableCollection<object> Items { get; }
        object? SelectedItem { get; set; }

        // Command properties  
        ReactiveCommand<Unit, Unit> AddCommand { get; }
        ReactiveCommand<object?, Unit> DeleteCommand { get; }
        ReactiveCommand<object?, Unit> CloneCommand { get; }
        ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        ReactiveCommand<Unit, Unit> HideValidationSummaryCommand { get; }

        // State properties
        bool CanDelete { get; }
        bool CanClone { get; }

        // Filtering properties
        string FilterText { get; set; }
        string FilterWatermark { get; }

        // Display text properties
        string DetailTitle { get; }
        string NoSelectionMessage { get; }
        string AddButtonText { get; }
        string AddToolTip { get; }
        string DeleteToolTip { get; }
        string CloneToolTip { get; }

        // Count and status properties
        string ItemsCountText { get; }
        string SelectionText { get; }

        // Validation properties
        bool IsSelectedItemValid { get; }
        bool IsSelectedItemModified { get; }
        string ValidationStatusText { get; }
        bool ShowValidationSummary { get; }
        ObservableCollection<string> ValidationSummaryItems { get; }

        // Template property for detail content
        IDataTemplate? DetailTemplate { get; }
    }
}