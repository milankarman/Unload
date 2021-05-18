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
        string workingDirectory = string.Empty;

        int totalVideoFrames = 0;
        int pickedLoadingFrame = 0;

        Dictionary<int, Digest> hashedFrames = null;
        List<int> sliderTicks = new List<int>();

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                VideoProcessor.SetFFMpegPath();
            }
            catch
            {
                MessageBox.Show("Failed to initialize FFMpeg. Make sure ffmpeg.exe and ffprobe.exe are located in the ffmpeg folder of this application.");
                Application.Current.Shutdown();
            }

            groupPickLoad.IsEnabled = false;
            groupVideo.IsEnabled = false;
            groupVideoControls.IsEnabled = false;
            groupFrameCount.IsEnabled = false;
            groupLoadDetection.IsEnabled = false;
            groupDetectedLoads.IsEnabled = false;
            btnExportTimes.IsEnabled = false;
            cbxSnapLoads.IsEnabled = false;
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                string targetDirectory = dialog.FileName + "_frames";

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                ProgressWindow progress = new ProgressWindow("Converting Video");
                progress.Owner = this;
                progress.Show();

                IsEnabled = false;

                Thread thread = new Thread(async () =>
                {
                    try
                    {
                        await VideoProcessor.ConvertToImageSequence(dialog.FileName, targetDirectory, progress.cts, (percent) =>
                        {
                            progress.percentage = percent;
                        });

                        await Dispatcher.Invoke(async () =>
                        {
                            IMediaInfo info = await FFmpeg.GetMediaInfo(dialog.FileName);
                            txtFPS.Text = info.VideoStreams.First()?.Framerate.ToString();
                            LoadFolder(targetDirectory);
                        });
                
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        progress.cts.Dispose();

                        Dispatcher.Invoke(() =>
                        {
                            IsEnabled = true;
                        });
                    }
                })
                {
                    IsBackground = true
                };

                thread.Start();


            }
        }

        private async void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                string targetDirectory = dialog.FileName + "_frames";

                if (!Directory.Exists(targetDirectory))
                {
                    MessageBox.Show("No _frames folder accompanying this video found. Convert the video first.");
                    return;
                }

                IMediaInfo info = await FFmpeg.GetMediaInfo(dialog.FileName);
                txtFPS.Text = info.VideoStreams.First()?.Framerate.ToString();
                LoadFolder(targetDirectory);
            }
        }

        private void LoadFolder(string dir)
        {
            workingDirectory = dir;
            totalVideoFrames = Directory.GetFiles(dir, "*.jpg").Length;

            txtStartFrame.Text = "1";
            txtStartFrame.IsEnabled = true;
            txtEndFrame.Text = totalVideoFrames.ToString();
            txtEndFrame.IsEnabled = true;
            txtLoadFrames.Text = "0";
            txtTimeOutput.Clear();

            hashedFrames = null;
            pickedLoadingFrame = 0;
            lbxLoads.Items.Clear();

            sliderTimeline.Maximum = totalVideoFrames;
            sliderTimeline.Value = 1;

            imageLoadFrame.Source = null;

            groupPickLoad.IsEnabled = true;
            groupVideo.IsEnabled = true;
            groupVideoControls.IsEnabled = true;
            groupFrameCount.IsEnabled = true;

            btnClearFrameHashes.IsEnabled = false;
            btnDetectLoadFrames.IsEnabled = false;
            btnCheckSimilarity.IsEnabled = false;
            cbxSnapLoads.IsEnabled = false;

            btnSetStart.IsEnabled = true;
            btnSetEnd.IsEnabled = true;

            sliderTicks.Clear();
            SetTimelineTicks();
            SetVideoFrame(1);
        }

        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void SetVideoFrame(int frame)
        {
            if (frame <= 0 || frame > totalVideoFrames)
            {
                return;
            }

            Uri image = new Uri(Path.Join(workingDirectory, $"{frame}.jpg"));
            txtFrame.Text = frame.ToString(); ;
            imageVideo.Source = new BitmapImage(image);

            txtSimilarity.IsEnabled = true;
        }

        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pickedLoadingFrame <= 0)
            {
                return;
            }

            UpdateCropPreview();
        }

        private void btnPickLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            btnIndexFrameHashes.IsEnabled = true;
            groupLoadDetection.IsEnabled = true;
            btnCheckSimilarity.IsEnabled = true;

            btnDetectLoadFrames.IsEnabled = false;
            btnClearFrameHashes.IsEnabled = false;

            pickedLoadingFrame = (int)sliderTimeline.Value;
            UpdateCropPreview();
        }

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            Rectangle crop = CropSlidersToRectangle();

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            loadFrame = ImageProcessor.CropImage(loadFrame, crop);

            Bitmap currentFrame = new Bitmap(Path.Join(workingDirectory, $"{(int)sliderTimeline.Value}.jpg"));
            currentFrame = ImageProcessor.CropImage(currentFrame, crop);

            MessageBox.Show($"Similarity: {ImageProcessor.CompareBitmapPhash(loadFrame, currentFrame)}");
        }

        private void btnIndexFrameHashes_Click(object sender, RoutedEventArgs e)
        {
            string text = "This will start the long and intense process of hashing every frame from start to end using the specified cropping." + Environment.NewLine + Environment.NewLine +
                "Make sure your start frame, end frame and load image are set properly before doing this, to change these you will have the clear the hash.";
            string caption = "Info";

            MessageBoxResult result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);
            Rectangle crop = CropSlidersToRectangle();

            ProgressWindow progress = new ProgressWindow("Indexing Frame Hashes");
            progress.Owner = this;
            progress.Show();

            IsEnabled = false;

            Thread thread = new Thread(() =>
            {
                try
                {
                    int doneFrames = 0;

                    hashedFrames = ImageProcessor.CropAndPhashFolder(workingDirectory, crop, startFrame, endFrame, concurrentTasks, progress.cts, () =>
                    {
                        doneFrames += 1;
                        double percentage = doneFrames / endFrame * 100;
                        progress.percentage = percentage;
                    });

                    progress.finished = true;
                    Dispatcher.Invoke(() => FinishIndexingFrameHashes());
                }
                catch (OperationCanceledException) { }
                finally
                {
                    progress.cts.Dispose();
                    Dispatcher.Invoke(() => IsEnabled = true);
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        private void FinishIndexingFrameHashes()
        {
            groupPickLoad.IsEnabled = false;
            txtStartFrame.IsEnabled = false;
            txtEndFrame.IsEnabled = false;

            btnSetEnd.IsEnabled = false;
            btnSetStart.IsEnabled = false;
            btnIndexFrameHashes.IsEnabled = false;

            btnDetectLoadFrames.IsEnabled = true;
            btnClearFrameHashes.IsEnabled = true;
        }

        private void btnClearFrameHashes_Click(object sender, RoutedEventArgs e)
        {
            string text = "Doing this will require the frame hashes to be indexed again. Are you sure?";
            string caption = "Info";

            MessageBoxResult result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                hashedFrames = null;
                lbxLoads.Items.Clear();
                sliderTimeline.Ticks.Clear();

                txtLoadFrames.Text = "0";
                txtTimeOutput.Text = string.Empty;
                btnExportTimes.IsEnabled = false;
                cbxSnapLoads.IsEnabled = true;

                groupPickLoad.IsEnabled = true;
                groupDetectedLoads.IsEnabled = false;
                txtStartFrame.IsEnabled = true;
                txtEndFrame.IsEnabled = true;
                btnSetEnd.IsEnabled = true;
                btnSetStart.IsEnabled = true;
                btnIndexFrameHashes.IsEnabled = true;

                btnDetectLoadFrames.IsEnabled = false;
                btnClearFrameHashes.IsEnabled = false;
                sliderTicks.Clear();
                SetTimelineTicks();
            }
        }

        private void btnDetectLoadFrames_Click(object sender, RoutedEventArgs e)
        {
            lbxLoads.Items.Clear();
            lbxLoads.Items.Add($"#\tFirst\tLast\tTotal");

            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);
            int concurrentTasks = int.Parse(txtConcurrentTasks.Text);

            double minSimilarity = double.Parse(txtSimilarity.Text, CultureInfo.InvariantCulture);

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            Rectangle cropPercentage = CropSlidersToRectangle();
            loadFrame = ImageProcessor.CropImage(loadFrame, cropPercentage);

            Dictionary<int, float> frameSimilarities = ImageProcessor.GetHashDictSimilarity(hashedFrames, loadFrame, concurrentTasks);

            int loadScreenCounter = 0;
            int loadFrameCounter = 0;

            int currentLoadStartFrame = 0;
            bool subsequentLoadFrame = false;

            for (int i = startFrame; i < endFrame; i++)
            {
                if (frameSimilarities[i] > minSimilarity && i < endFrame)
                {
                    loadFrameCounter += 1;

                    if (!subsequentLoadFrame)
                    {
                        loadScreenCounter += 1;
                        currentLoadStartFrame = i;
                        subsequentLoadFrame = true;
                    }
                }
                else
                {
                    if (subsequentLoadFrame)
                    {
                        lbxLoads.Items.Add($"{loadScreenCounter}\t{currentLoadStartFrame}\t{i - 1}\t{i - currentLoadStartFrame}");

                        sliderTicks.Add(currentLoadStartFrame);
                        sliderTicks.Add(i - 1);
                        subsequentLoadFrame = false;
                        currentLoadStartFrame = 0;
                    }
                }
            }

            sliderTicks.Add(startFrame);
            sliderTicks.Add(1);
            sliderTicks.Add(endFrame);
            sliderTicks.Add(totalVideoFrames);

            txtLoadFrames.Text = loadFrameCounter.ToString();
            btnDetectLoadFrames.IsEnabled = true;
            cbxSnapLoads.IsEnabled = true;
            groupDetectedLoads.IsEnabled = true;

            SetTimelineTicks();
            CalculateTimes();
        }

        private void btnCalcTimes_Click(object sender, RoutedEventArgs e)
        {
            CalculateTimes();
        }

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

            return $"{timeWithLoads.ToString(@"hh\:mm\:ss")}.{Math.Round(totalMilliseconds * 1000, 0)}";
        }

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

            return $"{timeWithoutLoads.ToString(@"hh\:mm\:ss")}.{Math.Round(loadlessMilliseconds * 1000, 0)}";
        }

        public bool IsValidFramedata()
        {
            if (string.IsNullOrEmpty(txtFPS.Text) || string.IsNullOrEmpty(txtEndFrame.Text) || string.IsNullOrEmpty(txtStartFrame.Text) || string.IsNullOrEmpty(txtLoadFrames.Text))
            {
                return false;
            }

            return true;
        }

        private void CalculateTimes()
        {
            txtTimeOutput.Text = "Time without loads:" + Environment.NewLine;
            txtTimeOutput.Text += GetLoadlessTimeString() + Environment.NewLine;

            txtTimeOutput.Text += "Time with loads:" + Environment.NewLine;
            txtTimeOutput.Text += GetTotalTimeString();

            btnExportTimes.IsEnabled = true;
        }

        private void lbxLoads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbxLoads.SelectedItem != null && lbxLoads.SelectedIndex > 0)
            {
                int frame = 0;

                frame = int.Parse(lbxLoads.SelectedItem.ToString().Split("\t")[1]);

                sliderTimeline.Value = frame;
                SetVideoFrame(frame);
            }
        }

        private void btnBackFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= (int)Math.Round(int.Parse(txtFPS.Text) / 4d);

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void btnForwardFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += (int)Math.Round(int.Parse(txtFPS.Text) / 4d);

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void btnSetStart_Click(object sender, RoutedEventArgs e)
        {
            txtStartFrame.Text = sliderTimeline.Value.ToString();
        }

        private void btnSetEnd_Click(object sender, RoutedEventArgs e)
        {
            txtEndFrame.Text = sliderTimeline.Value.ToString();
        }

        private void UpdateCropPreview()
        {
            Bitmap image = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            Bitmap croppedImage = ImageProcessor.CropImage(image, CropSlidersToRectangle());
            imageLoadFrame.Source = ImageProcessor.BitmapToBitmapImage(croppedImage);
        }

        private Rectangle CropSlidersToRectangle()
        {
            Rectangle cropPercentage = new Rectangle();

            cropPercentage.X = (int)Math.Round(sliderCropX.Value);
            cropPercentage.Y = (int)Math.Round(sliderCropY.Value);
            cropPercentage.Width = (int)Math.Round(sliderCropWidth.Value);
            cropPercentage.Height = (int)Math.Round(sliderCropHeight.Value);

            return cropPercentage;
        }

        private void txtFrame_TextChanged(object sender, EventArgs e)
        {
            if (totalVideoFrames <= 0)
            {
                return;
            }

            int frame = 1;

            if (!string.IsNullOrEmpty(txtFrame.Text))
            {
                frame = Math.Clamp(int.Parse(txtFrame.Text), 1, totalVideoFrames);
            }

            sliderTimeline.Value = frame;
            txtFrame.Text = frame.ToString();

            SetVideoFrame(frame);
        }

        private void btnExportTimes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Comma Seperated Values (*.csv)|*.csv|All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;

                List<string> lines = new List<string>();

                foreach (string loadString in lbxLoads.Items)
                {
                    lines.Add(loadString.Replace('\t', ','));
                }

                lines.Add("");
                lines.Add($"Time without loads,{GetLoadlessTimeString()}");
                lines.Add($"Time with loads,{GetTotalTimeString()}");


                File.WriteAllLinesAsync(path, lines);
            }
        }

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

        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void DoubleValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void cbxSnapLoads_Checked(object sender, RoutedEventArgs e)
        {
            SetTimelineTicks();
        }

        private void cbxSnapLoads_Unchecked(object sender, RoutedEventArgs e)
        {
            SetTimelineTicks();
        }
    }
}