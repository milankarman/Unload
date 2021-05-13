using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Shipwreck.Phash;
using Shipwreck.Phash.Bitmaps;

namespace auto_loadless
{
    public static class ImageProcessor
    {
        public static float ComparePhash(Bitmap image1, Bitmap image2)
        {
            Digest hash1 = ImagePhash.ComputeDigest(image1.ToLuminanceImage());
            Digest hash2 = ImagePhash.ComputeDigest(image2.ToLuminanceImage());

            float score = ImagePhash.GetCrossCorrelation(hash1, hash2);

            return score;
        }
    }
}
