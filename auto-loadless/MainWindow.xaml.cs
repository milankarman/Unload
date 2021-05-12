using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        BitmapImage currentFrame = null;
        CroppedBitmap currentCroppedFrame = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLoadVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            
            if (dialog.ShowDialog() == true)
            {
                ConvertToImageSequence(dialog.FileName);
            }
        }

        private void ConvertToImageSequence(string inputPath)
        {

            string ffmpegPath = Path.Join("C:", "Program Files", "ffmpeg", "bin", "ffmpeg.exe");
            MessageBox.Show(File.Exists(inputPath).ToString());

            Process process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.StartInfo.FileName = ffmpegPath;
            process.StartInfo.Arguments = $"-i {inputPath} -r 1/1 C:/TestOutput/%0d.bmp";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();
        }

        private void CompareImage()
        {
            OpenFileDialog img2Dialog = new OpenFileDialog();

            if (img2Dialog.ShowDialog() == true)
            {
                Bitmap image1 = BitmapFromSource(currentCroppedFrame.Source);
                Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());

                Uri img2Path = new Uri(img2Dialog.FileName);

                BitmapImage img2 = new BitmapImage(img2Path);

                int width = (int)Math.Round(sldWidth.Value / 100 * currentFrame.Width);
                int height = (int)Math.Round(sldHeight.Value / 100 * currentFrame.Height);
                
                int x = (int)Math.Round(sldX.Value / 100 * currentFrame.Width);
                int y = (int)Math.Round(sldY.Value / 100 * currentFrame.Height);
                
                x = Math.Clamp(x, 0, (int)currentFrame.Width - width);
                y = Math.Clamp(y, 0, (int)currentFrame.Height - height);

                CroppedBitmap img2Cropped = new CroppedBitmap(img2, new Int32Rect(x, y, width, height));

                // imgPreview2.Source = img2Cropped;

                Bitmap img2out = BitmapFromSource(img2Cropped);

                Digest hash2 = ImagePhash.ComputeDigest(img2out.ToLuminanceImage());

                float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

                MessageBox.Show($"Similarity: {score}");
            }
        }

        private void btnLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            Uri framePath = new Uri($"C:/TestOutput/{txtFrameNumber.Text}.png");
            currentFrame = new BitmapImage(framePath);

            imgVideo.Source = currentFrame;
        }

        private void UpdateCropPreview()
        {
            if (currentFrame == null)
            {
                return;
            }

            int width = (int)Math.Round(sldWidth.Value / 100 * currentFrame.Width);
            int height = (int)Math.Round(sldHeight.Value / 100 * currentFrame.Height);

            int x = (int)Math.Round(sldX.Value / 100 * currentFrame.Width);
            int y = (int)Math.Round(sldY.Value / 100 * currentFrame.Height);

            x = Math.Clamp(x, 0, (int)currentFrame.Width - width);
            y = Math.Clamp(y, 0, (int)currentFrame.Height - height);

            currentCroppedFrame = new CroppedBitmap(currentFrame, new Int32Rect(x, y, width, height));
            imgPickedLoad.Source = currentCroppedFrame;
        }

        private void sldX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCropPreview();
        }

        private void sldY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCropPreview();
        }

        private void sldWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCropPreview();
        }

        private void sldHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCropPreview();
        }

        private void btnCompareAgainst_Click(object sender, RoutedEventArgs e)
        {
            CompareImage();
        }

        public static Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            Bitmap bitmap;
            using (var outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }
            return bitmap;
        }
    }
}