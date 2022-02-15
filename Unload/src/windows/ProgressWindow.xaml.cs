using System;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

namespace unload
{
    public partial class ProgressWindow : Window
    {
        public double percentage;

        public CancellationTokenSource cts = new();

        readonly private string text;

        // Initalizes progress window and starts the timer to check for progress
        public ProgressWindow(string _text, Window owner)
        {
            InitializeComponent();
            text = _text;
            Owner = owner;
            Owner.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

            SetProgress();

            DispatcherTimer timer = new();

            timer.Interval = TimeSpan.FromSeconds(0.25);
            timer.Tick += timer_Tick;
            timer.Start();
        }

        public void SetProgress()
        {
            Owner.TaskbarItemInfo.ProgressValue = percentage / 100d;
            lblProgress.Content = $"{text}: {percentage:N2}%";
            progressBar.Value = percentage;
        }

        public void timer_Tick(object? sender, EventArgs e)
        {
            SetProgress();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Owner.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }
    }
}
