﻿using System;
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
    public partial class MainWindow : Window
    {
        // Keeps the directory of frames being used
        public string? workingDirectory = null;
        private string? targetDirectory = null;

        private int totalVideoFrames = 0;

        private readonly List<int> pickedLoadingFrames = new List<int>();
        private ObservableCollection<DetectedLoad> detectedLoads = new ObservableCollection<DetectedLoad>();
        private int pickedLoadingFrameIndex = -1;

        private const double defaultSimilarity = 0.95;

        // Variables to track user actions to store in export
        private double usedMinSimilarity = 0;
        private int usedMinFrames = 0;

        // Dictionary to keep hashed frames for quick comparison against multiple similarities
        private Dictionary<int, Digest> hashedFrames = new Dictionary<int, Digest>();

        // List to keep every tick the timeline slider will snap to such as loading screens
        private readonly List<int> sliderTicks = new List<int>();

        private const string FRAMES_SUFFIX = "_frames";

        public MainWindow()
        {
            InitializeComponent();
            Title += $" {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";

            lbxLoads.ItemsSource = detectedLoads;

            txtFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndFrame.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtConcurrentTasks.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStepSize.PreviewTextInput += TextBoxValidator.ForceInteger;

            txtFPS.PreviewTextInput += TextBoxValidator.ForceDouble;
            txtSimilarity.PreviewTextInput += TextBoxValidator.ForceDouble;
            txtSimilarity.Text = defaultSimilarity.ToString();

            // Set initial interface state
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

        // Prepares an image sequence and resets the application state
        public async void LoadFolder(string file, string dir)
        {
            workingDirectory = dir;
            double fps;
            int expectedFrames;

            string infoPath = Path.Join(dir, "conversion-info.json");

            // Attempt to get conversion info from json file, otherwise read values from original video
            if (File.Exists(infoPath))
            {
                string jsonString = File.ReadAllText(infoPath);
                ConversionInfo? info = JsonSerializer.Deserialize<ConversionInfo>(jsonString);

                if (info == null)
                {
                    string message = "Couldn't read \"conversion-info.json\" in frames folder. The file might be corrupted.";
                    MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                fps = info.FPS;
                expectedFrames = info.ExpectedFrames;
            }
            else
            {
                string message = "Couldn't find \"conversion-info.json\" in frames folder. If you converted with a custom frame rate then make sure to adjust for it manually.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                IMediaInfo info = await FFmpeg.GetMediaInfo(file);
                TimeSpan duration = info.VideoStreams.First().Duration;

                fps = info.VideoStreams.First().Framerate;
                expectedFrames = (int)(duration.TotalSeconds * fps);
            }

            // Check if the same amount of converted images are found as the video has frames
            if (File.Exists(Path.Join(dir, expectedFrames.ToString() + ".jpg")))
            {
                totalVideoFrames = expectedFrames;
            }
            else
            {
                string message = "Warning, fewer converted frames are found than expected. This could mean that the video has dropped frames.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                totalVideoFrames = Directory.GetFiles(dir, "*.jpg").Length;
            }

            txtEndFrame.Text = totalVideoFrames.ToString();
            txtFPS.Text = fps.ToString();

            sliderTimeline.Maximum = totalVideoFrames;
            sliderTimeline.Value = 1;

            SetVideoFrame(1);
        }


        // Update our video preview when moving the slider
        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SetVideoFrame((int)sliderTimeline.Value);

        // Loads in a given frame in the video preview
        private void SetVideoFrame(int frame)
        {
            // Ensure the requested frame exists
            if (frame <= 0 || frame > totalVideoFrames) return;

            // Set the video frame and update the interface
            Uri image = new Uri(Path.Join(workingDirectory, $"{frame}.jpg"));
            imageVideo.Source = new BitmapImage(image);

            txtFrame.Text = frame.ToString();
            txtSimilarity.IsEnabled = true;
        }

        // Update the cropping of the selected load screen when the cropping sliders change
        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Ensure a load frame is picked beforehand
            if (pickedLoadingFrames.Count > 0)
            { 
                UpdateLoadPreview();
            }
        }

        // Notifies the user of the similarity between the selected load frame and the current previewed frame
        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            // Load and crop the loading screen and current frame
            Rectangle crop = CropSlidersToRectangle();

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrames[pickedLoadingFrameIndex]}.jpg"));
            loadFrame = ImageProcessor.CropImage(loadFrame, crop);

            Bitmap currentFrame = new Bitmap(Path.Join(workingDirectory, $"{(int)sliderTimeline.Value}.jpg"));
            currentFrame = ImageProcessor.CropImage(currentFrame, crop);

            MessageBox.Show($"Similarity: {ImageProcessor.CompareBitmapPhash(loadFrame, currentFrame)}");
        }

        // Hashes every frame into a dictionary so similarity can be quickly checked and adjust without having to hash again.
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

            // Read user defined arguments
            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);

            Rectangle crop = CropSlidersToRectangle();

            // Create a new progress window to track progress
            ProgressWindow progress = new ProgressWindow("Preparing frames", this)
            {
                Owner = this
            };
            progress.Show();

            // Disable the main window
            IsEnabled = false;

            // Attempt to hash every frame in a new thread
            Thread thread = new Thread(() =>
            {
                try
                {
                    int doneFrames = 0;

                    hashedFrames = ImageProcessor.CropAndPhashFolder(workingDirectory, crop, startFrame, endFrame, concurrentTasks, progress.cts, () =>
                    {
                        // Notify the progress window when a new frame is hashed
                        doneFrames += 1;
                        double percentage = doneFrames / (double)endFrame * 100d;
                        progress.percentage = percentage;
                    });

                    // Enable new options when frame hashing succeeds
                    Dispatcher.Invoke(() =>
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
                    });
                }
                catch (OperationCanceledException) { }
                finally
                {
                    // Dispose our now unneeded cancellation token and re-enable the main window even if hashing failed
                    progress.cts.Dispose();
                    Dispatcher.Invoke(() =>
                    {
                        IsEnabled = true;
                        progress.Close();
                    });
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        // Clears the frame hash dictionairy and reenables load screen picking
        private void btnResetFrames_Click(object sender, RoutedEventArgs e)
        {
            // Warn user of the consequences of this action
            string text = "Doing this will require the frames to be prepared again. Are you sure?";
            MessageBoxResult result = MessageBox.Show(text, "Info", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Clear dictionairy and update interface states
                hashedFrames.Clear();
                detectedLoads.Clear();
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

        // Compares the hashed frames against the picked loading screen and counts frames above the specified similarity as load frames
        private void btnDetectLoadFrames_Click(object sender, RoutedEventArgs e)
        {
            // Reset the listbox and add headers
            detectedLoads.Clear();

            // Read user arguments
            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);

            double minSimilarity = double.Parse(txtSimilarity.Text);
            int minFrames = int.Parse(txtMinFrames.Text);

            // Crop and store the user's picked loading frames
            Bitmap[] loadFrames = new Bitmap[pickedLoadingFrames.Count];

            for (int i = 0; i < pickedLoadingFrames.Count; i++)
            {
                Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrames[i]}.jpg"));
                Rectangle cropPercentage = CropSlidersToRectangle();
                loadFrames[i] = ImageProcessor.CropImage(loadFrame, cropPercentage);
            }

            // Store the similarity of all frames and store this in another dictionary
            Dictionary<int, float[]> frameSimilarities = ImageProcessor.GetHashDictSimilarity(hashedFrames, loadFrames, concurrentTasks);

            int loadScreenCounter = 0;
            int loadFrameCounter = 0;

            int currentLoadStartFrame = 0;
            bool subsequentLoadFrame = false;

            // Check every frame similarities to the load images against the minimum similarity and list them as loads
            for (int i = startFrame; i < endFrame; i++)
            {
                for (int j = 0; j < frameSimilarities[i].Length; j++)
                {
                    if (frameSimilarities[i][j] > minSimilarity && i < endFrame)
                    {
                        loadFrameCounter += 1;

                        // If the previous frame wasn't a load frame then mark this as a new loading screen
                        if (!subsequentLoadFrame)
                        {
                            loadScreenCounter += 1;
                            currentLoadStartFrame = i;
                            subsequentLoadFrame = true;
                        }

                        break;
                    }
                    else if (j >= frameSimilarities[i].Length - 1 && subsequentLoadFrame)
                    {
                        int currentLoadEndFrame = i - 1;
                        int currentLoadTotalFrames = currentLoadEndFrame - currentLoadStartFrame + 1;

                        // Check if the detected loading screen matches the minimum number of frames set by user
                        if (currentLoadTotalFrames >= minFrames)
                        {
                            // Print out loading screen number, start and end frame - and total frames
                            detectedLoads.Add(new DetectedLoad(loadScreenCounter, currentLoadStartFrame, currentLoadEndFrame));

                            // Save screen start and end to snap the timeline slider to later
                            sliderTicks.Add(currentLoadStartFrame);
                            sliderTicks.Add(currentLoadEndFrame);
                        }

                        subsequentLoadFrame = false;
                        currentLoadStartFrame = 0;
                    }
                }
            }

            // Update the detected loads box and count the total load frames
            SetDetectedLoads();
            CountLoadFrames();

            // Save start frame, end frame, first frame and last frame to snap the timeline slider to later
            sliderTicks.Add(startFrame);
            sliderTicks.Add(1);
            sliderTicks.Add(endFrame);
            sliderTicks.Add(totalVideoFrames);

            // Update the interface
            btnDetectLoadFrames.IsEnabled = true;
            cbxSnapLoads.IsEnabled = true;
            groupDetectedLoads.IsEnabled = true;

            // Save settings used for detecting loads
            usedMinSimilarity = minSimilarity;
            usedMinFrames = minFrames;

            // Set the timeline ticks and calculate the final times
            SetTimelineTicks();
            CalculateTimes();
        }

        // Sorts and gives proper indexes to the detected loads
        private void SetDetectedLoads()
        {
            detectedLoads = new ObservableCollection<DetectedLoad>(detectedLoads.OrderBy(i => i.StartFrame));

            for (int i = 0; i < detectedLoads.Count; i++)
            {
                detectedLoads[i].Index = i + 1;
            }

            lbxLoads.ItemsSource = detectedLoads;
        }

        // Adds up the loads entered in the detected loads
        private void CountLoadFrames()
        {
            int frames = 0;

            foreach (DetectedLoad load in detectedLoads)
            {
                frames += load.EndFrame - load.StartFrame + 1;
            }

            txtLoadFrames.Text = frames.ToString();
        }

        private void btnAddLoad_Click(object sender, RoutedEventArgs e)
        {
            detectedLoads.Add(new DetectedLoad(0, 0, 0));
            SetDetectedLoads();
        }

        // Checks if all required fields for frame counting are filled in
        public bool IsValidFramedata()
        {
            if (string.IsNullOrEmpty(txtFPS.Text) || string.IsNullOrEmpty(txtEndFrame.Text) || string.IsNullOrEmpty(txtStartFrame.Text) || string.IsNullOrEmpty(txtLoadFrames.Text))
            {
                return false;
            }

            return true;
        }

        // Calculates the final times and adds them to the interface
        private void CalculateTimes()
        {
            double framesPerSecond = double.Parse(txtFPS.Text);
            int totalFrames = int.Parse(txtEndFrame.Text) - int.Parse(txtStartFrame.Text) + 1;
            int loadFrames = int.Parse(txtLoadFrames.Text);

            if (!IsValidFramedata())
            {
                string message = "Error. Make sure start/end frame and FPS are filled in properly.";
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtTimeOutput.Text = "Time without loads:" + Environment.NewLine + TimeCalculator.GetLoadlessTimeString(framesPerSecond, totalFrames, loadFrames) + Environment.NewLine;
            txtTimeOutput.Text += "Time with loads:" + Environment.NewLine + TimeCalculator.GetTotalTimeString(framesPerSecond, totalFrames) + Environment.NewLine;
            txtTimeOutput.Text += "Time spent loading:" + Environment.NewLine + TimeCalculator.GetTimeSpentLoadingString(framesPerSecond, totalFrames, loadFrames);

            // Enable the export button when times are calculated
            btnExportTimes.IsEnabled = true;
        }

        // Move the timeline back 1 frame
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline back 0.25 seconds
        private void btnBackFar_Click(object sender, RoutedEventArgs e)
        {
            double framesToSubtract = double.Parse(txtFPS.Text) / (1000 / double.Parse(txtStepSize.Text));
            sliderTimeline.Value -= (int)Math.Round(framesToSubtract);

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline forward 1 frame seconds
        private void btnForwardFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline forward by the entered step size
        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            double framesToAdvance = double.Parse(txtFPS.Text) / (1000 / double.Parse(txtStepSize.Text));
            sliderTimeline.Value += (int)Math.Round(framesToAdvance);

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Mark the current video preview frame as the start frame for timing
        private void btnSetStart_Click(object sender, RoutedEventArgs e)
        {
            txtStartFrame.Text = sliderTimeline.Value.ToString();
        }

        // Mark the current video preivew frame as the end frame for timing
        private void btnSetEnd_Click(object sender, RoutedEventArgs e)
        {
            txtEndFrame.Text = sliderTimeline.Value.ToString();
        }

        // Applies cropping to the picked load screen and shows it on the interface
        private void UpdateLoadPreview()
        {
            if (pickedLoadingFrames.Count >= 1)
            {
                Bitmap image = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrames[pickedLoadingFrameIndex]}.jpg"));
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
            Rectangle cropPercentage = new Rectangle
            {
                X = (int)Math.Round(sliderCropX.Value),
                Y = (int)Math.Round(sliderCropY.Value),
                Width = (int)Math.Round(sliderCropWidth.Value),
                Height = (int)Math.Round(sliderCropHeight.Value)
            };

            return cropPercentage;
        }

        // Moves the timeline and updates the video preview to the frame the user entered
        private void txtFrame_TextChanged(object sender, EventArgs e)
        {
            // Ensure this action is not attempted when no frames are loaded
            if (totalVideoFrames <= 0)
            {
                return;
            }

            // Limit the selected frame to frames that exist
            int frame = 1;

            if (!string.IsNullOrEmpty(txtFrame.Text))
            {
                frame = Math.Clamp(int.Parse(txtFrame.Text), 1, totalVideoFrames);
            }

            // Update the interface
            sliderTimeline.Value = frame;
            txtFrame.Text = frame.ToString();

            SetVideoFrame(frame);
        }

        // Exports the frame count and load times ranges to a CSV file
        private void btnExportTimes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Comma Seperated Values (*.csv)|*.csv",
                InitialDirectory = workingDirectory,
                // Remove "_frames" from the name for the csv file
                FileName = targetDirectory?.Substring(0, targetDirectory.Length - FRAMES_SUFFIX.Length),
                DefaultExt = "csv",
            };

            if (dialog.ShowDialog() == true)
            {
                int startFrame = int.Parse(txtStartFrame.Text);
                int endFrame = int.Parse(txtEndFrame.Text);
                int totalFrames = startFrame - endFrame + 1;

                if (!IsValidFramedata())
                {
                    string message = "Error. Make sure start/end frame and FPS are filled in properly.";
                    MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (dialog.FileName != null)
                {
                    try
                    {
                        ExportGenerator.ExportAndSave(dialog.FileName, double.Parse(txtFPS.Text), totalFrames, int.Parse(txtLoadFrames.Text), detectedLoads, usedMinSimilarity, usedMinFrames,
                            startFrame, endFrame);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK,MessageBoxImage.Error);
                    }
                }
            }
        }

        // Deleted the detected load when the user clicks on the button next to it
        private void btnDeleteDetectedLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;

            if (cmd.DataContext is DetectedLoad load)
            {
                detectedLoads.Remove(load);
            }

            SetDetectedLoads();
            CountLoadFrames();
            CalculateTimes();
        }

        // Updates the detected load start frame to the textbox value
        private void UpdateDetectedLoadStartFrame(object sender)
        {
            TextBox cmd = (TextBox)sender;

            if (cmd.DataContext is DetectedLoad load)
            {
                try
                {
                    detectedLoads[detectedLoads.IndexOf(load)].StartFrame = int.Parse(cmd.Text);
                }
                catch { }
            }

            SetDetectedLoads();
            CountLoadFrames();
            CalculateTimes();
        }

        // Bind detected load update to text changing
        private void txtStartFrameDetectedLoad_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdateDetectedLoadStartFrame(sender);
        }

        // Bind detected load update to hitting enter
        private void txtStartFrameDetectedLoad_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateDetectedLoadStartFrame(sender);
            }
        }

        // Updates the detected load start frame to the textbox value
        private void UpdateDetectedLoadEndFrame(object sender)
        {
            TextBox cmd = (TextBox)sender;

            if (cmd.DataContext is DetectedLoad load)
            {
                try
                {
                    detectedLoads[detectedLoads.IndexOf(load)].EndFrame = int.Parse(cmd.Text);
                }
                catch { }
            }

            SetDetectedLoads();
            CountLoadFrames();
            CalculateTimes();
        }

        // Bind detected load update to text changing
        private void txtEndFrameDetectedLoad_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdateDetectedLoadEndFrame(sender);
        }

        // Bind detected load update to hitting enter
        private void txtEndFrameDetectedLoad_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateDetectedLoadEndFrame(sender);
            }
        }

        // Moves the timeline to the start frame of the detected load
        private void btnGotoStartFrameDetectLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;

            if (cmd.DataContext is DetectedLoad load)
            {
                sliderTimeline.Value = load.StartFrame;
                txtFrame.Text = load.StartFrame.ToString();
            }
        }

        // Moves the timeline to the end frame of the detected load
        private void btnGotoEndFrameDetectLoad_Click(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;

            if (cmd.DataContext is DetectedLoad load)
            {
                sliderTimeline.Value = load.EndFrame;
                txtFrame.Text = load.EndFrame.ToString();
            }
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

        // Ensures every thread is shut down when the main window closes
        private void window_Closed(object sender, EventArgs e) => Application.Current.Shutdown();

        // Toggles snapping the timeline to load frames
        private void cbxSnapLoads_CheckedChanged(object sender, RoutedEventArgs e) => SetTimelineTicks();

        // Switch to the previous picked loading frames
        private void btnPreviousLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrameIndex--;
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        // Switch to the next picked loading frame
        private void btnNextLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrameIndex++;
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        // Pick and add the current video frame as loading frame
        private void btnAddLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.Add((int)sliderTimeline.Value);
            pickedLoadingFrameIndex++;

            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        // Remove the current loading frame from the list of picked loading frames
        private void btnRemoveLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrames.RemoveAt(pickedLoadingFrameIndex);
            pickedLoadingFrameIndex = Math.Clamp(pickedLoadingFrameIndex, -1, pickedLoadingFrames.Count - 1);
            UpdateLoadPreview();
            ToggleLoadPickerButtons();
        }

        // Checks which buttons for picking loading frames should be enabled/disabled and applies that action
        private void ToggleLoadPickerButtons()
        {
            btnNextLoadFrame.IsEnabled = pickedLoadingFrameIndex < pickedLoadingFrames.Count - 1;
            btnPreviousLoadFrame.IsEnabled = pickedLoadingFrameIndex > 0;

            if (hashedFrames == null)
            {
                btnRemoveLoadFrame.IsEnabled = pickedLoadingFrameIndex >= 0;
            }

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

                if (!btnDetectLoadFrames.IsEnabled)
                {
                    btnPrepareFrames.IsEnabled = true;
                }

                btnCheckSimilarity.IsEnabled = true;
            }
            else
            {
                btnPrepareFrames.IsEnabled = false;
                btnCheckSimilarity.IsEnabled = false;
            }
        }
    }
}