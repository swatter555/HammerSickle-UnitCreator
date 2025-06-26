using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HammerSickle.UnitCreator.ViewModels;
using HammerSickle.UnitCreator.Views;
using HammerSickle.UnitCreator.Services;
using HammerAndSickle.Services;
using System;

namespace HammerSickle.UnitCreator
{
    /// <summary>
    /// Application entry point with dependency injection setup for the Unit Creator.
    /// Manages service lifecycle, dependency resolution, and application initialization.
    /// </summary>
    public partial class App : Application
    {
        // Service instances - managed at application level for proper lifecycle
        private DataService? _dataService;
        private ValidationService? _validationService;
        private FileDialogService? _fileDialogService;
        private ProjectStateService? _projectStateService;
        private TabManagerService? _tabManagerService;
        private ExportService? _exportService;
        private MainWindowViewModel? _mainWindowViewModel;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Initialize services in dependency order
                    InitializeServices();

                    // Create main window and ViewModel with full dependency injection
                    var mainWindow = new MainWindow();
                    _mainWindowViewModel = CreateMainWindowViewModel();
                    mainWindow.DataContext = _mainWindowViewModel;

                    // Wire up application-level event handlers
                    SetupApplicationEventHandlers(mainWindow);

                    desktop.MainWindow = mainWindow;

                    AppService.CaptureUiMessage("Application initialized successfully with full dependency injection");
                }
                else
                {
                    AppService.CaptureUiMessage("Application initialized in non-desktop mode");
                }

                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(OnFrameworkInitializationCompleted), e);

                // Create a minimal fallback application state
                CreateFallbackApplication();
                base.OnFrameworkInitializationCompleted();
            }
        }

        #region Service Initialization

        /// <summary>
        /// Initializes all application services in proper dependency order
        /// </summary>
        private void InitializeServices()
        {
            try
            {
                AppService.CaptureUiMessage("Initializing application services...");

                // Core data services (no dependencies)
                _dataService = new DataService();
                AppService.CaptureUiMessage("DataService initialized");

                _validationService = new ValidationService(_dataService);
                AppService.CaptureUiMessage("ValidationService initialized");

                // UI and file services (minimal dependencies)
                _fileDialogService = new FileDialogService();
                AppService.CaptureUiMessage("FileDialogService initialized");

                _projectStateService = new ProjectStateService(_dataService);
                AppService.CaptureUiMessage("ProjectStateService initialized");

                // Coordination services (depend on other services)
                _tabManagerService = new TabManagerService();
                AppService.CaptureUiMessage("TabManagerService initialized");

                _exportService = new ExportService(_dataService, _validationService);
                AppService.CaptureUiMessage("ExportService initialized");

                AppService.CaptureUiMessage("All services initialized successfully");
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(InitializeServices), e);
                throw; // Re-throw to trigger fallback
            }
        }

        /// <summary>
        /// Creates the MainWindowViewModel with full dependency injection
        /// </summary>
        private MainWindowViewModel CreateMainWindowViewModel()
        {
            try
            {
                if (_fileDialogService == null || _projectStateService == null ||
                    _exportService == null || _validationService == null ||
                    _tabManagerService == null || _dataService == null)
                {
                    throw new InvalidOperationException("Required services are not initialized");
                }

                var viewModel = new MainWindowViewModel(
                    _fileDialogService,
                    _projectStateService,
                    _exportService,
                    _validationService,
                    _tabManagerService,
                    _dataService);

                AppService.CaptureUiMessage("MainWindowViewModel created with dependency injection");
                return viewModel;
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(CreateMainWindowViewModel), e);
                throw;
            }
        }

        #endregion

        #region Application Event Handlers

        /// <summary>
        /// Sets up application-level event handlers for proper lifecycle management
        /// </summary>
        private void SetupApplicationEventHandlers(MainWindow mainWindow)
        {
            try
            {
                // Handle application shutdown
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.ShutdownRequested += OnShutdownRequested;
                    AppService.CaptureUiMessage("Application shutdown handler registered");
                }

                // Handle window closing
                mainWindow.Closing += async (sender, e) =>
                {
                    try
                    {
                        if (_mainWindowViewModel != null)
                        {
                            // Cancel the close initially to handle unsaved changes
                            e.Cancel = true;

                            // Check if we can close safely
                            var canClose = await _mainWindowViewModel.HandleWindowClosingAsync();

                            if (canClose)
                            {
                                // Allow the close to proceed
                                e.Cancel = false;

                                // Dispose resources
                                DisposeServices();

                                // Now actually close the window
                                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                                {
                                    desktopLifetime.Shutdown();
                                }
                            }
                            // If canClose is false, the close operation is cancelled and window stays open
                        }
                    }
                    catch (Exception ex)
                    {
                        AppService.HandleException(nameof(App), "WindowClosing", ex);
                        // In case of error, allow close to prevent application hang
                        e.Cancel = false;
                    }
                };

                AppService.CaptureUiMessage("Window event handlers registered");
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(SetupApplicationEventHandlers), e);
            }
        }

        /// <summary>
        /// Handles application shutdown requests
        /// </summary>
        private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            try
            {
                AppService.CaptureUiMessage("Application shutdown requested");

                // The window closing handler will take care of unsaved changes checking
                // This is just for cleanup
                DisposeServices();

                AppService.CaptureUiMessage("Application shutdown completed");
            }
            catch (Exception ex)
            {
                AppService.HandleException(nameof(App), nameof(OnShutdownRequested), ex);
            }
        }

        #endregion

        #region Resource Management

        /// <summary>
        /// Properly disposes all services in reverse dependency order
        /// </summary>
        private void DisposeServices()
        {
            try
            {
                AppService.CaptureUiMessage("Disposing application services...");

                // Dispose in reverse dependency order
                _mainWindowViewModel?.Dispose();
                _mainWindowViewModel = null;

                _tabManagerService?.Dispose();
                _tabManagerService = null;

                _projectStateService?.Dispose();
                _projectStateService = null;

                // Export service, validation service, file dialog service, and data service
                // don't implement IDisposable but we'll null them for cleanup
                _exportService = null;
                _validationService = null;
                _fileDialogService = null;
                _dataService = null;

                AppService.CaptureUiMessage("All services disposed successfully");
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(DisposeServices), e);
            }
        }

        #endregion

        #region Error Recovery

        /// <summary>
        /// Creates a minimal fallback application when full initialization fails
        /// </summary>
        private void CreateFallbackApplication()
        {
            try
            {
                AppService.CaptureUiMessage("Creating fallback application due to initialization failure");

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Create a basic main window with minimal functionality
                    var mainWindow = new MainWindow();

                    // Create a minimal ViewModel without full dependency injection
                    var fallbackViewModel = CreateFallbackViewModel();
                    mainWindow.DataContext = fallbackViewModel;

                    desktop.MainWindow = mainWindow;

                    AppService.CaptureUiMessage("Fallback application created - limited functionality available");
                }
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(CreateFallbackApplication), e);
                // Last resort - let the application continue with whatever it has
            }
        }

        /// <summary>
        /// Creates a minimal ViewModel for fallback scenarios
        /// </summary>
        private MainWindowViewModel CreateFallbackViewModel()
        {
            try
            {
                // Create minimal services for basic functionality
                var dataService = new DataService();
                var validationService = new ValidationService(dataService);
                var fileDialogService = new FileDialogService();
                var projectStateService = new ProjectStateService(dataService);
                var tabManagerService = new TabManagerService();
                var exportService = new ExportService(dataService, validationService);

                return new MainWindowViewModel(
                    fileDialogService,
                    projectStateService,
                    exportService,
                    validationService,
                    tabManagerService,
                    dataService);
            }
            catch (Exception e)
            {
                AppService.HandleException(nameof(App), nameof(CreateFallbackViewModel), e);
                throw; // If we can't even create a fallback, let the application fail
            }
        }

        #endregion

        #region Service Access (for debugging/development)

        /// <summary>
        /// Gets the current DataService instance (for debugging purposes)
        /// </summary>
        public DataService? GetDataService() => _dataService;

        /// <summary>
        /// Gets the current ProjectStateService instance (for debugging purposes)
        /// </summary>
        public ProjectStateService? GetProjectStateService() => _projectStateService;

        /// <summary>
        /// Gets the current TabManagerService instance (for debugging purposes)
        /// </summary>
        public TabManagerService? GetTabManagerService() => _tabManagerService;

        #endregion
    }
}