using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Xabe.FFmpeg;

namespace unload
{
    public partial class ConvertWindow : Window
    {
        private readonly StartWindow startWindow;
        private readonly string filePath;
        private readonly string targetDirectory;

        private TimeSpan fileDuration;
        private double fileFramerate;

        public ConvertWindow(StartWindow _startWindow, string _filePath, string _targetDirectory)
        {
            // Remember file info
            Owner = _startWindow;
            startWindow = _startWindow;
            filePath = _filePath;
            targetDirectory = _targetDirectory;

            InitializeComponent();

            // Bind text validation methods to text boxes
            txtEndTimeH.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndTimeM.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndTimeS.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtEndTimeMS.PreviewTextInput += TextBoxValidator.ForceInteger;

            txtStartTimeH.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartTimeM.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartTimeS.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtStartTimeMS.PreviewTextInput += TextBoxValidator.ForceInteger;

            txtFrameWidth.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtFrameHeight.PreviewTextInput += TextBoxValidator.ForceInteger;
            txtFramesPerSecond.PreviewTextInput += TextBoxValidator.ForceDouble;
        }

        // Sets default values from video and shows the window
        public async void GetVideoInfoAndShow()
        {
            tblFilePath.Text = filePath;
            tblOutputFolderPath.Text = targetDirectory;

            // Reads video file info
            IMediaInfo info = await FFmpeg.GetMediaInfo(filePath);

            fileFramerate = info.VideoStreams.First().Framerate;
            txtFramesPerSecond.Text = fileFramerate.ToString();

            fileDuration = info.VideoStreams.First().Duration;

            // Shows and formats times
            txtEndTimeH.Text = $"{fileDuration.Hours:00}";
            txtEndTimeM.Text = $"{fileDuration.Minutes:00}";
            txtEndTimeS.Text = $"{fileDuration.Seconds:00}";
            txtEndTimeMS.Text = $"{fileDuration.Milliseconds:000}";

            Show();
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            // Check if _frames directory exists, otherwise create it
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Get user conversion settings
            TimeSpan startTime = GetStartTime();
            TimeSpan endTime = GetEndTime();
            int width = int.Parse(txtFrameWidth.Text);
            int height = int.Parse(txtFrameHeight.Text);
            double fps = double.Parse(txtFramesPerSecond.Text);
            int expectedFrames = (int)(fps * (endTime - startTime).TotalSeconds);

            ConversionInfo info = new ConversionInfo()
            {
                FPS = fps,
                ExpectedFrames = expectedFrames
            };

            // Save user conversion string into a file in json format
            string jsonString = JsonSerializer.Serialize(info);
            File.WriteAllText(Path.Join(targetDirectory, "conversion-info.json"), jsonString);

            // Show a progress window and hide this window
            ProgressWindow progress = new ProgressWindow("Converting video to images", startWindow);

            progress.Show();
            Visibility = Visibility.Hidden;

            // Attempt to run the conversion in a new background thread
            Thread thread = new Thread(async () =>
            {
                try
                {
                    // Start the video conversion with the user specificied parameters
                    await VideoProcessor.ConvertToImageSequence(filePath, targetDirectory, startTime, endTime, width,
                        height, fps, progress.cts, (percent) =>
                    {
                        // Update the progress windows
                        progress.percentage = percent;
                    });

                    // When done load in the files and re-enable the main window
                    Dispatcher.Invoke(() =>
                    {
                        progress.Close();
                        Close();
                    });
                }
                catch (OperationCanceledException)
                {
                    // If canceled close the progress window and show the conversion settings again
                    Dispatcher.Invoke(() =>
                    {
                        progress.Close();
                        Visibility = Visibility.Visible;
                    });
                }
                finally
                {
                    // Dispose our now unneeded cancellation token
                    progress.cts.Dispose();
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        // Closes the conversion window and re-enables the main window
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            startWindow.IsEnabled = true;
            Close();
        }

        // Reads user start time values and returns it as a timespan
        private TimeSpan GetStartTime()
        {
            int hours = int.Parse(txtStartTimeH.Text);
            int minutes = int.Parse(txtStartTimeM.Text);
            int seconds = int.Parse(txtStartTimeS.Text);
            int milliseconds = int.Parse(txtStartTimeMS.Text);

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        // Reads user end time values and returns it as a timespan
        private TimeSpan GetEndTime()
        {
            int hours = int.Parse(txtEndTimeH.Text);
            int minutes = int.Parse(txtEndTimeM.Text);
            int seconds = int.Parse(txtEndTimeS.Text);
            int milliseconds = int.Parse(txtEndTimeMS.Text);

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        // Ensures start time textbox values are in the right range and format
        private void ClampStartTimeValues()
        {
            if (!IsLoaded)
            {
                return;
            }

            TextBoxValidator.ClampInteger(txtStartTimeM, 0, 59, "00");
            TextBoxValidator.ClampInteger(txtStartTimeS, 0, 59, "00");
            TextBoxValidator.ClampInteger(txtStartTimeMS, 0, 999, "000");

            TimeSpan startTime = GetStartTime();
            TimeSpan endTime = GetEndTime();

            // Ensure start time is not later than the total video time
            if (startTime > fileDuration)
            {
                TextBoxValidator.ClampInteger(txtStartTimeH, 0, fileDuration.Hours, "00");
                TextBoxValidator.ClampInteger(txtStartTimeM, 0, fileDuration.Minutes, "00");
                TextBoxValidator.ClampInteger(txtStartTimeS, 0, fileDuration.Seconds, "00");
                TextBoxValidator.ClampInteger(txtStartTimeMS, 0, fileDuration.Milliseconds, "000");
            }

            // Ensure start time is not later than the end time
            if (startTime > endTime)
            {
                TextBoxValidator.ClampInteger(txtStartTimeH, 0, endTime.Hours, "00");
                TextBoxValidator.ClampInteger(txtStartTimeM, 0, endTime.Minutes, "00");
                TextBoxValidator.ClampInteger(txtStartTimeS, 0, endTime.Seconds, "00");
                TextBoxValidator.ClampInteger(txtStartTimeMS, 0, endTime.Milliseconds, "000");
            }
        }

        // Ensures end time textbox values are in the right range and format
        private void ClampEndTimeValues()
        {
            if (!IsLoaded)
            {
                return;
            }

            TextBoxValidator.ClampInteger(txtEndTimeM, 0, 59);
            TextBoxValidator.ClampInteger(txtEndTimeS, 0, 59);
            TextBoxValidator.ClampInteger(txtEndTimeMS, 0, 999, "000");

            TimeSpan startTime = GetStartTime();
            TimeSpan endTime = GetEndTime();

            // Ensure end time is not later than the total video time
            if (endTime > fileDuration)
            {
                TextBoxValidator.ClampInteger(txtEndTimeH, 0, fileDuration.Hours, "00");
                TextBoxValidator.ClampInteger(txtEndTimeM, 0, fileDuration.Minutes, "00");
                TextBoxValidator.ClampInteger(txtEndTimeS, 0, fileDuration.Seconds, "00");
                TextBoxValidator.ClampInteger(txtEndTimeMS, 0, fileDuration.Milliseconds, "000");
            }

            // Ensure end time is not later than start time
            if (endTime < startTime)
            {
                TextBoxValidator.ClampInteger(txtEndTimeH, startTime.Hours, fileDuration.Hours, "00");
                TextBoxValidator.ClampInteger(txtEndTimeM, startTime.Minutes, fileDuration.Minutes, "00");
                TextBoxValidator.ClampInteger(txtEndTimeS, startTime.Seconds, fileDuration.Seconds, "00");
                TextBoxValidator.ClampInteger(txtEndTimeMS, startTime.Milliseconds, fileDuration.Milliseconds, "000");
            }
        }

        // Ensure end time hours isn't empty and that the values are in the proper range
        private void txtEndTimeH_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeH.Text))
            {
                txtEndTimeH.Text = "00";
            }
            ClampEndTimeValues();
        }

        // Ensure end time minutes isn't empty and that the values are in the proper range
        private void txtEndTimeM_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeM.Text))
            {
                txtEndTimeM.Text = "00";
            }
            ClampEndTimeValues();
        }

        // Ensure end time seconds isn't empty and that the values are in the proper range
        private void txtEndTimeS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeS.Text))
            {
                txtEndTimeS.Text = "00";
            }
            ClampEndTimeValues();
        }

        // Ensure end time milliseconds isn't empty and that the values are in the proper range
        private void txtEndTimeMS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeMS.Text))
            {
                txtEndTimeMS.Text = "000";
            }
            ClampStartTimeValues();
        }

        // Ensure start time hours isn't empty and that the values are in the proper range
        private void txtStartTimeH_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeH.Text))
            {
                txtStartTimeH.Text = "00";
            }
            ClampStartTimeValues();
        }

        // Ensure start time minutes isn't empty and that the values are in the proper range
        private void txtStartTimeM_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeM.Text))
            {
                txtStartTimeM.Text = "00";
            }
            ClampStartTimeValues();
        }

        // Ensure start time seconds isn't empty and that the values are in the proper range
        private void txtStartTimeS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeS.Text))
            {
                txtStartTimeS.Text = "00";
            }
            ClampStartTimeValues();
        }

        // Ensure start milliseconds isn't empty and that the values are in the proper range
        private void txtStartTimeMS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeMS.Text))
            {
                txtStartTimeMS.Text = "000";
            }
            ClampStartTimeValues();
        }

        // Ensure picked framerate isn't higher than the video framerate
        private void txtFramesPerSecond_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.Parse(txtFramesPerSecond.Text) > fileFramerate)
            {
                txtFramesPerSecond.Text = fileFramerate.ToString();
            }
        }
    }
}
