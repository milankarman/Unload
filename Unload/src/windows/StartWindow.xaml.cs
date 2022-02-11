using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Shipwreck.Phash;
using unload.Properties;
using Xabe.FFmpeg;

namespace unload
{
    public partial class StartWindow : Window
    {
        public string? workingDirectory = null;
        private string? targetDirectory = null;
        private const string FRAMES_SUFFIX = "_frames";

        public StartWindow()
        {
            InitializeComponent();

            // Confirm FFmpeg is available
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

        public void StartMainWindow(string fileName)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.LoadFolder(fileName, targetDirectory);
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

                StartMainWindow(dialog.FileName);
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
