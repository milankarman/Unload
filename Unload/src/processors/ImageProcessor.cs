using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;

namespace unload
{
    public static class ImageProcessor
    {
        // Hashes bitmap images and returns their similarity using Phash
        public static float CompareBitmapPhash(Bitmap image1, Bitmap image2)
        {
            Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());
            Digest hash2 = ImagePhash.ComputeDigest(image2.ToLuminanceImage());

            float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

            return score;
        }

        // Applies cropping and hashes all frames in range in a folder and returns the hashes in a dictionary
        public static Dictionary<int, Digest> CropAndPhashFolder(string? path, Rectangle cropPercentage, int startFrame, int endFrame, int concurrentTasks, CancellationTokenSource cts, Action onProgress)
        {
            ConcurrentDictionary<int, Digest> frameHashes = new();

            // Hashes frames in parallel to the max amount of concurrent tasks the user defined. Holds a cancellation token so it can be stopped
            Parallel.For(startFrame, endFrame, new ParallelOptions { MaxDegreeOfParallelism = concurrentTasks, CancellationToken = cts.Token }, i =>
            {
                // Crops and hashes the current frame
                Bitmap currentFrame = new(Path.Join(path, $"{i}.jpg"));
                currentFrame = CropImage(currentFrame, cropPercentage);
                Digest currentFrameHash = ImagePhash.ComputeDigest(currentFrame.ToLuminanceImage());

                // Stores the hash at the frame number
                frameHashes[i] = currentFrameHash;

                // Clears the current frame from memory and notifies the caller of its progress
                currentFrame.Dispose();
                onProgress();
            });

            return new Dictionary<int, Digest>(frameHashes);
        }

        // Compares an entire dictiory against a single image and returns a dictionary of similarity
        public static Dictionary<int, float[]> GetHashDictSimilarity(Dictionary<int, Digest> hashDict, Bitmap[] references, int concurrentTasks)
        {
            Digest[] referenceHashes = new Digest[references.Length];

            // Calculated hashes of reference images
            for (int i = 0; i < references.Length; i++)
            {
                referenceHashes[i] = ImagePhash.ComputeDigest(references[i].ToLuminanceImage());
            }

            ConcurrentDictionary<int, float[]> frameSimilarities = new();

            // Gets similarity of all frames in parallel
            Parallel.ForEach(hashDict, new ParallelOptions { MaxDegreeOfParallelism = concurrentTasks }, hash =>
            {
                frameSimilarities[hash.Key] = new float[referenceHashes.Length];

                // Calculated hashes of reference images
                for (int i = 0; i < referenceHashes.Length; i++)
                {
                    float score = ImagePhash.GetCrossCorrelation(hash.Value, referenceHashes[i]);
                    frameSimilarities[hash.Key][i] = score;
                }
            });

            return new Dictionary<int, float[]>(frameSimilarities);
        }

        // Crops an image to by given percentages
        public static Bitmap CropImage(Image source, Rectangle cropPercentages)
        {
            // Converts crop values to pixel sizes in a rectangle
            Rectangle crop = new()
            {
                X = (int)Math.Round(cropPercentages.X / 100d * source.Width),
                Y = (int)Math.Round(cropPercentages.Y / 100d * source.Height),
                Width = (int)Math.Round(cropPercentages.Width / 100d * source.Width),
                Height = (int)Math.Round(cropPercentages.Height / 100d * source.Height)
            };

            // Draws old image on a new image cropped to the right size
            Bitmap bitmap = new(crop.Width, crop.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height), crop, GraphicsUnit.Pixel);
            }

            return bitmap;
        }

        // Converts a bitmap to a bitmap image that can be displayed on the interface
        public static BitmapImage BitmapToBitmapImage(this Bitmap bitmap)
        {
            using MemoryStream memory = new();

            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();

            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;

            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }
}
