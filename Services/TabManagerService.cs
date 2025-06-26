using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HammerAndSickle.Services;
using HammerSickle.UnitCreator.Models;
using HammerSickle.UnitCreator.ViewModels.Base;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// TabManagerService coordinates operations across all registered tab ViewModels in the Unit Creator.
    /// Provides centralized management for tab lifecycle, validation, change tracking, and event coordination.
    /// 
    /// Key responsibilities:
    /// - Tab registration and lifecycle management
    /// - Coordinated validation across all tabs
    /// - Unsaved changes tracking aggregation
    /// - Save/Load/New operation coordination
    /// - Event broadcasting to all tabs
    /// - State management and status reporting
    /// - Error aggregation and reporting
    /// </summary>
    public class TabManagerService : IDisposable
    {
        private const string CLASS_NAME = nameof(TabManagerService);
        private readonly List<ITabViewModel> _registeredTabs = new();
        private readonly object _tabsLock = new object();
        private bool _disposed = false;

        // Operation state tracking
        private bool _isOperationInProgress = false;
        private string _currentOperation = string.Empty;

        public TabManagerService()
        {
            try
            {
                AppService.CaptureUiMessage("TabManagerService initialized");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(TabManagerService), e);
            }
        }

        #region Tab Registration

        /// <summary>
        /// Registers a tab ViewModel for management
        /// </summary>
        /// <param name="tabViewModel">Tab ViewModel to register</param>
        /// <returns>True if registration was successful, false if already registered</returns>
        public bool RegisterTab(ITabViewModel tabViewModel)
        {
            if (tabViewModel == null)
            {
                AppService.CaptureUiMessage("Cannot register null tab ViewModel");
                return false;
            }

            try
            {
                lock (_tabsLock)
                {
                    // Check if already registered
                    if (_registeredTabs.Any(t => t.TabName == tabViewModel.TabName))
                    {
                        AppService.CaptureUiMessage($"Tab '{tabViewModel.TabName}' is already registered");
                        return false;
                    }

                    _registeredTabs.Add(tabViewModel);
                    AppService.CaptureUiMessage($"Tab '{tabViewModel.TabName}' registered successfully");
                    return true;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(RegisterTab), e);
                return false;
            }
        }

        /// <summary>
        /// Unregisters a tab ViewModel
        /// </summary>
        /// <param name="tabViewModel">Tab ViewModel to unregister</param>
        /// <returns>True if unregistration was successful</returns>
        public bool UnregisterTab(ITabViewModel tabViewModel)
        {
            if (tabViewModel == null)
                return false;

            try
            {
                lock (_tabsLock)
                {
                    bool removed = _registeredTabs.Remove(tabViewModel);
                    if (removed)
                    {
                        AppService.CaptureUiMessage($"Tab '{tabViewModel.TabName}' unregistered successfully");
                    }
                    return removed;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(UnregisterTab), e);
                return false;
            }
        }

        /// <summary>
        /// Unregisters a tab by name
        /// </summary>
        /// <param name="tabName">Name of the tab to unregister</param>
        /// <returns>True if unregistration was successful</returns>
        public bool UnregisterTab(string tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return false;

            try
            {
                lock (_tabsLock)
                {
                    var tab = _registeredTabs.FirstOrDefault(t => t.TabName == tabName);
                    if (tab != null)
                    {
                        return UnregisterTab(tab);
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(UnregisterTab), e);
                return false;
            }
        }

        /// <summary>
        /// Gets all registered tab names
        /// </summary>
        /// <returns>List of registered tab names</returns>
        public List<string> GetRegisteredTabNames()
        {
            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.Select(t => t.TabName).ToList();
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetRegisteredTabNames), e);
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets a registered tab by name
        /// </summary>
        /// <param name="tabName">Name of the tab to retrieve</param>
        /// <returns>Tab ViewModel or null if not found</returns>
        public ITabViewModel? GetTab(string tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return null;

            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.FirstOrDefault(t => t.TabName == tabName);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetTab), e);
                return null;
            }
        }

        #endregion

        #region Change Tracking

        /// <summary>
        /// Checks if any registered tab has unsaved changes
        /// </summary>
        /// <returns>True if any tab has unsaved changes</returns>
        public bool HasAnyUnsavedChanges()
        {
            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.Any(tab => tab.HasUnsavedChanges);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(HasAnyUnsavedChanges), e);
                return false; // Safe default - assume no changes if error
            }
        }

        /// <summary>
        /// Gets a list of tab names that have unsaved changes
        /// </summary>
        /// <returns>List of tab names with unsaved changes</returns>
        public List<string> GetTabsWithUnsavedChanges()
        {
            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs
                        .Where(tab => tab.HasUnsavedChanges)
                        .Select(tab => tab.TabName)
                        .ToList();
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetTabsWithUnsavedChanges), e);
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets the total count of managed objects across all tabs
        /// </summary>
        /// <returns>Total object count</returns>
        public int GetTotalManagedObjectCount()
        {
            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.Sum(tab => tab.ManagedObjectCount);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetTotalManagedObjectCount), e);
                return 0;
            }
        }

        /// <summary>
        /// Gets a summary of all tab states
        /// </summary>
        /// <returns>Formatted status summary</returns>
        public string GetOverallStatusSummary()
        {
            try
            {
                lock (_tabsLock)
                {
                    if (!_registeredTabs.Any())
                    {
                        return "No tabs registered";
                    }

                    var totalObjects = GetTotalManagedObjectCount();
                    var tabsWithChanges = GetTabsWithUnsavedChanges();
                    var changesText = tabsWithChanges.Any()
                        ? $"{tabsWithChanges.Count} tab(s) with changes"
                        : "No unsaved changes";

                    return $"{_registeredTabs.Count} tabs, {totalObjects} objects, {changesText}";
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetOverallStatusSummary), e);
                return "Status unavailable";
            }
        }

        #endregion

        #region Validation Operations

        /// <summary>
        /// Validates all registered tabs
        /// </summary>
        /// <returns>OperationResult with aggregated validation results</returns>
        public async Task<OperationResult> ValidateAllTabsAsync()
        {
            if (_isOperationInProgress)
            {
                return OperationResult.Failed("Cannot validate: Another operation is in progress");
            }

            try
            {
                _isOperationInProgress = true;
                _currentOperation = "Validation";

                List<ITabViewModel> tabsToValidate;
                lock (_tabsLock)
                {
                    tabsToValidate = _registeredTabs.ToList();
                }

                if (!tabsToValidate.Any())
                {
                    return OperationResult.Successful("No tabs to validate");
                }

                AppService.CaptureUiMessage($"Validating {tabsToValidate.Count} tab(s)...");

                var allErrors = new List<string>();
                var allWarnings = new List<string>();
                int successCount = 0;
                int errorCount = 0;

                foreach (var tab in tabsToValidate)
                {
                    try
                    {
                        var result = await tab.ValidateAsync();

                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            allErrors.Add($"{tab.TabName}: {result.Message}");
                        }

                        // Note: Individual validation results might not have warnings collection
                        // This is a simplified approach - can be enhanced later
                    }
                    catch (Exception e)
                    {
                        errorCount++;
                        allErrors.Add($"{tab.TabName}: Validation failed with exception - {e.Message}");
                        AppService.HandleException(CLASS_NAME, $"ValidateTab_{tab.TabName}", e);
                    }
                }

                var summary = $"Validation complete: {successCount} passed, {errorCount} failed";
                AppService.CaptureUiMessage(summary);

                if (allErrors.Any())
                {
                    return OperationResult.ValidationFailed(allErrors);
                }

                return OperationResult.Successful(summary);
            }
            catch (Exception e)
            {
                var errorMsg = $"Tab validation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(ValidateAllTabsAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            finally
            {
                _isOperationInProgress = false;
                _currentOperation = string.Empty;
            }
        }

        /// <summary>
        /// Gets whether all tabs are in a valid state (cached validation results)
        /// </summary>
        /// <returns>True if all tabs are valid</returns>
        public bool AreAllTabsValid()
        {
            try
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.All(tab => tab.IsInValidState);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(AreAllTabsValid), e);
                return false; // Safe default
            }
        }

        #endregion

        #region Lifecycle Operations

        /// <summary>
        /// Refreshes all tabs from their data sources (called after load operations)
        /// </summary>
        /// <returns>OperationResult indicating success or failure</returns>
        public async Task<OperationResult> RefreshAllTabsAsync()
        {
            if (_isOperationInProgress)
            {
                return OperationResult.Failed("Cannot refresh: Another operation is in progress");
            }

            try
            {
                _isOperationInProgress = true;
                _currentOperation = "Refresh";

                List<ITabViewModel> tabsToRefresh;
                lock (_tabsLock)
                {
                    tabsToRefresh = _registeredTabs.ToList();
                }

                if (!tabsToRefresh.Any())
                {
                    return OperationResult.Successful("No tabs to refresh");
                }

                AppService.CaptureUiMessage($"Refreshing {tabsToRefresh.Count} tab(s)...");

                var errors = new List<string>();
                int successCount = 0;

                foreach (var tab in tabsToRefresh)
                {
                    try
                    {
                        var result = await tab.RefreshFromDataAsync();

                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{tab.TabName}: {result.Message}");
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add($"{tab.TabName}: Refresh failed with exception - {e.Message}");
                        AppService.HandleException(CLASS_NAME, $"RefreshTab_{tab.TabName}", e);
                    }
                }

                var summary = $"Refresh complete: {successCount} succeeded";
                if (errors.Any())
                {
                    summary += $", {errors.Count} failed";
                }

                AppService.CaptureUiMessage(summary);

                return errors.Any()
                    ? OperationResult.Failed($"Some tabs failed to refresh: {string.Join("; ", errors)}")
                    : OperationResult.Successful(summary);
            }
            catch (Exception e)
            {
                var errorMsg = $"Tab refresh failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(RefreshAllTabsAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            finally
            {
                _isOperationInProgress = false;
                _currentOperation = string.Empty;
            }
        }

        /// <summary>
        /// Prepares all tabs for save operations
        /// </summary>
        /// <returns>OperationResult indicating readiness for save</returns>
        public async Task<OperationResult> PrepareAllTabsForSaveAsync()
        {
            if (_isOperationInProgress)
            {
                return OperationResult.Failed("Cannot prepare for save: Another operation is in progress");
            }

            try
            {
                _isOperationInProgress = true;
                _currentOperation = "Prepare for Save";

                List<ITabViewModel> tabsToPrepar;
                lock (_tabsLock)
                {
                    tabsToPrepar = _registeredTabs.ToList();
                }

                if (!tabsToPrepar.Any())
                {
                    return OperationResult.Successful("No tabs to prepare for save");
                }

                AppService.CaptureUiMessage($"Preparing {tabsToPrepar.Count} tab(s) for save...");

                var errors = new List<string>();
                int successCount = 0;

                foreach (var tab in tabsToPrepar)
                {
                    try
                    {
                        var result = await tab.PrepareForSaveAsync();

                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{tab.TabName}: {result.Message}");
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add($"{tab.TabName}: Save preparation failed - {e.Message}");
                        AppService.HandleException(CLASS_NAME, $"PrepareTabForSave_{tab.TabName}", e);
                    }
                }

                var summary = $"Save preparation complete: {successCount} ready";
                if (errors.Any())
                {
                    summary += $", {errors.Count} failed";
                    AppService.CaptureUiMessage(summary);
                    return OperationResult.Failed($"Cannot save: {string.Join("; ", errors)}");
                }

                AppService.CaptureUiMessage(summary);
                return OperationResult.Successful(summary);
            }
            catch (Exception e)
            {
                var errorMsg = $"Save preparation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(PrepareAllTabsForSaveAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            finally
            {
                _isOperationInProgress = false;
                _currentOperation = string.Empty;
            }
        }

        /// <summary>
        /// Prepares all tabs for close operations (checks unsaved changes)
        /// </summary>
        /// <returns>OperationResult indicating whether close can proceed</returns>
        public async Task<OperationResult> PrepareAllTabsForCloseAsync()
        {
            try
            {
                List<ITabViewModel> tabsToCheck;
                lock (_tabsLock)
                {
                    tabsToCheck = _registeredTabs.ToList();
                }

                if (!tabsToCheck.Any())
                {
                    return OperationResult.Successful("No tabs to check for close");
                }

                var tabsWithChanges = tabsToCheck.Where(t => t.HasUnsavedChanges).ToList();
                if (!tabsWithChanges.Any())
                {
                    return OperationResult.Successful("All tabs ready to close - no unsaved changes");
                }

                // Check if any tabs cannot be closed
                var cannotCloseTabs = new List<string>();
                foreach (var tab in tabsWithChanges)
                {
                    try
                    {
                        var result = await tab.PrepareForCloseAsync();
                        if (!result.Success)
                        {
                            cannotCloseTabs.Add($"{tab.TabName}: {result.Message}");
                        }
                    }
                    catch (Exception e)
                    {
                        cannotCloseTabs.Add($"{tab.TabName}: Close preparation failed - {e.Message}");
                        AppService.HandleException(CLASS_NAME, $"PrepareTabForClose_{tab.TabName}", e);
                    }
                }

                if (cannotCloseTabs.Any())
                {
                    return OperationResult.Failed($"Cannot close: {string.Join("; ", cannotCloseTabs)}");
                }

                var changesList = tabsWithChanges.Select(t => t.TabName).ToList();
                return OperationResult.Failed($"Unsaved changes in: {string.Join(", ", changesList)}");
            }
            catch (Exception e)
            {
                var errorMsg = $"Close preparation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(PrepareAllTabsForCloseAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        /// <summary>
        /// Clears all data in all tabs (for new project operations)
        /// </summary>
        /// <returns>OperationResult indicating success or failure</returns>
        public async Task<OperationResult> ClearAllTabsAsync()
        {
            if (_isOperationInProgress)
            {
                return OperationResult.Failed("Cannot clear tabs: Another operation is in progress");
            }

            try
            {
                _isOperationInProgress = true;
                _currentOperation = "Clear All Data";

                List<ITabViewModel> tabsToClear;
                lock (_tabsLock)
                {
                    tabsToClear = _registeredTabs.ToList();
                }

                if (!tabsToClear.Any())
                {
                    return OperationResult.Successful("No tabs to clear");
                }

                AppService.CaptureUiMessage($"Clearing data in {tabsToClear.Count} tab(s)...");

                var errors = new List<string>();
                int successCount = 0;

                foreach (var tab in tabsToClear)
                {
                    try
                    {
                        var result = await tab.ClearAllDataAsync();

                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{tab.TabName}: {result.Message}");
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add($"{tab.TabName}: Clear failed - {e.Message}");
                        AppService.HandleException(CLASS_NAME, $"ClearTab_{tab.TabName}", e);
                    }
                }

                var summary = $"Clear operation complete: {successCount} cleared";
                if (errors.Any())
                {
                    summary += $", {errors.Count} failed";
                }

                AppService.CaptureUiMessage(summary);

                return errors.Any()
                    ? OperationResult.Failed($"Some tabs failed to clear: {string.Join("; ", errors)}")
                    : OperationResult.Successful(summary);
            }
            catch (Exception e)
            {
                var errorMsg = $"Tab clear operation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(ClearAllTabsAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            finally
            {
                _isOperationInProgress = false;
                _currentOperation = string.Empty;
            }
        }

        #endregion

        #region Event Broadcasting

        /// <summary>
        /// Notifies all tabs that a save operation is starting
        /// </summary>
        public async Task NotifyAllTabsSaveStartingAsync()
        {
            await BroadcastNotificationAsync("Save Starting", tab => tab.OnSaveStartingAsync());
        }

        /// <summary>
        /// Notifies all tabs that a save operation completed
        /// </summary>
        /// <param name="success">Whether the save was successful</param>
        public async Task NotifyAllTabsSaveCompletedAsync(bool success)
        {
            await BroadcastNotificationAsync($"Save Completed ({success})", tab => tab.OnSaveCompletedAsync(success));
        }

        /// <summary>
        /// Notifies all tabs that a load operation is starting
        /// </summary>
        public async Task NotifyAllTabsLoadStartingAsync()
        {
            await BroadcastNotificationAsync("Load Starting", tab => tab.OnLoadStartingAsync());
        }

        /// <summary>
        /// Notifies all tabs that a load operation completed
        /// </summary>
        /// <param name="success">Whether the load was successful</param>
        public async Task NotifyAllTabsLoadCompletedAsync(bool success)
        {
            await BroadcastNotificationAsync($"Load Completed ({success})", tab => tab.OnLoadCompletedAsync(success));
        }

        /// <summary>
        /// Notifies all tabs that a new project is being created
        /// </summary>
        public async Task NotifyAllTabsNewProjectAsync()
        {
            await BroadcastNotificationAsync("New Project", tab => tab.OnNewProjectAsync());
        }

        /// <summary>
        /// Broadcasts a notification to all registered tabs
        /// </summary>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="notificationAction">Action to perform on each tab</param>
        private async Task BroadcastNotificationAsync(string operationName, Func<ITabViewModel, Task> notificationAction)
        {
            try
            {
                List<ITabViewModel> tabsToNotify;
                lock (_tabsLock)
                {
                    tabsToNotify = _registeredTabs.ToList();
                }

                if (!tabsToNotify.Any())
                {
                    return;
                }

                AppService.CaptureUiMessage($"Broadcasting '{operationName}' to {tabsToNotify.Count} tab(s)");

                var notificationTasks = tabsToNotify.Select(async tab =>
                {
                    try
                    {
                        await notificationAction(tab);
                    }
                    catch (Exception e)
                    {
                        AppService.HandleException(CLASS_NAME, $"Notify_{tab.TabName}_{operationName}", e);
                    }
                });

                await Task.WhenAll(notificationTasks);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(BroadcastNotificationAsync), e);
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Gets whether any operation is currently in progress
        /// </summary>
        public bool IsOperationInProgress => _isOperationInProgress;

        /// <summary>
        /// Gets the name of the current operation, if any
        /// </summary>
        public string CurrentOperation => _currentOperation;

        /// <summary>
        /// Gets the count of registered tabs
        /// </summary>
        public int RegisteredTabCount
        {
            get
            {
                lock (_tabsLock)
                {
                    return _registeredTabs.Count;
                }
            }
        }

        /// <summary>
        /// Gets whether all tabs can be safely closed
        /// </summary>
        public bool CanCloseAllTabs
        {
            get
            {
                try
                {
                    lock (_tabsLock)
                    {
                        return _registeredTabs.All(tab => tab.CanClose);
                    }
                }
                catch (Exception e)
                {
                    AppService.HandleException(CLASS_NAME, nameof(CanCloseAllTabs), e);
                    return false; // Safe default
                }
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Performs application-defined tasks associated with freeing resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        lock (_tabsLock)
                        {
                            _registeredTabs.Clear();
                        }
                        AppService.CaptureUiMessage("TabManagerService disposed");
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