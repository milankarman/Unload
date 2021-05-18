using System;
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

        public static void SetFFMpegPath()
        {
            FFmpeg.SetExecutablesPath(FFMPEG_PATH);
        }

        public static async Task<IConversionResult> ConvertToImageSequence(string inputPath, string outputPath, CancellationTokenSource cts, Action<double> onProgress)
        {
            Func<string, string> outputFileNameBuilder = _ => {
                return Path.Join(outputPath, "%d.jpg");
            };

            IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath).ConfigureAwait(false);
            IVideoStream videoStream = info.VideoStreams.First()?.SetSize(640, 360);

            IConversion conversion = FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .ExtractEveryNthFrame(1, outputFileNameBuilder);

            conversion.OnProgress += (sender, args) =>
            {
                double percent = Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds * 100, 2);
                onProgress(percent);
            };

            return await conversion.Start(cts.Token);
        }
    }
}
