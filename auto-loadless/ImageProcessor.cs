using System.Windows.Media.Imaging;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace auto_loadless
{
    public static class ImageProcessor
    {
        public static float ComparePhash(Bitmap image1, Bitmap image2)
        {
            Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());
            Digest hash2 = ImagePhash.ComputeDigest(image2.ToLuminanceImage());

            float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

            return score;
        }

        public static Bitmap CropImage(Image source, int xPercentage, int yPercentage, int widthPercentage, int heightPercentage)
        {
            double x = xPercentage / 100d * source.Width;
            double y = yPercentage / 100d * source.Height;
            double width = widthPercentage / 100d * source.Width;
            double height = heightPercentage / 100d * source.Height;

            Rectangle crop = new Rectangle((int)x, (int)y, (int)width, (int)height);

            Bitmap bmp = new Bitmap(crop.Width, crop.Height);

            using (Graphics gr = Graphics.FromImage(bmp))
            {
                gr.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
            }

            return bmp;
        }

        public static BitmapImage BitmapToBitmapImage(this Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
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
