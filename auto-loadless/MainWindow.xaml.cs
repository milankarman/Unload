using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace auto_loadless
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLoadVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == true)
            {
                ConvertToImageSequence(dialog.FileName);
            }
        }

        private void ConvertToImageSequence(string inputPath)
        {

            string ffmpegPath = Path.Join("C:", "Program Files", "ffmpeg", "bin", "ffmpeg.exe");
            MessageBox.Show(File.Exists(inputPath).ToString());

            Process process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.StartInfo.FileName = ffmpegPath;
            process.StartInfo.Arguments = $"-i {inputPath} -r 1/1 C:/TestOutput/%0d.bmp";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();
        }
    }
}