using System;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using Point = OpenCvSharp.Point;

namespace SmartLabel_Vision
{
    public static class TestImageGenerator
    {
        public static string GenerateSamples()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_images");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // 1. Golden Reference (PASS)
            CreateLabel(Path.Combine(folder, "sample_01_PASS.png"), skew: false, breakBarcode: false, hasExpiry: true);

            // 2. Defect: Label Skewed/Misaligned (FAIL)
            CreateLabel(Path.Combine(folder, "sample_02_SKEWED.png"), skew: true, breakBarcode: false, hasExpiry: true);

            // 3. Defect: Damaged / Unreadable Barcode (FAIL)
            CreateLabel(Path.Combine(folder, "sample_03_BAD_BARCODE.png"), skew: false, breakBarcode: true, hasExpiry: true);

            // 4. Defect: Missing Expiry Date OCR (FAIL)
            CreateLabel(Path.Combine(folder, "sample_04_MISSING_DATE.png"), skew: false, breakBarcode: false, hasExpiry: false);

            return folder;
        }

        private static void CreateLabel(string outputPath, bool skew, bool breakBarcode, bool hasExpiry)
        {
            using var canvas = new Mat(450, 600, MatType.CV_8UC3, new Scalar(230, 230, 230)); // Conveyor background

            // Label Rect
            var labelRect = new Rect(80, 50, 440, 350);
            Cv2.Rectangle(canvas, labelRect, new Scalar(255, 255, 255), -1);
            Cv2.Rectangle(canvas, labelRect, new Scalar(40, 40, 40), 2);

            // Product Header
            Cv2.PutText(canvas, "PHARMA SMART PACK - 500mg", new Point(100, 100),
                        HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 0), 2);

            // Generate a real, syntactically valid Code-128 Barcode via ZXing
            var barcodeWriter = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 70,
                    Width = 260,
                    Margin = 1,
                    PureBarcode = false // includes human-readable numbers under bars
                }
            };

            using (var barcodeBmp = barcodeWriter.Write("MED-90214-X"))
            using (var barcodeMat = BitmapConverter.ToMat(barcodeBmp))
            {
                // Convert to 3 channels if needed
                using var barcode3C = new Mat();
                if (barcodeMat.Channels() == 4)
                    Cv2.CvtColor(barcodeMat, barcode3C, ColorConversionCodes.BGRA2BGR);
                else
                    barcodeMat.CopyTo(barcode3C);

                // Overlay barcode onto label canvas
                var roi = new Rect(110, 130, barcode3C.Width, barcode3C.Height);
                barcode3C.CopyTo(new Mat(canvas, roi));
            }

            if (breakBarcode)
            {
                // Simulate a torn or heavily smudged vertical defect across the bars
                Cv2.Rectangle(canvas, new Rect(190, 125, 45, 80), new Scalar(255, 255, 255), -1);
            }

            // Expiry Date Region
            if (hasExpiry)
            {
                Cv2.PutText(canvas, "EXP: 2026/12", new Point(110, 260),
                            HersheyFonts.HersheySimplex, 0.9, new Scalar(0, 0, 0), 2);
                Cv2.PutText(canvas, "LOT: B7094-V", new Point(110, 300),
                            HersheyFonts.HersheySimplex, 0.7, new Scalar(80, 80, 80), 2);
            }

            // Skew defect
            if (skew)
            {
                var center = new Point2f(canvas.Cols / 2f, canvas.Rows / 2f);
                var rotMat = Cv2.GetRotationMatrix2D(center, 12.0, 1.0);
                using var rotated = new Mat();
                Cv2.WarpAffine(canvas, rotated, rotMat, canvas.Size(), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(230, 230, 230));
                rotated.SaveImage(outputPath);
                return;
            }

            canvas.SaveImage(outputPath);
        }
    }
}