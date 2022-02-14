using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using unload.Properties;

namespace unload
{
    public partial class StartWindow : Window
    {
        public string? workingDirectory = null;

        private const string FRAMES_SUFFIX = "_frames";

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

            workingDirectory = Settings.Default.WorkingDirectory;
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new();

            if (dialog.ShowDialog() == true)
            {
                // Remove symbols from path and append _frames
                string? fileDirectory = Path.GetDirectoryName(dialog.FileName);

                string framesDirectory = Path.Join(workingDirectory ?? fileDirectory,
                    RemoveSymbols(dialog.SafeFileName) + FRAMES_SUFFIX);

                if (!Directory.Exists(framesDirectory))
                {
                    MessageBox.Show(
                        $"No {FRAMES_SUFFIX} folder accompanying this video found. Convert the video first.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                LoadProject(dialog.FileName, framesDirectory);
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

                IsEnabled = false;
                ConvertWindow convertWindow = new(this, dialog.FileName, framesDirectory);
                convertWindow.GetVideoInfoAndShow();
            }
        }

        private void btnStartSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LoadProject(string filePath, string framesDirectory)
        {
            string infoPath = Path.Join(framesDirectory, "conversion-info.json");

            // Attempt to get conversion info from json file, otherwise read values from original video
            if (!File.Exists(infoPath))
            {
                string message = "Couldn't find \"conversion-info.json\" in frames folder. Please convert the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            string jsonString = File.ReadAllText(infoPath);
            ConversionInfo? info = JsonConvert.DeserializeObject<ConversionInfo>(jsonString);

            if (info == null)
            {
                string message = "Couldn't read \"conversion-info.json\" in frames folder. The file might be corrupted. Please convert the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            double fps = info.FPS;
            int totalFrames = info.ExpectedFrames;

            // Check if the same amount of converted images are found as the video has frames
            if (!File.Exists(Path.Join(workingDirectory, totalFrames.ToString() + ".jpg")))
            {
                string message = "Warning, fewer converted frames are found than expected. This could mean that the video has dropped frames.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                totalFrames = Directory.GetFiles(framesDirectory, "*.jpg").Length;
            }

            Project project = new(filePath, framesDirectory, totalFrames, fps);

            MainWindow mainWindow = new(project);
            mainWindow.Show();
            Close();
        }

        // Removes symbols that conflict with FFmpeg arguments
        private static string RemoveSymbols(string path)
        {
            return Regex.Replace(path, @"[^0-9a-zA-Z\/\\:]+", "");
        }
    }
}
