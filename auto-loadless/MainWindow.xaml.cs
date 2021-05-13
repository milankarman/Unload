using System;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Windows;
using Microsoft.Win32;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        BitmapImage currentFrame = null;
        BitmapImage pickedLoadFrame = null;
        CroppedBitmap pickedLoadFrameCropped = null;

        int loadedFrames = 0;
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
            currentFrame = new BitmapImage(image);
            imageVideo.Source = currentFrame;
        }

        private void slidersCropping_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pickedLoadFrame == null)
            {
                return;
            }

            UpdateCropPreview();
        }

        private void UpdateCropPreview()
        {
            int width = (int)Math.Round(sliderCropWidth.Value / 100 * pickedLoadFrame.Width);
            int height = (int)Math.Round(sliderCropHeight.Value / 100 * pickedLoadFrame.Height);

            int x = (int)Math.Round(sliderCropX.Value / 100 * pickedLoadFrame.Width);
            int y = (int)Math.Round(sliderCropY.Value / 100 * pickedLoadFrame.Height);

            x = Math.Clamp(x, 0, (int)currentFrame.Width - width);
            y = Math.Clamp(y, 0, (int)currentFrame.Height - height);

            pickedLoadFrameCropped = new CroppedBitmap(pickedLoadFrame, new Int32Rect(x, y, width, height));
            imageLoadFrame.Source = pickedLoadFrameCropped;
        }

        private void btnPickLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            pickedLoadFrame = currentFrame.Clone();
            UpdateCropPreview();

            imageLoadFrame.Source = pickedLoadFrame;
        }

        private void btnCheckSimilarity_Click(object sender, RoutedEventArgs e)
        {
            Bitmap image1 = ImageProcessor.BitmapFromSource(pickedLoadFrameCropped.Source);
            Bitmap image2 = ImageProcessor.BitmapFromBitmapImage(currentFrame);

            MessageBox.Show(ImageProcessor.ComparePhash(image1, image2).ToString());
        }
    }
}