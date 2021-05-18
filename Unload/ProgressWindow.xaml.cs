using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace unload
{
    public partial class ProgressWindow : Window
    {
        public double percentage = 0;
        public bool finished = false;
        public CancellationTokenSource cts = new CancellationTokenSource();

        private string text = string.Empty;

        public ProgressWindow(string _text)
        {
            InitializeComponent();

            text = _text;

            SetProgress();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.25);
            timer.Tick += timer_Tick;
            timer.Start();
        }

        public void timer_Tick(object sender, EventArgs e)
        {
            if (finished)
            {
                Close();
            }

            SetProgress();
        }

        private void SetProgress()
        {
            label.Content = $"{text}: {percentage}%";
            progressBar.Value = percentage;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            Close();
        }
    }
}
