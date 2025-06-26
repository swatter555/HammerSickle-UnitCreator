// Option 1: Update MainWindow.axaml.cs to provide window reference
using Avalonia.Controls;
using System;
using HammerSickle.UnitCreator.ViewModels;
using HammerAndSickle.Services;

namespace HammerSickle.UnitCreator.Views
{
    public partial class MainWindow : Window
    {
        private const string CLASS_NAME = nameof(MainWindow);

        public MainWindow()
        {
            try
            {
                InitializeComponent();
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
        /// Sets the ViewModel and establishes the window reference
        /// </summary>
        public void SetViewModel(MainWindowViewModel viewModel)
        {
            try
            {
                if (viewModel == null)
                    throw new ArgumentNullException(nameof(viewModel));

                // Set the window reference in the ViewModel BEFORE setting DataContext
                viewModel.SetMainWindow(this);
                DataContext = viewModel;

                AppService.CaptureUiMessage("MainWindow ViewModel set successfully with window reference");
            }
            catch (Exception e)
            {
                AppService.HandleException(CLASS_NAME, nameof(SetViewModel), e);
                throw;
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            try
            {
                AppService.CaptureUiMessage("MainWindow closing requested");
                e.Cancel = true;

                if (DataContext is MainWindowViewModel viewModel)
                {
                    var canClose = await viewModel.HandleWindowClosingAsync();

                    if (canClose)
                    {
                        AppService.CaptureUiMessage("Window closing approved by ViewModel");
                        e.Cancel = false;
                        base.OnClosing(e);
                    }
                    else
                    {
                        AppService.CaptureUiMessage("Window closing cancelled by user or ViewModel");
                    }
                }
                else
                {
                    AppService.CaptureUiMessage("No ViewModel found - allowing window to close");
                    e.Cancel = false;
                    base.OnClosing(e);
                }
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnClosing), ex);
                AppService.CaptureUiMessage("Error during window closing - allowing close to prevent hang");
                e.Cancel = false;
                base.OnClosing(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                AppService.CaptureUiMessage("MainWindow closed - performing final cleanup");

                if (DataContext is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                    AppService.CaptureUiMessage("ViewModel disposed");
                }

                DataContext = null;
                base.OnClosed(e);
                AppService.CaptureUiMessage("MainWindow cleanup completed");
            }
            catch (Exception ex)
            {
                AppService.HandleException(CLASS_NAME, nameof(OnClosed), ex);
                base.OnClosed(e);
            }
        }

        public MainWindowViewModel? GetViewModel()
        {
            return DataContext as MainWindowViewModel;
        }
    }
}