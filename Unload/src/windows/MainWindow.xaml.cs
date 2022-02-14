using System;
using System.Collections.Generic;
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
        private Project project;

        private readonly List<int> pickedLoadingFrames = new List<int>();
        private readonly List<int> sliderTicks = new List<int>();

        private int pickedLoadingFrameIndex = -1;
        private const double defaultSimilarity = 0.95;

        int videoFrame = 0;

        public MainWindow(Project _project)
        {
            project = _project;

            InitializeComponent();
            Title += $" {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";

            lbxLoads.ItemsSource = project.DetectedLoads;
            txtMinSimilarity.Text = defaultSimilarity.ToString();

            BindValidationMethods();
            SetInitialUIState();
        }

        // Loads in a given frame in the video preview
        private void SetVideoFrame(int frame)
        {
            // Ensure the requested frame exists
            if (frame <= 0 || frame > project.totalFrames) return;

            // Set the video frame and update the interface
            Uri image = new Uri(Path.Join(project.framesDirectory, $"{frame}.jpg"));
            imageVideo.Source = new BitmapImage(image);

            txtVideoFrame.Text = frame.ToString();
            txtMinSimilarity.IsEnabled = true;
        }

        // Sorts and gives proper indexes to the detected loads
        private void SetDetectedLoads()
        {
            //detectedLoadsCollection = new(project.DetectedLoads.OrderBy(i => i.StartFrame));

            //for (int i = 0; i < detectedLoadsCollection.Count; i++)
            //{
            //    detectedLoadsCollection[i].Index = i + 1;
            //}

            //lbxLoads.ItemsSource = detectedLoadsCollection;
        }

        // Calculates the final times and adds them to the interface
        private void CalculateTimes()
        {
            txtTimeOutput.Text = "Time without loads:" + Environment.NewLine + project.GetLoadlessTimeString() + Environment.NewLine;
            txtTimeOutput.Text += "Time with loads:" + Environment.NewLine + project.GetTotalTimeString() + Environment.NewLine;
            txtTimeOutput.Text += "Time spent loading:" + Environment.NewLine + project.GetTimeSpentLoading();

            btnExportTimes.IsEnabled = true;
        }

        // Applies cropping to the picked load screen and shows it on the interface
        private void UpdateLoadPreview()
        {
            if (pickedLoadingFrames.Count >= 1)
            {
                Bitmap image = new Bitmap(Path.Join(project.framesDirectory, $"{pickedLoadingFrames[pickedLoadingFrameIndex]}.jpg"));
                Bitmap croppedImage = ImageProcessor.CropImage(image, CropSlidersToRectangle());
                imageLoadFrame.Source = ImageProcessor.BitmapToBitmapImage(croppedImage);
            }
            else
            {
                imageLoadFrame.Source = null;
            }
        }

        // Reads cropping slider values and returns them in a rectangle class
        private Rectangle CropSlidersToRectangle()
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

        // Checks which buttons for picking loading frames should be enabled/disabled and applies that action
        private void ToggleLoadPickerButtons()
        {
            btnNextLoadFrame.IsEnabled = pickedLoadingFrameIndex < pickedLoadingFrames.Count - 1;
            btnPreviousLoadFrame.IsEnabled = pickedLoadingFrameIndex > 0;

            if (project.HashedFrames == null) btnRemoveLoadFrame.IsEnabled = pickedLoadingFrameIndex >= 0;

            if (pickedLoadingFrames.Count > 1)
            {
                lblPickedLoadCount.Visibility = Visibility.Visible;
                lblPickedLoadCount.Content = $"{pickedLoadingFrameIndex + 1} / {pickedLoadingFrames.Count}";
            }
            else
            {
                lblPickedLoadCount.Visibility = Visibility.Hidden;
            }

            if (pickedLoadingFrames.Count > 0)
            {
                groupLoadDetection.IsEnabled = true;

                if (!btnDetectLoadFrames.IsEnabled) btnPrepareFrames.IsEnabled = true;

                btnCheckSimilarity.IsEnabled = true;
            }
            else
            {
                btnPrepareFrames.IsEnabled = false;
                btnCheckSimilarity.IsEnabled = false;
            }
        }

        // Exports the frame count and load times ranges to a CSV file
        private void btnExportTimes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Comma Seperated Values (*.csv)|*.csv",
                InitialDirectory = project.videoPath,
                // Remove "_frames" from the name for the csv file
                FileName = project.videoPath?.Substring(0, project.videoPath.Length),
                DefaultExt = "csv",
            };

            if (dialog.ShowDialog() == true && dialog.FileName != null)
            {
                try
                {
                    project.ExportCSV(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
        }

        // Adds a new blank detected load
        private void btnAddLoad_Click(object sender, RoutedEventArgs e)
        {
            project.DetectedLoads.Add(new DetectedLoad(0, 0, 0));
            SetDetectedLoads();
            CalculateTimes();
        }

        // Updates the detected load selected frame to the TextBox value
        private void UpdateDetectedLoadStartFrame(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox cmd = (TextBox)sender;
                if (cmd.DataContext is DetectedLoad load) load.StartFrame = int.Parse(cmd.Text);
            }
            catch { }

            SetDetectedLoads();
            CalculateTimes();
        }

        private void UpdateDetectedLoadEndFrame(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox cmd = (TextBox)sender;
                if (cmd.DataContext is DetectedLoad load) load.EndFrame = int.Parse(cmd.Text);
            }
            catch { }

            SetDetectedLoads();
            CalculateTimes();
        }


        // Moves the timeline and updates the video preview to the frame the user entered
        private void txtVideoFrame_TextChanged(object sender, EventArgs e)
        {
            if (project.totalFrames == 0) return;

            if (!string.IsNullOrEmpty(txtVideoFrame.Text))
                videoFrame = Math.Clamp(int.Parse(txtVideoFrame.Text), 1, project.totalFrames);

            txtVideoFrame.Text = videoFrame.ToString();
            SetVideoFrame(videoFrame);
        }

        // Bind detected load update to hitting enter
        private void txtStartFrameDetectedLoad_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) UpdateDetectedLoadStartFrame(sender, e);
        }

        private void txtEndFrameDetectedLoad_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) UpdateDetectedLoadEndFrame(sender, e);
        }

        // Moves the timeline to the selected frame of the detected load
        private void btnGotoStartFrameDetectLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load) txtVideoFrame.Text = load.StartFrame.ToString();
        }

        private void btnGotoEndFrameDetectLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load) txtVideoFrame.Text = load.EndFrame.ToString();
        }

        // Deleted the detected load when the user clicks on the button next to it
        private void btnDeleteDetectedLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            if (cmd.DataContext is DetectedLoad load) project.DetectedLoads.Remove(load);

            CalculateTimes();
        }

        // Methods for adding and removing picked load frames
        private void btnPreviousLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrameIndex--;
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        private void btnNextLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrameIndex++;
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        private void btnAddLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.Add((int)sliderTimeline.Value);
            pickedLoadingFrameIndex++;

            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        private void btnRemoveLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.RemoveAt(pickedLoadingFrameIndex);
            pickedLoadingFrameIndex = Math.Clamp(pickedLoadingFrameIndex, -1, pickedLoadingFrames.Count - 1);
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        private void window_Closed(object sender, EventArgs e) => Application.Current.Shutdown();

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnDetectLoadFrames_Click(object sender, RoutedEventArgs e)
        {
            int minSimilarity = int.Parse(txtMinSimilarity.Text);
            int minFrames = int.Parse(txtMinFrames.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);
            
            project.DetectLoadFrames(minSimilarity, minFrames, pickedLoadingFrames, CropSlidersToRectangle(), concurrentTasks);
        }

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

            Rectangle crop = CropSlidersToRectangle();

            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);


            ProgressWindow progress = new ProgressWindow("Preparing frames", this);
            progress.Show();

            IsEnabled = false;

            Thread thread = new(() => project.PrepareFrames(startFrame, endFrame, crop, concurrentTasks,
                null, null, null));

            thread.IsBackground = true;
            thread.Start();
        }

        private void btnResetFrames_Click(object sender, RoutedEventArgs e) => project.ClearFrames();

        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateLoadPreview();
        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SetVideoFrame((int)sliderTimeline.Value);

        private void cbxSnapLoads_CheckedChanged(object sender, RoutedEventArgs e) => SetTimelineTicks();

        // Update videoFrame along with the video controls
        private void btnBack_Click(object sender, RoutedEventArgs e) => txtVideoFrame.Text = (videoFrame - 1).ToString();
        private void btnBackFar_Click(object sender, RoutedEventArgs e) => txtVideoFrame.Text = (videoFrame - project.fps / (1000 / int.Parse(txtStepSize.Text))).ToString();
        private void btnForwardFar_Click(object sender, RoutedEventArgs e) => txtVideoFrame.Text = (videoFrame + 1).ToString();
        private void btnForward_Click(object sender, RoutedEventArgs e) => txtVideoFrame.Text = (videoFrame + project.fps / (1000 / int.Parse(txtStepSize.Text))).ToString();

        // Set start and end frame buttons update their TextBoxes
        private void btnSetStart_Click(object sender, RoutedEventArgs e) => txtStartFrame.Text = ((int)sliderTimeline.Value).ToString();
        private void btnSetEnd_Click(object sender, RoutedEventArgs e) => txtEndFrame.Text = ((int)sliderTimeline.Value).ToString();

        private void BindValidationMethods()
        {
            txtVideoFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtConcurrentTasks.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStepSize.PreviewTextInput += TextBoxValidator.ForceInteger;

            txtFPS.PreviewTextInput += TextBoxValidator.ForceDouble;
            txtMinSimilarity.PreviewTextInput += TextBoxValidator.ForceDouble;
        }

        // Methods that change the UI state depending on how far the frame count is
        private void SetInitialUIState()
        {
            groupLoadDetection.IsEnabled = false;
            groupDetectedLoads.IsEnabled = false;

            btnExportTimes.IsEnabled = false;
            btnNextLoadFrame.IsEnabled = false;
            btnPreviousLoadFrame.IsEnabled = false;
            btnRemoveLoadFrame.IsEnabled = false;
            btnCheckSimilarity.IsEnabled = false;

            cbxSnapLoads.IsEnabled = false;
            lblPickedLoadCount.Visibility = Visibility.Hidden;
        }

        private void SetFramesHashedState()
        {
            btnAddLoadFrame.IsEnabled = false;
            btnRemoveLoadFrame.IsEnabled = false;

            sliderCropHeight.IsEnabled = false;
            sliderCropWidth.IsEnabled = false;
            sliderCropX.IsEnabled = false;
            sliderCropY.IsEnabled = false;

            txtStartFrame.IsEnabled = false;
            txtEndFrame.IsEnabled = false;

            btnSetEnd.IsEnabled = false;
            btnSetStart.IsEnabled = false;
            btnPrepareFrames.IsEnabled = false;

            btnDetectLoadFrames.IsEnabled = true;
            btnResetFrames.IsEnabled = true;
        }

        private void SetResetFramesUIState()
        {
            sliderTimeline.Ticks.Clear();

            txtLoadFrames.Text = "0";
            txtTimeOutput.Text = string.Empty;
            btnExportTimes.IsEnabled = false;
            cbxSnapLoads.IsEnabled = true;

            sliderCropHeight.IsEnabled = true;
            sliderCropWidth.IsEnabled = true;
            sliderCropX.IsEnabled = true;
            sliderCropY.IsEnabled = true;

            btnAddLoadFrame.IsEnabled = true;
            ToggleLoadPickerButtons();

            groupDetectedLoads.IsEnabled = false;
            txtStartFrame.IsEnabled = true;
            txtEndFrame.IsEnabled = true;
            btnSetEnd.IsEnabled = true;
            btnSetStart.IsEnabled = true;
            btnPrepareFrames.IsEnabled = true;

            btnDetectLoadFrames.IsEnabled = false;
            btnResetFrames.IsEnabled = false;
            sliderTicks.Clear();
            SetTimelineTicks();
        }
    }
}