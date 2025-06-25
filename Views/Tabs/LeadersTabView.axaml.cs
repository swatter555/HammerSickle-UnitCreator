using Avalonia.Controls;
using HammerSickle.UnitCreator.ViewModels.Tabs;

namespace HammerSickle.UnitCreator.Views.Tabs
{
    public partial class LeadersTabView : UserControl
    {
        public LeadersTabView()
        {
            InitializeComponent();

            // Set up data context if not already set
            if (DataContext == null)
            {
                DataContext = new LeadersTabViewModel();
            }
        }
    }
}