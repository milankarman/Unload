﻿using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Xabe.FFmpeg;

namespace unload
{
    public partial class ConvertWindow : Window
    {
        private MainWindow mainWindow;
        private string filePath;
        private string targetDirectory;

        private TimeSpan fileDuration;
        private double fileFramerate;

        public ConvertWindow(MainWindow _mainWindow, string _filePath, string _targetDirectory)
        {
            mainWindow = _mainWindow;
            filePath = _filePath;
            targetDirectory = _targetDirectory;

            InitializeComponent();
        }

        public async void GetVideoInfoAndShow()
        {
            lblFilePath.Content = filePath;

            IMediaInfo info = await FFmpeg.GetMediaInfo(filePath);

            fileFramerate = info.VideoStreams.First().Framerate;
            txtFramesPerSecond.Text = fileFramerate.ToString();

            fileDuration = info.VideoStreams.First().Duration;

            txtEndTimeH.Text = $"{fileDuration.Hours:00}";
            txtEndTimeM.Text = $"{fileDuration.Minutes:00}";
            txtEndTimeS.Text = $"{fileDuration.Seconds:00}";
            txtEndTimeMS.Text = $"{fileDuration.Milliseconds:000}";

            Show();
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Show a progress window and disable the main window
            ProgressWindow progress = new ProgressWindow("Converting video to images", mainWindow)
            {
                Owner = this
            };

            progress.Show();
            Visibility = Visibility.Hidden;

            // Attempt to run the conversion in a new background thread
            Thread thread = new Thread(async () =>
            {
                try
                {
                    await VideoProcessor.ConvertToImageSequence(filePath, targetDirectory, progress.cts, (percent) =>
                    {
                        // Update the progress windows
                        progress.percentage = percent;
                    });

                    // When done load in the files
                    Dispatcher.Invoke(() =>
                    {
                        mainWindow.LoadFolder(filePath, targetDirectory);
                        mainWindow.IsEnabled = true;
                        progress.Close();
                        Close();
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.Close();
                        Visibility = Visibility.Visible;
                    });
                }
                finally
                {
                    // Dispose our now unneeded cancellation token and re-enable the main window
                    progress.cts.Dispose();
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.IsEnabled = true;
            Close();
        }

        private void txtStartTimeH_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStartTimeH.Text))
            {
                ClampStartTimeValues();
            }
        }

        private void txtStartTimeM_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStartTimeM.Text))
            {
                ClampStartTimeValues();
            }
        }

        private void txtStartTimeS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStartTimeS.Text))
            {
                ClampStartTimeValues();
            }
        }

        private void txtStartTimeMS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStartTimeMS.Text))
            {
                ClampStartTimeValues();
            }
        }

        private TimeSpan GetStartTime()
        {
            int hours = int.Parse(txtStartTimeH.Text);
            int minutes = int.Parse(txtStartTimeM.Text);
            int seconds = int.Parse(txtStartTimeS.Text);
            int milliseconds = int.Parse(txtStartTimeMS.Text);

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        private TimeSpan GetEndTime()
        {
            int hours = int.Parse(txtEndTimeH.Text);
            int minutes = int.Parse(txtEndTimeM.Text);
            int seconds = int.Parse(txtEndTimeS.Text);
            int milliseconds = int.Parse(txtEndTimeMS.Text);

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        private void ClampStartTimeValues()
        {
            if (!IsLoaded)
            {
                return;
            }

            ClampTextboxValue(txtStartTimeM, 0, 59);
            ClampTextboxValue(txtStartTimeS, 0, 59);
            ClampTextboxValue(txtStartTimeMS, 0, 999, "000");

            TimeSpan startTime = GetStartTime();
            TimeSpan endTime = GetEndTime();

            if (startTime > fileDuration)
            {
                ClampTextboxValue(txtStartTimeH, 0, fileDuration.Hours);
                ClampTextboxValue(txtStartTimeM, 0, fileDuration.Minutes);
                ClampTextboxValue(txtStartTimeS, 0, fileDuration.Seconds);
                ClampTextboxValue(txtStartTimeMS, 0, fileDuration.Milliseconds, "000");
            }

            if (startTime > endTime)
            {
                ClampTextboxValue(txtStartTimeH, 0, endTime.Hours);
                ClampTextboxValue(txtStartTimeM, 0, endTime.Minutes);
                ClampTextboxValue(txtStartTimeS, 0, endTime.Seconds);
                ClampTextboxValue(txtStartTimeMS, 0, endTime.Milliseconds, "000");
            }
        }

        private void ClampEndTimeValues()
        {
            if (!IsLoaded)
            {
                return;
            }

            ClampTextboxValue(txtEndTimeM, 0, 59);
            ClampTextboxValue(txtEndTimeS, 0, 59);
            ClampTextboxValue(txtEndTimeMS, 0, 999, "000");

            TimeSpan startTime = GetStartTime();
            TimeSpan endTime = GetEndTime();

            if (endTime > fileDuration)
            {
                ClampTextboxValue(txtEndTimeH, 0, fileDuration.Hours);
                ClampTextboxValue(txtEndTimeM, 0, fileDuration.Minutes);
                ClampTextboxValue(txtEndTimeS, 0, fileDuration.Seconds);
                ClampTextboxValue(txtEndTimeMS, 0, fileDuration.Milliseconds, "000");
            }

            if (endTime < startTime)
            {
                ClampTextboxValue(txtEndTimeH, startTime.Hours, fileDuration.Hours);
                ClampTextboxValue(txtEndTimeM, startTime.Minutes, fileDuration.Minutes);
                ClampTextboxValue(txtEndTimeS, startTime.Seconds, fileDuration.Seconds);
                ClampTextboxValue(txtEndTimeMS, startTime.Milliseconds, fileDuration.Milliseconds, "000");
            }
        }

        private static void ClampTextboxValue(TextBox textBox, int minValue, int maxValue, string formatting = "00")
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                int value = Math.Clamp(int.Parse(textBox.Text), minValue, maxValue);
                textBox.Text = value.ToString(formatting);
            }
        }

        // When applied to a text box this prevents anything other than a round number to be entered
        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void txtEndTimeH_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeH.Text))
            {
                txtEndTimeH.Text = "00";
            }
            ClampEndTimeValues();
        }

        private void txtEndTimeM_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeM.Text))
            {
                txtEndTimeM.Text = "00";
            }
            ClampEndTimeValues();
        }

        private void txtEndTimeS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeS.Text))
            {
                txtEndTimeS.Text = "00";
            }
            ClampEndTimeValues();
        }

        private void txtEndTimeMS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtEndTimeMS.Text))
            {
                txtEndTimeMS.Text = "000";
            }
            ClampStartTimeValues();
        }

        private void txtStartTimeH_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeH.Text))
            {
                txtStartTimeH.Text = "00";
            }
            ClampStartTimeValues();
        }

        private void txtStartTimeM_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeM.Text))
            {
                txtStartTimeM.Text = "00";
            }
            ClampStartTimeValues();
        }

        private void txtStartTimeS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeS.Text))
            {
                txtStartTimeS.Text = "00";
            }
            ClampStartTimeValues();
        }

        private void txtStartTimeMS_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtStartTimeMS.Text))
            {
                txtStartTimeMS.Text = "000";
            }
            ClampStartTimeValues();
        }
    }
}