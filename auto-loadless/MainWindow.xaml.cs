using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        string workingDirectory = string.Empty;

        int totalVideoFrames = 0;
        int pickedLoadingFrame = 0;

        Dictionary<int, Digest> hashedFrames = null;

        public MainWindow()
        {
            InitializeComponent();

            groupPickLoad.IsEnabled = false;
            groupVideo.IsEnabled = false;
            groupVideoControls.IsEnabled = false;
            groupFrameCount.IsEnabled = false;
            groupLoadDetection.IsEnabled = false;
            groupDetectedLoadFrames.IsEnabled = false;

            btnIndexFrameHashes.IsEnabled = false;
            btnCheckSimilarity.IsEnabled = false;
            btnDetectLoadFrames.IsEnabled = false;
            btnClearFrameHashes.IsEnabled = false;

            txtSimilarity.IsEnabled = false;
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

                FFMPEG.ConvertToImageSequence(dialog.FileName, targetDirectory);
                MessageBox.Show("Done!");
            }
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                string targetDirectory = dialog.FileName + "_frames";

                if (!Directory.Exists(targetDirectory))
                {
                    MessageBox.Show("No _frames folder accompanying this video found. Convert it first.");
                    return;
                }

                totalVideoFrames = Directory.GetFiles(targetDirectory, "*.jpg").Length;
                workingDirectory = targetDirectory;

                sliderTimeline.Minimum = 1;
                sliderTimeline.Maximum = totalVideoFrames;
                sliderTimeline.Value = 1;

                groupPickLoad.IsEnabled = true;
                groupVideo.IsEnabled = true;
                groupVideoControls.IsEnabled = true;
                groupFrameCount.IsEnabled = true;
                groupLoadDetection.IsEnabled = true;
                groupDetectedLoadFrames.IsEnabled = true;

                SetVideoFrame(1);
            }
        }

        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetVideoFrame((int)Math.Round(sliderTimeline.Value));
        }

        private void SetVideoFrame(int frame)
        {
            if (frame <= 0 || frame >= totalVideoFrames)
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
            btnIndexFrameHashes.IsEnabled = true;
        }

        private void btnPickLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            btnCheckSimilarity.IsEnabled = true;
            pickedLoadingFrame = (int)sliderTimeline.Value;
            UpdateCropPreview();
        }

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            Rectangle crop = CropSlidersToRectange();

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            loadFrame = ImageProcessor.CropImage(loadFrame, crop);

            Bitmap currentFrame = new Bitmap(Path.Join(workingDirectory, $"{(int)Math.Round(sliderTimeline.Value)}.jpg"));
            currentFrame = ImageProcessor.CropImage(currentFrame, crop);

            MessageBox.Show($"Similarity: {ImageProcessor.CompareBitmapPhash(loadFrame, currentFrame)}");
        }

        private void btnIndexFrameHashes_Click(object sender, RoutedEventArgs e)
        {
            string text = "This will start the lengthy process of hashing every frame from start to end." + Environment.NewLine +
                "Make sure your start frame, end frame and load image are set properly before doing this, changing these after requires the frames to be hashed again.";
            string caption = "Information";

            MessageBoxResult result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);

            hashedFrames = ImageProcessor.CropAndPhashFolder(workingDirectory, CropSlidersToRectange(), startFrame, endFrame);

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
            string text = "Doing this value will require the frame hashes to be indexed again. Are you sure?";
            string caption = "Information";

            MessageBoxResult result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                hashedFrames = null;

                groupPickLoad.IsEnabled = true;
                txtStartFrame.IsEnabled = true;
                txtEndFrame.IsEnabled = true;
                btnSetEnd.IsEnabled = true;
                btnSetStart.IsEnabled = true;
                btnIndexFrameHashes.IsEnabled = true;

                btnDetectLoadFrames.IsEnabled = false;
                btnClearFrameHashes.IsEnabled = false;
            }
        }

        private void btnDetectLoadFrames_Click(object sender, RoutedEventArgs e)
        {
            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);

            double minSimilarity = double.Parse(txtSimilarity.Text, CultureInfo.InvariantCulture);

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            Rectangle cropPercentage = CropSlidersToRectange();
            loadFrame = ImageProcessor.CropImage(loadFrame, cropPercentage);
            Digest loadFrameHash = ImagePhash.ComputeDigest(loadFrame.ToLuminanceImage());

            Dictionary<int, float> frameSimilarities = ImageProcessor.GetHashDictSimilarity(hashedFrames, loadFrame);

            int loadCounter = 0;

            for (int i = startFrame; i < endFrame; i++)
            {
                if (frameSimilarities[i] > minSimilarity)
                {
                    lbxLoads.Items.Add(i + "\t" + frameSimilarities[i]);
                    loadCounter += 1;
                }
            }

            txtLoadFrames.Text = loadCounter.ToString();
            btnDetectLoadFrames.IsEnabled = true;

            MessageBox.Show($"Done. {loadCounter} frames of loading found.");
        }

        private void btnCalcTimes_Click(object sender, RoutedEventArgs e)
        {
            double framesPerSecond = double.Parse(txtFPS.Text, CultureInfo.InvariantCulture);

            int totalFrames = int.Parse(txtEndFrame.Text) - int.Parse(txtStartFrame.Text);
            int loadlessFrames = totalFrames - int.Parse(txtLoadFrames.Text);

            double totalSeconds = totalFrames / framesPerSecond;
            double loadlessSeconds = loadlessFrames / framesPerSecond;

            TimeSpan timeWithLoads = TimeSpan.FromSeconds(totalSeconds);
            TimeSpan timeWithoutLoads = TimeSpan.FromSeconds(loadlessSeconds);

            txtTimeOutput.Text = "Time with loads:" + Environment.NewLine;
            txtTimeOutput.Text += timeWithLoads.ToString(@"hh\:mm\:ss\:fff") + Environment.NewLine;
            txtTimeOutput.Text += "Time without loads:" + Environment.NewLine;
            txtTimeOutput.Text += timeWithoutLoads.ToString(@"hh\:mm\:ss\:fff") + Environment.NewLine;

        }

        private void lbxLoads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int frame = int.Parse(lbxLoads.SelectedItem.ToString().Split("\t")[0]);
            sliderTimeline.Value = frame;
            SetVideoFrame(frame);
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

        private void txtFrame_TextChanged(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Figure out later
            if (totalVideoFrames > 0)
            {
                sliderTimeline.Value = int.Parse(txtFrame.Text);
                SetVideoFrame((int)sliderTimeline.Value);
            }
        }

        private void btnSetStart_Click(object sender, RoutedEventArgs e)
        {
            txtStartFrame.Text = sliderTimeline.Value.ToString();
        }

        private void btnSetEnd_Click(object sender, RoutedEventArgs e)
        {
            txtEndFrame.Text = sliderTimeline.Value.ToString();
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

        private void UpdateCropPreview()
        {
            Bitmap image = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            Bitmap croppedImage = ImageProcessor.CropImage(image, CropSlidersToRectange());
            imageLoadFrame.Source = ImageProcessor.BitmapToBitmapImage(croppedImage);
        }

        private Rectangle CropSlidersToRectange()
        {
            Rectangle cropPercentage = new Rectangle();

            cropPercentage.X = (int)Math.Round(sliderCropX.Value);
            cropPercentage.Y = (int)Math.Round(sliderCropY.Value);
            cropPercentage.Width = (int)Math.Round(sliderCropWidth.Value);
            cropPercentage.Height = (int)Math.Round(sliderCropHeight.Value);

            return cropPercentage;
        }
    }
}