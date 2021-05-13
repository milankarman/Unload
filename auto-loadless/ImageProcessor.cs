using System.Windows.Media.Imaging;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;
using System.Drawing;
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

        public static Bitmap BitmapFromSource(BitmapSource source)
        {
            Bitmap bitmap = null;

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(outStream);
                bitmap = new Bitmap(outStream);
            }

            return bitmap;
        }

        public static Bitmap BitmapFromBitmapImage(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }
    }
}
