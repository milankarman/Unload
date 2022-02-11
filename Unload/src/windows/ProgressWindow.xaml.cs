using System;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

namespace unload
{
    public partial class ProgressWindow : Window
    {
        // Keeps track of the progress percentage
        public double percentage = 0;

        // Holds a cancellation token for the process this progress window is linked to
        public CancellationTokenSource cts = new CancellationTokenSource();

        readonly private MainWindow mainWindow;
        readonly private string text = string.Empty;

        // Initalizes progress window and starts the timer to check for progress
        public ProgressWindow(string _text, MainWindow _mainWindow)
        {
            InitializeComponent();
            text = _text;
            mainWindow = _mainWindow;
            mainWindow.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

            SetProgress();

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.25)
            };

            timer.Tick += timer_Tick;
            timer.Start();
        }

        // Updates the progress every timer tick
        public void timer_Tick(object? sender, EventArgs e)
        {
            SetProgress();
        }

        // Shows the user the percentage of progress
        private void SetProgress()
        {
            mainWindow.TaskbarItemInfo.ProgressValue = percentage / 100d;
            lblProgress.Content = $"{text}: {percentage:N2}%";
            progressBar.Value = percentage;
        }

        // Calls for a cancel on the cancellation token and closes the progress window when the user hits cancel 
        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            Close();
        }

        // Clear the taskbar progres state when the progress window closes
        private void Window_Closed(object sender, EventArgs e)
        {
            mainWindow.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }
    }
}
