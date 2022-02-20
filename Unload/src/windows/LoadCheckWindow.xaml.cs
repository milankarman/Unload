using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using unload.Properties;

namespace unload
{
    public partial class LoadCheckWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Project project;
        private readonly MainWindow mainWindow;

        private int loadIndex;
        private int startFrame;
        private int endFrame;

        private bool returningToMainWindow;

        private readonly int largeStepSize = 100;

        public int LoadNumber
        {
            get => loadIndex + 1;
            set
            {
                loadIndex = Math.Clamp(value, 1, project.DetectedLoads.Count) - 1;
                ShowLoadScreen(loadIndex);
                ShowLoadInfo(loadIndex);
                OnPropertyChanged();
            }
        }

        public int StartFrame
        {
            get => startFrame;
            set
            {
                startFrame = Math.Clamp(value, 1, project.totalFrames);
                ShowStartFrame(startFrame);

                DetectedLoad load = project.DetectedLoads[loadIndex];
                load.StartFrame = startFrame;
                UpdateDetectedLoads();

                if (LoadNumber != load.Number) LoadNumber = load.Number;

                ShowLoadInfo(loadIndex);
                OnPropertyChanged();
            }
        }

        public int EndFrame
        {
            get => endFrame;
            set
            {
                endFrame = Math.Clamp(value, 1, project.totalFrames);
                ShowEndFrame(endFrame);

                DetectedLoad load = project.DetectedLoads[loadIndex];
                load.EndFrame = endFrame;
                UpdateDetectedLoads();

                if (LoadNumber != load.Number) LoadNumber = load.Number;

                ShowLoadInfo(loadIndex);
                OnPropertyChanged();
            }
        }

        public LoadCheckWindow(Project _project, MainWindow _mainWindow)
        {
            project = _project;
            mainWindow = _mainWindow;

            Owner = _mainWindow;

            InitializeComponent();
            DataContext = this;
            Title += $" {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion} - Load Checking";

            try
            {
                largeStepSize = Settings.Default.LargeAdjustmentStepSize;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load in user settings. {Environment.NewLine}{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            txtStepSize.Text = largeStepSize.ToString();

            Left = mainWindow.Left;
            Top = mainWindow.Top;
            Height = mainWindow.Height;
            Width = mainWindow.Width;
            WindowState = mainWindow.WindowState;

            txtLoadNumber.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStepSize.PreviewTextInput += TextBoxValidator.ForceInteger;

            UpdateDetectedLoads();

            DetectedLoad firstLoad = project.DetectedLoads[0];
            LoadNumber = 1;
            StartFrame = firstLoad.StartFrame;
            EndFrame = firstLoad.EndFrame;
        }

        private void ShowLoadScreen(int loadIndex)
        {
            DetectedLoad load = project.DetectedLoads[loadIndex];
            StartFrame = load.StartFrame;
            EndFrame = load.EndFrame;
        }

        private void ShowStartFrame(int frameIndex)
        {
            Uri startFrame = new(Path.Join(project.framesDirectory, $"{frameIndex}.jpg"));
            imgStartFrame.Source = new BitmapImage(startFrame);

            Uri startFrameBefore = new(Path.Join(project.framesDirectory, $"{frameIndex - 1}.jpg"));

            if (frameIndex > 1)
            {
                imgStartFrameBefore.Source = new BitmapImage(startFrameBefore);
            }
            else
            {
                imgStartFrameBefore.Source = null;
            }
        }

        private void ShowEndFrame(int frameIndex)
        {
            Uri endFrame = new(Path.Join(project.framesDirectory, $"{frameIndex}.jpg"));
            imgEndFrame.Source = new BitmapImage(endFrame);

            Uri endFrameAfter = new(Path.Join(project.framesDirectory, $"{frameIndex + 1}.jpg"));

            if (frameIndex < project.totalFrames)
            {
                imgEndFrameAfter.Source = new BitmapImage(endFrameAfter);
            }
            else
            {
                imgEndFrameAfter.Source = null;
            }
        }

        private void ShowLoadInfo(int frameIndex)
        {
            DetectedLoad load = project.DetectedLoads[frameIndex];
            int frames = load.EndFrame - load.StartFrame + 1;
            TimeSpan loadTime = TimeSpan.FromSeconds(Math.Round(frames / project.fps, 3));
            txtLoadInfo.Text = $"Frames: {frames}, Duration: {loadTime:mm\\:ss\\.fff}";
        }

        private void UpdateDetectedLoads()
        {
            project.OrderLoads();
            lbxLoads.ItemsSource = new ObservableCollection<DetectedLoad>(project.DetectedLoads);
            sliderTimeline.Maximum = project.DetectedLoads.Count;
        }

        private int GetStepSizeFrames() => (int)(project.fps / (1000 / int.Parse(txtStepSize.Text)));

        private void btnBack_Click(object sender, RoutedEventArgs e) => LoadNumber--;

        private void btnForward_Click(object sender, RoutedEventArgs e) => LoadNumber++;

        private void btnStartFrameBackFar_Click(object sender, RoutedEventArgs e) => StartFrame -= GetStepSizeFrames();

        private void btnStartFrameBack_Click(object sender, RoutedEventArgs e) => StartFrame--;

        private void btnStartFrameForward_Click(object sender, RoutedEventArgs e) => StartFrame++;

        private void btnStartFrameForwardFar_Click(object sender, RoutedEventArgs e) => StartFrame += GetStepSizeFrames();

        private void btnEndFrameBackFar_Click(object sender, RoutedEventArgs e) => EndFrame -= GetStepSizeFrames();

        private void btnEndFrameBack_Click(object sender, RoutedEventArgs e) => EndFrame--;

        private void btnEndFrameForward_Click(object sender, RoutedEventArgs e) => EndFrame++;

        private void btnEndFrameForwardFar_Click(object sender, RoutedEventArgs e) => EndFrame += GetStepSizeFrames();

        private void btnDLoadAdd_Click(object sender, RoutedEventArgs e)
        {
            project.DetectedLoads.Add(new DetectedLoad(0, 1, 1));
            UpdateDetectedLoads();
        }

        private void btnDLoadNumber_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is DetectedLoad load) LoadNumber = load.Number;
        }

        private void btnDLoadDelete_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load)
            {
                project.DetectedLoads.Remove(load);

                if (project.DetectedLoads.Count == 0) project.DetectedLoads.Add(new DetectedLoad(0, 1, 1));
                if (load.Number == LoadNumber) LoadNumber--;

                UpdateDetectedLoads();
            }
        }

        private void btnRemoveLoad_Click(object sender, RoutedEventArgs e)
        {
            project.DetectedLoads.RemoveAt(loadIndex);

            if (project.DetectedLoads.Count == 0) project.DetectedLoads.Add(new DetectedLoad(0, 1, 1));
            LoadNumber--;

            UpdateDetectedLoads();
        }

        private void btnMainWindow_Click(object sender, RoutedEventArgs e)
        {
            returningToMainWindow = true;
            SaveSettings();
            mainWindow.UpdateDetectedLoads();
            mainWindow.Left = Left;
            mainWindow.Top = Top;
            mainWindow.Height = Height;
            mainWindow.Width = Width;
            mainWindow.WindowState = WindowState;
            mainWindow.Show();
            Close();
        }

        private void SaveSettings()
        {
            Settings.Default.LargeAdjustmentStepSize = int.Parse(txtStepSize.Text);
            Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (returningToMainWindow) return;

            mainWindow.Close();

            if (mainWindow != null) e.Cancel = true;
            SaveSettings();
        }

        protected void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
