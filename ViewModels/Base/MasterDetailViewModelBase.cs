using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls.Templates;
using ReactiveUI;
using HammerSickle.UnitCreator.Services;
using ValidationResult = HammerSickle.UnitCreator.Services.ValidationResult;

namespace HammerSickle.UnitCreator.ViewModels.Base
{
    /// <summary>
    /// Base ViewModel implementation for master-detail pattern.
    /// Provides common functionality and can be inherited by specific tab ViewModels.
    /// </summary>
    public abstract class MasterDetailViewModelBase<T> : ViewModelBase, IMasterDetailViewModel
        where T : class
    {
        protected readonly DataService _dataService;
        protected readonly ValidationService _validationService;

        private object? _selectedItem;
        private string _filterText = string.Empty;
        private bool _showValidationSummary;

        protected MasterDetailViewModelBase(DataService dataService, ValidationService validationService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));

            Items = new ObservableCollection<object>();
            ValidationSummaryItems = new ObservableCollection<string>();

            // Initialize commands with lambda expressions to fix method group conversion errors
            AddCommand = ReactiveCommand.Create(ExecuteAdd);

            var canDelete = this.WhenAnyValue(x => x.SelectedItem).Select(item => item != null);
            DeleteCommand = ReactiveCommand.Create<object?>(item => ExecuteDelete(item), canDelete);

            var canClone = this.WhenAnyValue(x => x.SelectedItem).Select(item => item != null);
            CloneCommand = ReactiveCommand.Create<object?>(item => ExecuteClone(item), canClone);

            RefreshCommand = ReactiveCommand.Create(ExecuteRefresh);
            HideValidationSummaryCommand = ReactiveCommand.Create(ExecuteHideValidationSummary);

            // Set up reactive property subscriptions
            this.WhenAnyValue(x => x.SelectedItem)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(CanDelete));
                    this.RaisePropertyChanged(nameof(CanClone));
                    this.RaisePropertyChanged(nameof(IsSelectedItemValid));
                    this.RaisePropertyChanged(nameof(ValidationStatusText));
                    this.RaisePropertyChanged(nameof(SelectionText));
                });

            this.WhenAnyValue(x => x.FilterText)
                .Throttle(TimeSpan.FromMilliseconds(300)) // Debounce filter changes
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyFilter());

            this.WhenAnyValue(x => x.Items.Count)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ItemsCountText)));
        }

        #region Public Properties

        // Core collections
        public ObservableCollection<object> Items { get; }
        public ObservableCollection<string> ValidationSummaryItems { get; }

        public object? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        public string FilterText
        {
            get => _filterText;
            set => this.RaiseAndSetIfChanged(ref _filterText, value);
        }

        public bool ShowValidationSummary
        {
            get => _showValidationSummary;
            set => this.RaiseAndSetIfChanged(ref _showValidationSummary, value);
        }

        // Commands
        public ReactiveCommand<Unit, Unit> AddCommand { get; }
        public ReactiveCommand<object?, Unit> DeleteCommand { get; }
        public ReactiveCommand<object?, Unit> CloneCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> HideValidationSummaryCommand { get; }

        // State properties
        public virtual bool CanDelete => SelectedItem != null;
        public virtual bool CanClone => SelectedItem != null;

        // Computed properties
        public string ItemsCountText => $"{Items.Count} item{(Items.Count != 1 ? "s" : "")}";

        public string SelectionText => SelectedItem != null ? "1 selected" : "None selected";

        public virtual bool IsSelectedItemValid
        {
            get
            {
                if (SelectedItem == null) return true;
                try
                {
                    return ValidateSelectedItem().IsValid;
                }
                catch
                {
                    return false;
                }
            }
        }

        public virtual bool IsSelectedItemModified => false; // Override in derived classes if tracking modification

        public string ValidationStatusText => IsSelectedItemValid ? "Valid" : "Invalid";

        #endregion

        #region Abstract Properties

        // Abstract properties to be implemented by derived classes
        public abstract string FilterWatermark { get; }
        public abstract string DetailTitle { get; }
        public abstract string NoSelectionMessage { get; }
        public abstract string AddButtonText { get; }
        public abstract string AddToolTip { get; }
        public abstract string DeleteToolTip { get; }
        public abstract string CloneToolTip { get; }
        public abstract IDataTemplate? DetailTemplate { get; }

        #endregion

        #region Command Execution Methods

        private Unit ExecuteAdd()
        {
            try
            {
                OnAdd();
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(ExecuteAdd), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteDelete(object? item)
        {
            try
            {
                OnDelete(item);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(ExecuteDelete), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteClone(object? item)
        {
            try
            {
                OnClone(item);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(ExecuteClone), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteRefresh()
        {
            try
            {
                OnRefresh();
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(ExecuteRefresh), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteHideValidationSummary()
        {
            ShowValidationSummary = false;
            return Unit.Default;
        }

        #endregion

        #region Abstract Methods

        // Abstract methods for derived classes to implement
        protected abstract void OnAdd();
        protected abstract void OnDelete(object? item);
        protected abstract void OnClone(object? item);
        protected abstract void OnRefresh();
        protected abstract void ApplyFilter();
        protected abstract ValidationResult ValidateSelectedItem();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the validation summary with errors and warnings from a validation result
        /// </summary>
        protected void UpdateValidationSummary(ValidationResult result)
        {
            if (result == null) return;

            ValidationSummaryItems.Clear();

            // Add up to 5 errors
            foreach (var error in result.Errors.Take(5))
            {
                ValidationSummaryItems.Add($"• {error}");
            }

            // Add up to 3 warnings  
            foreach (var warning in result.Warnings.Take(3))
            {
                ValidationSummaryItems.Add($"• {warning}");
            }

            ShowValidationSummary = ValidationSummaryItems.Any();
        }

        /// <summary>
        /// Refreshes the items collection by reapplying the current filter
        /// </summary>
        protected void RefreshItemsCollection()
        {
            ApplyFilter();
            this.RaisePropertyChanged(nameof(ItemsCountText));
        }

        /// <summary>
        /// Validates if the current selection can be deleted safely
        /// </summary>
        protected virtual bool ValidateCanDelete(object? item)
        {
            return item != null;
        }

        /// <summary>
        /// Validates if the current selection can be cloned
        /// </summary>
        protected virtual bool ValidateCanClone(object? item)
        {
            return item != null;
        }

        /// <summary>
        /// Safely selects an item in the collection after operations
        /// </summary>
        protected void SafeSelectItem(object? itemToSelect)
        {
            if (itemToSelect != null && Items.Contains(itemToSelect))
            {
                SelectedItem = itemToSelect;
            }
            else if (Items.Any())
            {
                SelectedItem = Items.First();
            }
            else
            {
                SelectedItem = null;
            }
        }

        /// <summary>
        /// Clears selection and validation summary
        /// </summary>
        protected void ClearSelection()
        {
            SelectedItem = null;
            ShowValidationSummary = false;
            ValidationSummaryItems.Clear();
        }

        #endregion
    }
}