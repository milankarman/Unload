using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using Newtonsoft.Json;
using unload.Properties;

namespace unload
{
    public partial class StartWindow : Window
    {
        private class PreviousVideo
        {
            public string FileName { get; set; }
            public string FramesDirectory { get; set; }
            public DateTime LastOpened { get; set; }

            public PreviousVideo(string fileName, string framesDirectory, DateTime lastOpened)
            {
                FileName = fileName;
                FramesDirectory = framesDirectory;
                LastOpened = lastOpened;
            }
        }

        public string? workingDirectory;

        private const string FRAMES_SUFFIX = "_frames";

        private ObservableCollection<PreviousVideo> previousVideos;

        public StartWindow()
        {
            InitializeComponent();

            Title += $" {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";

            try
            {
                VideoProcessor.SetFFMpegPath();
            }
            catch
            {
                string message = "Failed to initialize FFMpeg. Make sure ffmpeg.exe and ffprobe.exe are located in the ffmpeg folder of this application.";
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            PreviousVideo[] previousVideosArray = JsonConvert.DeserializeObject<PreviousVideo[]>(Settings.Default.PreviousVideos);
            previousVideos = new(previousVideosArray);

            if (previousVideos.Count > 0)
            {
                lbxPreviousVideos.ItemsSource = previousVideos;
            }
            else
            {
                lbxPreviousVideos.Visibility = Visibility.Hidden;
                lblNoPreviousVideos.Visibility = Visibility.Visible;
            }

            workingDirectory = Settings.Default.WorkingDirectory;
            if (workingDirectory.Length == 0) workingDirectory = null;
        }

        public void LoadProject(string framesDirectory)
        {
            string infoPath = Path.Join(framesDirectory, "conversion-info.json");

            // Check if conversion info file can be found
            if (!File.Exists(infoPath))
            {
                string message = "Couldn't find \"conversion-info.json\" in frames folder. Please convert the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            string jsonString = File.ReadAllText(infoPath);
            ConversionInfo? info = JsonConvert.DeserializeObject<ConversionInfo>(jsonString);

            // Check if conversion info contains data
            if (info == null)
            {
                string message = "Couldn't read \"conversion-info.json\" in frames folder. The file might be corrupted. Please convert the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            string fileName = info.FileName;
            double fps = info.FPS;
            int totalFrames = info.ExpectedFrames;

            // Check if the same amount of converted images are found as the video has frames
            if (!File.Exists(Path.Join(framesDirectory, (totalFrames - 1).ToString() + ".jpg")))
            {
                string message = "Warning, fewer converted frames are found than expected. This could mean that the video has dropped frames.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                totalFrames = Directory.GetFiles(framesDirectory, "*.jpg").Length;
            }

            if (fileName != null)
            {
                // Add to previously opened videos then sort and save them
                PreviousVideo? existing = previousVideos.FirstOrDefault(i => i.FileName == fileName);

                if (existing != null)
                {
                    existing.LastOpened = DateTime.Now;
                }
                else
                {
                    PreviousVideo previousVideo = new(fileName, framesDirectory, DateTime.Now);
                    previousVideos.Add(previousVideo);
                }

                previousVideos = new(previousVideos.OrderByDescending(i => i.LastOpened).Take(5).ToList());

                Settings.Default.PreviousVideos = JsonConvert.SerializeObject(previousVideos);
                Settings.Default.Save();
            }

            // Create the project and start the main window
            Project project = new(framesDirectory, fileName ?? "Unkown", totalFrames, fps);

            MainWindow mainWindow = new(project);
            mainWindow.Show();
            Close();
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new();
            dialog.Filter = "Unload Supported Formats|*.3g2;*.3gp;*.3gp2;*.asf;*.avi;*.dvrms;*.flv;*.h261;*.h263;*.h264;*.m2t;" +
                "*.m2ts;*.m4v;*.mkv;*.mod;*.mov;*.mp4;*.mpg;*.mxf;*.webm;*.wmv;*.xmv;conversion-info.json;";

            if (dialog.ShowDialog() == true)
            {
                string? framesDirectory;

                if (dialog.SafeFileName == "conversion-info.json")
                {
                    framesDirectory = Path.GetDirectoryName(dialog.FileName);
                }
                else
                {
                    string? fileDirectory = Path.GetDirectoryName(dialog.FileName);
                    framesDirectory = Path.Join(workingDirectory ?? fileDirectory,
                        RemoveSymbols(dialog.SafeFileName) + FRAMES_SUFFIX);
                }

                if (framesDirectory == null || !Directory.Exists(framesDirectory))
                {
                    MessageBox.Show(
                        $"No {FRAMES_SUFFIX} folder accompanying this video found. Convert the video first.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                LoadProject(framesDirectory);
            }
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new();

            if (dialog.ShowDialog() == true)
            {
                // Create _frames folder to store the image sequence, ommiting illegal symbols
                string? fileDirectory = Path.GetDirectoryName(dialog.FileName);

                string framesDirectory = Path.Join(workingDirectory ?? fileDirectory,
                    RemoveSymbols(dialog.SafeFileName) + FRAMES_SUFFIX);

                if (!Directory.Exists(framesDirectory))
                {
                    Directory.CreateDirectory(framesDirectory);
                }

                void onFinished()
                {
                    gridContainer.IsEnabled = true;
                    LoadProject(framesDirectory);
                }

                gridContainer.IsEnabled = false;
                ConvertWindow convertWindow = new(this, dialog.FileName, framesDirectory, onFinished);
                convertWindow.GetVideoInfoAndShow();
            }
        }

        private void btnStartSettings_Click(object sender, RoutedEventArgs e)
        {
            StartSettingsWindow startSettingsWindow = new(this, workingDirectory);
            startSettingsWindow.Show();
            gridContainer.IsEnabled = false;
        }

        private void btnPreviousVideoLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is PreviousVideo previousVideo)
            {
                if (!Directory.Exists(previousVideo.FramesDirectory))
                {
                    MessageBox.Show(
                        $"{previousVideo.FramesDirectory} not found. The working directory might have changed or the frames folder " +
                        $"could have been moved or deleted.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                LoadProject(previousVideo.FramesDirectory);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}") { CreateNoWindow = true });
            e.Handled = true;
        }

        // Removes symbols that conflict with FFmpeg arguments
        private static string RemoveSymbols(string path)
        {
            return Regex.Replace(path, @"[^0-9a-zA-Z\/\\:]+", "");
        }
    }
}
