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
        Bitmap currentCrop = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLoadVideo_Click(object sender, RoutedEventArgs e)
        {
            CompareImage();

            // OpenFileDialog dialog = new OpenFileDialog();
            // 
            // if (dialog.ShowDialog() == true)
            // {
            //     ConvertToImageSequence(dialog.FileName);
            // }
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
            OpenFileDialog img1 = new OpenFileDialog();
            OpenFileDialog img2 = new OpenFileDialog();

            if (img1.ShowDialog() == true && img2.ShowDialog() == true)
            {
                Bitmap image1 = (Bitmap)Image.FromFile(img1.FileName);
                Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());
   
                Bitmap image2 = (Bitmap)Image.FromFile(img2.FileName);
                Digest hash2 = ImagePhash.ComputeDigest(image2.ToLuminanceImage());

                float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

                MessageBox.Show($"Similarity: {score}");
            }
        }

        private void btnLoadFrame_Click(object sender, RoutedEventArgs e)
        {
            currentCrop = (Bitmap)Image.FromFile($"C:/TestOutput/{txtFrameNumber.Text}.bmp");

            imgPreview.Source = ToBitmapImage(currentCrop);
        }

        private void btnUpdateCrop_Click(object sender, RoutedEventArgs e)
        {
            sldLeft.Value
        }

        public static BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}