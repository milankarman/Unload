using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace unload
{
    public partial class MainWindow : Window
    {
        private enum ProjectState
        {
            START,
            PICKED_LOADS,
            PREPARED_FRAMES,
            DETECTED_LOADS,
        }

        private readonly Project project;
        private ProjectState projectState;

        private readonly List<int> pickedLoadingFrames = new();
        private readonly List<int> sliderTicks = new();

        private readonly bool ready;

        private int selectedPickedLoad = -1;
        private const double defaultSimilarity = 0.95;

        private bool hasExported;
        private bool shouldOpenStart;

        public MainWindow(Project _project)
        {
            project = _project;

            InitializeComponent();
            Title += $" {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";

            txtMinSimilarity.Text = defaultSimilarity.ToString();

            UpdateDetectedLoads();

            sliderTimeline.Maximum = project.totalFrames;
            txtEndFrame.Text = project.totalFrames.ToString();
            txtFPS.Text = project.fps.ToString();

            BindValidationMethods();
            SetProjectState(ProjectState.START);
            UpdateLoadPickerState();

            ready = true;

            SetVideoFrame(1);
        }

        // Loads in a given frame in the video preview
        private void SetVideoFrame(int frameIndex)
        {
            if (!ready) return;

            frameIndex = Math.Clamp(frameIndex, 1, project.totalFrames);
            txtVideoFrame.Text = frameIndex.ToString();

            // Set the video frame and update the interface
            Uri image = new(Path.Join(project.framesDirectory, $"{frameIndex}.jpg"));
            imageVideo.Source = new BitmapImage(image);

            txtVideoFrame.Text = frameIndex.ToString();
        }

        // Sorts and gives proper indexes to the detected loads
        private void UpdateDetectedLoads()
        {
            project.OrderLoads();
            lbxLoads.ItemsSource = new ObservableCollection<DetectedLoad>(project.DetectedLoads);
        }

        // Applies cropping to the picked load screen and shows it on the interface
        private void UpdateLoadPreview()
        {
            if (pickedLoadingFrames.Count >= 1)
            {
                Bitmap image = new(Path.Join(project.framesDirectory, $"{pickedLoadingFrames[selectedPickedLoad]}.jpg"));
                Bitmap croppedImage = ImageProcessor.CropImage(image, GetCropRect());
                imageLoadFrame.Source = ImageProcessor.BitmapToBitmapImage(croppedImage);
            }
            else
            {
                imageLoadFrame.Source = null;
            }
        }

        // Calculates the final times and adds them to the interface
        private void SetFinalTimes()
        {
            txtTimeOutput.Text = $"Time without loads:{Environment.NewLine}{project.GetLoadlessTime():hh\\:mm\\:ss\\.fff}{Environment.NewLine}";
            txtTimeOutput.Text += $"Time with loads:{Environment.NewLine}{project.GetTotalTime():hh\\:mm\\:ss\\.fff}{Environment.NewLine}";
            txtTimeOutput.Text += $"Time spent loading:{Environment.NewLine}{project.GetTimeSpentLoading():hh\\:mm\\:ss\\.fff}{Environment.NewLine}";
        }

        // Checks which buttons for picking loading frames should be enabled/disabled and applies that action
        private void UpdateLoadPickerState()
        {
            btnNextLoadFrame.IsEnabled = selectedPickedLoad < pickedLoadingFrames.Count - 1;
            btnPreviousLoadFrame.IsEnabled = selectedPickedLoad > 0;

            if (project.HashedFrames == null) btnRemoveLoadFrame.IsEnabled = selectedPickedLoad >= 0;

            if (pickedLoadingFrames.Count > 1)
            {
                lblPickedLoadCount.Visibility = Visibility.Visible;
                lblPickedLoadCount.Content = $"{selectedPickedLoad + 1} / {pickedLoadingFrames.Count}";
            }
            else
            {
                lblPickedLoadCount.Visibility = Visibility.Hidden;
            }

            if (pickedLoadingFrames.Count > 0)
            {
                if (projectState == ProjectState.START)
                {
                    SetProjectState(ProjectState.PICKED_LOADS);
                }

                if (projectState == ProjectState.PREPARED_FRAMES || projectState == ProjectState.DETECTED_LOADS)
                {
                    btnDetectLoadFrames.IsEnabled = true;
                }

                btnRemoveLoadFrame.IsEnabled = true;
            }
            else
            {
                if (projectState == ProjectState.PICKED_LOADS)
                {
                    SetProjectState(ProjectState.START);
                }

                if (projectState == ProjectState.PREPARED_FRAMES || projectState == ProjectState.DETECTED_LOADS)
                {
                    btnDetectLoadFrames.IsEnabled = false;
                }

                btnRemoveLoadFrame.IsEnabled = false;
            }
        }

        // Reads cropping slider values and returns them in a rectangle class
        private Rectangle GetCropRect()
        {
            return new Rectangle
            {
                X = (int)Math.Round(sliderCropX.Value),
                Y = (int)Math.Round(sliderCropY.Value),
                Width = (int)Math.Round(sliderCropWidth.Value),
                Height = (int)Math.Round(sliderCropHeight.Value)
            };
        }

        // Checks if the user wants the timeline to snap to loads and makes it happen
        private void SetTimelineTicks()
        {
            sliderTimeline.Ticks.Clear();

            if (cbxSnapLoads.IsChecked == true)
            {
                foreach (int tick in sliderTicks)
                {
                    sliderTimeline.Ticks.Add(tick);
                }
            }
        }

        private void BindValidationMethods()
        {
            txtVideoFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStepSize.PreviewTextInput += TextBoxValidator.ForceInteger;

            txtFPS.PreviewTextInput += TextBoxValidator.ForceDouble;
            txtMinSimilarity.PreviewTextInput += TextBoxValidator.ForceDouble;
        }

        private void SetProjectState(ProjectState _projectState)
        {
            projectState = _projectState;
            UpdateUIFromState();
        }

        private void UpdateUIFromState()
        {
            switch (projectState)
            {
                case ProjectState.START:
                    groupLoadDetection.IsEnabled = false;
                    groupDetectedLoads.IsEnabled = false;

                    btnDetectLoadFrames.IsEnabled = false;
                    btnPrepareFrames.IsEnabled = false;
                    btnResetFrames.IsEnabled = false;
                    btnCheckSimilarity.IsEnabled = false;
                    btnExportTimes.IsEnabled = false;
                    break;

                case ProjectState.PICKED_LOADS:
                    groupLoadDetection.IsEnabled = true;
                    groupDetectedLoads.IsEnabled = false;

                    btnDetectLoadFrames.IsEnabled = false;
                    btnPrepareFrames.IsEnabled = true;
                    btnResetFrames.IsEnabled = false;
                    btnCheckSimilarity.IsEnabled = true;
                    btnSetStart.IsEnabled = true;
                    btnSetEnd.IsEnabled = true;
                    btnExportTimes.IsEnabled = false;

                    txtStartFrame.IsEnabled = true;
                    txtEndFrame.IsEnabled = true;

                    sliderCropHeight.IsEnabled = true;
                    sliderCropWidth.IsEnabled = true;
                    sliderCropX.IsEnabled = true;
                    sliderCropY.IsEnabled = true;
                    break;

                case ProjectState.PREPARED_FRAMES:
                    groupDetectedLoads.IsEnabled = false;

                    btnPrepareFrames.IsEnabled = false;
                    btnResetFrames.IsEnabled = true;
                    btnDetectLoadFrames.IsEnabled = true;
                    btnSetStart.IsEnabled = false;
                    btnSetEnd.IsEnabled = false;
                    btnExportTimes.IsEnabled = false;

                    txtStartFrame.IsEnabled = false;
                    txtEndFrame.IsEnabled = false;

                    sliderCropHeight.IsEnabled = false;
                    sliderCropWidth.IsEnabled = false;
                    sliderCropX.IsEnabled = false;
                    sliderCropY.IsEnabled = false;
                    break;

                case ProjectState.DETECTED_LOADS:
                    groupDetectedLoads.IsEnabled = true;

                    btnPrepareFrames.IsEnabled = false;
                    btnResetFrames.IsEnabled = true;
                    btnDetectLoadFrames.IsEnabled = true;
                    btnSetStart.IsEnabled = false;
                    btnSetEnd.IsEnabled = false;
                    btnExportTimes.IsEnabled = true;

                    sliderCropHeight.IsEnabled = false;
                    sliderCropWidth.IsEnabled = false;
                    sliderCropX.IsEnabled = false;
                    sliderCropY.IsEnabled = false;
                    break;
            }
        }

        // UI Events
        // Video Navigation
        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            SetVideoFrame((int)sliderTimeline.Value);

        private void txtVideoFrame_TextChanged(object sender, EventArgs e)
        {
            int frameIndex = int.Parse(txtVideoFrame.Text);
            SetVideoFrame(frameIndex);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) =>
            txtVideoFrame.Text = (int.Parse(txtVideoFrame.Text) - 1).ToString();

        private void btnBackFar_Click(object sender, RoutedEventArgs e) =>
            txtVideoFrame.Text = (int.Parse(txtVideoFrame.Text) - (int)(project.fps / (1000 / int.Parse(txtStepSize.Text)))).ToString();

        private void btnForwardFar_Click(object sender, RoutedEventArgs e) =>
            txtVideoFrame.Text = (int.Parse(txtVideoFrame.Text) + 1).ToString();

        private void btnForward_Click(object sender, RoutedEventArgs e) =>
            txtVideoFrame.Text = (int.Parse(txtVideoFrame.Text) + (int)(project.fps / (1000 / int.Parse(txtStepSize.Text)))).ToString();

        // Range selection
        private void btnSetStart_Click(object sender, RoutedEventArgs e) => txtStartFrame.Text = ((int)sliderTimeline.Value).ToString();

        private void btnSetEnd_Click(object sender, RoutedEventArgs e) => txtEndFrame.Text = ((int)sliderTimeline.Value).ToString();

        // Load picking
        private void btnPreviousLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            selectedPickedLoad--;
            UpdateLoadPreview();
            UpdateLoadPickerState();
        }

        private void btnNextLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            selectedPickedLoad++;
            UpdateLoadPreview();
            UpdateLoadPickerState();
        }

        private void btnAddLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.Add(int.Parse(txtVideoFrame.Text));
            selectedPickedLoad++;

            UpdateLoadPreview();
            UpdateLoadPickerState();
        }

        private void btnRemoveLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.RemoveAt(selectedPickedLoad);
            selectedPickedLoad = Math.Clamp(selectedPickedLoad, -1, pickedLoadingFrames.Count - 1);
            UpdateLoadPreview();
            UpdateLoadPickerState();
        }

        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateLoadPreview();

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = pickedLoadingFrames[selectedPickedLoad];
            int videoFrame = int.Parse(txtVideoFrame.Text);
            float similarity = project.GetSimilarity(loadIndex, videoFrame, GetCropRect());

            MessageBox.Show(similarity.ToString());
        }

        // Load detection
        private void btnPrepareFrames_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user on the length of this process
            string text = "This will start the long and intense process of preparing every frame from start to end using the specified cropping." + Environment.NewLine + Environment.NewLine +
                "Make sure your start frame and end frame are set properly, and that the load image cropping is correct. To change these after you will have to reset frames first.";
            MessageBoxResult result = MessageBox.Show(text, "Info", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            Rectangle crop = GetCropRect();

            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);

            ProgressWindow progress = new("Preparing frames", this);
            progress.Show();

            IsEnabled = false;

            void onProgress(double percentage) => progress.percentage = percentage;

            void onFinished()
            {
                Dispatcher.Invoke(() =>
                {
                    progress.Close();
                    IsEnabled = true;
                    SetProjectState(ProjectState.PREPARED_FRAMES);
                });
            }

            Thread thread = new(() =>
            {
                project.PrepareFrames(startFrame, endFrame, crop,
                    progress.cts, onProgress, onFinished);
            });

            thread.IsBackground = true;
            thread.Start();
        }

        private void btnResetFrames_Click(object sender, RoutedEventArgs e)
        {
            project.ClearFrames();
            project.DetectedLoads.Clear();
            UpdateDetectedLoads();
            SetProjectState(ProjectState.PICKED_LOADS);
        }

        private void btnDetectLoadFrames_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double minSimilarity = double.Parse(txtMinSimilarity.Text);
                int minFrames = int.Parse(txtMinFrames.Text);

                project.DetectLoadFrames(minSimilarity, minFrames, pickedLoadingFrames, GetCropRect());

                UpdateDetectedLoads();
                SetProjectState(ProjectState.DETECTED_LOADS);
                SetFinalTimes();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to detect load frames {Environment.NewLine}{ex.Message}");
            }
        }

        // Settings
        private void cbxSnapLoads_CheckedChanged(object sender, RoutedEventArgs e) => SetTimelineTicks();

        private void btnStartWindow_Click(object sender, RoutedEventArgs e) 
        {
            shouldOpenStart = true;
            Close();
        }

        // Detected load frame controls
        private void btnDLoadAdd_Click(object sender, RoutedEventArgs e)
        {
            project.DetectedLoads.Add(new DetectedLoad(0, 0, 0));
            UpdateDetectedLoads();
            SetFinalTimes();
        }

        private void btnDLoadGotoStart_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load) txtVideoFrame.Text = load.StartFrame.ToString();
        }

        private void btnDLoadGotoEnd_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load) txtVideoFrame.Text = load.EndFrame.ToString();
        }

        private void txtDLoadStart_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox cmd = (TextBox)sender;
                if (cmd.DataContext is DetectedLoad load) load.StartFrame = int.Parse(cmd.Text);
            }
            catch { }

            UpdateDetectedLoads();
            SetFinalTimes();
        }

        private void txtDLoadEnd_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox cmd = (TextBox)sender;
                if (cmd.DataContext is DetectedLoad load) load.EndFrame = int.Parse(cmd.Text);
            }
            catch { }

            UpdateDetectedLoads();
            SetFinalTimes();
        }

        private void txtDLoadStart_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) txtDLoadStart_LostFocus(sender, e);
        }

        private void txtDLoadEnd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) txtDLoadEnd_LostFocus(sender, e);
        }

        private void btnDLoadDelete_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load)
            {
                project.DetectedLoads.Remove(load);
                UpdateDetectedLoads();
            }

            SetFinalTimes();
        }

        // Exports the frame count and load times ranges to a CSV file
        private void btnExportTimes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "Comma Seperated Values (*.csv)|*.csv",
                InitialDirectory = project.videoPath,
                FileName = Path.GetFileNameWithoutExtension(project.videoPath),
                DefaultExt = "csv",
            };

            if (dialog.ShowDialog() == true && dialog.FileName != null)
            {
                try
                {
                    project.ExportCSV(dialog.FileName);
                    hasExported = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Window events
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!hasExported && projectState == ProjectState.DETECTED_LOADS)
            {
                MessageBoxResult result = MessageBox.Show("It appears you haven't exported the project yet. Are you sure you want close it?",
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

            if (shouldOpenStart)
            {
                StartWindow startWindow = new();
                startWindow.Show();
            }
        }
    }
}
