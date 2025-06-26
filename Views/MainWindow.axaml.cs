using Avalonia.Controls;
using System;
using HammerSickle.UnitCreator.ViewModels;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.Views
{
    /// <summary>
    /// MainWindow code-behind with enhanced window lifecycle management.
    /// Handles window closing events and coordinates with the MainWindowViewModel
    /// for proper unsaved changes checking and graceful shutdown.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string CLASS_NAME = nameof(MainWindow);

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Set up window properties
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                AppService.CaptureUiMessage("MainWindow initialized");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(MainWindow), e);
                throw;
            }
        }

        /// <summary>
        /// Handles the window closing event with proper unsaved changes checking.
        /// Coordinates with the MainWindowViewModel to ensure data integrity.
        /// </summary>
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            try
            {
                AppService.CaptureUiMessage("MainWindow closing requested");

                // Always cancel initially to allow proper handling
                e.Cancel = true;

                // Get the ViewModel and handle closing logic
                if (DataContext is MainWindowViewModel viewModel)
                {
                    var canClose = await viewModel.HandleWindowClosingAsync();

                    if (canClose)
                    {
                        AppService.CaptureUiMessage("Window closing approved by ViewModel");

                        // Allow the close to proceed by calling base without canceling
                        e.Cancel = false;
                        base.OnClosing(e);
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Window closing cancelled by user or ViewModel");
                        // e.Cancel remains true, so the window won't close
                    }
                }
                else
                {
                    AppService.CaptureUiMessage("No ViewModel found - allowing window to close");
                    // No ViewModel to check with, allow close
                    e.Cancel = false;
                    base.OnClosing(e);
                }
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnClosing), ex);

                // In case of error, allow close to prevent application hang
                AppService.CaptureUiMessage("Error during window closing - allowing close to prevent hang");
                e.Cancel = false;
                base.OnClosing(e);
            }
        }

        /// <summary>
        /// Called when the window is actually closed (after OnClosing approval).
        /// Performs final cleanup operations.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                AppService.CaptureUiMessage("MainWindow closed - performing final cleanup");

                // Dispose the ViewModel if it implements IDisposable
                if (DataContext is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                    AppService.CaptureUiMessage("ViewModel disposed");
                }

                // Clear the DataContext
                DataContext = null;

                base.OnClosed(e);

                AppService.CaptureUiMessage("MainWindow cleanup completed");
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnClosed), ex);
                // Continue with base cleanup even if our cleanup fails
                base.OnClosed(e);
            }
        }

        /// <summary>
        /// Provides access to the MainWindowViewModel for external coordination.
        /// Used by the App class for dependency injection setup.
        /// </summary>
        public MainWindowViewModel? GetViewModel()
        {
            return DataContext as MainWindowViewModel;
        }

        /// <summary>
        /// Sets the ViewModel with proper type checking and event setup.
        /// Used by the App class during initialization.
        /// </summary>
        public void SetViewModel(MainWindowViewModel viewModel)
        {
            try
            {
                if (viewModel == null)
                    throw new ArgumentNullException(nameof(viewModel));

                DataContext = viewModel;
                AppService.CaptureUiMessage("MainWindow ViewModel set successfully");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SetViewModel), e);
                throw;
            }
        }
    }
}