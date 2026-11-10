using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace unload
{
    public static class VideoProcessor
    {
        private const string FFMPEG_PATH = "./ffmpeg";

        // Points the FFmpeg library to the right executables
        public static void SetFFMpegPath()
        {
            FFmpeg.SetExecutablesPath(FFMPEG_PATH);
#if DEBUG
            // Automatically downloads FFmpeg useful for development
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FFmpeg.ExecutablesPath);
#endif
        }

        // Outputs a video file as images of every individual frame in the specified directory
        public static async Task<IConversionResult> ConvertToImageSequence(string inputPath, string outputPath, TimeSpan startTime,
            TimeSpan endTime, int frameWidth, int frameHeight, double fps, CancellationTokenSource cts, Action<double> onProgress)
        {
            // Reads in the video file
            IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.First();

            // Input seeking resets timestamps to 0, so an output -to would act as a duration
            TimeSpan duration = endTime - startTime;

            // Prepares for converting every video frame to an image.
            // fps runs before scale so frames that get dropped are never scaled.
            IConversion conversion = FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .AddParameter($"-ss {startTime}", ParameterPosition.PreInput)
                .AddParameter($"-t {duration}")
                .AddParameter($"-vf fps={fps},scale={frameWidth}:{frameHeight}")
                .AddParameter("-q:v 3")
                .SetOutput(Path.Join(outputPath, "%d.jpg"));

            // Notifies the calling location on the progress of converting
            conversion.OnProgress += (sender, args) =>
            {
                double percent = Math.Round(args.Duration.TotalSeconds / duration.TotalSeconds * 100, 2);
                onProgress(percent);
            };

            // Starts and awaits the conversion while giving it a cancellation token so it can be stopped at will
            return await conversion.Start(cts.Token);
        }
    }
}
