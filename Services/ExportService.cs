using HammerAndSickle.Models;
using HammerAndSickle.Services;
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
        public async Task<bool> SaveProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AppService.CaptureUiMessage("Save project failed: No file path provided");
                return false;
            }

            try
            {
                // Ensure .sce extension (GameDataManager unified format)
                if (!filePath.EndsWith(PROJECT_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    filePath += PROJECT_EXTENSION;
                }

                // Create backup if file exists
                await CreateBackupAsync(filePath);

                // Validate data before saving
                var validationResult = _validationService.ValidateAllData();
                if (!validationResult.IsValid)
                {
                    AppService.CaptureUiMessage($"Save project warning: {validationResult.Errors.Count} validation errors exist");
                    // Continue with save but warn user
                }

                // Save using GameDataManager
                bool success = await Task.Run(() => _gameDataManager.SaveGameState(filePath));

                if (success)
                {
                    AppService.CaptureUiMessage($"Project saved successfully: {Path.GetFileName(filePath)}");
                    return true;
                }
                else
                {
                    AppService.CaptureUiMessage("Save project failed: GameDataManager save operation failed");
                    return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SaveProjectAsync), e);
                AppService.CaptureUiMessage($"Save project failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a project from a .sce file
        /// </summary>
        public async Task<bool> LoadProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AppService.CaptureUiMessage("Load project failed: No file path provided");
                return false;
            }

            if (!File.Exists(filePath))
            {
                AppService.CaptureUiMessage($"Load project failed: File not found: {filePath}");
                return false;
            }

            try
            {
                AppService.CaptureUiMessage($"Loading project: {Path.GetFileName(filePath)}");

                // Load using GameDataManager - two-phase loading
                bool success = await Task.Run(() =>
                {
                    // Phase 1: Load raw data
                    bool loadResult = _gameDataManager.LoadGameState(filePath);

                    if (loadResult)
                    {
                        // Phase 2: Resolve object references
                        int resolvedCount = _gameDataManager.ResolveAllReferences();
                        AppService.CaptureUiMessage($"Resolved {resolvedCount} object references");
                    }

                    return loadResult;
                });

                if (success)
                {
                    // Sync DataService collections with loaded data
                    _dataService.SyncCollectionsFromManager();

                    // Validate loaded data
                    var validationResult = _validationService.ValidateAllData();
                    if (validationResult.Errors.Count > 0)
                    {
                        AppService.CaptureUiMessage($"Project loaded with {validationResult.Errors.Count} validation errors");
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Project loaded successfully");
                    }

                    return true;
                }
                else
                {
                    AppService.CaptureUiMessage("Load project failed: GameDataManager load operation failed");
                    return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(LoadProjectAsync), e);
                AppService.CaptureUiMessage($"Load project failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new empty project
        /// </summary>
        public async Task<bool> NewProjectAsync()
        {
            try
            {
                AppService.CaptureUiMessage("Creating new project");

                // Clear all existing data
                await Task.Run(() => _dataService.ClearAll());

                AppService.CaptureUiMessage("New project created successfully");
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(NewProjectAsync), e);
                AppService.CaptureUiMessage($"New project creation failed: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Scenario Export Operations

        /// <summary>
        /// Exports current data to .oob format for scenario editor import
        /// </summary>
        public async Task<bool> ExportScenarioAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AppService.CaptureUiMessage("Export scenario failed: No file path provided");
                return false;
            }

            try
            {
                // Ensure .oob extension
                if (!filePath.EndsWith(SCENARIO_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    filePath += SCENARIO_EXTENSION;
                }

                // Comprehensive validation before export
                var validationResult = _validationService.ValidateAllData();
                if (!validationResult.IsValid)
                {
                    AppService.CaptureUiMessage($"Export failed: {validationResult.Errors.Count} validation errors must be fixed before export");
                    return false;
                }

                if (validationResult.Warnings.Count > 0)
                {
                    AppService.CaptureUiMessage($"Export warning: {validationResult.Warnings.Count} validation warnings exist");
                }

                // Check export readiness
                if (!ValidateExportReadiness())
                {
                    return false;
                }

                AppService.CaptureUiMessage($"Exporting scenario: {Path.GetFileName(filePath)}");

                // Create backup if file exists
                await CreateBackupAsync(filePath);

                // Export using GameDataManager's direct save method
                bool success = await Task.Run(() => _gameDataManager.SaveGameState(filePath));

                if (success)
                {
                    AppService.CaptureUiMessage($"Scenario exported successfully: {Path.GetFileName(filePath)}");
                    return true;
                }
                else
                {
                    AppService.CaptureUiMessage("Export scenario failed: GameDataManager export operation failed");
                    return false;
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ExportScenarioAsync), e);
                AppService.CaptureUiMessage($"Export scenario failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that the current data is ready for export
        /// </summary>
        private bool ValidateExportReadiness()
        {
            try
            {
                // Check minimum requirements
                if (_dataService.TotalObjectCount == 0)
                {
                    AppService.CaptureUiMessage("Export failed: No data to export");
                    return false;
                }

                if (_dataService.CombatUnits.Count == 0)
                {
                    AppService.CaptureUiMessage("Export failed: No combat units defined");
                    return false;
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
                    AppService.CaptureUiMessage($"Export failed: {unitsWithoutProfiles} units missing required profiles");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ValidateExportReadiness), e);
                return false;
            }
        }

        #endregion

        #region Backup Management

        /// <summary>
        /// Creates a backup of an existing file
        /// </summary>
        private async Task CreateBackupAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return;

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
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(CreateBackupAsync), e);
                // Non-critical error - continue with save operation
            }
        }

        /// <summary>
        /// Restores a file from its backup
        /// </summary>
        public async Task<bool> RestoreFromBackupAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                string backupPath = Path.ChangeExtension(filePath, BACKUP_EXTENSION);

                if (!File.Exists(backupPath))
                {
                    AppService.CaptureUiMessage($"Restore failed: No backup found for {Path.GetFileName(filePath)}");
                    return false;
                }

                await Task.Run(() =>
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    File.Copy(backupPath, filePath);
                });

                AppService.CaptureUiMessage($"File restored from backup: {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(RestoreFromBackupAsync), e);
                AppService.CaptureUiMessage($"Restore from backup failed: {e.Message}");
                return false;
            }
        }

        #endregion

        #region File Utilities

        /// <summary>
        /// Gets a safe filename for saving (removes invalid characters)
        /// </summary>
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