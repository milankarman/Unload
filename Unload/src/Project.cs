using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Shipwreck.Phash;

namespace unload
{
    public class Project
    {
        public readonly string framesDirectory;
        public readonly string videoName;

        public readonly int totalFrames;
        public readonly double fps;

        public Dictionary<int, Digest> HashedFrames { get; private set; }
        public List<DetectedLoad> DetectedLoads { get; private set; }

        private int usedStartFrame;
        private int usedEndFrame;
        private double usedMinSimilarity;
        private int usedMinLoadFrames;

        public Project(string _framesDirectory, string _videoName, int _totalFrames, double _fps)
        {
            framesDirectory = _framesDirectory;
            videoName = _videoName; 
            totalFrames = _totalFrames;
            fps = _fps;

            HashedFrames = new();
            DetectedLoads = new();
        }

        // Returns the similarity of 2 frames ranging from 0 to 1
        public float GetSimilarity(int frame1Index, int frame2Index, Rectangle crop)
        {
            Bitmap frame1 = new(Path.Join(framesDirectory, $"{frame1Index}.jpg"));
            Bitmap frame2 = new(Path.Join(framesDirectory, $"{frame2Index}.jpg"));

            frame1 = ImageProcessor.CropImage(frame1, crop);
            frame2 = ImageProcessor.CropImage(frame2, crop);

            return ImageProcessor.CompareBitmapPhash(frame1, frame2);
        }

        // Crops and hashes every video frame in the selected range and stores it for later comparisons
        public void PrepareFrames(int startFrame, int endFrame, Rectangle crop,
            CancellationTokenSource cts, Action<double> onProgress, Action onFinished)
        {
            HashedFrames = ImageProcessor.CropAndPhashFolder(framesDirectory, crop, startFrame,
                 endFrame, cts, onProgress, onFinished);

            usedStartFrame = startFrame;
            usedEndFrame = endFrame;
        }

        // Clears the prepared frames
        public void ClearFrames()
        {
            HashedFrames.Clear();
        }

        // Compares every frame hash against the picked loads hashes and stores them
        public void DetectLoadFrames(double minSimilarity, int minLoadFrames,
            List<int> pickedLoadsIndeces, Rectangle crop)
        {
            DetectedLoads.Clear();

            Bitmap[] loadFrames = new Bitmap[pickedLoadsIndeces.Count];

            for (int i = 0; i < pickedLoadsIndeces.Count; i++)
            {
                Bitmap loadFrame = new(Path.Join(framesDirectory, $"{pickedLoadsIndeces[i]}.jpg"));
                loadFrames[i] = ImageProcessor.CropImage(loadFrame, crop);
            }

            // Store the similarity of all frames and store this in another dictionary
            Dictionary<int, float[]> frameSimilarities = ImageProcessor.GetHashDictSimilarity(HashedFrames, loadFrames);

            int loadScreenCounter = 0;
            int loadFrameCounter = 0;

            int currentLoadStartFrame = 0;
            bool subsequentLoadFrame = false;

            // Check every frame similarities to the load images against the minimum similarity and list them as loads
            for (int i = usedStartFrame; i < usedEndFrame; i++)
            {
                for (int j = 0; j < frameSimilarities[i].Length; j++)
                {
                    if (frameSimilarities[i][j] > minSimilarity && i < usedEndFrame)
                    {
                        loadFrameCounter += 1;

                        // If the previous frame wasn't a load frame then mark this as a new loading screen
                        if (!subsequentLoadFrame)
                        {
                            loadScreenCounter += 1;
                            currentLoadStartFrame = i;
                            subsequentLoadFrame = true;
                        }

                        break;
                    }
                    else if (j >= frameSimilarities[i].Length - 1 && subsequentLoadFrame)
                    {
                        int currentLoadEndFrame = i - 1;
                        int currentLoadTotalFrames = currentLoadEndFrame - currentLoadStartFrame + 1;

                        // Check if the detected loading screen matches the minimum number of frames set by user
                        if (currentLoadTotalFrames >= minLoadFrames)
                        {
                            DetectedLoads.Add(new DetectedLoad(loadScreenCounter, currentLoadStartFrame, currentLoadEndFrame));
                        }

                        subsequentLoadFrame = false;
                        currentLoadStartFrame = 0;
                    }
                }
            }

            usedMinSimilarity = minSimilarity;
            usedMinLoadFrames = minLoadFrames;
        }

        // Orders loads and updates their load numbers
        public void OrderLoads()
        {
            DetectedLoads = DetectedLoads.OrderBy(i => i.StartFrame).ToList();

            for (int i = 0; i < DetectedLoads.Count; i++)
            {
                DetectedLoads[i].Number = i + 1;
            }
        }

        // Returns the total detected load frames
        public int GetDetectedLoadFrames()
        {
            int frames = 0;

            foreach (DetectedLoad load in DetectedLoads)
            {
                frames += load.EndFrame - load.StartFrame + 1;
            }

            return frames;
        }

        public TimeSpan GetTotalTime()
        {
            double totalSecondsDouble = totalFrames / fps;
            return TimeSpan.FromSeconds(Math.Round(totalSecondsDouble, 3));
        }

        public TimeSpan GetLoadlessTime()
        {
            int loadlessFrames = totalFrames - GetDetectedLoadFrames();
            double loadlessSecondsDouble = loadlessFrames / fps;
            return TimeSpan.FromSeconds(Math.Round(loadlessSecondsDouble, 3));
        }

        public TimeSpan GetTimeSpentLoading()
        {
            double loadlessSecondsDouble = GetDetectedLoadFrames() / fps;
            return TimeSpan.FromSeconds(Math.Round(loadlessSecondsDouble, 3));
        }

        public void ExportCSV(string path)
        {
            List<string> lines = new()
            {
                "Loading Screens",
                "#,First,Last,Total"
            };

            foreach (DetectedLoad load in DetectedLoads)
            {
                lines.Add($"{load.Number},{load.StartFrame},{load.EndFrame},{load.EndFrame - load.StartFrame + 1}");
            }

            lines.Add("");
            lines.Add("Final Times");
            lines.Add($"Time without loads,\"{GetLoadlessTime():hh\\:mm\\:ss\\.fff}\"");
            lines.Add($"Time with loads,\"{GetTotalTime():hh\\:mm\\:ss\\.fff}\"");
            lines.Add($"Time spent loading,\"{GetTimeSpentLoading():hh\\:mm\\:ss\\.fff}\"");

            lines.Add("");
            lines.Add("Unload Settings");
            lines.Add($"Minimum similarity,\"{usedMinSimilarity}\"");
            lines.Add($"Minimum frames,{usedMinLoadFrames}");
            lines.Add($"Start frame,{usedStartFrame}");
            lines.Add($"End frame,{usedEndFrame}");

            File.WriteAllLinesAsync(path, lines);
        }
    }
}
