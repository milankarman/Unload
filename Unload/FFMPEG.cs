using System;
using System.IO;
using System.Diagnostics;
using FFMpegCore;

namespace unload
{
    public static class FFMPEG
    {
        private const string FFMPEG_PATH = "./Resources/ffmpeg.exe";

        public static void InitFFMPEGCore()
        {
            if (!File.Exists(FFMPEG_PATH))
            {
                throw new Exception();
            }

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = Path.GetDirectoryName(FFMPEG_PATH),
                TemporaryFilesFolder = "/tmp"
            });
        }

        public static void ConvertToImageSequence(string inputPath, string outputPath)
        {
            Process process = new Process();

            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;

            process.StartInfo.FileName = FFMPEG_PATH;

            string args = $"-i \"{inputPath}\" -s 640x360 -q:v 2 \"{outputPath}/%d.jpg\"";
            process.StartInfo.Arguments = args;

            process.Start();
            process.WaitForExit();
        }
    }
}
