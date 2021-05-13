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

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                FFMPEG.ConvertToImageSequence(dialog.FileName, "C:/TestOuput");
                MessageBox.Show("Done!");
            }
        }
        private void sliderCropX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void UpdateCropPreview(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentFrame == null)
            {
                return;
            }

            int width = (int)Math.Round(sliderCropWidth.Value / 100 * currentFrame.Width);
            int height = (int)Math.Round(sliderCropHeight.Value / 100 * currentFrame.Height);

            int x = (int)Math.Round(sliderCropX.Value / 100 * currentFrame.Width);
            int y = (int)Math.Round(sliderCropY.Value / 100 * currentFrame.Height);

            x = Math.Clamp(x, 0, (int)currentFrame.Width - width);
            y = Math.Clamp(y, 0, (int)currentFrame.Height - height);

            currentCroppedFrame = new CroppedBitmap(currentFrame, new Int32Rect(x, y, width, height));
            imageLoadFrame.Source = currentCroppedFrame;
        }
    }
}