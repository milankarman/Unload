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
        private readonly Project project;

        public LoadCheckWindow()
        {
            string videoPath = "C:\\Users\\Milan\\Downloads\\[WR] The Hobbit(PC) World Record in 11_48 _ Any% NMG[ikrXK1HM4u0].webm";
            string framesDirectory = "C:\\Users\\Milan\\Downloads\\WRTheHobbitPCWorldRecordin1148AnyNMGikrXK1HM4u0webm_frames";

            project = new Project(videoPath, framesDirectory, 44505, 60);

            List<DetectedLoad> loads = new()
            {
                new DetectedLoad(1, 176, 191),
                new DetectedLoad(2, 1289, 1310),
                new DetectedLoad(3, 4005, 4024),
                new DetectedLoad(4, 5059, 5081),
                new DetectedLoad(5, 5848, 5876),
                new DetectedLoad(6, 15093, 15116),
                new DetectedLoad(7, 20918, 20937),
                new DetectedLoad(8, 25887, 25921),
                new DetectedLoad(9, 26696, 26725),
                new DetectedLoad(10, 31314, 31331),
                new DetectedLoad(11, 33856, 33882),
                new DetectedLoad(12, 35743, 35775),
            };

            project.TEST_SetDetectedLoads(loads);

            InitializeComponent();

            UpdateDetectedLoads();
        }

        private void UpdateDetectedLoads()
        {
            lbxDetectedLoads.Items.Clear();

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
            txtStartFrame.Text = load.StartFrame.ToString();
            txtEndFrame.Text = load.EndFrame.ToString();
        }

        private void lbxDetectedLoads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbxDetectedLoads.SelectedIndex >= 0)
                SetLoadScreen(project.DetectedLoads[lbxDetectedLoads.SelectedIndex]);
        }

        private void sliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            SetLoadScreen(project.DetectedLoads[(int)sliderTimeline.Value - 1]);

        private void btnStartFrameBackFar_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.StartFrame = Math.Clamp(load.StartFrame - 5, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnStartFrameBack_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.StartFrame = Math.Clamp(load.StartFrame - 1, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnStartFrameForward_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.StartFrame = Math.Clamp(load.StartFrame + 1, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnStartFrameForwardFar_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.StartFrame = Math.Clamp(load.StartFrame + 5, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnEndFrameBackFar_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.EndFrame = Math.Clamp(load.EndFrame - 5, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnEndFrameBack_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.EndFrame = Math.Clamp(load.EndFrame - 1, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnEndFrameForward_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.EndFrame = Math.Clamp(load.EndFrame + 1, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }

        private void btnEndFrameForwardFar_Click(object sender, RoutedEventArgs e)
        {
            int loadIndex = int.Parse(txtLoadNumber.Text) - 1;
            DetectedLoad load = project.DetectedLoads[loadIndex];

            load.EndFrame = Math.Clamp(load.EndFrame + 5, 0, project.totalFrames);

            UpdateDetectedLoads();
            SetLoadScreen(load);
        }
    }
}
