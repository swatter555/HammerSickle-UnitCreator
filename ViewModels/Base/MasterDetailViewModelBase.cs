using Avalonia.Controls.Templates;
using HammerSickle.UnitCreator.Services;
using HammerSickle.UnitCreator.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace HammerSickle.UnitCreator.ViewModels.Base
{
    /// <summary>
    /// Base ViewModel implementation for master-detail pattern with full ITabViewModel integration.
    /// Provides common functionality for tab-based editing with lifecycle management, validation,
    /// and coordination support for the Unit Creator's extensible tab system.
    /// </summary>
    public abstract class MasterDetailViewModelBase<T> : ViewModelBase, IMasterDetailViewModel, ITabViewModel, IDisposable
        where T : class
    {
        private const string CLASS_NAME = nameof(MasterDetailViewModelBase<T>);

        protected readonly DataService _dataService;
        protected readonly ValidationService _validationService;

        // Optimize reactive subscriptions with proper disposal tracking
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        private object? _selectedItem;
        private string _filterText = string.Empty;
        private bool _showValidationSummary;
        private bool _isActive;
        private bool _isEnabled = true;
        private bool _isBusy;
        private OperationResult? _lastValidationResult;

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

            SetupReactiveSubscriptions();
        }

        #region IMasterDetailViewModel Properties

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
        public virtual bool CanDelete => SelectedItem != null && ValidateCanDelete(SelectedItem);
        public virtual bool CanClone => SelectedItem != null && ValidateCanClone(SelectedItem);

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

        #region ITabViewModel Properties

        public abstract string TabName { get; }

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public virtual bool HasUnsavedChanges
        {
            get
            {
                try
                {
                    // Check if any items have been modified or if there are validation errors that indicate changes
                    return Items.Any() && (_dataService.HasUnsavedChanges || IsSelectedItemModified);
                }
                catch (Exception e)
                {
                    HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(HasUnsavedChanges), e);
                    return false;
                }
            }
        }

        public virtual int ManagedObjectCount => Items.Count;

        public virtual string StatusSummary
        {
            get
            {
                try
                {
                    var validationStatus = IsInValidState ? "Valid" : "Invalid";
                    var changesStatus = HasUnsavedChanges ? "Modified" : "Saved";
                    return $"{TabName}: {ManagedObjectCount} items, {validationStatus}, {changesStatus}";
                }
                catch (Exception e)
                {
                    HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(StatusSummary), e);
                    return $"{TabName}: Status unavailable";
                }
            }
        }

        public virtual bool IsInValidState
        {
            get
            {
                try
                {
                    return _lastValidationResult?.Success ?? true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            protected set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public OperationResult? LastValidationResult
        {
            get => _lastValidationResult;
            protected set => this.RaiseAndSetIfChanged(ref _lastValidationResult, value);
        }

        public virtual bool CanClose => !HasUnsavedChanges || !IsInValidState;

        // Optional advanced operations (default implementations)
        public virtual bool SupportsImport => false;
        public virtual bool SupportsExport => false;

        #endregion

        #region Abstract Properties (IMasterDetailViewModel)

        public abstract string FilterWatermark { get; }
        public abstract string DetailTitle { get; }
        public abstract string NoSelectionMessage { get; }
        public abstract string AddButtonText { get; }
        public abstract string AddToolTip { get; }
        public abstract string DeleteToolTip { get; }
        public abstract string CloneToolTip { get; }
        public abstract IDataTemplate? DetailTemplate { get; }

        #endregion

        #region Initialization

        private void SetupReactiveSubscriptions()
        {
            try
            {
                this.WhenAnyValue(x => x.SelectedItem)
                    .Where(item => item != null)
                    .Throttle(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        this.RaisePropertyChanged(nameof(CanDelete));
                        this.RaisePropertyChanged(nameof(CanClone));
                        this.RaisePropertyChanged(nameof(IsSelectedItemValid));
                        this.RaisePropertyChanged(nameof(ValidationStatusText));
                        this.RaisePropertyChanged(nameof(SelectionText));
                        this.RaisePropertyChanged(nameof(StatusSummary));

                        UpdateValidationForSelectedItem();
                    })
                    .DisposeWith(_subscriptions);

                this.WhenAnyValue(x => x.FilterText)
                    .Where(text => text != null)
                    .Throttle(TimeSpan.FromMilliseconds(500))
                    .DistinctUntilChanged()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => ApplyFilter())
                    .DisposeWith(_subscriptions);

                this.WhenAnyValue(x => x.Items.Count)
                    .Throttle(TimeSpan.FromMilliseconds(200))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ =>
                    {
                        this.RaisePropertyChanged(nameof(ItemsCountText));
                        this.RaisePropertyChanged(nameof(ManagedObjectCount));
                        this.RaisePropertyChanged(nameof(StatusSummary));
                        this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                    })
                    .DisposeWith(_subscriptions);
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(SetupReactiveSubscriptions), e);
            }
        }

        #endregion

        #region ITabViewModel Lifecycle Operations

        public virtual async Task<OperationResult> ValidateAsync()
        {
            try
            {
                IsBusy = true;

                await Task.Delay(1); // Make it actually async

                var result = new ValidationResult();

                // Validate all items in the collection
                foreach (var item in Items.OfType<T>())
                {
                    var itemResult = ValidateItem(item);
                    result.Merge(itemResult);
                }

                // Store the last validation result
                var operationResult = result.IsValid
                    ? OperationResult.Successful($"{TabName} validation passed: {Items.Count} items validated")
                    : OperationResult.ValidationFailed(result.Errors);

                LastValidationResult = operationResult;

                this.RaisePropertyChanged(nameof(IsInValidState));
                this.RaisePropertyChanged(nameof(StatusSummary));

                return operationResult;
            }
            catch (Exception e)
            {
                var errorResult = OperationResult.FromException(e, $"{TabName} validation failed");
                LastValidationResult = errorResult;
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ValidateAsync), e);
                return errorResult;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public virtual async Task<OperationResult> RefreshFromDataAsync()
        {
            try
            {
                IsBusy = true;

                await Task.Delay(1); // Make it actually async

                OnRefresh();

                this.RaisePropertyChanged(nameof(StatusSummary));
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));

                return OperationResult.Successful($"{TabName} refreshed: {Items.Count} items loaded");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(RefreshFromDataAsync), e);
                return OperationResult.FromException(e, $"{TabName} refresh failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public virtual async Task<OperationResult> PrepareForSaveAsync()
        {
            try
            {
                IsBusy = true;

                // Validate before save
                var validationResult = await ValidateAsync();
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // Perform any pre-save operations (override in derived classes)
                await OnPrepareForSaveAsync();

                return OperationResult.Successful($"{TabName} prepared for save");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(PrepareForSaveAsync), e);
                return OperationResult.FromException(e, $"{TabName} save preparation failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public virtual async Task<OperationResult> PrepareForCloseAsync()
        {
            try
            {
                if (HasUnsavedChanges)
                {
                    return OperationResult.Failed($"{TabName} has unsaved changes");
                }

                await Task.Delay(1); // Make it actually async
                return OperationResult.Successful($"{TabName} ready to close");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(PrepareForCloseAsync), e);
                return OperationResult.FromException(e, $"{TabName} close preparation failed");
            }
        }

        public virtual async Task<OperationResult> ClearAllDataAsync()
        {
            try
            {
                IsBusy = true;

                await Task.Delay(1); // Make it actually async

                var originalCount = Items.Count;

                Items.Clear();
                ValidationSummaryItems.Clear();
                ShowValidationSummary = false;
                SelectedItem = null;
                LastValidationResult = null;

                this.RaisePropertyChanged(nameof(StatusSummary));
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));

                return OperationResult.Successful($"{TabName} cleared: {originalCount} items removed");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ClearAllDataAsync), e);
                return OperationResult.FromException(e, $"{TabName} clear failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Optional advanced operations with default implementations
        public virtual async Task<OperationResult> ImportDataAsync(string importPath)
        {
            await Task.Delay(1);
            return OperationResult.Successful($"{TabName} import not implemented");
        }

        public virtual async Task<OperationResult> ExportDataAsync(string exportPath)
        {
            await Task.Delay(1);
            return OperationResult.Successful($"{TabName} export not implemented");
        }

        #endregion

        #region ITabViewModel Notification Support

        public virtual async Task OnSaveStartingAsync()
        {
            try
            {
                await Task.Delay(1);
                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Save operation starting");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnSaveStartingAsync), e);
            }
        }

        public virtual async Task OnSaveCompletedAsync(bool success)
        {
            try
            {
                await Task.Delay(1);
                if (success)
                {
                    this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                    this.RaisePropertyChanged(nameof(StatusSummary));
                }
                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Save operation completed ({success})");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnSaveCompletedAsync), e);
            }
        }

        public virtual async Task OnLoadStartingAsync()
        {
            try
            {
                await Task.Delay(1);
                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Load operation starting");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnLoadStartingAsync), e);
            }
        }

        public virtual async Task OnLoadCompletedAsync(bool success)
        {
            try
            {
                if (success)
                {
                    await RefreshFromDataAsync();
                }
                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Load operation completed ({success})");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnLoadCompletedAsync), e);
            }
        }

        public virtual async Task OnNewProjectAsync()
        {
            try
            {
                await ClearAllDataAsync();
                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: New project created");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(OnNewProjectAsync), e);
            }
        }

        #endregion

        #region Command Execution Methods

        private Unit ExecuteAdd()
        {
            try
            {
                OnAdd();
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                this.RaisePropertyChanged(nameof(StatusSummary));
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteAdd), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteDelete(object? item)
        {
            try
            {
                OnDelete(item);
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                this.RaisePropertyChanged(nameof(StatusSummary));
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteDelete), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteClone(object? item)
        {
            try
            {
                OnClone(item);
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
                this.RaisePropertyChanged(nameof(StatusSummary));
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteClone), e);
            }
            return Unit.Default;
        }

        private Unit ExecuteRefresh()
        {
            try
            {
                OnRefresh();
                this.RaisePropertyChanged(nameof(StatusSummary));
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ExecuteRefresh), e);
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

        protected abstract void OnAdd();
        protected abstract void OnDelete(object? item);
        protected abstract void OnClone(object? item);
        protected abstract void OnRefresh();
        protected abstract void ApplyFilter();
        protected abstract ValidationResult ValidateSelectedItem();

        // Virtual methods for tab-specific behavior
        protected virtual ValidationResult ValidateItem(T item)
        {
            // Default implementation - override in derived classes
            return new ValidationResult();
        }

        protected virtual async Task OnPrepareForSaveAsync()
        {
            // Default implementation - override in derived classes if needed
            await Task.Delay(1);
        }

        #endregion

        #region Helper Methods

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
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(UpdateValidationForSelectedItem), e);
            }
        }

        protected void UpdateValidationSummary(ValidationResult result)
        {
            if (result == null) return;

            ValidationSummaryItems.Clear();

            foreach (var error in result.Errors.Take(5))
            {
                ValidationSummaryItems.Add($"• {error}");
            }

            foreach (var warning in result.Warnings.Take(3))
            {
                ValidationSummaryItems.Add($"• {warning}");
            }

            ShowValidationSummary = ValidationSummaryItems.Any();
        }

        protected void RefreshItemsCollection()
        {
            ApplyFilter();
            this.RaisePropertyChanged(nameof(ItemsCountText));
            this.RaisePropertyChanged(nameof(ManagedObjectCount));
            this.RaisePropertyChanged(nameof(StatusSummary));
            this.RaisePropertyChanged(nameof(HasUnsavedChanges));
        }

        protected virtual bool ValidateCanDelete(object? item)
        {
            return item != null;
        }

        protected virtual bool ValidateCanClone(object? item)
        {
            return item != null;
        }

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

        protected void ClearSelection()
        {
            SelectedItem = null;
            ShowValidationSummary = false;
            ValidationSummaryItems.Clear();
        }

        #endregion

        #region Error Recovery

        protected void RecoverFromCollectionError(Exception exception, string operation)
        {
            try
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, $"CollectionError_{operation}", exception);

                ClearSelection();

                if (Items.Any())
                {
                    OnRefresh();
                }

                HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Recovered from error during {operation}. Data refreshed.");
            }
            catch (Exception recoveryException)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(RecoverFromCollectionError), recoveryException);
            }
        }

        protected bool ValidateAndRecoverState()
        {
            try
            {
                if (Items == null)
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Items collection was null, reinitializing");
                    return false;
                }

                if (SelectedItem != null && !Items.Contains(SelectedItem))
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Selection was out of sync, clearing");
                    ClearSelection();
                }

                if (_dataService == null)
                {
                    HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: Data service unavailable");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ValidateAndRecoverState), e);
                return false;
            }
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
                    try
                    {
                        _subscriptions?.Dispose();
                        HammerAndSickle.Services.AppService.CaptureUiMessage($"{TabName}: ViewModel disposed");
                    }
                    catch (Exception e)
                    {
                        HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(Dispose), e);
                    }
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
    }
}