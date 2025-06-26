using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.Services
{
    /// <summary>
    /// Result enumeration for unsaved changes dialog
    /// </summary>
    public enum UnsavedChangesResult
    {
        Save,        // User chose to save changes
        DontSave,    // User chose to discard changes
        Cancel       // User chose to cancel the operation
    }

    /// <summary>
    /// FileDialogService provides centralized file dialog functionality for the Unit Creator application.
    /// Handles Open, Save, and Save As dialogs with proper file filtering and error handling.
    /// Also provides unsaved changes confirmation dialogs for user workflow management.
    /// 
    /// Key responsibilities:
    /// - Display Avalonia file dialogs with proper filtering and defaults
    /// - Handle unsaved changes confirmation workflow
    /// - Provide consistent error handling and user feedback
    /// - Support project file (.sce) and export file (.oob) operations
    /// - Manage dialog parent window relationships for proper modality
    /// </summary>
    public class FileDialogService
    {
        private const string CLASS_NAME = nameof(FileDialogService);

        // File filter definitions
        private static readonly FilePickerFileType ProjectFileType = new("Hammer & Sickle Project Files")
        {
            Patterns = new[] { "*.sce" },
            AppleUniformTypeIdentifiers = new[] { "public.data" },
            MimeTypes = new[] { "application/octet-stream" }
        };

        private static readonly FilePickerFileType ExportFileType = new("Order of Battle Files")
        {
            Patterns = new[] { "*.oob" },
            AppleUniformTypeIdentifiers = new[] { "public.data" },
            MimeTypes = new[] { "application/octet-stream" }
        };

        private static readonly FilePickerFileType AllFilesType = new("All Files")
        {
            Patterns = new[] { "*.*" },
            AppleUniformTypeIdentifiers = new[] { "public.item" },
            MimeTypes = new[] { "*/*" }
        };

        // Default directories
        private readonly string _defaultProjectsDirectory;
        private readonly string _defaultExportsDirectory;

        public FileDialogService()
        {
            try
            {
                // Set up default directories using common user locations
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                _defaultProjectsDirectory = Path.Combine(documentsPath, "HammerSickle", "UnitCreator", "Projects");
                _defaultExportsDirectory = Path.Combine(documentsPath, "HammerSickle", "UnitCreator", "Exports");

                // Ensure directories exist
                EnsureDirectoryExists(_defaultProjectsDirectory);
                EnsureDirectoryExists(_defaultExportsDirectory);
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(FileDialogService), e);

                // Fallback to current directory if setup fails
                _defaultProjectsDirectory = Environment.CurrentDirectory;
                _defaultExportsDirectory = Environment.CurrentDirectory;
            }
        }

        #region Open File Dialog

        /// <summary>
        /// Shows an open file dialog for project files (.sce)
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        public async Task<string?> ShowOpenFileDialogAsync(Window parent)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage("Cannot show open dialog: parent window is null");
                return null;
            }

            try
            {
                var storageProvider = parent.StorageProvider;
                if (storageProvider == null)
                {
                    AppService.CaptureUiMessage("Cannot show open dialog: storage provider unavailable");
                    return null;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Open Hammer & Sickle Project",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { ProjectFileType, AllFilesType }
                };

                // Set suggested start location
                if (Directory.Exists(_defaultProjectsDirectory))
                {
                    options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(_defaultProjectsDirectory);
                }

                var result = await storageProvider.OpenFilePickerAsync(options);

                if (result != null && result.Count > 0)
                {
                    var selectedFile = result[0];
                    var filePath = selectedFile.TryGetLocalPath();

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        AppService.CaptureUiMessage($"File selected for opening: {Path.GetFileName(filePath)}");
                        return filePath;
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Selected file path could not be resolved");
                        return null;
                    }
                }

                // User cancelled
                AppService.CaptureUiMessage("Open file dialog cancelled by user");
                return null;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowOpenFileDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing open file dialog: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Save File Dialogs

        /// <summary>
        /// Shows a save file dialog for project files (.sce)
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="defaultName">Default filename (without extension)</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        public async Task<string?> ShowSaveFileDialogAsync(Window parent, string? defaultName = null)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage("Cannot show save dialog: parent window is null");
                return null;
            }

            try
            {
                var storageProvider = parent.StorageProvider;
                if (storageProvider == null)
                {
                    AppService.CaptureUiMessage("Cannot show save dialog: storage provider unavailable");
                    return null;
                }

                // Generate default name if not provided
                var suggestedName = string.IsNullOrWhiteSpace(defaultName)
                    ? GenerateDefaultProjectName()
                    : EnsureProjectExtension(defaultName);

                var options = new FilePickerSaveOptions
                {
                    Title = "Save Hammer & Sickle Project",
                    SuggestedFileName = suggestedName,
                    DefaultExtension = "sce",
                    FileTypeChoices = new[] { ProjectFileType, AllFilesType }
                };

                // Set suggested start location
                if (Directory.Exists(_defaultProjectsDirectory))
                {
                    options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(_defaultProjectsDirectory);
                }

                var result = await storageProvider.SaveFilePickerAsync(options);

                if (result != null)
                {
                    var filePath = result.TryGetLocalPath();

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        // Ensure the file has the correct extension
                        filePath = EnsureProjectExtension(filePath);
                        AppService.CaptureUiMessage($"File selected for saving: {Path.GetFileName(filePath)}");
                        return filePath;
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Selected save path could not be resolved");
                        return null;
                    }
                }

                // User cancelled
                AppService.CaptureUiMessage("Save file dialog cancelled by user");
                return null;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowSaveFileDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing save file dialog: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Shows a save file dialog for export files (.oob)
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="defaultName">Default filename (without extension)</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        public async Task<string?> ShowExportFileDialogAsync(Window parent, string? defaultName = null)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage("Cannot show export dialog: parent window is null");
                return null;
            }

            try
            {
                var storageProvider = parent.StorageProvider;
                if (storageProvider == null)
                {
                    AppService.CaptureUiMessage("Cannot show export dialog: storage provider unavailable");
                    return null;
                }

                // Generate default name if not provided
                var suggestedName = string.IsNullOrWhiteSpace(defaultName)
                    ? GenerateDefaultExportName()
                    : EnsureExportExtension(defaultName);

                var options = new FilePickerSaveOptions
                {
                    Title = "Export Order of Battle File",
                    SuggestedFileName = suggestedName,
                    DefaultExtension = "oob",
                    FileTypeChoices = new[] { ExportFileType, AllFilesType }
                };

                // Set suggested start location
                if (Directory.Exists(_defaultExportsDirectory))
                {
                    options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(_defaultExportsDirectory);
                }

                var result = await storageProvider.SaveFilePickerAsync(options);

                if (result != null)
                {
                    var filePath = result.TryGetLocalPath();

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        // Ensure the file has the correct extension
                        filePath = EnsureExportExtension(filePath);
                        AppService.CaptureUiMessage($"File selected for export: {Path.GetFileName(filePath)}");
                        return filePath;
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Selected export path could not be resolved");
                        return null;
                    }
                }

                // User cancelled
                AppService.CaptureUiMessage("Export file dialog cancelled by user");
                return null;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowExportFileDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing export file dialog: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Unsaved Changes Dialog

        /// <summary>
        /// Shows an unsaved changes confirmation dialog
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="projectName">Name of the project with unsaved changes</param>
        /// <returns>User's choice: Save, DontSave, or Cancel</returns>
        public async Task<UnsavedChangesResult> ShowUnsavedChangesDialogAsync(Window parent, string? projectName = null)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage("Cannot show unsaved changes dialog: parent window is null");
                return UnsavedChangesResult.Cancel;
            }

            try
            {
                var displayName = string.IsNullOrWhiteSpace(projectName) ? "the current project" : $"'{projectName}'";

                // For now, create a simple dialog using basic Avalonia components
                // This can be enhanced with a proper message box library later
                var dialog = new Window
                {
                    Title = "Unsaved Changes",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                var result = UnsavedChangesResult.Cancel;

                var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = $"Do you want to save changes to {displayName}?",
                    FontSize = 14,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(0, 0, 0, 10)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "Your changes will be lost if you don't save them.",
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(0, 0, 0, 20)
                });

                var buttonPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10
                };

                var saveButton = new Button { Content = "Save", MinWidth = 80 };
                var dontSaveButton = new Button { Content = "Don't Save", MinWidth = 80 };
                var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };

                saveButton.Click += (s, e) => { result = UnsavedChangesResult.Save; dialog.Close(); };
                dontSaveButton.Click += (s, e) => { result = UnsavedChangesResult.DontSave; dialog.Close(); };
                cancelButton.Click += (s, e) => { result = UnsavedChangesResult.Cancel; dialog.Close(); };

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(dontSaveButton);
                buttonPanel.Children.Add(cancelButton);
                panel.Children.Add(buttonPanel);

                dialog.Content = panel;

                await dialog.ShowDialog(parent);

                return result;
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowUnsavedChangesDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing unsaved changes dialog: {e.Message}");
                return UnsavedChangesResult.Cancel;
            }
        }

        #endregion

        #region Error and Information Dialogs

        /// <summary>
        /// Shows an error message dialog
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Error message</param>
        public async Task ShowErrorDialogAsync(Window parent, string title, string message)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage($"ERROR - {title}: {message}");
                return;
            }

            try
            {
                // For now, log the error message
                // This can be replaced with a proper error dialog later
                AppService.CaptureUiMessage($"ERROR - {title}: {message}");

                // TODO: Implement proper error dialog when message box library is available
                await Task.Delay(1); // Placeholder for async operation
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowErrorDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing error dialog: {e.Message}");
            }
        }

        /// <summary>
        /// Shows an information message dialog
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Information message</param>
        public async Task ShowInformationDialogAsync(Window parent, string title, string message)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage($"INFO - {title}: {message}");
                return;
            }

            try
            {
                // For now, log the information message
                // This can be replaced with a proper info dialog later
                AppService.CaptureUiMessage($"INFO - {title}: {message}");

                // TODO: Implement proper information dialog when message box library is available
                await Task.Delay(1); // Placeholder for async operation
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowInformationDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing information dialog: {e.Message}");
            }
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes/No options
        /// </summary>
        /// <param name="parent">Parent window for modal dialog</param>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Confirmation message</param>
        /// <returns>True if user chose Yes, false if No</returns>
        public async Task<bool> ShowConfirmationDialogAsync(Window parent, string title, string message)
        {
            if (parent == null)
            {
                AppService.CaptureUiMessage($"CONFIRM - {title}: {message} (Auto-confirming: NO)");
                return false;
            }

            try
            {
                // For now, log the confirmation request and return false (safe default)
                // This can be replaced with a proper confirmation dialog later
                AppService.CaptureUiMessage($"CONFIRM - {title}: {message} (Auto-confirming: NO)");

                // TODO: Implement proper confirmation dialog when message box library is available
                await Task.Delay(1); // Placeholder for async operation
                return false; // Safe default - don't confirm destructive actions
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(ShowConfirmationDialogAsync), e);
                AppService.CaptureUiMessage($"Error showing confirmation dialog: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Ensures a directory exists, creating it if necessary
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        private static void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(EnsureDirectoryExists), e);
                // Non-critical error - continue without the directory
            }
        }

        /// <summary>
        /// Ensures a filename has the correct project extension
        /// </summary>
        /// <param name="fileName">Filename to check</param>
        /// <returns>Filename with .sce extension</returns>
        private static string EnsureProjectExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "project.sce";

            return fileName.EndsWith(".sce", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : Path.ChangeExtension(fileName, ".sce");
        }

        /// <summary>
        /// Ensures a filename has the correct export extension
        /// </summary>
        /// <param name="fileName">Filename to check</param>
        /// <returns>Filename with .oob extension</returns>
        private static string EnsureExportExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "scenario.oob";

            return fileName.EndsWith(".oob", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : Path.ChangeExtension(fileName, ".oob");
        }

        /// <summary>
        /// Generates a default project filename with timestamp
        /// </summary>
        /// <returns>Default project filename</returns>
        private static string GenerateDefaultProjectName()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                return $"UnitCreator_Project_{timestamp}.sce";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GenerateDefaultProjectName), e);
                return "UnitCreator_Project.sce";
            }
        }

        /// <summary>
        /// Generates a default export filename with timestamp
        /// </summary>
        /// <returns>Default export filename</returns>
        private static string GenerateDefaultExportName()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                return $"Scenario_{timestamp}.oob";
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(GenerateDefaultExportName), e);
                return "Scenario.oob";
            }
        }

        #endregion
    }
}