using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Input;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Shipwreck.Phash;
using Xabe.FFmpeg;

namespace unload
{
    public partial class MainWindow : Window
    {
        // Keeps the directory of frames being used
        private string workingDirectory = string.Empty;

        private int totalVideoFrames = 0;

        private readonly List<int> pickedLoadingFrames = new List<int>();
        private int pickedLoadingFrameIndex = -1;

        // Dictionary to keep hashed frames for quick comparison against multiple similarities
        Dictionary<int, Digest> hashedFrames = null;

        // List to keep every tick the timeline slider will snap to such as loading screens
        readonly List<int> sliderTicks = new List<int>();

        public MainWindow()
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

            // Set initial interface state
            groupPickLoad.IsEnabled = false;
            groupVideo.IsEnabled = false;
            groupVideoControls.IsEnabled = false;
            groupFrameCount.IsEnabled = false;
            groupLoadDetection.IsEnabled = false;
            groupDetectedLoads.IsEnabled = false;
            btnExportTimes.IsEnabled = false;
            cbxSnapLoads.IsEnabled = false;
            lblPickedLoadCount.Visibility = Visibility.Hidden;
        }

        // Prompts the user for a file to convert and attempts to convert it
        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                // Create _frames folder to store the image sequence, ommiting illegal symbols
                string targetDirectory = RemoveSymbols(dialog.FileName) + "_frames";

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                // Show a progress window and disable the main window
                ProgressWindow progress = new ProgressWindow("Converting video to images", this)
                {
                    Owner = this
                };
                progress.Show();

                IsEnabled = false;

                // Attempt to run the conversion in a new background thread
                Thread thread = new Thread(async () =>
                {
                    try
                    {
                        await VideoProcessor.ConvertToImageSequence(dialog.FileName, targetDirectory, progress.cts, (percent) =>
                        {
                            // Update the progress windows
                            progress.percentage = percent;
                        });

                        // When done load in the files
                        Dispatcher.Invoke(() =>
                        {
                            LoadFolder(dialog.FileName, targetDirectory);
                        });

                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        // Dispose our now unneeded cancellation token and re-enable the main window
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
        }

        // Checks if the video file has been converted, if so it loads it
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                // Remove symbols from path and append _frames
                string targetDirectory = RemoveSymbols(dialog.FileName) + "_frames";

                if (!Directory.Exists(targetDirectory))
                {
                    MessageBox.Show("No _frames folder accompanying this video found. Convert the video first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LoadFolder(dialog.FileName, targetDirectory);
            }
        }

        // Prepares an image sequence and resets the application state
        private async void LoadFolder(string file, string dir)
        {
            workingDirectory = dir;

            // Get the video framerate
            IMediaInfo info = await FFmpeg.GetMediaInfo(file);

            double framerate = info.VideoStreams.First().Framerate;
            TimeSpan duration = info.VideoStreams.First().Duration;
            int expectedFrames = (int)(duration.TotalSeconds * framerate) - 1;

            // Check if the same amount of converted images are found as the video has frames
            if (File.Exists(Path.Join(dir, expectedFrames.ToString() + ".jpg")))
            {
                totalVideoFrames = expectedFrames;
            }
            else
            {
                string message = "Warning, fewer converted frames are found than expected. If you run into issues try converting the video again.";
                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                totalVideoFrames = Directory.GetFiles(dir, "*.jpg").Length;
            }

            txtFPS.Text = framerate.ToString();
            txtStartFrame.Text = "1";
            txtStartFrame.IsEnabled = true;
            txtEndFrame.Text = totalVideoFrames.ToString();
            txtEndFrame.IsEnabled = true;
            txtLoadFrames.Text = "0";
            txtTimeOutput.Clear();

            hashedFrames = null;
            pickedLoadingFrames.Clear();
            pickedLoadingFrameIndex = -1;
            lbxLoads.Items.Clear();

            sliderTimeline.Maximum = totalVideoFrames;
            sliderTimeline.Value = 1;

            imageLoadFrame.Source = null;

            groupPickLoad.IsEnabled = true;
            groupVideo.IsEnabled = true;
            groupVideoControls.IsEnabled = true;
            groupFrameCount.IsEnabled = true;

            btnResetFrames.IsEnabled = false;
            btnDetectLoadFrames.IsEnabled = false;
            btnCheckSimilarity.IsEnabled = false;
            btnNextLoadFrame.IsEnabled = false;
            btnPreviousLoadFrame.IsEnabled = false;
            btnRemoveLoadFrame.IsEnabled = false;
            cbxSnapLoads.IsEnabled = false;

            sliderCropHeight.IsEnabled = true;
            sliderCropWidth.IsEnabled = true;
            sliderCropX.IsEnabled = true;
            sliderCropY.IsEnabled = true;

            btnSetStart.IsEnabled = true;
            btnSetEnd.IsEnabled = true;

            sliderTicks.Clear();
            SetTimelineTicks();
            SetVideoFrame(1);
        }

        // Removes symbols that conflict with FFmpeg arguments
        private static string RemoveSymbols(string path)
        {
            return Regex.Replace(path, @"[^0-9a-zA-Z\/\\:]+", ""); ;
        }

        // Update our video preview when moving the slider
        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Loads in a given frame in the video preview
        private void SetVideoFrame(int frame)
        {
            // Ensure the requested frame should exist
            if (frame <= 0 || frame > totalVideoFrames)
            {
                return;
            }

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
            if (pickedLoadingFrames.Count == 0)
            {
                return;
            }

            UpdateLoadPreview();
        }

        // Notifies the user of the similarity between the selected load frame and the current previewed frame
        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            // Load snd crop the loading screen and current frame
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
                        double percentage = (double)doneFrames / (double)endFrame * 100d;
                        progress.percentage = percentage;
                    });

                    // Enable new options when frame hashing succeeds
                    Dispatcher.Invoke(() =>
                    {
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
                hashedFrames = null;
                lbxLoads.Items.Clear();
                sliderTimeline.Ticks.Clear();

                txtLoadFrames.Text = "0";
                txtTimeOutput.Text = string.Empty;
                btnExportTimes.IsEnabled = false;
                cbxSnapLoads.IsEnabled = true;

                sliderCropHeight.IsEnabled = true;
                sliderCropWidth.IsEnabled = true;
                sliderCropX.IsEnabled = true;
                sliderCropY.IsEnabled = true;

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
            lbxLoads.Items.Clear();
            lbxLoads.Items.Add($"#\tFirst\tLast\tTotal");

            // Read user arguments
            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);

            double minSimilarity = double.Parse(txtSimilarity.Text, CultureInfo.InvariantCulture);

            // Process the user's picked loading frame
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

            // Check every frame similarity against the minimum similarity and list them as loads
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
                        // Print out loading screen number, start and end frame - and total frames
                        lbxLoads.Items.Add($"{loadScreenCounter}\t{currentLoadStartFrame}\t{i - 1}\t{i - currentLoadStartFrame}");

                        // Save screen start and end to snap the timeline slider to later
                        sliderTicks.Add(currentLoadStartFrame);
                        sliderTicks.Add(i - 1);

                        subsequentLoadFrame = false;
                        currentLoadStartFrame = 0;
                    }
                }
            }

            // Save start frame, end frame, first frame and last frame to snap the timeline slider to later
            sliderTicks.Add(startFrame);
            sliderTicks.Add(1);
            sliderTicks.Add(endFrame);
            sliderTicks.Add(totalVideoFrames);

            // Update the interface
            txtLoadFrames.Text = loadFrameCounter.ToString();
            btnDetectLoadFrames.IsEnabled = true;
            cbxSnapLoads.IsEnabled = true;
            groupDetectedLoads.IsEnabled = true;

            // Set the timeline ticks and calculate the final times
            SetTimelineTicks();
            CalculateTimes();
        }

        // Calculates the final times
        private void btnCalcTimes_Click(object sender, RoutedEventArgs e)
        {
            CalculateTimes();
        }

        // Verifies user input is correct and returns the total (with loads) time formatted as a string
        private string GetTotalTimeString()
        {
            if (!IsValidFramedata())
            {
                return "Error. Make sure start/end frame and FPS are filled in properly.";
            }

            double framesPerSecond = double.Parse(txtFPS.Text, CultureInfo.InvariantCulture);
            int totalFrames = int.Parse(txtEndFrame.Text) - int.Parse(txtStartFrame.Text) + 1;

            if (totalFrames <= 0)
            {
                return "End frame must be after start frame.";
            }

            if (framesPerSecond <= 0)
            {
                return "Frames per second must be greater than 0.";
            }

            double totalSecondsDouble = totalFrames / framesPerSecond;
            int totalSecondsInt = (int)totalSecondsDouble;
            double totalMilliseconds = totalSecondsDouble - totalSecondsInt;
            TimeSpan timeWithLoads = TimeSpan.FromSeconds(totalSecondsDouble);

            return $"{timeWithLoads:hh\\:mm\\:ss}.{Math.Round(totalMilliseconds * 1000, 0)}";
        }

        // Verifies user input is correct and returns the loadless time formatted as a string
        private string GetLoadlessTimeString()
        {
            if (!IsValidFramedata())
            {
                return "Error. Make sure start/end frame and FPS are filled in properly.";
            }

            double framesPerSecond = double.Parse(txtFPS.Text, CultureInfo.InvariantCulture);
            int totalFrames = int.Parse(txtEndFrame.Text) - int.Parse(txtStartFrame.Text) + 1;

            if (totalFrames <= 0)
            {
                return "End frame must be after start frame.";
            }

            if (framesPerSecond <= 0)
            {
                return "Frames per second must be greater than 0.";
            }

            int loadlessFrames = totalFrames - int.Parse(txtLoadFrames.Text);
            double loadlessSecondsDouble = loadlessFrames / framesPerSecond;
            int loadlessSecondsInt = (int)loadlessSecondsDouble;
            double loadlessMilliseconds = loadlessSecondsDouble - loadlessSecondsInt;
            TimeSpan timeWithoutLoads = TimeSpan.FromSeconds(loadlessSecondsDouble);

            return $"{timeWithoutLoads:hh\\:mm\\:ss}.{Math.Round(loadlessMilliseconds * 1000, 0)}";
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
            txtTimeOutput.Text = "Time without loads:" + Environment.NewLine;
            txtTimeOutput.Text += GetLoadlessTimeString() + Environment.NewLine;

            txtTimeOutput.Text += "Time with loads:" + Environment.NewLine;
            txtTimeOutput.Text += GetTotalTimeString();

            // Enable the export button when times are calculated
            btnExportTimes.IsEnabled = true;
        }

        // Move the video preview to the selected loading screen
        private void lbxLoads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbxLoads.SelectedItem != null && lbxLoads.SelectedIndex > 0)
            {
                int frame = int.Parse(lbxLoads.SelectedItem.ToString().Split("\t")[1]);

                sliderTimeline.Value = frame;
                SetVideoFrame(frame);
            }
        }

        // Move the timeline back 0.25 seconds
        private void btnBackFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= (int)Math.Round(int.Parse(txtFPS.Text) / 4d);

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline back 1 frame
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline forward 0.25 seconds
        private void btnForwardFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        // Move the timeline forward 1 frame
        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += (int)Math.Round(int.Parse(txtFPS.Text) / 4d);

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
                Filter = "Comma Seperated Values (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;

                // Read all load screens into to write to file
                List<string> lines = new List<string>();

                foreach (string loadString in lbxLoads.Items)
                {
                    lines.Add(loadString.Replace('\t', ','));
                }

                // Add final times into list to write to file
                lines.Add("");
                lines.Add($"Time without loads,{GetLoadlessTimeString()}");
                lines.Add($"Time with loads,{GetTotalTimeString()}");

                // Write all lines to file
                File.WriteAllLinesAsync(path, lines);
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

        // When applied to a text box this prevents anything other than a round number to be entered
        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // When applied to a text box this prevents anything other than any number to be entered
        private void DoubleValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // Ensures every thread is shut down when the main window closes
        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Toggles snapping the timeline to load frames
        private void cbxSnapLoads_Checked(object sender, RoutedEventArgs e)
        {
            SetTimelineTicks();
        }

        // Toggles snapping the timeline to load frames
        private void cbxSnapLoads_Unchecked(object sender, RoutedEventArgs e)
        {
            SetTimelineTicks();
        }

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

        private void ToggleLoadPickerButtons()
        {
            btnNextLoadFrame.IsEnabled = pickedLoadingFrameIndex < pickedLoadingFrames.Count - 1;
            btnPreviousLoadFrame.IsEnabled = pickedLoadingFrameIndex > 0;
            btnRemoveLoadFrame.IsEnabled = pickedLoadingFrameIndex >= 0;

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