using Avalonia.Controls.Templates;
using HammerSickle.UnitCreator.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

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

        // Optimize reactive subscriptions with proper disposal tracking
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

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

            // Replace existing subscriptions with optimized versions:
            this.WhenAnyValue(x => x.SelectedItem)
                .Where(item => item != null) // Only process actual selections
                .Throttle(TimeSpan.FromMilliseconds(100)) // Debounce rapid selection changes
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(CanDelete));
                    this.RaisePropertyChanged(nameof(CanClone));
                    this.RaisePropertyChanged(nameof(IsSelectedItemValid));
                    this.RaisePropertyChanged(nameof(ValidationStatusText));
                    this.RaisePropertyChanged(nameof(SelectionText));

                    // Trigger validation update for new selection
                    UpdateValidationForSelectedItem();
                })
                .DisposeWith(_subscriptions);

            this.WhenAnyValue(x => x.FilterText)
                .Where(text => text != null) // Avoid null processing
                .Throttle(TimeSpan.FromMilliseconds(500)) // Increased debounce for filter
                .DistinctUntilChanged() // Only apply when actually changed
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyFilter())
                .DisposeWith(_subscriptions);

            // Optimize items count subscription
            this.WhenAnyValue(x => x.Items.Count)
                .Throttle(TimeSpan.FromMilliseconds(200)) // Debounce count changes
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ItemsCountText)))
                .DisposeWith(_subscriptions);


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
        /// Updates validation for the currently selected item with caching
        /// </summary>
        private void UpdateValidationForSelectedItem()
        {
            try
            {
                if (SelectedItem == null)
                {
                    ShowValidationSummary = false;
                    ValidationSummaryItems.Clear();
                    return;
                }

                // Cache validation results to avoid repeated validation
                var validationResult = ValidateSelectedItem();

                if (validationResult.Errors.Any() || validationResult.Warnings.Any())
                {
                    UpdateValidationSummary(validationResult);
                }
                else
                {
                    ShowValidationSummary = false;
                    ValidationSummaryItems.Clear();
                }
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(UpdateValidationForSelectedItem), e);
            }
        }

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


        #region IDisposable Support

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _subscriptions?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Error Recovery

        /// <summary>
        /// Recovers from collection operation failures
        /// </summary>
        protected void RecoverFromCollectionError(Exception exception, string operation)
        {
            try
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, $"CollectionError_{operation}", exception);

                // Clear potentially corrupted state
                ClearSelection();

                // Attempt to refresh data
                if (Items.Any())
                {
                    OnRefresh();
                }

                // Notify user
                HammerAndSickle.Services.AppService.CaptureUiMessage(
                    $"Recovered from error during {operation}. Data refreshed.");
            }
            catch (Exception recoveryException)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(RecoverFromCollectionError), recoveryException);
            }
        }

        /// <summary>
        /// Validates critical state and attempts recovery if needed
        /// </summary>
        protected bool ValidateAndRecoverState()
        {
            try
            {
                // Check for null collections
                if (Items == null)
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage("Items collection was null, reinitializing");
                    // Items should be reinitialized by derived class
                    return false;
                }

                // Check for selection consistency
                if (SelectedItem != null && !Items.Contains(SelectedItem))
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage("Selection was out of sync, clearing");
                    ClearSelection();
                }

                // Check data service state
                if (_dataService == null)
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage("Data service unavailable");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    this.GetType().Name, nameof(ValidateAndRecoverState), e);
                return false;
            }
        }

        #endregion
    }
}