using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;

namespace auto_loadless
{
    public static class ImageProcessor
    {
        public static float CompareBitmapPhash(Bitmap image1, Bitmap image2)
        {
            Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());
            Digest hash2 = ImagePhash.ComputeDigest(image2.ToLuminanceImage());

            float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

            return score;
        }

        public static Dictionary<int, Digest> CropAndPhashFolder(string path, Rectangle cropPercentage, int startFrame, int endFrame)
        {
            ConcurrentDictionary<int, Digest> frameHashes = new ConcurrentDictionary<int, Digest>();

            Parallel.For(startFrame, endFrame, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                Bitmap currentFrame = new Bitmap(Path.Join(path, $"{i}.jpg"));
                currentFrame = CropImage(currentFrame, cropPercentage);
                Digest currentFrameHash = ImagePhash.ComputeDigest(currentFrame.ToLuminanceImage());

                frameHashes[i] = currentFrameHash;

                currentFrame.Dispose();
            });

            return new Dictionary<int, Digest>(frameHashes);
        }

        public static Dictionary<int, float> GetHashDictSimilarity(Dictionary<int, Digest> hashDict, Bitmap reference)
        {
            Digest referenceHash = ImagePhash.ComputeDigest(reference.ToLuminanceImage());

            ConcurrentDictionary<int, float> frameSimilarities = new ConcurrentDictionary<int, float>();

            Parallel.ForEach(hashDict, new ParallelOptions { MaxDegreeOfParallelism = 50 }, hash =>
            {
                float score = ImagePhash.GetCrossCorrelation(hash.Value, referenceHash);
                frameSimilarities[hash.Key] = score;
            });

            return new Dictionary<int, float>(frameSimilarities);
        }

        public static Bitmap CropImage(Image source, Rectangle cropPercentages)
        {
            Rectangle crop = new Rectangle();

            crop.X = (int)Math.Round(cropPercentages.X / 100d * source.Width);
            crop.Y = (int)Math.Round(cropPercentages.Y / 100d * source.Height);
            crop.Width = (int)Math.Round(cropPercentages.Width / 100d * source.Width);
            crop.Height = (int)Math.Round(cropPercentages.Height / 100d * source.Height);

            Bitmap bitmap = new Bitmap(crop.Width, crop.Height);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height), crop, GraphicsUnit.Pixel);
            }

            return bitmap;
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
