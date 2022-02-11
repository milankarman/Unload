using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace unload
{
    public static class ExportGenerator
    {
        public static void ExportAndSave(string path, double framesPerSecond, int totalFrames, int loadFrames, ObservableCollection<DetectedLoad> detectedLoads, 
            double usedMinSimilarity, int usedMinFrames, int startFrame, int endFrame)
        {
            // Read all load screens into to write to file
            List<string> lines = new List<string>
            {
                "Loading Screens",
                "#,First,Last,Total"
            };

            foreach (DetectedLoad load in detectedLoads)
            {
                lines.Add($"{load.Index},{load.StartFrame},{load.EndFrame},{load.EndFrame - load.StartFrame + 1}");
            }

            // Add final times into list to write to file
            lines.Add("");
            lines.Add("Final Times");
            lines.Add($"Time without loads,\"{TimeCalculator.GetLoadlessTimeString(framesPerSecond, totalFrames, loadFrames)}\"");
            lines.Add($"Time with loads,\"{TimeCalculator.GetTotalTimeString(framesPerSecond, totalFrames)}\"");
            lines.Add($"Time spent loading,\"{TimeCalculator.GetTimeSpentLoadingString(framesPerSecond, totalFrames, loadFrames)}\"");

            // Add unload settings into the list to write to the file
            lines.Add("");
            lines.Add("Unload Settings");
            lines.Add($"Minimum similarity,\"{usedMinSimilarity}\"");
            lines.Add($"Minimum frames,{usedMinFrames}");
            lines.Add($"Start frame,{startFrame}");
            lines.Add($"End frame,{endFrame}");

            File.WriteAllLinesAsync(path, lines);
        }
    }
}
