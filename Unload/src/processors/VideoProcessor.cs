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
            // Automatically download FFmpeg, useful for development
            // Release can ship with FFmpeg executables already at the right location
            FFmpeg.SetExecutablesPath(FFMPEG_PATH);
            // Note: While, we could leave this on in Release, keeping the FFmpeg library always up to date,
            // It would incur a ~222mb download everytime and *may* accidentally include breaking changes.
#if DEBUG
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FFmpeg.ExecutablesPath);
#endif
        }

        // Outputs a video file as images of every individual frame in the specified directory
        public static async Task<IConversionResult> ConvertToImageSequence(string inputPath, string outputPath, TimeSpan startTime, TimeSpan endTime, int frameWidth, int frameHeight, double fps, CancellationTokenSource cts, Action<double> onProgress)
        {
            // Reads in the video file
            IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.First();

            // Prepares for converting every video frame to an image
            IConversion conversion = FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .AddParameter($"-ss {startTime}", ParameterPosition.PreInput)
                .AddParameter($"-to {endTime}")
                .AddParameter($"-s {frameWidth}x{frameHeight}")
                .AddParameter("-q:v 3")
                .SetFrameRate(fps)
                .SetVideoSyncMethod(VideoSyncMethod.cfr)
                .SetOutput(Path.Join(outputPath, "%d.jpg"));

            // Notifies the calling location on the progress of converting
            conversion.OnProgress += (sender, args) =>
            {
                double percent = Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds * 100, 2);
                onProgress(percent);
            };

            // Starts and awaits the conversion while giving it a cancellation token so it can be stopped at will
            return await conversion.Start(cts.Token);
        }
    }
}
