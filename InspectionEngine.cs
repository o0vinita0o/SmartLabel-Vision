using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Tesseract;
using ZXing;
using ZXing.Windows.Compatibility;

namespace SmartLabel_Vision
{
    public class InspectionResult
    {
        public bool IsPass { get; set; }
        public string Verdict => IsPass ? "PASS" : "FAIL";
        public string FailureReason { get; set; } = string.Empty;
        public string BarcodeData { get; set; } = "N/A";
        public string ExpiryText { get; set; } = "N/A";
        public double SkewAngle { get; set; }
        public double AreaRatio { get; set; }
        public double CycleTimeMs { get; set; }
        public Mat AnnotatedImage { get; set; } = new Mat();
    }

    public class InspectionEngine : IDisposable
    {
        private readonly TesseractEngine _ocrEngine;
        private readonly BarcodeReader _barcodeReader;

        public InspectionEngine()
        {
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            _barcodeReader = new BarcodeReader
            {
                AutoRotate = false,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new[] { BarcodeFormat.CODE_128, BarcodeFormat.QR_CODE, BarcodeFormat.CODE_39 }
                }
            };
        }

        public InspectionResult Inspect(string imagePath)
        {
            var sw = Stopwatch.StartNew();
            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            var result = new InspectionResult { AnnotatedImage = src.Clone() };

            // 1. Label Contour & Skew Detection
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 50, 150);

            Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            OpenCvSharp.Point[]? maxContour = null;
            double maxArea = 0;

            foreach (var c in contours)
            {
                double a = Cv2.ContourArea(c);
                if (a > maxArea)
                {
                    maxArea = a;
                    maxContour = c;
                }
            }

            if (maxContour != null && maxArea > 20000)
            {
                var minRect = Cv2.MinAreaRect(maxContour);
                double angle = minRect.Angle;
                if (angle < -45) angle += 90;
                result.SkewAngle = Math.Round(angle, 2);
                result.AreaRatio = Math.Round(maxArea / (src.Width * src.Height), 3);

                // Draw bounding contour on annotated preview
                var boxPoints = Cv2.BoxPoints(minRect);
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(result.AnnotatedImage,
                             (OpenCvSharp.Point)boxPoints[i],
                             (OpenCvSharp.Point)boxPoints[(i + 1) % 4],
                             new Scalar(255, 0, 0), 2);
                }

                if (Math.Abs(result.SkewAngle) > 5.0)
                {
                    result.IsPass = false;
                    result.FailureReason = $"LABEL_SKEWED ({result.SkewAngle}°)";
                }
            }
            else
            {
                result.IsPass = false;
                result.FailureReason = "LABEL_BOUNDARY_NOT_FOUND";
            }

            // 2. Barcode Reading
            using (var bmp = BitmapConverter.ToBitmap(src))
            {
                var decodeResult = _barcodeReader.Decode(bmp);
                if (decodeResult != null)
                {
                    result.BarcodeData = decodeResult.Text;
                }
                else if (string.IsNullOrEmpty(result.FailureReason))
                {
                    result.IsPass = false;
                    result.FailureReason = "BARCODE_UNREADABLE";
                }
            }

            // 3. OCR Text Verification (Expiry Date)
            byte[] imageBytes = gray.ToBytes(".png");
            using (var pix = Pix.LoadFromMemory(imageBytes))
            using (var page = _ocrEngine.Process(pix))
            {
                string fullText = page.GetText();
                var match = Regex.Match(fullText, @"EXP:\s*(\d{4}/\d{2})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    result.ExpiryText = match.Groups[1].Value;
                }
                else if (string.IsNullOrEmpty(result.FailureReason))
                {
                    result.IsPass = false;
                    result.FailureReason = "EXPIRY_DATE_MISSING_OR_ILLEGIBLE";
                }
            }

            // If no failure was triggered across all stages: PASS
            if (string.IsNullOrEmpty(result.FailureReason))
            {
                result.IsPass = true;
            }

            sw.Stop();
            result.CycleTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);

            // Overlay Verdict on Annotated Image
            Scalar badgeColor = result.IsPass ? new Scalar(0, 180, 0) : new Scalar(0, 0, 220);
            Cv2.PutText(result.AnnotatedImage, $"[{result.Verdict}] {result.FailureReason}",
                        new OpenCvSharp.Point(20, 40), HersheyFonts.HersheySimplex, 0.9, badgeColor, 2);
            Cv2.PutText(result.AnnotatedImage, $"Cycle: {result.CycleTimeMs} ms | Expiry: {result.ExpiryText}",
                        new OpenCvSharp.Point(20, src.Height - 20), HersheyFonts.HersheySimplex, 0.6, new Scalar(50, 50, 50), 2);

            return result;
        }

        public void Dispose()
        {
            _ocrEngine.Dispose();
        }
    }
}