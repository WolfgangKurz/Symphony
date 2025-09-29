using Symphony.AdvancedSearchGUI.ViewModel;

using System.Windows;

namespace Symphony.AdvancedSearchGUI {
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
			this.DataContext = new AdvancedSearchViewModel();
		}
	}
}
