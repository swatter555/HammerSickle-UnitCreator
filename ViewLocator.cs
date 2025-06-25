using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HammerSickle.UnitCreator.ViewModels.Base;

namespace HammerSickle.UnitCreator
{
    /// <summary>
    /// ViewLocator provides automatic View resolution for ViewModels in the Hammer & Sickle Unit Creator.
    /// 
    /// Maps ViewModels to their corresponding Views using naming conventions:
    /// - MainWindowViewModel → MainWindow
    /// - LeadersTabViewModel → LeadersTabView  
    /// - WeaponSystemsTabViewModel → WeaponSystemsTabView
    /// - etc.
    /// 
    /// Follows the pattern: ViewModels.* → Views.*
    /// Handles both direct ViewModel→View mapping and ViewModel→ViewControl mapping.
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        private const string CLASS_NAME = nameof(ViewLocator);

        /// <summary>
        /// Builds a Control for the given ViewModel data object
        /// </summary>
        public Control? Build(object? data)
        {
            if (data == null)
                return CreateNotFoundView("No data provided");

            try
            {
                var viewModelType = data.GetType();
                var viewType = ResolveViewType(viewModelType);

                if (viewType != null)
                {
                    var view = Activator.CreateInstance(viewType) as Control;
                    if (view != null)
                    {
                        view.DataContext = data;
                        return view;
                    }
                }

                // If we can't find a specific view, return a generic not found view
                return CreateNotFoundView($"View not found for {viewModelType.Name}");
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(Build), e);
                return CreateNotFoundView($"Error creating view: {e.Message}");
            }
        }

        /// <summary>
        /// Determines if this template can handle the given data type
        /// </summary>
        public bool Match(object? data)
        {
            // Handle all ViewModelBase derived types
            return data is ViewModelBase;
        }

        /// <summary>
        /// Resolves the View type for a given ViewModel type using naming conventions
        /// </summary>
        private Type? ResolveViewType(Type viewModelType)
        {
            try
            {
                var viewModelTypeName = viewModelType.FullName;
                if (string.IsNullOrEmpty(viewModelTypeName))
                    return null;

                // Apply naming convention transformations
                var viewTypeName = TransformViewModelNameToViewName(viewModelTypeName);

                // Try to find the type in the same assembly
                var viewType = viewModelType.Assembly.GetType(viewTypeName);

                if (viewType != null && typeof(Control).IsAssignableFrom(viewType))
                {
                    return viewType;
                }

                return null;
            }
            catch (Exception e)
            {
                HammerAndSickle.Services.AppService.HandleException(CLASS_NAME, nameof(ResolveViewType), e);
                return null;
            }
        }

        /// <summary>
        /// Transforms ViewModel type name to View type name using convention
        /// </summary>
        private string TransformViewModelNameToViewName(string viewModelTypeName)
        {
            // Transform: HammerSickle.UnitCreator.ViewModels.* → HammerSickle.UnitCreator.Views.*
            var viewTypeName = viewModelTypeName.Replace(".ViewModels.", ".Views.");

            // Handle specific naming patterns
            if (viewTypeName.EndsWith("ViewModel"))
            {
                // For most ViewModels, just replace "ViewModel" with "View"
                // MainWindowViewModel → MainWindowView (but we want MainWindow)
                // LeadersTabViewModel → LeadersTabView

                if (viewTypeName.Contains("MainWindowViewModel"))
                {
                    // Special case: MainWindowViewModel → MainWindow
                    viewTypeName = viewTypeName.Replace("MainWindowViewModel", "MainWindow");
                }
                else
                {
                    // Standard case: SomethingViewModel → SomethingView
                    viewTypeName = viewTypeName.Replace("ViewModel", "View");
                }
            }

            return viewTypeName;
        }

        /// <summary>
        /// Creates a fallback view when the target view cannot be found or created
        /// </summary>
        private Control CreateNotFoundView(string message)
        {
            return new TextBlock
            {
                Text = $"View Not Found: {message}",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Red,
                FontWeight = Avalonia.Media.FontWeight.Bold
            };
        }
    }
}