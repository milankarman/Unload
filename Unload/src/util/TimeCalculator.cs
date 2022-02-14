using System;

namespace unload
{
    public static class TimeCalculator
    {
        // Verifies user input is correct and returns the total (with loads) time formatted as a string
        public static string GetTotalTimeString(double framesPerSecond, int totalFrames)
        {
            double totalSecondsDouble = totalFrames / framesPerSecond;
            TimeSpan timeWithoutLoads = TimeSpan.FromSeconds(Math.Round(totalSecondsDouble, 3));

            return $"{timeWithoutLoads:hh\\:mm\\:ss\\.fff}";
        }

        // Verifies user input is correct and returns the loadless time formatted as a string
        public static string GetLoadlessTimeString(double framesPerSecond, int totalFrames, int loadFrames)
        {
            int loadlessFrames = totalFrames - loadFrames;
            double loadlessSecondsDouble = loadlessFrames / framesPerSecond;
            TimeSpan timeWithoutLoads = TimeSpan.FromSeconds(Math.Round(loadlessSecondsDouble, 3));

            return $"{timeWithoutLoads:hh\\:mm\\:ss\\.fff}";
        }

        // Verifies user input is correct and returns the loadless time formatted as a string
        public static string GetTimeSpentLoadingString(double framesPerSecond, int loadFrames)
        {
            double loadlessSecondsDouble = loadFrames / framesPerSecond;
            TimeSpan timeSpentLoading = TimeSpan.FromSeconds(Math.Round(loadlessSecondsDouble, 3));

            return $"{timeSpentLoading:hh\\:mm\\:ss\\.fff}";
        }
    }
}
