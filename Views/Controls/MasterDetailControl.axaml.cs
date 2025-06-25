using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HammerSickle.UnitCreator.Views.Controls
{
    public partial class MasterDetailControl : UserControl
    {
        public MasterDetailControl()
        {
            InitializeComponent();
        }

        #region Core Data Properties

        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
            AvaloniaProperty.Register<MasterDetailControl, IEnumerable?>(nameof(ItemsSource));

        public static readonly StyledProperty<object?> SelectedItemProperty =
            AvaloniaProperty.Register<MasterDetailControl, object?>(nameof(SelectedItem));

        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        #endregion

        #region Command Properties

        public static readonly StyledProperty<ICommand?> AddCommandProperty =
            AvaloniaProperty.Register<MasterDetailControl, ICommand?>(nameof(AddCommand));

        public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
            AvaloniaProperty.Register<MasterDetailControl, ICommand?>(nameof(DeleteCommand));

        public static readonly StyledProperty<ICommand?> CloneCommandProperty =
            AvaloniaProperty.Register<MasterDetailControl, ICommand?>(nameof(CloneCommand));

        public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
            AvaloniaProperty.Register<MasterDetailControl, ICommand?>(nameof(RefreshCommand));

        public static readonly StyledProperty<ICommand?> HideValidationSummaryCommandProperty =
            AvaloniaProperty.Register<MasterDetailControl, ICommand?>(nameof(HideValidationSummaryCommand));

        public ICommand? AddCommand
        {
            get => GetValue(AddCommandProperty);
            set => SetValue(AddCommandProperty, value);
        }

        public ICommand? DeleteCommand
        {
            get => GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        public ICommand? CloneCommand
        {
            get => GetValue(CloneCommandProperty);
            set => SetValue(CloneCommandProperty, value);
        }

        public ICommand? RefreshCommand
        {
            get => GetValue(RefreshCommandProperty);
            set => SetValue(RefreshCommandProperty, value);
        }

        public ICommand? HideValidationSummaryCommand
        {
            get => GetValue(HideValidationSummaryCommandProperty);
            set => SetValue(HideValidationSummaryCommandProperty, value);
        }

        #endregion

        #region State Properties

        public static readonly StyledProperty<bool> CanDeleteProperty =
            AvaloniaProperty.Register<MasterDetailControl, bool>(nameof(CanDelete));

        public static readonly StyledProperty<bool> CanCloneProperty =
            AvaloniaProperty.Register<MasterDetailControl, bool>(nameof(CanClone));

        public bool CanDelete
        {
            get => GetValue(CanDeleteProperty);
            set => SetValue(CanDeleteProperty, value);
        }

        public bool CanClone
        {
            get => GetValue(CanCloneProperty);
            set => SetValue(CanCloneProperty, value);
        }

        #endregion

        #region Filter Properties

        public static readonly StyledProperty<string> FilterTextProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(FilterText), defaultValue: string.Empty);

        public static readonly StyledProperty<string> FilterWatermarkProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(FilterWatermark), defaultValue: "Type to filter...");

        public string FilterText
        {
            get => GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        public string FilterWatermark
        {
            get => GetValue(FilterWatermarkProperty);
            set => SetValue(FilterWatermarkProperty, value);
        }

        #endregion

        #region Display Text Properties

        public static readonly StyledProperty<string> DetailTitleProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(DetailTitle), defaultValue: "Item Details");

        public static readonly StyledProperty<string> NoSelectionMessageProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(NoSelectionMessage), defaultValue: "Select an item to view details");

        public static readonly StyledProperty<string> AddButtonTextProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(AddButtonText), defaultValue: "Add");

        public static readonly StyledProperty<string> AddToolTipProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(AddToolTip), defaultValue: "Add new item");

        public static readonly StyledProperty<string> DeleteToolTipProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(DeleteToolTip), defaultValue: "Delete selected item");

        public static readonly StyledProperty<string> CloneToolTipProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(CloneToolTip), defaultValue: "Clone selected item");

        public string DetailTitle
        {
            get => GetValue(DetailTitleProperty);
            set => SetValue(DetailTitleProperty, value);
        }

        public string NoSelectionMessage
        {
            get => GetValue(NoSelectionMessageProperty);
            set => SetValue(NoSelectionMessageProperty, value);
        }

        public string AddButtonText
        {
            get => GetValue(AddButtonTextProperty);
            set => SetValue(AddButtonTextProperty, value);
        }

        public string AddToolTip
        {
            get => GetValue(AddToolTipProperty);
            set => SetValue(AddToolTipProperty, value);
        }

        public string DeleteToolTip
        {
            get => GetValue(DeleteToolTipProperty);
            set => SetValue(DeleteToolTipProperty, value);
        }

        public string CloneToolTip
        {
            get => GetValue(CloneToolTipProperty);
            set => SetValue(CloneToolTipProperty, value);
        }

        #endregion

        #region Count and Status Properties

        public static readonly StyledProperty<string> ItemsCountTextProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(ItemsCountText), defaultValue: "0 items");

        public static readonly StyledProperty<string> SelectionTextProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(SelectionText), defaultValue: "None selected");

        public string ItemsCountText
        {
            get => GetValue(ItemsCountTextProperty);
            set => SetValue(ItemsCountTextProperty, value);
        }

        public string SelectionText
        {
            get => GetValue(SelectionTextProperty);
            set => SetValue(SelectionTextProperty, value);
        }

        #endregion

        #region Validation Properties

        public static readonly StyledProperty<bool> IsSelectedItemValidProperty =
            AvaloniaProperty.Register<MasterDetailControl, bool>(nameof(IsSelectedItemValid), defaultValue: true);

        public static readonly StyledProperty<bool> IsSelectedItemModifiedProperty =
            AvaloniaProperty.Register<MasterDetailControl, bool>(nameof(IsSelectedItemModified), defaultValue: false);

        public static readonly StyledProperty<string> ValidationStatusTextProperty =
            AvaloniaProperty.Register<MasterDetailControl, string>(nameof(ValidationStatusText), defaultValue: "Valid");

        public static readonly StyledProperty<bool> ShowValidationSummaryProperty =
            AvaloniaProperty.Register<MasterDetailControl, bool>(nameof(ShowValidationSummary), defaultValue: false);

        public static readonly StyledProperty<IEnumerable?> ValidationSummaryItemsProperty =
            AvaloniaProperty.Register<MasterDetailControl, IEnumerable?>(nameof(ValidationSummaryItems));

        public bool IsSelectedItemValid
        {
            get => GetValue(IsSelectedItemValidProperty);
            set => SetValue(IsSelectedItemValidProperty, value);
        }

        public bool IsSelectedItemModified
        {
            get => GetValue(IsSelectedItemModifiedProperty);
            set => SetValue(IsSelectedItemModifiedProperty, value);
        }

        public string ValidationStatusText
        {
            get => GetValue(ValidationStatusTextProperty);
            set => SetValue(ValidationStatusTextProperty, value);
        }

        public bool ShowValidationSummary
        {
            get => GetValue(ShowValidationSummaryProperty);
            set => SetValue(ShowValidationSummaryProperty, value);
        }

        public IEnumerable? ValidationSummaryItems
        {
            get => GetValue(ValidationSummaryItemsProperty);
            set => SetValue(ValidationSummaryItemsProperty, value);
        }

        #endregion

        #region Template Property

        public static readonly StyledProperty<IDataTemplate?> DetailTemplateProperty =
            AvaloniaProperty.Register<MasterDetailControl, IDataTemplate?>(nameof(DetailTemplate));

        public IDataTemplate? DetailTemplate
        {
            get => GetValue(DetailTemplateProperty);
            set => SetValue(DetailTemplateProperty, value);
        }

        #endregion

        #region Master Item Template Property

        public static readonly StyledProperty<IDataTemplate?> MasterItemTemplateProperty =
            AvaloniaProperty.Register<MasterDetailControl, IDataTemplate?>(nameof(MasterItemTemplate));

        public IDataTemplate? MasterItemTemplate
        {
            get => GetValue(MasterItemTemplateProperty);
            set => SetValue(MasterItemTemplateProperty, value);
        }

        #endregion

        #region Property Change Handlers & Validation

        static MasterDetailControl()
        {
            // Set up property change callbacks for validation
            ItemsSourceProperty.Changed.AddClassHandler<MasterDetailControl>((x, e) => x.OnItemsSourceChanged(e));
            DetailTemplateProperty.Changed.AddClassHandler<MasterDetailControl>((x, e) => x.OnDetailTemplateChanged(e));
            FilterTextProperty.Changed.AddClassHandler<MasterDetailControl>((x, e) => x.OnFilterTextChanged(e));
        }

        private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            // Validate that ItemsSource is compatible
            if (e.NewValue != null && e.NewValue is not IEnumerable)
            {
                HammerAndSickle.Services.AppService.HandleException(
                    nameof(MasterDetailControl),
                    nameof(OnItemsSourceChanged),
                    new ArgumentException("ItemsSource must implement IEnumerable"));
            }
        }

        private void OnDetailTemplateChanged(AvaloniaPropertyChangedEventArgs e)
        {
            // Log template changes for debugging
            if (e.NewValue == null)
            {
                HammerAndSickle.Services.AppService.CaptureUiMessage("MasterDetailControl: DetailTemplate set to null, using fallback");
            }
        }

        private void OnFilterTextChanged(AvaloniaPropertyChangedEventArgs e)
        {
            // Validate filter text length
            if (e.NewValue is string filterText && filterText.Length > 500)
            {
                HammerAndSickle.Services.AppService.CaptureUiMessage("MasterDetailControl: Filter text is unusually long, performance may be affected");
            }
        }

        #endregion

        #region Template Fallback Methods

        /// <summary>
        /// Gets the detail template to use, with fallback if none specified
        /// </summary>
        public IDataTemplate GetEffectiveDetailTemplate()
        {
            return DetailTemplate ?? CreateFallbackDetailTemplate();
        }

        /// <summary>
        /// Gets the master item template to use, with fallback if none specified
        /// </summary>
        public IDataTemplate GetEffectiveMasterItemTemplate()
        {
            return MasterItemTemplate ?? CreateFallbackMasterItemTemplate();
        }

        /// <summary>
        /// Creates a fallback detail template when none is provided
        /// </summary>
        private static IDataTemplate CreateFallbackDetailTemplate()
        {
            return new FuncDataTemplate<object>((item, scope) =>
            {
                if (item == null) return null;

                return new StackPanel
                {
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(16),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Detail View",
                            FontSize = 16,
                            FontWeight = Avalonia.Media.FontWeight.Bold
                        },
                        new TextBlock
                        {
                            Text = $"Type: {item.GetType().Name}",
                            FontSize = 12,
                            Foreground = Avalonia.Media.Brushes.Gray
                        },
                        new TextBlock
                        {
                            Text = item.ToString() ?? "No string representation",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 12
                        },
                        new TextBlock
                        {
                            Text = "⚠️ No DetailTemplate provided - using fallback",
                            FontSize = 11,
                            FontStyle = Avalonia.Media.FontStyle.Italic,
                            Foreground = Avalonia.Media.Brushes.Orange,
                            Margin = new Avalonia.Thickness(0, 8, 0, 0)
                        }
                    }
                };
            });
        }

        /// <summary>
        /// Creates a fallback master item template when none is provided
        /// </summary>
        private static IDataTemplate CreateFallbackMasterItemTemplate()
        {
            return new FuncDataTemplate<object>((item, scope) =>
            {
                if (item == null) return null;

                return new DockPanel
                {
                    Margin = new Avalonia.Thickness(4)
                }.WithChildren(
                    new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = Avalonia.Media.Brushes.Gray,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        [DockPanel.DockProperty] = Dock.Left,
                        Margin = new Avalonia.Thickness(0, 0, 8, 0)
                    },
                    new TextBlock
                    {
                        Text = item.ToString() ?? item.GetType().Name,
                        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                );
            });
        }

        #endregion

        #region Design-Time Support

        /// <summary>
        /// Gets design-time sample data for previewing
        /// </summary>
        public static object[] GetDesignTimeItems()
        {
            return new object[]
            {
                new { DisplayName = "Sample Item 1", DisplaySubtext = "Description 1" },
                new { DisplayName = "Sample Item 2", DisplaySubtext = "Description 2" },
                new { DisplayName = "Sample Item 3", DisplaySubtext = "Description 3" }
            };
        }

        #endregion
    }

    #region Extension Methods for Fluent API

    public static class ControlExtensions
    {
        public static T WithChildren<T>(this T panel, params Control[] children) where T : Panel
        {
            foreach (var child in children)
            {
                panel.Children.Add(child);
            }
            return panel;
        }
    }

    #endregion
}