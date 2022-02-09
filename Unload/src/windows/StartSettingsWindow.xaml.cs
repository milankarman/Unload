using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace unload
{
    public partial class StartSettingsWindow : Window
    {
        private readonly MainWindow mainWindow;

        public StartSettingsWindow(MainWindow _mainWindow)
        {
            mainWindow = _mainWindow;
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
          
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.IsEnabled = true;
            Close();
        }
    }
}
