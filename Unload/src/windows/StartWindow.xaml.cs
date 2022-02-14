using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using Xabe.FFmpeg;

namespace unload
{
    public partial class StartWindow : Window
    {
        public string? workingDirectory = null;

        private const string FRAMES_SUFFIX = "_frames";

        public StartWindow()
        {
            InitializeComponent();

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
        }

        public void LoadProject(string filePath)
        {
            string infoPath = Path.Join(workingDirectory, "conversion-info.json");

            // Attempt to get conversion info from json file, otherwise read values from original video
            if (!File.Exists(infoPath))
            {
                string message = "Couldn't find \"conversion-info.json\" in frames folder. Please convert the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            string jsonString = File.ReadAllText(infoPath);
            ConversionInfo? info = JsonSerializer.Deserialize<ConversionInfo>(jsonString);

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

                totalFrames = Directory.GetFiles(workingDirectory, "*.jpg").Length;
            }

            Project project = new Project(filePath, workingDirectory, totalFrames, fps);

            MainWindow mainWindow = new MainWindow(project);
            mainWindow.Show();
            Close();
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                // Remove symbols from path and append _frames
                string? fileDirectory = Path.GetDirectoryName(dialog.FileName);

                targetDirectory = Path.Join(
                    workingDirectory ?? fileDirectory,
                    RemoveSymbols(dialog.SafeFileName) + FRAMES_SUFFIX);

                if (!Directory.Exists(targetDirectory))
                {
                    MessageBox.Show(
                        $"No {FRAMES_SUFFIX} folder accompanying this video found. Convert the video first.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                LoadProject(dialog.FileName);
            }
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                // Create _frames folder to store the image sequence, ommiting illegal symbols
                string? fileDirectory = Path.GetDirectoryName(dialog.FileName);

                targetDirectory = Path.Join(
                    workingDirectory ?? fileDirectory,
                    RemoveSymbols(dialog.SafeFileName) + FRAMES_SUFFIX);

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                IsEnabled = false;
                ConvertWindow convertWindow = new ConvertWindow(this, dialog.FileName, targetDirectory);
                convertWindow.GetVideoInfoAndShow();
            }
        }


        //// Prepares an image sequence and resets the application state
        //public async void LoadFolder(string file, string dir)
        //{

        //}

        private void btnStartSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        // Removes symbols that conflict with FFmpeg arguments
        private static string RemoveSymbols(string path)
        {
            return Regex.Replace(path, @"[^0-9a-zA-Z\/\\:]+", "");
        }
    }
}
