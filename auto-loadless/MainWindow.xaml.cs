using System;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Globalization;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;
using Microsoft.Win32;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        int loadedFrames = 0;
        int pickedLoadingFrame = 0;

        string workingDirectory = String.Empty;

        public MainWindow()
        {
            InitializeComponent();
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

                loadedFrames = Directory.GetFiles(targetDirectory, "*.jpg").Length;
                workingDirectory = targetDirectory;

                sliderTimeline.Minimum = 1;
                sliderTimeline.Maximum = loadedFrames;
                sliderTimeline.Value = 1;

                SetVideoFrame(1);
            }
        }

        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetVideoFrame((int)Math.Round(sliderTimeline.Value));
        }

        private void SetVideoFrame(int frame)
        {
            Uri image = new Uri(Path.Join(workingDirectory, $"{frame}.jpg"));
            txtFrame.Text = frame.ToString(); ;
            imageVideo.Source = new BitmapImage(image);
        }

        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pickedLoadingFrame != 0)
            {
                UpdateCropPreview();
            }
        }

        private void UpdateCropPreview()
        {
            Bitmap image = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            Bitmap croppedImage = CropImageWithSliders(image);
            imageLoadFrame.Source = ImageProcessor.BitmapToBitmapImage(croppedImage);
        }

        private void btnPickLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadingFrame = (int)sliderTimeline.Value;
            UpdateCropPreview();
        }

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            Bitmap image1 = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            image1 = CropImageWithSliders(image1);

            Bitmap image2 = new Bitmap(Path.Join(workingDirectory, $"{(int)Math.Round(sliderTimeline.Value)}.jpg"));
            image2 = CropImageWithSliders(image2);

            MessageBox.Show(ImageProcessor.ComparePhash(image1, image2).ToString());
        }

        private Bitmap CropImageWithSliders(Bitmap bitmap)
        {
            int x = (int)Math.Round(sliderCropX.Value);
            int y = (int)Math.Round(sliderCropY.Value);
            int width = (int)Math.Round(sliderCropWidth.Value);
            int height = (int)Math.Round(sliderCropHeight.Value);

            return ImageProcessor.CropImage(bitmap, x, y, width, height);
        }

        private void btnCountLoads_Click(object sender, RoutedEventArgs e)
        {
            int startFrame = int.Parse(txtStartFrame.Text);
            int endFrame = int.Parse(txtEndFrame.Text);

            int cropX = (int)Math.Round(sliderCropX.Value);
            int cropY = (int)Math.Round(sliderCropY.Value);
            int cropWidth = (int)Math.Round(sliderCropWidth.Value);
            int cropHeight = (int)Math.Round(sliderCropHeight.Value);

            Bitmap loadFrame = new Bitmap(Path.Join(workingDirectory, $"{pickedLoadingFrame}.jpg"));
            loadFrame = ImageProcessor.CropImage(loadFrame, cropX, cropY, cropWidth, cropHeight);
            Digest loadFrameHash = ImagePhash.ComputeDigest(loadFrame.ToLuminanceImage());

            ConcurrentDictionary<int, float> frameSimilarity = new ConcurrentDictionary<int, float>();

            Parallel.For(startFrame, endFrame, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                Bitmap currentFrame = new Bitmap(Path.Join(workingDirectory, $"{i}.jpg"));
                currentFrame = ImageProcessor.CropImage(currentFrame, cropX, cropY, cropWidth, cropHeight);
                Digest currentFrameHash = ImagePhash.ComputeDigest(currentFrame.ToLuminanceImage());

                float similarity = ImagePhash.GetCrossCorrelation(loadFrameHash, currentFrameHash);
                frameSimilarity[i] = similarity;

                currentFrame.Dispose();
            });

            int loadCounter = 0;
            double minSimilarity = double.Parse(txtSimilarity.Text, CultureInfo.InvariantCulture);

            for (int i = startFrame; i < endFrame; i++)
            {
                if (frameSimilarity[i] > minSimilarity)
                {
                    lbxLoads.Items.Add(i + "\t" + frameSimilarity[i]);
                    loadCounter += 1;
                }
            }

            txtLoadFrames.Text = loadCounter.ToString();
            MessageBox.Show($"Done. {loadCounter} frames of loading found.");
        }

        private void lbxLoads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int frame = Int32.Parse(lbxLoads.SelectedItem.ToString().Split("\t")[0]);
            sliderTimeline.Value = frame;
            SetVideoFrame(frame);
        }

        private void buttonBackFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= 60;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void buttonBack_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value -= 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void buttonForward_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += 1;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void buttonForwardFar_Click(object sender, RoutedEventArgs e)
        {
            sliderTimeline.Value += 60;

            SetVideoFrame((int)sliderTimeline.Value);
        }

        private void txtFrame_TextChanged(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Figure out later
            if (loadedFrames > 0)
            {
                sliderTimeline.Value = Int32.Parse(txtFrame.Text);
                SetVideoFrame((int)sliderTimeline.Value);
            }
        }

        private void buttonRetime_Click(object sender, RoutedEventArgs e)
        {
            double framesPerSecond = double.Parse(txtFPS.Text);

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
    }
}