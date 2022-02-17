using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace unload
{
    public partial class LoadCheckWindow : Window
    {
        private Project project;

        public LoadCheckWindow()
        {
            string videoPath = "C:\\Users\\milan\\Downloads\\[WR] The Hobbit (PC) World Record in 11_48 _ Any% NMG [ikrXK1HM4u0].mp4";
            string framesDirectory = "C:\\Users\\milan\\Downloads\\WRTheHobbitPCWorldRecordin1148AnyNMGikrXK1HM4u0mp4_frames";

            project = new Project(videoPath, framesDirectory, 22252, 30);

            List<DetectedLoad> loads = new()
            {
                new DetectedLoad(1, 89, 96),
                new DetectedLoad(2, 645, 655),
                new DetectedLoad(3, 2003, 2012),
                new DetectedLoad(4, 2530, 2541),
                new DetectedLoad(5, 2925, 2938),
                new DetectedLoad(6, 7547, 7558),
                new DetectedLoad(7, 10460, 10469),
                new DetectedLoad(8, 12944, 12961),
                new DetectedLoad(9, 13349, 13363),
                new DetectedLoad(10, 15658, 15666),
                new DetectedLoad(11, 16929, 16941),
                new DetectedLoad(12, 17872, 17888),
            };

            project.TEST_SetDetectedLoads(loads);

            InitializeComponent();

            foreach (DetectedLoad load in project.DetectedLoads)
            {
                lbxDetectedLoads.Items.Add($"{load.Index}\t{load.StartFrame}\t{load.EndFrame}");
            }

            sliderTimeline.Maximum = project.DetectedLoads.Count;
        }

        private void SetStartFrame(int frameIndex)
        {
            frameIndex = Math.Clamp(frameIndex, 1, project.totalFrames);

            Uri startFrame = new(Path.Join(project.framesDirectory, $"{frameIndex}.jpg"));
            imgStartFrame.Source = new BitmapImage(startFrame);

            Uri startFrameBefore = new(Path.Join(project.framesDirectory, $"{frameIndex - 1}.jpg"));
            imgStartFrameBefore.Source = new BitmapImage(startFrameBefore);
        }

        private void SetEndFrame(int frameIndex)
        {
            frameIndex = Math.Clamp(frameIndex, 1, project.totalFrames);

            Uri endFrame = new(Path.Join(project.framesDirectory, $"{frameIndex}.jpg"));
            imgEndFrame.Source = new BitmapImage(endFrame);

            Uri endFrameAfter = new(Path.Join(project.framesDirectory, $"{frameIndex + 1}.jpg"));
            imgEndFrameAfter.Source = new BitmapImage(endFrameAfter);
        }

        private void SetLoadScreen(DetectedLoad load)
        {
            SetStartFrame(load.StartFrame);
            SetEndFrame(load.EndFrame);
            txtLoadNumber.Text = load.Index.ToString();
        }

        private void lbxDetectedLoads_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            SetLoadScreen(project.DetectedLoads[lbxDetectedLoads.SelectedIndex]);

        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            SetLoadScreen(project.DetectedLoads[(int)sliderTimeline.Value - 1]);
    }
}
