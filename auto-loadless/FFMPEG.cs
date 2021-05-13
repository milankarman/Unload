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
            process.StartInfo.Arguments = $"-i {inputPath} -r 1/1 {outputPath}/%0d.bmp";
            // process.StartInfo.Arguments = $"-i {inputPath} -s 640x480 -vf fps=1 {outputPath}/%d.jpg";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();
        }
    }
}
