using HammerAndSickle.Services;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// ProjectStateService manages the current project state including file paths, 
    /// unsaved changes tracking, and project lifecycle events for the Unit Creator application.
    /// 
    /// Key responsibilities:
    /// - Track current project file path for Save vs Save As operations
    /// - Manage project name for window title display
    /// - Monitor unsaved changes status across the application
    /// - Provide reactive notifications for UI binding and updates
    /// - Handle new project creation and project clearing operations
    /// </summary>
    public class ProjectStateService : IDisposable
    {
        private const string CLASS_NAME = nameof(ProjectStateService);
        private const string NEW_PROJECT_NAME = "Untitled Project";
        private const string DEFAULT_PROJECT_EXTENSION = ".sce";

        private readonly DataService _dataService;
        private string? _currentProjectPath;
        private bool _isNewProject = true;
        private bool _disposed = false;

        // Reactive subjects for change notifications
        private readonly BehaviorSubject<string?> _currentProjectPathSubject = new(null);
        private readonly BehaviorSubject<bool> _isNewProjectSubject = new(true);
        private readonly BehaviorSubject<bool> _hasUnsavedChangesSubject = new(false);

        public ProjectStateService(DataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            try
            {
                // Initialize as a new project
                MarkAsNewProject();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ProjectStateService), e);
                throw;
            }
        }

        #region Public Properties

        /// <summary>
        /// Gets the current project file path, or null if this is a new unsaved project
        /// </summary>
        public string? CurrentProjectPath
        {
            get => _currentProjectPath;
            private set
            {
                if (_currentProjectPath != value)
                {
                    _currentProjectPath = value;
                    _currentProjectPathSubject.OnNext(value);
                }
            }
        }

        /// <summary>
        /// Gets the current project name for display purposes
        /// </summary>
        public string CurrentProjectName
        {
            get
            {
                try
                {
                    if (IsNewProject || string.IsNullOrWhiteSpace(CurrentProjectPath))
                    {
                        return NEW_PROJECT_NAME;
                    }

                    return Path.GetFileNameWithoutExtension(CurrentProjectPath) ?? NEW_PROJECT_NAME;
                }
                catch (Exception e)
                {
                    AppService.HandleException(CLASS_NAME, nameof(CurrentProjectName), e);
                    return NEW_PROJECT_NAME;
                }
            }
        }

        /// <summary>
        /// Gets whether there are unsaved changes in the current project
        /// </summary>
        public bool HasUnsavedChanges
        {
            get
            {
                try
                {
                    // For new projects, consider them to have unsaved changes if there's any data
                    if (IsNewProject)
                    {
                        return _dataService.TotalObjectCount > 0;
                    }

                    // For existing projects, check the data service's unsaved changes flag
                    return _dataService.HasUnsavedChanges;
                }
                catch (Exception e)
                {
                    AppService.HandleException(CLASS_NAME, nameof(HasUnsavedChanges), e);
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets whether this is a new project that hasn't been saved yet
        /// </summary>
        public bool IsNewProject
        {
            get => _isNewProject;
            private set
            {
                if (_isNewProject != value)
                {
                    _isNewProject = value;
                    _isNewProjectSubject.OnNext(value);
                }
            }
        }

        /// <summary>
        /// Gets whether the current project has a valid file path for saving
        /// </summary>
        public bool CanSaveDirectly => !IsNewProject && !string.IsNullOrWhiteSpace(CurrentProjectPath);

        /// <summary>
        /// Gets the project directory path, or null if this is a new project
        /// </summary>
        public string? ProjectDirectory
        {
            get
            {
                try
                {
                    return string.IsNullOrWhiteSpace(CurrentProjectPath)
                        ? null
                        : Path.GetDirectoryName(CurrentProjectPath);
                }
                catch (Exception e)
                {
                    AppService.HandleException(CLASS_NAME, nameof(ProjectDirectory), e);
                    return null;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the current project to an existing file path
        /// </summary>
        /// <param name="filePath">Path to the project file</param>
        public void SetCurrentProject(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AppService.CaptureUiMessage("Cannot set project path: path is null or empty");
                return;
            }

            try
            {
                // Normalize the file path
                var normalizedPath = Path.GetFullPath(filePath);

                // Ensure the file has the correct extension
                if (!normalizedPath.EndsWith(DEFAULT_PROJECT_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedPath = Path.ChangeExtension(normalizedPath, DEFAULT_PROJECT_EXTENSION);
                }

                CurrentProjectPath = normalizedPath;
                IsNewProject = false;

                AppService.CaptureUiMessage($"Project set to: {CurrentProjectName}");
                NotifyStateChanged();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SetCurrentProject), e);
                AppService.CaptureUiMessage($"Failed to set project path: {e.Message}");
            }
        }

        /// <summary>
        /// Marks the current state as a new project
        /// </summary>
        public void MarkAsNewProject()
        {
            try
            {
                CurrentProjectPath = null;
                IsNewProject = true;

                AppService.CaptureUiMessage("New project created");
                NotifyStateChanged();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(MarkAsNewProject), e);
            }
        }

        /// <summary>
        /// Clears the current project state
        /// </summary>
        public void ClearCurrentProject()
        {
            try
            {
                CurrentProjectPath = null;
                IsNewProject = true;

                AppService.CaptureUiMessage("Project cleared");
                NotifyStateChanged();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ClearCurrentProject), e);
            }
        }

        /// <summary>
        /// Generates a suggested filename for Save As operations
        /// </summary>
        /// <returns>Suggested filename with timestamp if it's a new project</returns>
        public string GenerateSuggestedFileName()
        {
            try
            {
                if (!IsNewProject && !string.IsNullOrWhiteSpace(CurrentProjectPath))
                {
                    return Path.GetFileName(CurrentProjectPath);
                }

                // Generate timestamped name for new projects
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                return $"UnitCreator_Project_{timestamp}{DEFAULT_PROJECT_EXTENSION}";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GenerateSuggestedFileName), e);
                return $"UnitCreator_Project{DEFAULT_PROJECT_EXTENSION}";
            }
        }

        /// <summary>
        /// Forces a refresh of the unsaved changes status
        /// </summary>
        public void RefreshUnsavedChangesStatus()
        {
            try
            {
                NotifyStateChanged();
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(RefreshUnsavedChangesStatus), e);
            }
        }

        /// <summary>
        /// Gets a display-friendly project status summary
        /// </summary>
        /// <returns>Status text for UI display</returns>
        public string GetProjectStatusSummary()
        {
            try
            {
                var status = IsNewProject ? "New Project" : $"Project: {CurrentProjectName}";
                var changeStatus = HasUnsavedChanges ? " (Modified)" : " (Saved)";
                var objectCount = $" - {_dataService.TotalObjectCount} objects";

                return status + changeStatus + objectCount;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GetProjectStatusSummary), e);
                return "Project status unavailable";
            }
        }

        /// <summary>
        /// Validates whether the current project state is suitable for saving
        /// </summary>
        /// <returns>True if the project can be saved, false otherwise</returns>
        public bool CanSaveProject()
        {
            try
            {
                // Check if there's any data to save
                if (_dataService.TotalObjectCount == 0)
                {
                    return false;
                }

                // For new projects, can always save (will prompt for location)
                if (IsNewProject)
                {
                    return true;
                }

                // For existing projects, ensure the path is valid
                return !string.IsNullOrWhiteSpace(CurrentProjectPath);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(CanSaveProject), e);
                return false;
            }
        }

        #endregion

        #region Observable Properties for Reactive UI

        /// <summary>
        /// Observable stream of current project path changes
        /// </summary>
        public IObservable<string?> CurrentProjectPathChanges => _currentProjectPathSubject.AsObservable();

        /// <summary>
        /// Observable stream of new project status changes
        /// </summary>
        public IObservable<bool> IsNewProjectChanges => _isNewProjectSubject.AsObservable();

        /// <summary>
        /// Observable stream of unsaved changes status
        /// </summary>
        public IObservable<bool> HasUnsavedChangesChanges => _hasUnsavedChangesSubject.AsObservable();

        #endregion

        #region Private Methods

        /// <summary>
        /// Notifies all reactive subscribers of state changes
        /// </summary>
        private void NotifyStateChanged()
        {
            try
            {
                _hasUnsavedChangesSubject.OnNext(HasUnsavedChanges);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(NotifyStateChanged), e);
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
                        _currentProjectPathSubject?.Dispose();
                        _isNewProjectSubject?.Dispose();
                        _hasUnsavedChangesSubject?.Dispose();
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