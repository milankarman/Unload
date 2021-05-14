using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace auto_loadless
{
    public partial class ProgressWindow : Window
    {
        public int totalTasks = 0;
        public int currentTask = 0;

        public bool finished = false;

        public Action onFinishedAction = null;
        public Action onCloseAction = null;

        public CancellationTokenSource cts = new CancellationTokenSource();

        public ProgressWindow()
        {
            InitializeComponent();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.25);
            timer.Tick += timer_Tick;

            timer.Start();
        }

        public void timer_Tick(object sender, EventArgs e)
        {
            if (finished)
            {
                onFinishedAction();
                Close();
            }

            label.Content = $"{currentTask} / {totalTasks}";

            double percentage = (double)currentTask / (double)totalTasks * 100d;
            progressBar.Value = percentage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cts.Cancel();
        }
    }
}
