﻿namespace unload
{
    // Class holding data of a single detected loading screen
    public class DetectedLoad
    {
        public int Index { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }

        public DetectedLoad(int _index, int _startFrame, int _endFrame)
        {
            Index = _index;
            StartFrame = _startFrame;
            EndFrame = _endFrame;
        }
    }
}