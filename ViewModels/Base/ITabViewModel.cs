using System.Threading.Tasks;
using HammerSickle.UnitCreator.Models;

namespace HammerSickle.UnitCreator.ViewModels.Base
{
    /// <summary>
    /// Interface for tab ViewModels in the Unit Creator application.
    /// Provides a contract for tab lifecycle management, validation, and data operations
    /// to support the extensible tab system and unified file operations.
    /// 
    /// Key responsibilities:
    /// - Tab identification and metadata
    /// - Unsaved changes tracking for file operation workflows
    /// - Data validation for save operations
    /// - Data refresh after load operations
    /// - Pre-save preparation and cleanup
    /// - Tab-specific state management
    /// </summary>
    public interface ITabViewModel
    {
        #region Tab Identification

        /// <summary>
        /// Gets the display name for this tab
        /// </summary>
        string TabName { get; }

        /// <summary>
        /// Gets whether this tab is currently active/selected
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Gets whether this tab is enabled for user interaction
        /// </summary>
        bool IsEnabled { get; set; }

        #endregion

        #region Change Tracking

        /// <summary>
        /// Gets whether this tab has unsaved changes that would be lost on close/new/load operations
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Gets the number of objects this tab is managing (for progress/status display)
        /// </summary>
        int ManagedObjectCount { get; }

        /// <summary>
        /// Gets a summary of this tab's current state for status display
        /// </summary>
        string StatusSummary { get; }

        #endregion

        #region Lifecycle Operations

        /// <summary>
        /// Validates all data in this tab
        /// </summary>
        /// <returns>OperationResult indicating validation success or failure with error details</returns>
        Task<OperationResult> ValidateAsync();

        /// <summary>
        /// Refreshes this tab's data from the underlying data service (called after load operations)
        /// </summary>
        /// <returns>OperationResult indicating refresh success or failure</returns>
        Task<OperationResult> RefreshFromDataAsync();

        /// <summary>
        /// Prepares this tab for save operations (validation, cleanup, final data sync)
        /// </summary>
        /// <returns>OperationResult indicating preparation success or failure</returns>
        Task<OperationResult> PrepareForSaveAsync();

        /// <summary>
        /// Prepares this tab for close/new/load operations (confirm unsaved changes, cleanup)
        /// </summary>
        /// <returns>OperationResult indicating whether the tab is ready to close</returns>
        Task<OperationResult> PrepareForCloseAsync();

        /// <summary>
        /// Clears all data in this tab (called during new project operations)
        /// </summary>
        /// <returns>OperationResult indicating clear success or failure</returns>
        Task<OperationResult> ClearAllDataAsync();

        #endregion

        #region Optional Advanced Operations

        /// <summary>
        /// Gets whether this tab supports import operations
        /// </summary>
        bool SupportsImport { get; }

        /// <summary>
        /// Gets whether this tab supports export operations
        /// </summary>
        bool SupportsExport { get; }

        /// <summary>
        /// Performs tab-specific import operations (optional, return success if not implemented)
        /// </summary>
        /// <param name="importPath">Path to import from</param>
        /// <returns>OperationResult indicating import success or failure</returns>
        Task<OperationResult> ImportDataAsync(string importPath);

        /// <summary>
        /// Performs tab-specific export operations (optional, return success if not implemented)
        /// </summary>
        /// <param name="exportPath">Path to export to</param>
        /// <returns>OperationResult indicating export success or failure</returns>
        Task<OperationResult> ExportDataAsync(string exportPath);

        #endregion

        #region Notification Support

        /// <summary>
        /// Notifies this tab that the application is about to save
        /// </summary>
        /// <returns>Task for async notification handling</returns>
        Task OnSaveStartingAsync();

        /// <summary>
        /// Notifies this tab that a save operation completed
        /// </summary>
        /// <param name="success">Whether the save operation was successful</param>
        /// <returns>Task for async notification handling</returns>
        Task OnSaveCompletedAsync(bool success);

        /// <summary>
        /// Notifies this tab that the application is about to load
        /// </summary>
        /// <returns>Task for async notification handling</returns>
        Task OnLoadStartingAsync();

        /// <summary>
        /// Notifies this tab that a load operation completed
        /// </summary>
        /// <param name="success">Whether the load operation was successful</param>
        /// <returns>Task for async notification handling</returns>
        Task OnLoadCompletedAsync(bool success);

        /// <summary>
        /// Notifies this tab that a new project is being created
        /// </summary>
        /// <returns>Task for async notification handling</returns>
        Task OnNewProjectAsync();

        #endregion

        #region State Queries

        /// <summary>
        /// Gets whether this tab is in a valid state (no critical errors)
        /// </summary>
        bool IsInValidState { get; }

        /// <summary>
        /// Gets whether this tab is busy with an operation
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Gets the last validation result for this tab (for caching/performance)
        /// </summary>
        OperationResult? LastValidationResult { get; }

        /// <summary>
        /// Gets whether this tab can be safely closed (no critical unsaved data)
        /// </summary>
        bool CanClose { get; }

        #endregion
    }
}