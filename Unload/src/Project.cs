﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Shipwreck.Phash;
using Xabe.FFmpeg;

namespace unload
{
    public class Project
    {
        public readonly string? videoPath;
        public readonly string? framesDirectory;

        public readonly int totalFrames;
        public readonly double fps;

        public Dictionary<int, Digest> HashedFrames { get; private set; }
        public List<DetectedLoad> DetectedLoads { get; private set; }

        private int usedStartFrame;
        private int usedEndFrame;
        private float usedMinSimilarity;
        private int usedMinLoadFrames;


        public Project(string _videoPath, string _framesDirectory, int _totalFrames, double _fps)
        {
            videoPath = _videoPath;
            framesDirectory = _framesDirectory;
            totalFrames = _totalFrames;
            fps = _fps;

            HashedFrames = new();
            DetectedLoads = new();
        }

        // Returns the similarity of 2 frames ranging from 0 to 1
        public float GetSimilarity(int frame1Index, int frame2Index, Rectangle crop)
        {
            Bitmap frame1 = new Bitmap(Path.Join(framesDirectory, $"{frame1Index}.jpg"));
            Bitmap frame2 = new Bitmap(Path.Join(framesDirectory, $"{frame2Index}.jpg"));

            frame1 = ImageProcessor.CropImage(frame1, crop);
            frame2 = ImageProcessor.CropImage(frame2, crop);

            return ImageProcessor.CompareBitmapPhash(frame1, frame2);
        }

        // Crops and hashes every video frame in the selected range and stores it for later comparisons
        public void PrepareFrames(int startFrame, int endFrame, Rectangle crop, int concurrentTasks,
            CancellationTokenSource cts, Action<double> onProgress, Action onFinished)
        {
            int doneFrames = 0;

            HashedFrames = ImageProcessor.CropAndPhashFolder(framesDirectory, crop, startFrame,
                endFrame, concurrentTasks, cts, () =>
            {
                doneFrames += 1;
                double percentage = doneFrames / (double)endFrame * 100d;
                onProgress(percentage);
            });

            onFinished();
        }

        // Clears the prepared frames
        public void ClearFrames()
        {
            HashedFrames.Clear();
        }

        // Compares every frame hash against the picked loads hashes and stores them
        public void DetectLoadFrames(int startFrame, int endFrame, double minSimilarity, int minFrames,
            List<int> pickedLoadsIndeces, Rectangle crop, int concurrentTasks)
        {
            DetectedLoads.Clear();

            Bitmap[] loadFrames = new Bitmap[pickedLoadsIndeces.Count];

            for (int i = 0; i < pickedLoadsIndeces.Count; i++)
            {
                Bitmap loadFrame = new Bitmap(Path.Join(framesDirectory, $"{i}.jpg"));
                loadFrames[i] = ImageProcessor.CropImage(loadFrame, crop);
            }

            // Store the similarity of all frames and store this in another dictionary
            Dictionary<int, float[]> frameSimilarities = ImageProcessor.GetHashDictSimilarity(HashedFrames, loadFrames, concurrentTasks);

            int loadScreenCounter = 0;
            int loadFrameCounter = 0;

            int currentLoadStartFrame = 0;
            bool subsequentLoadFrame = false;

            // Check every frame similarities to the load images against the minimum similarity and list them as loads
            for (int i = startFrame; i < endFrame; i++)
            {
                for (int j = 0; j < frameSimilarities[i].Length; j++)
                {
                    if (frameSimilarities[i][j] > minSimilarity && i < endFrame)
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
                        if (currentLoadTotalFrames >= minFrames)
                        {
                            DetectedLoads.Add(new DetectedLoad(loadScreenCounter, currentLoadStartFrame, currentLoadEndFrame));
                        }

                        subsequentLoadFrame = false;
                        currentLoadStartFrame = 0;
                    }
                }
            }
        }

        // Adds up the loads entered in the detected loads
        public int GetDetectedLoadFrames()
        {
            int frames = 0;

            foreach (DetectedLoad load in DetectedLoads)
            {
                frames += load.EndFrame - load.StartFrame + 1;
            }

            return frames;
        }

        public void ExportCSV(string path)
        {
            // Read all load screens into to write to file
            List<string> lines = new List<string>
            {
                "Loading Screens",
                "#,First,Last,Total"
            };

            foreach (DetectedLoad load in DetectedLoads)
            {
                lines.Add($"{load.Index},{load.StartFrame},{load.EndFrame},{load.EndFrame - load.StartFrame + 1}");
            }

            int loadFrames = GetDetectedLoadFrames();

            // Add final times into list to write to file
            lines.Add("");
            lines.Add("Final Times");
            lines.Add($"Time without loads,\"{TimeCalculator.GetLoadlessTimeString(fps, totalFrames, loadFrames)}\"");
            lines.Add($"Time with loads,\"{TimeCalculator.GetTotalTimeString(fps, totalFrames)}\"");
            lines.Add($"Time spent loading,\"{TimeCalculator.GetTimeSpentLoadingString(fps, totalFrames, loadFrames)}\"");

            // Add unload settings into the list to write to the file
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
