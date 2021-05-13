using System.IO;
using System.Diagnostics;
using System.Windows;

namespace auto_loadless
{
    public static class FFMPEG
    {
        public static void ConvertToImageSequence(string inputPath, string outputPath)
        {
            string ffmpegPath = Path.Join("C:", "Program Files", "ffmpeg", "bin", "ffmpeg.exe");

            Process process = new Process();

            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;

            process.StartInfo.FileName = ffmpegPath;

            string args = $"-i {inputPath} -s 640x360 -q:v 4 {outputPath}/%d.jpg";
            process.StartInfo.Arguments = args;

            process.Start();
        }
    }
}
