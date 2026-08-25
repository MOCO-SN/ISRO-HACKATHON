using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ISRO_HACKATHON.Models;
using ISRO_HACKATHON.Services;

namespace ISRO_HACKATHON
{
    public partial class Form1 : Form
    {
        private string selectedFilePath = string.Empty;

        private Mat? rawImageMat;
        private Mat? processedImageMat;

        private PythonService? pythonService;

        private List<DetectionResult> lastDetections = new();

        private bool _processing;
        private bool _suppressRefresh;

        // Number of hottest regions to display.
        // No guna2NumericUpDown1 is required.
        private const int MaxHeatAreas = 5;

        public Form1()
        {
            InitializeComponent();

            string projectRoot = FindProjectRoot();

            string pythonExe = Path.Combine(
                projectRoot,
                "Python",
                ".venv",
                "Scripts",
                "python.exe");

            string detector = Path.Combine(
                projectRoot,
                "Python",
                "detector.py");

            pythonService = new PythonService(
                pythonExe,
                detector);
        }

        // ============================================================
        // PROJECT ROOT
        // ============================================================

        private static string FindProjectRoot()
        {
            string directory = AppContext.BaseDirectory;

            for (int i = 0; i < 10; i++)
            {
                if (string.IsNullOrEmpty(directory))
                    break;

                string pythonFolder =
                    Path.Combine(directory, "Python");

                if (Directory.Exists(pythonFolder))
                    return directory;

                DirectoryInfo? parent =
                    Directory.GetParent(directory);

                if (parent == null)
                    break;

                directory = parent.FullName;
            }

            return Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    ".."));
        }

        // ============================================================
        // FORM LOAD
        // ============================================================

        private void Form1_Load(object sender, EventArgs e)
        {
            ConfigureControls();
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            ConfigureControls();
        }

        private void ConfigureControls()
        {
            guna2PictureBox1.SizeMode =
                PictureBoxSizeMode.Zoom;

            guna2PictureBox2.SizeMode =
                PictureBoxSizeMode.Zoom;

            guna2PictureBox3.SizeMode =
                PictureBoxSizeMode.Zoom;

            _suppressRefresh = true;

            try
            {
                guna2TrackBar1.Value = 100;
                guna2TrackBar2.Value = 100;
                guna2TrackBar3.Value = 100;
                guna2TrackBar4.Value = 100;

                // 255 = full opacity.
                guna2TrackBar5.Value = 255;

                guna2TrackBar6.Value = 100;

                // Overlay opacity.
                guna2TrackBar7.Value = 50;

                guna2HtmlLabel32.Text =
                    $"{guna2TrackBar7.Value}%";

                guna2HtmlLabel23.Text =
                    $"{guna2TrackBar6.Value}%";

                guna2ComboBox1.Items.Clear();

                guna2ComboBox1.Items.AddRange(
                    new object[]
                    {
                        "Select Model...",
                        "V1",
                        "V2"
                    });

                guna2ComboBox1.SelectedIndex = 0;

                guna2ToggleSwitch1.Checked = false;
                guna2ToggleSwitch2.Checked = false;
                guna2ToggleSwitch3.Checked = false;
                guna2ToggleSwitch4.Checked = false;
                guna2ToggleSwitch5.Checked = false;

                guna2ProgressBar1.Value = 0;
                guna2ProgressBar2.Value = 0;

                guna2HtmlLabel33.Text = "Time: --";
                guna2HtmlLabel34.Text = "Objects: --";
                guna2HtmlLabel35.Text = "Conf: --";
                guna2HtmlLabel36.Text = "Resolution: --";
                guna2HtmlLabel37.Text = "Ready.";
            }
            finally
            {
                _suppressRefresh = false;
            }
        }

        // ============================================================
        // IMAGE OPEN
        // ============================================================

        private void guna2PictureBox3_Click(
            object sender,
            EventArgs e)
        {
            using OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title = "Select a satellite image",

                    Filter =
                        "Image Files|" +
                        "*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp",

                    Multiselect = false
                };

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            try
            {
                selectedFilePath =
                    dialog.FileName;

                DisposeMat(ref rawImageMat);
                DisposeMat(ref processedImageMat);

                lastDetections.Clear();

                rawImageMat =
                    Cv2.ImRead(
                        selectedFilePath,
                        ImreadModes.AnyColor);

                if (rawImageMat.Empty())
                {
                    throw new InvalidOperationException(
                        "OpenCV could not read the selected image.");
                }

                using Bitmap bitmap =
                    new Bitmap(selectedFilePath);

                ReplacePictureBoxImage(
                    guna2PictureBox3,
                    new Bitmap(bitmap));

                int cols = rawImageMat.Cols;
                int rows = rawImageMat.Rows;

                guna2HtmlLabel36.Text =
                    $"{cols}x{rows}";

                guna2ProgressBar1.Value = 100;

                guna2HtmlLabel37.Text =
                    "Raw image loaded successfully.";

                ApplyZoomAndDisplay();

                RefreshPipeline();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load image.\r\n\r\n" +
                    ex.Message,
                    "Image Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // MAIN PROCESS
        // ============================================================

        private async void guna2GradientButton1_Click(
            object sender,
            EventArgs e)
        {
            await ProcessCurrentImageAsync(true);
        }

        private async Task ProcessCurrentImageAsync(
            bool runDetector)
        {
            if (_processing ||
                _suppressRefresh)
            {
                return;
            }

            if (rawImageMat == null ||
                rawImageMat.Empty())
            {
                MessageBox.Show(
                    "Please load an image first.",
                    "No Image",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            _processing = true;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            try
            {
                guna2HtmlLabel37.Text =
                    runDetector
                        ? "Processing image + AI detection..."
                        : "Updating image...";

                // ----------------------------------------------------
                // SETTINGS
                // ----------------------------------------------------

                var settings =
                    new Dictionary<string, object>
                    {
                        ["nuc"] =
                            guna2ToggleSwitch4.Checked,

                        ["fusion"] =
                            guna2ToggleSwitch5.Checked,

                        ["false_color"] =
                            false,

                        ["rgb_adjust"] =
                            new[]
                            {
                                (double)guna2TrackBar2.Value / 100.0,
                                (double)guna2TrackBar3.Value / 100.0,
                                (double)guna2TrackBar4.Value / 100.0
                            }
                    };

                // ----------------------------------------------------
                // IMAGE ENHANCEMENT
                // ----------------------------------------------------

                Mat processed =
                    EnhanceIrImage(
                        rawImageMat,
                        settings);

                // ----------------------------------------------------
                // SEGMENTATION
                // ----------------------------------------------------

                if (guna2ToggleSwitch1.Checked)
                {
                    Mat segmented =
                        ApplySegmentation(processed);

                    processed.Dispose();

                    processed = segmented;
                }

                // ----------------------------------------------------
                // HEAT DETECTION
                // ----------------------------------------------------

                List<DetectionResult> heatDetections =
                    DetectMaximumHeatAreas(
                        processed,
                        MaxHeatAreas);

                lastDetections = heatDetections;

                // ----------------------------------------------------
                // DETECTION / BOX OVERLAY
                //
                // ToggleSwitch2 controls BOTH:
                //  - maximum heat boxes
                //  - Python AI detection boxes
                // ----------------------------------------------------

                List<DetectionResult> aiDetections =
                    new List<DetectionResult>();

                if (guna2ToggleSwitch2.Checked)
                {
                    DrawHeatBoxes(
                        processed,
                        heatDetections);

                    if (runDetector)
                    {
                        aiDetections =
                            await RunPythonDetectionAsync(
                                selectedFilePath);

                        DrawDetections(
                            processed,
                            aiDetections);
                    }
                }

                // ----------------------------------------------------
                // ROAD / GRID OVERLAY
                //
                // ToggleSwitch3 controls this.
                // ----------------------------------------------------

                if (guna2ToggleSwitch3.Checked)
                {
                    double opacity =
                        guna2TrackBar7.Value / 100.0;

                    Mat overlay =
                        OverlayRoadNetwork(
                            processed,
                            opacity);

                    processed.Dispose();

                    processed = overlay;
                }

                // ----------------------------------------------------
                // FINAL OPACITY
                // ----------------------------------------------------

                double alpha =
                    guna2TrackBar5.Value / 255.0;

                alpha =
                    Math.Clamp(
                        alpha,
                        0.0,
                        1.0);

                if (alpha < 1.0)
                {
                    using Mat black =
                        Mat.Zeros(
                            processed.Size(),
                            processed.Type());

                    Cv2.AddWeighted(
                        processed,
                        alpha,
                        black,
                        1.0 - alpha,
                        0,
                        processed);
                }

                // ----------------------------------------------------
                // STORE PROCESSED IMAGE
                // ----------------------------------------------------

                DisposeMat(
                    ref processedImageMat);

                processedImageMat =
                    processed;

                stopwatch.Stop();

                // ----------------------------------------------------
                // DISPLAY
                // ----------------------------------------------------

                ApplyZoomAndDisplay();

                guna2HtmlLabel33.Text =
                    $"{stopwatch.Elapsed.TotalSeconds:F3}s";

                if (guna2ToggleSwitch2.Checked)
                {
                    guna2HtmlLabel34.Text =
                        $"{heatDetections.Count + aiDetections.Count}";
                }
                else
                {
                    guna2HtmlLabel34.Text =
                        "Boxes OFF";
                }

                double maxHeat =
                    heatDetections.Count == 0
                        ? 0
                        : heatDetections
                            .Max(x => x.Confidence) * 100.0;

                guna2HtmlLabel35.Text =
                    heatDetections.Count == 0
                        ? "Heat: --"
                        : $"Heat: {maxHeat:F1}%";

                guna2HtmlLabel37.Text =
                    BuildStatusText(
                        runDetector);
            }
            catch (Exception ex)
            {
                guna2HtmlLabel37.Text =
                    "Processing failed.";

                MessageBox.Show(
                    ex.ToString(),
                    "Processing Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _processing = false;
            }
        }

        private string BuildStatusText(
            bool runDetector)
        {
            string boxes =
                guna2ToggleSwitch2.Checked
                    ? "Boxes ON"
                    : "Boxes OFF";

            string overlay =
                guna2ToggleSwitch3.Checked
                    ? "Overlay ON"
                    : "Overlay OFF";

            string ai =
                runDetector &&
                guna2ToggleSwitch2.Checked
                    ? "AI ON"
                    : "AI OFF";

            return
                $"Completed | {boxes} | {overlay} | {ai}";
        }

        // ============================================================
        // PYTHON DETECTOR
        // ============================================================

        private async Task<List<DetectionResult>>
            RunPythonDetectionAsync(
                string imagePath)
        {
            if (pythonService == null)
            {
                return new List<DetectionResult>();
            }

            if (!pythonService.IsReady(
                    out string reason))
            {
                guna2HtmlLabel37.Text =
                    reason;

                return new List<DetectionResult>();
            }

            string selectedModel =
                guna2ComboBox1.SelectedItem?
                    .ToString()
                    ?? "V1";

            if (selectedModel ==
                "Select Model...")
            {
                selectedModel = "V1";
            }

            string json =
                await pythonService.RunDetectorAsync(
                    imagePath,
                    selectedModel);

            PythonDetectionResponse? response =
                JsonSerializer.Deserialize
                <PythonDetectionResponse>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (response == null)
            {
                throw new InvalidOperationException(
                    "Python returned an empty response.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(
                        response.Error)
                        ? "Python detector failed."
                        : response.Error);
            }

            return response.Detections ??
                   new List<DetectionResult>();
        }

        // ============================================================
        // MAXIMUM HEAT DETECTION
        // ============================================================

        private List<DetectionResult>
            DetectMaximumHeatAreas(
                Mat source,
                int requestedAreas)
        {
            var detections =
                new List<DetectionResult>();

            if (source.Empty())
                return detections;

            int maxAreas =
                Math.Clamp(
                    requestedAreas,
                    1,
                    50);

            int sourceRows =
                source.Rows;

            int sourceCols =
                source.Cols;

            using Mat gray =
                new Mat();

            if (source.Channels() == 1)
            {
                source.CopyTo(gray);
            }
            else if (source.Channels() == 4)
            {
                Cv2.CvtColor(
                    source,
                    gray,
                    ColorConversionCodes.BGRA2GRAY);
            }
            else
            {
                Cv2.CvtColor(
                    source,
                    gray,
                    ColorConversionCodes.BGR2GRAY);
            }

            using Mat normalized =
                new Mat();

            Cv2.Normalize(
                gray,
                normalized,
                0,
                255,
                NormTypes.MinMax);

            double threshold =
                CalculatePercentile(
                    normalized,
                    98.0);

            if (threshold < 5)
                threshold = 5;

            using Mat hotMask =
                new Mat();

            Cv2.Threshold(
                normalized,
                hotMask,
                threshold,
                255,
                ThresholdTypes.Binary);

            using Mat kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Ellipse,
                    new OpenCvSharp.Size(9, 9));

            Cv2.MorphologyEx(
                hotMask,
                hotMask,
                MorphTypes.Close,
                kernel,
                iterations: 2);

            Cv2.MorphologyEx(
                hotMask,
                hotMask,
                MorphTypes.Open,
                kernel,
                iterations: 1);

            Cv2.FindContours(
                hotMask,
                out OpenCvSharp.Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            var candidates =
                new List<
                    (Rect Rect,
                     double Area,
                     double Peak)>();

            double imageArea =
                sourceRows * (double)sourceCols;

            double minArea =
                Math.Max(
                    25.0,
                    imageArea * 0.00015);

            foreach (OpenCvSharp.Point[] contour
                     in contours)
            {
                double area =
                    Cv2.ContourArea(
                        contour);

                if (area < minArea)
                    continue;

                Rect rect =
                    Cv2.BoundingRect(
                        contour);

                if (rect.Width < 8 ||
                    rect.Height < 8)
                {
                    continue;
                }

                using Mat roi =
                    new Mat(
                        normalized,
                        new OpenCvSharp.Rect(
                            rect.X,
                            rect.Y,
                            rect.Width,
                            rect.Height));

                Cv2.MinMaxLoc(
                    roi,
                    out _,
                    out double maxValue,
                    out _,
                    out _);

                candidates.Add(
                    (rect,
                     area,
                     maxValue));
            }

            foreach (var candidate
                     in candidates
                         .OrderByDescending(
                             x => x.Peak)
                         .ThenByDescending(
                             x => x.Area)
                         .Take(maxAreas))
            {
                double heat =
                    Math.Clamp(
                        candidate.Peak / 255.0,
                        0.0,
                        1.0);

                detections.Add(
                    new DetectionResult
                    {
                        ClassName =
                            "HOT AREA",

                        Confidence =
                            heat,

                        X1 =
                            candidate.Rect.X,

                        Y1 =
                            candidate.Rect.Y,

                        X2 =
                            candidate.Rect.X +
                            candidate.Rect.Width,

                        Y2 =
                            candidate.Rect.Y +
                            candidate.Rect.Height
                    });
            }

            return detections;
        }

        // ============================================================
        // PERCENTILE
        // ============================================================

        private static double CalculatePercentile(
            Mat image,
            double percentile)
        {
            if (image.Empty())
                return 0;

            int rows =
                image.Rows;

            int cols =
                image.Cols;

            int total =
                rows * cols;

            const int maxSamples =
                200000;

            int step =
                Math.Max(
                    1,
                    total / maxSamples);

            image.GetArray(
                out byte[] data);

            var values =
                new List<byte>(
                    Math.Min(
                        total,
                        maxSamples));

            for (int index = 0;
                 index < total;
                 index += step)
            {
                values.Add(
                    data[index]);
            }

            if (values.Count == 0)
                return 0;

            values.Sort();

            double position =
                (percentile / 100.0) *
                (values.Count - 1);

            int lower =
                (int)Math.Floor(
                    position);

            int upper =
                (int)Math.Ceiling(
                    position);

            if (lower == upper)
                return values[lower];

            double fraction =
                position - lower;

            return
                values[lower] +
                (values[upper] -
                 values[lower]) *
                fraction;
        }

        // ============================================================
        // HEAT BOXES
        // ============================================================

        private static void DrawHeatBoxes(
            Mat image,
            IEnumerable<DetectionResult>
                hotAreas)
        {
            int imageRows =
                image.Rows;

            int imageCols =
                image.Cols;

            foreach (DetectionResult area
                     in hotAreas)
            {
                int x1 =
                    Math.Clamp(
                        area.X1,
                        0,
                        imageCols - 1);

                int y1 =
                    Math.Clamp(
                        area.Y1,
                        0,
                        imageRows - 1);

                int x2 =
                    Math.Clamp(
                        area.X2,
                        0,
                        imageCols - 1);

                int y2 =
                    Math.Clamp(
                        area.Y2,
                        0,
                        imageRows - 1);

                if (x2 <= x1 ||
                    y2 <= y1)
                {
                    continue;
                }

                // Red heat box.
                Cv2.Rectangle(
                    image,
                    new OpenCvSharp.Point(
                        x1,
                        y1),
                    new OpenCvSharp.Point(
                        x2,
                        y2),
                    Scalar.Red,
                    3);

                string label =
                    $"MAX HEAT " +
                    $"{area.Confidence * 100.0:F1}%";

                Cv2.GetTextSize(
                    label,
                    HersheyFonts.HersheySimplex,
                    0.58,
                    2,
                    out int baseline);

                OpenCvSharp.Size textSize =
                    Cv2.GetTextSize(
                        label,
                        HersheyFonts.HersheySimplex,
                        0.58,
                        2,
                        out baseline);

                int labelTop =
                    Math.Max(
                        0,
                        y1 -
                        textSize.Height -
                        10);

                int labelRight =
                    Math.Min(
                        imageCols - 1,
                        x1 +
                        textSize.Width +
                        10);

                int labelBottom =
                    Math.Min(
                        imageRows - 1,
                        labelTop +
                        textSize.Height +
                        8);

                Cv2.Rectangle(
                    image,
                    new OpenCvSharp.Point(
                        x1,
                        labelTop),
                    new OpenCvSharp.Point(
                        labelRight,
                        labelBottom),
                    Scalar.Red,
                    -1);

                Cv2.PutText(
                    image,
                    label,
                    new OpenCvSharp.Point(
                        x1 + 5,
                        labelTop +
                        textSize.Height +
                        1),
                    HersheyFonts.HersheySimplex,
                    0.58,
                    Scalar.White,
                    2);
            }
        }

        // ============================================================
        // AI DETECTION BOXES
        // ============================================================

        private static void DrawDetections(
            Mat image,
            IEnumerable<DetectionResult>
                detections)
        {
            int imageRows =
                image.Rows;

            int imageCols =
                image.Cols;

            foreach (DetectionResult obj
                     in detections)
            {
                int x1 =
                    Math.Clamp(
                        obj.X1,
                        0,
                        imageCols - 1);

                int y1 =
                    Math.Clamp(
                        obj.Y1,
                        0,
                        imageRows - 1);

                int x2 =
                    Math.Clamp(
                        obj.X2,
                        0,
                        imageCols - 1);

                int y2 =
                    Math.Clamp(
                        obj.Y2,
                        0,
                        imageRows - 1);

                if (x2 <= x1 ||
                    y2 <= y1)
                {
                    continue;
                }

                Cv2.Rectangle(
                    image,
                    new OpenCvSharp.Point(
                        x1,
                        y1),
                    new OpenCvSharp.Point(
                        x2,
                        y2),
                    Scalar.Yellow,
                    2);

                string label =
                    $"{obj.ClassName} " +
                    $"{obj.Confidence * 100.0:F1}%";

                OpenCvSharp.Size textSize =
                    Cv2.GetTextSize(
                        label,
                        HersheyFonts.HersheySimplex,
                        0.55,
                        1,
                        out int baseline);

                int textY =
                    Math.Max(
                        y1 - 5,
                        textSize.Height + 5);

                Cv2.Rectangle(
                    image,
                    new OpenCvSharp.Point(
                        x1,
                        textY -
                        textSize.Height -
                        6),
                    new OpenCvSharp.Point(
                        Math.Min(
                            imageCols - 1,
                            x1 +
                            textSize.Width +
                            6),
                        textY + 2),
                    Scalar.Black,
                    -1);

                Cv2.PutText(
                    image,
                    label,
                    new OpenCvSharp.Point(
                        x1 + 3,
                        textY - 2),
                    HersheyFonts.HersheySimplex,
                    0.55,
                    Scalar.Yellow,
                    1);
            }
        }

        // ============================================================
        // IR ENHANCEMENT
        // ============================================================

        private Mat EnhanceIrImage(
            Mat image,
            Dictionary<string, object> settings)
        {
            Mat gray =
                new Mat();

            if (image.Channels() == 3)
            {
                Cv2.CvtColor(
                    image,
                    gray,
                    ColorConversionCodes.BGR2GRAY);
            }
            else if (image.Channels() == 4)
            {
                Cv2.CvtColor(
                    image,
                    gray,
                    ColorConversionCodes.BGRA2GRAY);
            }
            else
            {
                gray =
                    image.Clone();
            }

            if (settings.TryGetValue(
                    "nuc",
                    out object? nuc) &&
                nuc is bool useNuc &&
                useNuc)
            {
                Mat nucResult =
                    ApplyNuc(gray);

                gray.Dispose();

                gray = nucResult;
            }

            using (CLAHE clahe =
                   Cv2.CreateCLAHE(
                       3.0,
                       new OpenCvSharp.Size(
                           8,
                           8)))
            {
                clahe.Apply(
                    gray,
                    gray);
            }

            Mat filtered =
                new Mat();

            Cv2.BilateralFilter(
                gray,
                filtered,
                9,
                75,
                75);

            gray.Dispose();

            gray = filtered;

            Mat edges =
                new Mat();

            Cv2.Canny(
                gray,
                edges,
                50,
                150);

            Mat enhancedGray =
                new Mat();

            Cv2.AddWeighted(
                gray,
                0.85,
                edges,
                0.15,
                0,
                enhancedGray);

            edges.Dispose();
            gray.Dispose();

            Mat enhanced;

            if (settings.TryGetValue(
                    "fusion",
                    out object? fusion) &&
                fusion is bool useFusion &&
                useFusion)
            {
                enhanced =
                    MultiSpectralFusion(
                        enhancedGray);
            }
            else if (settings.TryGetValue(
                         "false_color",
                         out object? falseColor) &&
                     falseColor is bool useFalseColor &&
                     useFalseColor)
            {
                enhanced =
                    new Mat();

                Cv2.ApplyColorMap(
                    enhancedGray,
                    enhanced,
                    ColormapTypes.Inferno);
            }
            else
            {
                enhanced =
                    new Mat();

                Cv2.CvtColor(
                    enhancedGray,
                    enhanced,
                    ColorConversionCodes.GRAY2BGR);
            }

            enhancedGray.Dispose();

            if (enhanced.Channels() == 1)
            {
                Mat colorImage =
                    new Mat();

                Cv2.ApplyColorMap(
                    enhanced,
                    colorImage,
                    ColormapTypes.Jet);

                enhanced.Dispose();

                enhanced =
                    colorImage;
            }
            else if (enhanced.Channels() == 4)
            {
                Mat bgrImage =
                    new Mat();

                Cv2.CvtColor(
                    enhanced,
                    bgrImage,
                    ColorConversionCodes.BGRA2BGR);

                enhanced.Dispose();

                enhanced =
                    bgrImage;
            }

            // --------------------------------------------------------
            // RGB ADJUST
            // --------------------------------------------------------

            if (settings.TryGetValue(
                    "rgb_adjust",
                    out object? rgbObject) &&
                rgbObject is double[] rgbFactors &&
                enhanced.Channels() == 3)
            {
                Mat[] rgbChannels =
                    Cv2.Split(enhanced);

                try
                {
                    // BGR order.
                    rgbChannels[0].ConvertTo(
                        rgbChannels[0],
                        -1,
                        rgbFactors[2]);

                    rgbChannels[1].ConvertTo(
                        rgbChannels[1],
                        -1,
                        rgbFactors[1]);

                    rgbChannels[2].ConvertTo(
                        rgbChannels[2],
                        -1,
                        rgbFactors[0]);

                    Cv2.Merge(
                        rgbChannels,
                        enhanced);
                }
                finally
                {
                    foreach (Mat channel
                             in rgbChannels)
                    {
                        channel.Dispose();
                    }
                }
            }

            Mat gammaCorrected =
                new Mat();

            enhanced.ConvertTo(
                gammaCorrected,
                -1,
                1.2,
                0);

            enhanced.Dispose();

            return gammaCorrected;
        }

        // ============================================================
        // NUC
        // ============================================================

        private Mat ApplyNuc(
            Mat image)
        {
            using Mat imgFloat =
                new Mat();

            image.ConvertTo(
                imgFloat,
                MatType.CV_32FC1);

            Cv2.MeanStdDev(
                imgFloat,
                out Scalar mean,
                out Scalar stddev);

            double m =
                mean.Val0;

            double s =
                stddev.Val0;

            Mat result =
                new Mat();

            if (s > 0)
            {
                Cv2.Subtract(
                    imgFloat,
                    new Scalar(m),
                    imgFloat);

                Cv2.Divide(
                    imgFloat,
                    new Scalar(s),
                    imgFloat);

                Cv2.Max(
                    imgFloat,
                    -3.0,
                    imgFloat);

                Cv2.Min(
                    imgFloat,
                    3.0,
                    imgFloat);

                Cv2.Add(
                    imgFloat,
                    new Scalar(3.0),
                    imgFloat);

                Cv2.Multiply(
                    imgFloat,
                    new Scalar(255.0 / 6.0),
                    imgFloat);

                imgFloat.ConvertTo(
                    result,
                    MatType.CV_8UC1);
            }
            else
            {
                result =
                    image.Clone();
            }

            return result;
        }

        // ============================================================
        // MULTISPECTRAL FUSION
        // ============================================================

        private Mat MultiSpectralFusion(
            Mat image)
        {
            Mat gray =
                new Mat();

            if (image.Channels() == 1)
            {
                gray =
                    image.Clone();
            }
            else
            {
                Cv2.CvtColor(
                    image,
                    gray,
                    ColorConversionCodes.BGR2GRAY);
            }

            Mat color =
                new Mat();

            Cv2.ApplyColorMap(
                gray,
                color,
                ColormapTypes.Magma);

            gray.Dispose();

            Mat hsv =
                new Mat();

            Cv2.CvtColor(
                color,
                hsv,
                ColorConversionCodes.BGR2HSV);

            color.Dispose();

            // IMPORTANT:
            // Renamed from "channels" to prevent
            // local-variable scope conflict.
            Mat[] hsvChannels =
                Cv2.Split(hsv);

            try
            {
                double saturationScale =
                    guna2TrackBar1.Value / 100.0;

                hsvChannels[1].ConvertTo(
                    hsvChannels[1],
                    -1,
                    saturationScale * 1.2);

                hsvChannels[2].ConvertTo(
                    hsvChannels[2],
                    -1,
                    1.3);

                Cv2.Merge(
                    hsvChannels,
                    hsv);
            }
            finally
            {
                foreach (Mat channel
                         in hsvChannels)
                {
                    channel.Dispose();
                }
            }

            Mat fused =
                new Mat();

            Cv2.CvtColor(
                hsv,
                fused,
                ColorConversionCodes.HSV2BGR);

            hsv.Dispose();

            return fused;
        }

        // ============================================================
        // SEGMENTATION
        // ============================================================

        private Mat ApplySegmentation(
            Mat image)
        {
            Mat gray =
                new Mat();

            Cv2.CvtColor(
                image,
                gray,
                ColorConversionCodes.BGR2GRAY);

            Mat threshold =
                new Mat();

            Cv2.Threshold(
                gray,
                threshold,
                0,
                255,
                ThresholdTypes.Binary |
                ThresholdTypes.Otsu);

            gray.Dispose();

            Mat segmented =
                new Mat();

            Cv2.ApplyColorMap(
                threshold,
                segmented,
                ColormapTypes.Bone);

            threshold.Dispose();

            Mat output =
                new Mat();

            Cv2.AddWeighted(
                image,
                0.6,
                segmented,
                0.4,
                0,
                output);

            segmented.Dispose();

            return output;
        }

        // ============================================================
        // ROAD / GRID OVERLAY
        // ============================================================

        private Mat OverlayRoadNetwork(
            Mat image,
            double opacity)
        {
            int rows =
                image.Rows;

            int cols =
                image.Cols;

            opacity =
                Math.Clamp(
                    opacity,
                    0.0,
                    1.0);

            using Mat overlay =
                Mat.Zeros(
                    image.Size(),
                    image.Type());

            int step = 60;

            for (int y = step;
                 y < rows;
                 y += step)
            {
                Cv2.Line(
                    overlay,
                    new OpenCvSharp.Point(
                        0,
                        y),
                    new OpenCvSharp.Point(
                        cols - 1,
                        y),
                    Scalar.Lime,
                    2);
            }

            for (int x = step;
                 x < cols;
                 x += step)
            {
                Cv2.Line(
                    overlay,
                    new OpenCvSharp.Point(
                        x,
                        0),
                    new OpenCvSharp.Point(
                        x,
                        rows - 1),
                    Scalar.Lime,
                    2);
            }

            Mat output =
                new Mat();

            Cv2.AddWeighted(
                image,
                1.0 - opacity,
                overlay,
                opacity,
                0,
                output);

            return output;
        }

        // ============================================================
        // EXPORT
        // ============================================================

        private async void guna2Button1_Click(
            object sender,
            EventArgs e)
        {
            if (processedImageMat == null ||
                processedImageMat.Empty())
            {
                MessageBox.Show(
                    "There is no processed image available to export.",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            using SaveFileDialog dialog =
                new SaveFileDialog
                {
                    Title =
                        "Export Processed Image",

                    Filter =
                        "PNG Image|*.png|" +
                        "JPEG Image|*.jpg|" +
                        "Bitmap Image|*.bmp",

                    DefaultExt =
                        "png"
                };

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            try
            {
                guna2ProgressBar2.Value =
                    20;

                guna2HtmlLabel37.Text =
                    "Exporting processed image...";

                using Bitmap bitmap =
                    BitmapConverter.ToBitmap(
                        processedImageMat);

                guna2ProgressBar2.Value =
                    60;

                ImageFormat format =
                    ImageFormat.Png;

                string extension =
                    Path.GetExtension(
                        dialog.FileName)
                        .ToLowerInvariant();

                if (extension == ".jpg" ||
                    extension == ".jpeg")
                {
                    format =
                        ImageFormat.Jpeg;
                }
                else if (extension == ".bmp")
                {
                    format =
                        ImageFormat.Bmp;
                }

                bitmap.Save(
                    dialog.FileName,
                    format);

                guna2ProgressBar2.Value =
                    100;

                guna2HtmlLabel37.Text =
                    $"Exported: " +
                    $"{Path.GetFileName(dialog.FileName)}";

                MessageBox.Show(
                    "Processed image exported successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            await Task.CompletedTask;
        }

        // ============================================================
        // RESET
        // ============================================================

        private async void guna2GradientButton2_Click(
            object sender,
            EventArgs e)
        {
            _suppressRefresh = true;

            try
            {
                guna2TrackBar1.Value = 100;
                guna2TrackBar2.Value = 100;
                guna2TrackBar3.Value = 100;
                guna2TrackBar4.Value = 100;
                guna2TrackBar5.Value = 255;
                guna2TrackBar6.Value = 100;
                guna2TrackBar7.Value = 50;

                guna2HtmlLabel32.Text =
                    "50%";

                guna2HtmlLabel23.Text =
                    "100%";

                guna2ToggleSwitch1.Checked =
                    false;

                guna2ToggleSwitch2.Checked =
                    false;

                guna2ToggleSwitch3.Checked =
                    false;

                guna2ToggleSwitch4.Checked =
                    false;

                guna2ToggleSwitch5.Checked =
                    false;

                lastDetections.Clear();

                ReplacePictureBoxImage(
                    guna2PictureBox1,
                    null);

                ReplacePictureBoxImage(
                    guna2PictureBox2,
                    null);

                ReplacePictureBoxImage(
                    guna2PictureBox3,
                    null);

                DisposeMat(
                    ref rawImageMat);

                DisposeMat(
                    ref processedImageMat);

                selectedFilePath =
                    string.Empty;

                guna2ProgressBar1.Value =
                    0;

                guna2ProgressBar2.Value =
                    0;

                guna2HtmlLabel33.Text =
                    "Time: --";

                guna2HtmlLabel34.Text =
                    "Objects: --";

                guna2HtmlLabel35.Text =
                    "Conf: --";

                guna2HtmlLabel36.Text =
                    "Resolution: --";

                guna2HtmlLabel37.Text =
                    "State reset complete.";
            }
            finally
            {
                _suppressRefresh = false;
            }

            await Task.CompletedTask;

            MessageBox.Show(
                "All options and images have been reset.",
                "Reset",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ============================================================
        // ZOOM
        // ============================================================

        private void guna2TrackBar6_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            guna2HtmlLabel23.Text =
                $"{guna2TrackBar6.Value}%";

            ApplyZoomAndDisplay();
        }

        private void ApplyZoomAndDisplay()
        {
            if (rawImageMat == null ||
                rawImageMat.Empty())
            {
                return;
            }

            double scale =
                guna2TrackBar6.Value / 100.0;

            using Mat rawDisplay =
                EnsureBgr(rawImageMat);

            DisplayScaled(
                guna2PictureBox1,
                rawDisplay,
                scale);

            if (processedImageMat != null &&
                !processedImageMat.Empty())
            {
                using Mat processedDisplay =
                    EnsureBgr(
                        processedImageMat);

                DisplayScaled(
                    guna2PictureBox2,
                    processedDisplay,
                    scale);
            }
        }

        private static Mat EnsureBgr(
            Mat source)
        {
            Mat output =
                new Mat();

            if (source.Channels() == 1)
            {
                Cv2.CvtColor(
                    source,
                    output,
                    ColorConversionCodes.GRAY2BGR);
            }
            else if (source.Channels() == 4)
            {
                Cv2.CvtColor(
                    source,
                    output,
                    ColorConversionCodes.BGRA2BGR);
            }
            else
            {
                output =
                    source.Clone();
            }

            return output;
        }

        private static void DisplayScaled(
            PictureBox pictureBox,
            Mat image,
            double scale)
        {
            int cols =
                image.Cols;

            int rows =
                image.Rows;

            int width =
                Math.Max(
                    1,
                    (int)(cols * scale));

            int height =
                Math.Max(
                    1,
                    (int)(rows * scale));

            using Mat resized =
                new Mat();

            Cv2.Resize(
                image,
                resized,
                new OpenCvSharp.Size(
                    width,
                    height));

            Bitmap bitmap =
                BitmapConverter.ToBitmap(
                    resized);

            ReplacePictureBoxImage(
                pictureBox,
                bitmap);
        }

        private static void ReplacePictureBoxImage(
            PictureBox box,
            Image? image)
        {
            Image? old =
                box.Image;

            box.Image =
                image;

            old?.Dispose();
        }

        // ============================================================
        // OVERLAY OPACITY
        // ============================================================

        private void guna2TrackBar7_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            guna2HtmlLabel32.Text =
                $"{guna2TrackBar7.Value}%";

            RefreshPipeline();
        }

        // ============================================================
        // TOGGLES
        // ============================================================

        private void guna2ToggleSwitch1_CheckedChanged(
            object sender,
            EventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2ToggleSwitch2_CheckedChanged(
            object sender,
            EventArgs e)
        {
            // Detection / heat-box overlay.
            RefreshPipeline();
        }

        private void guna2ToggleSwitch3_CheckedChanged(
            object sender,
            EventArgs e)
        {
            // Road/grid overlay.
            RefreshPipeline();
        }

        private void guna2ToggleSwitch4_CheckedChanged(
            object sender,
            EventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2ToggleSwitch5_CheckedChanged(
            object sender,
            EventArgs e)
        {
            RefreshPipeline();
        }

        // ============================================================
        // TRACKBARS
        // ============================================================

        private void guna2TrackBar1_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2TrackBar2_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2TrackBar3_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2TrackBar4_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            RefreshPipeline();
        }

        private void guna2TrackBar5_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            RefreshPipeline();
        }

        // ============================================================
        // REFRESH
        // ============================================================

        private async void RefreshPipeline()
        {
            if (_suppressRefresh ||
                rawImageMat == null ||
                rawImageMat.Empty() ||
                _processing)
            {
                return;
            }

            await ProcessCurrentImageAsync(
                false);
        }

        // ============================================================
        // MODEL SELECT
        // ============================================================

        private void guna2ComboBox1_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            string? selectedModel =
                guna2ComboBox1.SelectedItem?
                    .ToString();

            if (string.IsNullOrEmpty(
                    selectedModel) ||
                selectedModel ==
                    "Select Model...")
            {
                return;
            }

            if (selectedModel == "V1")
            {
                LoadModelV1();
            }
            else if (selectedModel == "V2")
            {
                LoadModelV2();
            }

            lastDetections.Clear();
        }

        private void LoadModelV1()
        {
            guna2ComboBox1.FillColor =
                Color.LightBlue;

            guna2HtmlLabel37.Text =
                "Model V1 selected.";
        }

        private void LoadModelV2()
        {
            guna2ComboBox1.FillColor =
                Color.LightGreen;

            guna2HtmlLabel37.Text =
                "Model V2 selected.";
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        private static void DisposeMat(
            ref Mat? mat)
        {
            if (mat != null)
            {
                mat.Dispose();
                mat = null;
            }
        }

        protected override void OnFormClosed(
            FormClosedEventArgs e)
        {
            DisposeMat(
                ref rawImageMat);

            DisposeMat(
                ref processedImageMat);

            base.OnFormClosed(e);
        }

        // ============================================================
        // DESIGNER EVENT HANDLERS
        // ============================================================

        private void guna2ProgressBar1_ValueChanged(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel1_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2TextBox1_TextChanged(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel3_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel4_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2PictureBox1_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2PictureBox2_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2ProgressBar2_ValueChanged(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel24_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2vSeparator1_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel23_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2ContainerControl3_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel30_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2CirclePictureBox1_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel32_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel36_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel34_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel33_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel35_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2HtmlLabel37_Click(
            object sender,
            EventArgs e)
        {
        }

        private void guna2ImageButton1_Click(
            object sender,
            EventArgs e)
        {
            Application.Exit();
        }

        private void guna2HtmlLabel29_Click(
            object sender,
            EventArgs e)
        {
        }
    }
}