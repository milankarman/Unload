﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace unload
{
    public static class VideoProcessor
    {
        private const string FFMPEG_PATH = "./ffmpeg";

        // Points the FFmpeg library to the right executables
        public static void SetFFMpegPath()
        {
            FFmpeg.SetExecutablesPath(FFMPEG_PATH);
        }

        // Outputs a video file as images of every individual frame in the specified directory
        public static async Task<IConversionResult> ConvertToImageSequence(string inputPath, string outputPath, CancellationTokenSource cts, Action<double> onProgress)
        {
            // Names every image file as their frame number
            string outputFileNameBuilder(string _)
            {
                return Path.Join(outputPath, "%d.jpg");
            }

            // Reads in the video file and configures to resize it
            IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.First()?.SetSize(640, 360);

            // Prepares for converting every video frame to an image
            IConversion conversion = FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .ExtractEveryNthFrame(1, outputFileNameBuilder)
                .AddParameter("-q:v 3");

            // Notifies the calling location on the progress of converting
            conversion.OnProgress += (sender, args) =>
            {
                double percent = Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds * 100, 2);
                onProgress(percent);
            };

            // Starts and awaits the convertion gives it a cancellation token so it can be stopped
            return await conversion.Start(cts.Token);
        }
    }
}
