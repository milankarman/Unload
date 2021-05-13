using System;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using Microsoft.Win32;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        // BitmapImage pickedLoadFrame = null;
        // CroppedBitmap pickedLoadFrameCropped = null;

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
    }
}