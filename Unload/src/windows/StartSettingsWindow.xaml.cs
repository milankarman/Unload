using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using unload.Properties;

namespace unload
{
    public partial class StartSettingsWindow : Window
    {
        private readonly MainWindow mainWindow;

        private string? workingDirectory = null;

        private const string NO_WORKING_DIRECTORY = "Same as video";

        public StartSettingsWindow(MainWindow _mainWindow, string? _workingDirectory)
        {
            Owner = _mainWindow;
            mainWindow = _mainWindow;
            workingDirectory = _workingDirectory;

            InitializeComponent();

            txtWorkingDirectory.Text = string.IsNullOrEmpty(workingDirectory) ? NO_WORKING_DIRECTORY : workingDirectory;

            if (_workingDirectory != null) btnClear.IsEnabled = true;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
                Title = "Select working directory for creating frames",
                IsFolderPicker = true,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureValidNames = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                workingDirectory = dialog.FileName;
                txtWorkingDirectory.Text = workingDirectory;
                btnClear.IsEnabled = true;
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtWorkingDirectory.Text = NO_WORKING_DIRECTORY;
            workingDirectory = null;
            btnClear.IsEnabled = false;
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            // workingDirectory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory;

            Settings.Default.WorkingDirectory = workingDirectory;
            Settings.Default.Save();

            mainWindow.IsEnabled = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.IsEnabled = true;
            Close();
        }
    }
}
