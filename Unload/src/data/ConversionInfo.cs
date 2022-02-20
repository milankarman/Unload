namespace unload
{
    // Class holding info about a converted image sequence
    public class ConversionInfo
    {
        public string FileName { get; set; }
        public double FPS { get; set; }
        public int ExpectedFrames { get; set; }

        public ConversionInfo(string fileName, double fps, int expectedFrames)
        {
            FileName = fileName;
            FPS = fps;
            ExpectedFrames = expectedFrames;
        }
    }
}
