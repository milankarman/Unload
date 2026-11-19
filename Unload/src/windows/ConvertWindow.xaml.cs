using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Xabe.FFmpeg;

namespace unload;

public partial class ConvertWindow : Window
{
    private readonly StartWindow startWindow;
    private readonly string filePath;
    private readonly string targetDirectory;

    private TimeSpan fileDuration;
    private double fileFramerate;
    private readonly Action onFinished;

    public ConvertWindow(StartWindow _startWindow, string _filePath, string _targetDirectory, Action _onFinished)
    {
        // Remember file info
        Owner = _startWindow;
        Owner.LocationChanged += new EventHandler(OnOwnerMoves);

        startWindow = _startWindow;
        filePath = _filePath;
        targetDirectory = _targetDirectory;
        onFinished = _onFinished;

        InitializeComponent();

        // Bind text validation methods to text boxes
        TextBoxValidator.ForceInteger(txtEndTimeH);
        TextBoxValidator.ForceInteger(txtEndTimeM);
        TextBoxValidator.ForceInteger(txtEndTimeS);
        TextBoxValidator.ForceInteger(txtEndTimeMS);

        TextBoxValidator.ForceInteger(txtStartTimeH);
        TextBoxValidator.ForceInteger(txtStartTimeM);
        TextBoxValidator.ForceInteger(txtStartTimeS);
        TextBoxValidator.ForceInteger(txtStartTimeMS);

        TextBoxValidator.ForceInteger(txtFrameWidth);
        TextBoxValidator.ForceInteger(txtFrameHeight);
        TextBoxValidator.ForceDouble(txtFramesPerSecond);
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

    // Reads user start time values and returns it as a timespan
    private TimeSpan GetStartTime()
    {
        int hours = TextBoxValidator.GetInteger(txtStartTimeH, 0);
        int minutes = TextBoxValidator.GetInteger(txtStartTimeM, 0);
        int seconds = TextBoxValidator.GetInteger(txtStartTimeS, 0);
        int milliseconds = TextBoxValidator.GetInteger(txtStartTimeMS, 0);

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }

    // Reads user end time values and returns it as a timespan
    private TimeSpan GetEndTime()
    {
        int hours = TextBoxValidator.GetInteger(txtEndTimeH, 0);
        int minutes = TextBoxValidator.GetInteger(txtEndTimeM, 0);
        int seconds = TextBoxValidator.GetInteger(txtEndTimeS, 0);
        int milliseconds = TextBoxValidator.GetInteger(txtEndTimeMS, 0);

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

    private void OnOwnerMoves(object? sender, EventArgs e)
    {
        Left = Owner.Left + (Owner.Width - ActualWidth) / 2;
        Top = Owner.Top + (Owner.Height - ActualHeight) / 2;
    }

    private void btnConvert_Click(object sender, RoutedEventArgs e)
    {
        // Get user conversion settings
        TimeSpan startTime = GetStartTime();
        TimeSpan endTime = GetEndTime();
        int width = TextBoxValidator.GetInteger(txtFrameWidth, 640);
        int height = TextBoxValidator.GetInteger(txtFrameHeight, 360);
        double fps = TextBoxValidator.GetDouble(txtFramesPerSecond, fileFramerate);
        int expectedFrames = (int)(fps * (endTime - startTime).TotalSeconds);

        // Covers an empty time range and a 0 framerate.
        // Checked before deleting the _frames directory so existing frames are kept.
        if (expectedFrames < 1)
        {
            MessageBox.Show(
                "The selected time range and framerate don't produce any frames.",
                "Nothing to convert",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Check if _frames directory exists, otherwise create it
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }

        Directory.CreateDirectory(targetDirectory);

        ConversionInfo info = new(Path.GetFileName(filePath), fps, expectedFrames);

        // Save user conversion string into a file in json format
        string jsonString = JsonSerializer.Serialize(info);
        File.WriteAllText(Path.Join(targetDirectory, "conversion-info.json"), jsonString);

        // Show a progress window and hide this window
        ProgressWindow progress = new("Converting video to images", startWindow);

        progress.Show();
        Visibility = Visibility.Hidden;

        void onProgress(double percentage) => progress.percentage = percentage;

        Thread thread = new(async () =>
        {
            try
            {
                await VideoProcessor.ConvertToImageSequence(filePath, targetDirectory, startTime, endTime, width,
                    height, fps, progress.cts, onProgress);
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
                Dispatcher.Invoke(() =>
                {
                    progress.cts.Dispose();
                    progress.Close();
                    Close();
                    onFinished();
                });
            }
        });

        thread.IsBackground = true;
        thread.Start();
    }

    // Closes the conversion window and re-enables the main window
    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        startWindow.gridContainer.IsEnabled = true;
        Close();
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

    // Ensure picked framerate is a number no higher than the video framerate
    private void txtFramesPerSecond_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(txtFramesPerSecond.Text, out double fps) || fps > fileFramerate)
        {
            txtFramesPerSecond.Text = fileFramerate.ToString();
        }
    }
}
