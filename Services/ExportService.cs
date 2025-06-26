using HammerAndSickle.Models;
using HammerAndSickle.Services;
using HammerSickle.UnitCreator.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// ExportService handles all file operations for the Unit Creator application including
    /// project persistence and scenario export. Uses GameDataManager's unified .sce format
    /// for both project files and scenario exports.
    /// 
    /// Key responsibilities:
    /// - Project file save/load operations with backup creation
    /// - Export to .sce format for scenario editor compatibility
    /// - File system path management for desktop application conventions
    /// - Error handling and recovery with detailed user feedback
    /// - Integration with GameDataManager for persistence operations
    /// - Two-phase loading support (deserialization + reference resolution)
    /// </summary>
    public class ExportService
    {
        private const string CLASS_NAME = nameof(ExportService);
        private readonly DataService _dataService;
        private readonly ValidationService _validationService;
        private readonly GameDataManager _gameDataManager;

        // File extensions and paths
        private const string PROJECT_EXTENSION = ".sce";  // GameDataManager uses unified .sce format
        private const string SCENARIO_EXTENSION = ".oob"; // Format for both project and scenario files
        private const string BACKUP_EXTENSION = ".bak";

        // Default directories
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HammerSickle", "UnitCreator");

        private static readonly string DocumentsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HammerSickle", "UnitCreator");

        private static readonly string ProjectsPath = Path.Combine(DocumentsPath, "Projects");
        private static readonly string ExportsPath = Path.Combine(DocumentsPath, "Exports");

        public ExportService(DataService dataService, ValidationService validationService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _gameDataManager = GameDataManager.Instance;

            EnsureDirectoriesExist();
        }

        #region Directory Management

        /// <summary>
        /// Ensures all required directories exist
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            try
            {
                Directory.CreateDirectory(AppDataPath);
                Directory.CreateDirectory(DocumentsPath);
                Directory.CreateDirectory(ProjectsPath);
                Directory.CreateDirectory(ExportsPath);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(EnsureDirectoriesExist), e);
                // Don't throw - app can still function with current directory
            }
        }

        /// <summary>
        /// Gets the default projects directory path
        /// </summary>
        public string GetProjectsDirectory() => ProjectsPath;

        /// <summary>
        /// Gets the default exports directory path
        /// </summary>
        public string GetExportsDirectory() => ExportsPath;

        #endregion

        #region Project File Operations

        /// <summary>
        /// Saves the current project state to a .sce file
        /// </summary>
        /// <param name="filePath">Path where to save the project</param>
        /// <returns>OperationResult indicating success or failure with details</returns>
        public async Task<OperationResult> SaveProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var errorMsg = "Save project failed: No file path provided";
                AppService.CaptureUiMessage(errorMsg);
                return OperationResult.Failed(errorMsg);
            }

            try
            {
                // Ensure .sce extension (GameDataManager unified format)
                var normalizedPath = filePath;
                if (!normalizedPath.EndsWith(PROJECT_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath += PROJECT_EXTENSION;
                }

                // Validate directory exists and is writable
                var directory = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception e)
                    {
                        var errorMsg = $"Cannot create directory '{directory}': {e.Message}";
                        AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                        return OperationResult.Failed(errorMsg, e);
                    }
                }

                // Create backup if file exists
                var backupResult = await CreateBackupAsync(normalizedPath);
                if (!backupResult)
                {
                    AppService.CaptureUiMessage("Warning: Could not create backup file");
                }

                // Validate data before saving
                var validationResult = _validationService.ValidateAllData();
                if (!validationResult.IsValid)
                {
                    var warningMsg = $"Save project warning: {validationResult.Errors.Count} validation errors exist";
                    AppService.CaptureUiMessage(warningMsg);
                    // Continue with save but include warning in result
                }

                // Save using GameDataManager
                bool saveSuccess = await Task.Run(() => _gameDataManager.SaveGameState(normalizedPath));

                if (saveSuccess)
                {
                    var successMsg = $"Project saved successfully: {Path.GetFileName(normalizedPath)}";
                    AppService.CaptureUiMessage(successMsg);

                    // Include validation warnings in success message if any
                    if (validationResult.Warnings.Count > 0)
                    {
                        successMsg += $" (Note: {validationResult.Warnings.Count} validation warnings)";
                    }

                    return OperationResult.Successful(successMsg);
                }
                else
                {
                    var errorMsg = "Save project failed: GameDataManager save operation failed";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                var errorMsg = $"Save project failed: Access denied to '{filePath}'. Check file permissions.";
                AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (DirectoryNotFoundException e)
            {
                var errorMsg = $"Save project failed: Directory not found for '{filePath}'";
                AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (IOException e)
            {
                var errorMsg = $"Save project failed: I/O error while writing to '{filePath}': {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (Exception e)
            {
                var errorMsg = $"Save project failed: Unexpected error - {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        /// <summary>
        /// Loads a project from a .sce file
        /// </summary>
        /// <param name="filePath">Path to the project file to load</param>
        /// <returns>OperationResult indicating success or failure with details</returns>
        public async Task<OperationResult> LoadProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var errorMsg = "Load project failed: No file path provided";
                AppService.CaptureUiMessage(errorMsg);
                return OperationResult.Failed(errorMsg);
            }

            if (!File.Exists(filePath))
            {
                var errorMsg = $"Load project failed: File not found: {filePath}";
                AppService.CaptureUiMessage(errorMsg);
                return OperationResult.Failed(errorMsg);
            }

            try
            {
                // Validate file accessibility
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    var errorMsg = $"Load project failed: File is empty: {Path.GetFileName(filePath)}";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }

                AppService.CaptureUiMessage($"Loading project: {Path.GetFileName(filePath)}");

                // Load using GameDataManager - two-phase loading
                var loadResult = await Task.Run(() =>
                {
                    try
                    {
                        // Phase 1: Load raw data
                        bool loadSuccess = _gameDataManager.LoadGameState(filePath);

                        if (loadSuccess)
                        {
                            // Phase 2: Resolve object references
                            int resolvedCount = _gameDataManager.ResolveAllReferences();
                            return new { Success = true, ResolvedCount = resolvedCount, Error = (Exception?)null };
                        }
                        else
                        {
                            return new { Success = false, ResolvedCount = 0, Error = (Exception?)null };
                        }
                    }
                    catch (Exception e)
                    {
                        return new { Success = false, ResolvedCount = 0, Error = e };
                    }
                });

                if (loadResult.Error != null)
                {
                    var errorMsg = $"Load project failed: {loadResult.Error.Message}";
                    AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), loadResult.Error);
                    return OperationResult.Failed(errorMsg, loadResult.Error);
                }

                if (loadResult.Success)
                {
                    // Sync DataService collections with loaded data
                    _dataService.SyncCollectionsFromManager();

                    // Validate loaded data
                    var validationResult = _validationService.ValidateAllData();

                    var successMsg = $"Project loaded successfully: {Path.GetFileName(filePath)}";
                    successMsg += $" (Resolved {loadResult.ResolvedCount} object references)";

                    if (validationResult.Errors.Count > 0)
                    {
                        successMsg += $" - Warning: {validationResult.Errors.Count} validation errors detected";
                        AppService.CaptureUiMessage(successMsg);

                        // Return success but indicate validation issues
                        return OperationResult.Successful(successMsg);
                    }
                    else if (validationResult.Warnings.Count > 0)
                    {
                        successMsg += $" - Note: {validationResult.Warnings.Count} validation warnings";
                    }

                    AppService.CaptureUiMessage(successMsg);
                    return OperationResult.Successful(successMsg);
                }
                else
                {
                    var errorMsg = "Load project failed: GameDataManager load operation failed";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                var errorMsg = $"Load project failed: Access denied to '{filePath}'. Check file permissions.";
                AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (FileNotFoundException e)
            {
                var errorMsg = $"Load project failed: File not found: {filePath}";
                AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (IOException e)
            {
                var errorMsg = $"Load project failed: I/O error while reading '{filePath}': {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (Exception e)
            {
                var errorMsg = $"Load project failed: Unexpected error - {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        /// <summary>
        /// Creates a new empty project
        /// </summary>
        /// <returns>OperationResult indicating success or failure</returns>
        public async Task<OperationResult> NewProjectAsync()
        {
            try
            {
                AppService.CaptureUiMessage("Creating new project");

                // Clear all existing data
                await Task.Run(() => _dataService.ClearAll());

                var successMsg = "New project created successfully - all data cleared";
                AppService.CaptureUiMessage(successMsg);
                return OperationResult.Successful(successMsg);
            }
            catch (Exception e)
            {
                var errorMsg = $"New project creation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(NewProjectAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        #endregion

        #region Scenario Export Operations

        /// <summary>
        /// Exports current data to .oob format for scenario editor import
        /// </summary>
        /// <param name="filePath">Path where to save the exported scenario</param>
        /// <returns>OperationResult indicating success or failure with details</returns>
        public async Task<OperationResult> ExportScenarioAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var errorMsg = "Export scenario failed: No file path provided";
                AppService.CaptureUiMessage(errorMsg);
                return OperationResult.Failed(errorMsg);
            }

            try
            {
                // Ensure .oob extension
                var normalizedPath = filePath;
                if (!normalizedPath.EndsWith(SCENARIO_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath += SCENARIO_EXTENSION;
                }

                // Comprehensive validation before export
                var validationResult = _validationService.ValidateAllData();
                if (!validationResult.IsValid)
                {
                    var errorMsg = $"Export failed: {validationResult.Errors.Count} validation errors must be fixed before export";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.ValidationFailed(validationResult.Errors);
                }

                if (validationResult.Warnings.Count > 0)
                {
                    AppService.CaptureUiMessage($"Export warning: {validationResult.Warnings.Count} validation warnings exist");
                }

                // Check export readiness
                var readinessResult = ValidateExportReadiness();
                if (!readinessResult)
                {
                    return readinessResult;
                }

                AppService.CaptureUiMessage($"Exporting scenario: {Path.GetFileName(normalizedPath)}");

                // Create backup if file exists
                var backupResult = await CreateBackupAsync(normalizedPath);
                if (!backupResult)
                {
                    AppService.CaptureUiMessage("Warning: Could not create backup file");
                }

                // Export using GameDataManager's direct save method
                bool exportSuccess = await Task.Run(() => _gameDataManager.SaveGameState(normalizedPath));

                if (exportSuccess)
                {
                    var counts = GetExportCounts();
                    var successMsg = $"Scenario exported successfully: {Path.GetFileName(normalizedPath)}";
                    successMsg += $" ({counts.CombatUnits} units, {counts.Leaders} leaders, {counts.WeaponProfiles} weapon profiles, {counts.UnitProfiles} unit profiles)";

                    AppService.CaptureUiMessage(successMsg);
                    return OperationResult.Successful(successMsg);
                }
                else
                {
                    var errorMsg = "Export scenario failed: GameDataManager export operation failed";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                var errorMsg = $"Export scenario failed: Access denied to '{filePath}'. Check file permissions.";
                AppService.HandleException(CLASS_NAME, nameof(ExportScenarioAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (DirectoryNotFoundException e)
            {
                var errorMsg = $"Export scenario failed: Directory not found for '{filePath}'";
                AppService.HandleException(CLASS_NAME, nameof(ExportScenarioAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (IOException e)
            {
                var errorMsg = $"Export scenario failed: I/O error while writing to '{filePath}': {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(ExportScenarioAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
            catch (Exception e)
            {
                var errorMsg = $"Export scenario failed: Unexpected error - {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(ExportScenarioAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        /// <summary>
        /// Validates that the current data is ready for export
        /// </summary>
        /// <returns>OperationResult indicating if export is possible</returns>
        private OperationResult ValidateExportReadiness()
        {
            try
            {
                // Check minimum requirements
                if (_dataService.TotalObjectCount == 0)
                {
                    var errorMsg = "Export failed: No data to export";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }

                if (_dataService.CombatUnits.Count == 0)
                {
                    var errorMsg = "Export failed: No combat units defined";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }

                // Check that units have required profiles
                int unitsWithoutProfiles = 0;
                foreach (var unit in _dataService.CombatUnits)
                {
                    if (unit.DeployedProfile == null || unit.UnitProfile == null)
                    {
                        unitsWithoutProfiles++;
                    }
                }

                if (unitsWithoutProfiles > 0)
                {
                    var errorMsg = $"Export failed: {unitsWithoutProfiles} units missing required profiles";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }

                return OperationResult.Successful("Export readiness validation passed");
            }
            catch (Exception e)
            {
                var errorMsg = $"Export readiness validation failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(ValidateExportReadiness), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        #endregion

        #region Backup Management

        /// <summary>
        /// Creates a backup of an existing file
        /// </summary>
        /// <param name="filePath">Path to the file to backup</param>
        /// <returns>True if backup was created successfully, false otherwise</returns>
        private async Task<bool> CreateBackupAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return true; // No file to backup

            try
            {
                string backupPath = Path.ChangeExtension(filePath, BACKUP_EXTENSION);

                await Task.Run(() =>
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Copy(filePath, backupPath);
                });

                AppService.CaptureUiMessage($"Backup created: {Path.GetFileName(backupPath)}");
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(CreateBackupAsync), e);
                // Non-critical error - continue with save operation
                return false;
            }
        }

        /// <summary>
        /// Restores a file from its backup
        /// </summary>
        /// <param name="filePath">Path to the file to restore</param>
        /// <returns>OperationResult indicating success or failure</returns>
        public async Task<OperationResult> RestoreFromBackupAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return OperationResult.Failed("Restore failed: No file path provided");
            }

            try
            {
                string backupPath = Path.ChangeExtension(filePath, BACKUP_EXTENSION);

                if (!File.Exists(backupPath))
                {
                    var errorMsg = $"Restore failed: No backup found for {Path.GetFileName(filePath)}";
                    AppService.CaptureUiMessage(errorMsg);
                    return OperationResult.Failed(errorMsg);
                }

                await Task.Run(() =>
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    File.Copy(backupPath, filePath);
                });

                var successMsg = $"File restored from backup: {Path.GetFileName(filePath)}";
                AppService.CaptureUiMessage(successMsg);
                return OperationResult.Successful(successMsg);
            }
            catch (Exception e)
            {
                var errorMsg = $"Restore from backup failed: {e.Message}";
                AppService.HandleException(CLASS_NAME, nameof(RestoreFromBackupAsync), e);
                return OperationResult.Failed(errorMsg, e);
            }
        }

        #endregion

        #region File Utilities

        /// <summary>
        /// Gets a safe filename for saving (removes invalid characters)
        /// </summary>
        /// <param name="fileName">Original filename</param>
        /// <returns>Safe filename with invalid characters replaced</returns>
        public string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Unnamed";

            try
            {
                // Remove invalid filename characters
                char[] invalidChars = Path.GetInvalidFileNameChars();
                string safeFileName = fileName;

                foreach (char invalidChar in invalidChars)
                {
                    safeFileName = safeFileName.Replace(invalidChar, '_');
                }

                // Ensure reasonable length
                if (safeFileName.Length > 100)
                {
                    safeFileName = safeFileName.Substring(0, 100);
                }

                return safeFileName;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetSafeFileName), e);
                return "Unnamed";
            }
        }

        /// <summary>
        /// Generates a default project filename with timestamp
        /// </summary>
        /// <returns>Default project filename with current timestamp</returns>
        public string GenerateDefaultProjectName()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                return $"UnitCreator_Project_{timestamp}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GenerateDefaultProjectName), e);
                return "UnitCreator_Project";
            }
        }

        /// <summary>
        /// Generates a default scenario filename with timestamp  
        /// </summary>
        /// <returns>Default scenario filename with current timestamp</returns>
        public string GenerateDefaultScenarioName()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                return $"Scenario_{timestamp}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GenerateDefaultScenarioName), e);
                return "Scenario";
            }
        }

        /// <summary>
        /// Checks if a file exists and is accessible
        /// </summary>
        /// <param name="filePath">Path to check</param>
        /// <returns>True if file exists and has content, false otherwise</returns>
        public bool IsFileAccessible(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(IsFileAccessible), e);
                return false;
            }
        }

        /// <summary>
        /// Gets file information for display
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Formatted file information string</returns>
        public string GetFileInfo(string filePath)
        {
            if (!IsFileAccessible(filePath))
                return "File not accessible";

            try
            {
                var fileInfo = new FileInfo(filePath);
                return $"Size: {fileInfo.Length:N0} bytes, Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetFileInfo), e);
                return "File information unavailable";
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Checks if there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges => _dataService.HasUnsavedChanges;

        /// <summary>
        /// Gets count of objects that would be exported
        /// </summary>
        /// <returns>Tuple with counts of each object type</returns>
        public (int CombatUnits, int Leaders, int WeaponProfiles, int UnitProfiles) GetExportCounts()
        {
            try
            {
                return (
                    _dataService.CombatUnits.Count,
                    _dataService.Leaders.Count,
                    _dataService.WeaponProfiles.Count,
                    _dataService.UnitProfiles.Count
                );
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetExportCounts), e);
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Gets summary information about current project
        /// </summary>
        /// <returns>Formatted project summary string</returns>
        public string GetProjectSummary()
        {
            try
            {
                var counts = GetExportCounts();
                var validationResult = _validationService.ValidateAllData();

                return $"Units: {counts.CombatUnits}, Leaders: {counts.Leaders}, " +
                       $"Weapon Profiles: {counts.WeaponProfiles}, Unit Profiles: {counts.UnitProfiles} " +
                       $"(Errors: {validationResult.Errors.Count}, Warnings: {validationResult.Warnings.Count}) " +
                       $"[Format: .oob export]";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetProjectSummary), e);
                return "Project summary unavailable";
            }
        }

        #endregion
    }
}