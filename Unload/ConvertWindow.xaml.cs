using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using Xabe.FFmpeg;

namespace unload
{
    public partial class ConvertWindow : Window
    {
        private MainWindow mainWindow;
        private string filePath;
        private string targetDirectory;

        private TimeSpan fileDuration;
        private double fileFramerate;

        public ConvertWindow(MainWindow _mainWindow, string _filePath, string _targetDirectory)
        {
            mainWindow = _mainWindow;
            filePath = _filePath;
            targetDirectory = _targetDirectory;

            InitializeComponent();
        }

        public async void GetVideoInfoAndShow()
        {
            lblFilePath.Content = filePath;

            IMediaInfo info = await FFmpeg.GetMediaInfo(filePath);

            fileFramerate = info.VideoStreams.First().Framerate;
            txtFramesPerSecond.Text = fileFramerate.ToString();

            fileDuration = info.VideoStreams.First().Duration;

            txtEndTimeH.Text = $"{fileDuration.Hours:00}";
            txtEndTimeM.Text = $"{fileDuration.Minutes:00}";
            txtEndTimeS.Text = $"{fileDuration.Seconds:00}";
            txtEndTimeMS.Text = $"{fileDuration.Milliseconds:000}";

            Show();
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Show a progress window and disable the main window
            ProgressWindow progress = new ProgressWindow("Converting video to images", mainWindow)
            {
                Owner = this
            };

            progress.Show();
            Visibility = Visibility.Hidden;

            // Attempt to run the conversion in a new background thread
            Thread thread = new Thread(async () =>
            {
                try
                {
                    await VideoProcessor.ConvertToImageSequence(filePath, targetDirectory, progress.cts, (percent) =>
                    {
                        // Update the progress windows
                        progress.percentage = percent;
                    });

                    // When done load in the files
                    Dispatcher.Invoke(() =>
                    {
                        mainWindow.LoadFolder(filePath, targetDirectory);
                        mainWindow.IsEnabled = true;
                        progress.Close();
                        Close();
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.Close();
                        Visibility = Visibility.Visible;
                    });
                }
                finally
                {
                    // Dispose our now unneeded cancellation token and re-enable the main window
                    progress.cts.Dispose();
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.IsEnabled = true;
            Close();
        }
    }
}
