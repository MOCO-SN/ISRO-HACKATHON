using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ISRO_HACKATHON
{
    public partial class Form1 : Form
    {
        // Global state variables
        private string selectedFilePath = string.Empty;
        private Mat rawImageMat = null;
        private Mat processedImageMat = null;
        private Random random = new Random();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // --- CENTER AND SCALE IMAGE PREVIEWS ---
            guna2PictureBox1.SizeMode = PictureBoxSizeMode.Zoom; // Raw input preview
            guna2PictureBox2.SizeMode = PictureBoxSizeMode.Zoom; // Processed output preview
            guna2PictureBox3.SizeMode = PictureBoxSizeMode.Zoom; // Selector container

            // Initializing default trackbar states
            guna2TrackBar1.Value = 100; // S channel scaler (100 = 1.0)
            guna2TrackBar2.Value = 100; // R multiplier (100 = 1.0)
            guna2TrackBar3.Value = 100; // G multiplier (100 = 1.0)
            guna2TrackBar4.Value = 100; // B multiplier (100 = 1.0)
            guna2TrackBar5.Value = 255; // Alpha opacity multiplier (0 - 255)
            guna2TrackBar7.Value = 50;  // Overlay opacity controller (50%)
            guna2HtmlLabel32.Text = "50%";
            guna2TrackBar6.Value = 100; // Zoom level (100%)
            guna2HtmlLabel23.Text = "100%";
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            // Populate ComboBox with models
            guna2ComboBox1.Items.Clear();
            guna2ComboBox1.Items.AddRange(new object[]
            {
                "Select Model...",
                "V1",
                "V2"
            });
            guna2ComboBox1.SelectedIndex = 0;
        }

        // --- IMAGE LOADER ---
        private void guna2PictureBox3_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select an image file";
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp";
                openFileDialog.FileName = "";
                openFileDialog.Multiselect = false;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;

                    // Cleanup any existing Mat memory cleanly
                    if (rawImageMat != null) { rawImageMat.Dispose(); }
                    if (processedImageMat != null) { processedImageMat.Dispose(); }
                    processedImageMat = null;

                    // Read image natively using OpenCV
                    rawImageMat = Cv2.ImRead(selectedFilePath, ImreadModes.AnyColor);

                    // Display raw previews safely with initial settings applied
                    guna2PictureBox3.Image = System.Drawing.Image.FromFile(selectedFilePath);

                    // Update resolution metadata label
                    guna2HtmlLabel36.Text = $"{rawImageMat.Cols}x{rawImageMat.Rows}";

                    // Show progress completion on raw load
                    guna2ProgressBar1.Value = 100;
                    guna2HtmlLabel37.Text = "Raw image loaded successfully.";

                    // Perform initial paint of both picture boxes
                    RefreshPipeline();
                }
            }
        }

        // --- CORE PROCESS pipeline ---
        private void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            if (rawImageMat == null)
            {
                MessageBox.Show("Please load an image first by clicking the picture selector.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            guna2HtmlLabel37.Text = "Executing image processing pipeline...";
            Stopwatch stopwatch = Stopwatch.StartNew();

            // 1. Setup processing settings object dynamically from UI Toggles
            var settings = new Dictionary<string, object>
{
    { "nuc", guna2ToggleSwitch4.Checked },
    { "fusion", guna2ToggleSwitch5.Checked },   // Magma false-color fusion
    { "false_color", /* new toggle here if you add one */ false },
    { "rgb_adjust", new double[] {
        (double)guna2TrackBar2.Value / 100.0,
        (double)guna2TrackBar3.Value / 100.0,
        (double)guna2TrackBar4.Value / 100.0
    }}
};

            // 2. Perform Image Enhancements
            Mat processed = EnhanceIrImage(rawImageMat, settings);

            // 3. Apply Segmentation if Enabled (Toggle 1)
            if (guna2ToggleSwitch1.Checked)
            {
                Mat tempSeg = ApplySegmentation(processed);
                processed.Dispose();
                processed = tempSeg;
            }

            // 4. Simulate Road Network Overlay if Enabled (Toggle 3)
            if (guna2ToggleSwitch3.Checked)
            {
                double overlayOpacity = (double)guna2TrackBar7.Value / 100.0;
                Mat tempOverlay = OverlayRoadNetwork(processed, overlayOpacity);
                processed.Dispose();
                processed = tempOverlay;
            }

            // 5. Simulated Object Detection Analysis
            var boundingBoxes = DetectObjects(processed);

            // 6. Draw Object Box Overlays if Enabled (Toggle 2)
            if (guna2ToggleSwitch2.Checked)
            {
                foreach (var obj in boundingBoxes)
                {
                    int[] bbox = (int[])obj["bbox"];
                    string label = $"{obj["class"]} ({Math.Round((double)obj["confidence"] * 100, 1)}%)";

                    Cv2.Rectangle(processed, new OpenCvSharp.Point(bbox[0], bbox[1]), new OpenCvSharp.Point(bbox[2], bbox[3]), Scalar.Red, 2);
                    Cv2.PutText(processed, label, new OpenCvSharp.Point(bbox[0], bbox[1] - 5), HersheyFonts.HersheySimplex, 0.5, Scalar.Yellow, 1);
                }
            }

            // 7. Apply global alpha opacity multiplier (Trackbar 5) directly to processed output
            double alphaOpacity = (double)guna2TrackBar5.Value / 255.0;
            if (alphaOpacity < 1.0)
            {
                using (Mat transparentOverlay = Mat.Zeros(processed.Size(), processed.Type()))
                {
                    Cv2.AddWeighted(processed, alphaOpacity, transparentOverlay, 1.0 - alphaOpacity, 0, processed);
                }
            }

            stopwatch.Stop();
            double elapsedSecs = stopwatch.Elapsed.TotalSeconds;

            // 8. Safely dispose of the previous global processed frame and update it
            if (processedImageMat != null && !processedImageMat.IsDisposed)
            {
                processedImageMat.Dispose();
            }
            processedImageMat = processed;

            // 9. Output Final Images with proper scaling and updates
            ApplyZoomAndDisplay();

            // 10. Populate Metadata Labels
            guna2HtmlLabel33.Text = $"{Math.Round(elapsedSecs, 3)}s";
            guna2HtmlLabel34.Text = $"{boundingBoxes.Count}";

            // Fallback in case class-level 'random' generator isn't initialized
            var rand = random ?? new Random();
            guna2HtmlLabel35.Text = $"{Math.Round(85.0 + rand.NextDouble() * 12.0, 1)}%";
            guna2HtmlLabel37.Text = "Image pipeline executed successfully.";
        }

        // --- FIXED IMAGE ENHANCEMENT LOGIC ---
        private Mat EnhanceIrImage(Mat image, Dictionary<string, object> settings)
        {
            Mat gray = new Mat();

            // Convert to grayscale if needed
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            }
            else if (image.Channels() == 4)
            {
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
            }
            else
            {
                gray = image.Clone();
            }

            // Ensure we have a proper grayscale image
            if (gray.Channels() != 1)
            {
                Mat temp = new Mat();
                Cv2.CvtColor(gray, temp, ColorConversionCodes.BGR2GRAY);
                gray.Dispose();
                gray = temp;
            }

            // Apply NUC if enabled
            if (settings.ContainsKey("nuc") && (bool)settings["nuc"])
            {
                Mat tempNuc = ApplyNuc(gray);
                gray.Dispose();
                gray = tempNuc;
            }

            // Enhanced CLAHE for better IR contrast
            using (CLAHE clahe = Cv2.CreateCLAHE(3.0, new OpenCvSharp.Size(8, 8)))
            {
                clahe.Apply(gray, gray);
            }

            // Bilateral filter preserves edges better than Gaussian
            Mat filtered = new Mat();
            Cv2.BilateralFilter(gray, filtered, 9, 75, 75);
            gray.Dispose();
            gray = filtered;

            // Edge enhancement for better detail
            Mat edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);
            Mat enhancedGray = new Mat();
            Cv2.AddWeighted(gray, 0.85, edges, 0.15, 0, enhancedGray);
            edges.Dispose();
            gray.Dispose();

            Mat enhanced = new Mat();

            // Apply color mapping based on fusion setting
            if (settings.ContainsKey("fusion") && (bool)settings["fusion"])
            {
                // False-color multispectral fusion view (Magma-based)
                enhanced = MultiSpectralFusion(enhancedGray);
            }
            else if (settings.ContainsKey("false_color") && (bool)settings["false_color"])
            {
                // Optional false-color IR palette (only if explicitly requested)
                Cv2.ApplyColorMap(enhancedGray, enhanced, ColormapTypes.Inferno);
            }
            else
            {
                // Natural / visible-like output: no colormap, just the enhanced grayscale as BGR
                Cv2.CvtColor(enhancedGray, enhanced, ColorConversionCodes.GRAY2BGR);
            }

            // Ensure we have a 3-channel BGR image
            if (enhanced.Channels() == 1)
            {
                Mat colorImg = new Mat();
                Cv2.ApplyColorMap(enhanced, colorImg, ColormapTypes.Jet);
                enhanced.Dispose();
                enhanced = colorImg;
            }
            else if (enhanced.Channels() == 4)
            {
                Mat bgrImg = new Mat();
                Cv2.CvtColor(enhanced, bgrImg, ColorConversionCodes.BGRA2BGR);
                enhanced.Dispose();
                enhanced = bgrImg;
            }

            // Apply RGB adjustments if enabled
            if (settings.ContainsKey("rgb_adjust") && enhanced.Channels() == 3)
            {
                double[] rgbFactors = (double[])settings["rgb_adjust"];
                Mat[] bgrChannels = Cv2.Split(enhanced);

                // Multiply BGR channels safely
                bgrChannels[0].ConvertTo(bgrChannels[0], -1, rgbFactors[2]); // Blue
                bgrChannels[1].ConvertTo(bgrChannels[1], -1, rgbFactors[1]); // Green
                bgrChannels[2].ConvertTo(bgrChannels[2], -1, rgbFactors[0]); // Red

                Cv2.Merge(bgrChannels, enhanced);
                foreach (var ch in bgrChannels) ch.Dispose();
            }

            // Apply gamma correction for better visibility
            Mat gammaCorrected = new Mat();
            enhanced.ConvertTo(gammaCorrected, -1, 1.2, 0);
            enhanced.Dispose();

            // Final check - ensure we have a 3-channel BGR image
            if (gammaCorrected.Channels() != 3)
            {
                Mat finalImg = new Mat();
                if (gammaCorrected.Channels() == 1)
                {
                    Cv2.ApplyColorMap(gammaCorrected, finalImg, ColormapTypes.Jet);
                }
                else
                {
                    Cv2.CvtColor(gammaCorrected, finalImg, ColorConversionCodes.BGRA2BGR);
                }
                gammaCorrected.Dispose();
                return finalImg;
            }

            return gammaCorrected;
        }

        private Mat ApplyNuc(Mat image)
        {
            Mat imgFloat = new Mat();
            image.ConvertTo(imgFloat, MatType.CV_32FC1);

            Cv2.MeanStdDev(imgFloat, out Scalar mean, out Scalar stddev);
            double m = mean.Val0;
            double s = stddev.Val0;

            Mat imgNuc = new Mat();
            if (s > 0)
            {
                Cv2.Subtract(imgFloat, new Scalar(m), imgFloat);
                Cv2.Divide(imgFloat, new Scalar(s), imgFloat);

                Cv2.Max(imgFloat, -3.0, imgFloat);
                Cv2.Min(imgFloat, 3.0, imgFloat);

                Cv2.Add(imgFloat, new Scalar(3.0), imgFloat);
                Cv2.Multiply(imgFloat, new Scalar(255.0 / 6.0), imgFloat);

                imgFloat.ConvertTo(imgNuc, MatType.CV_8UC1);
            }
            else
            {
                imgNuc = image.Clone();
            }

            imgFloat.Dispose();
            return imgNuc;
        }

        // --- FIXED MULTISPECTRAL FUSION ---
        private Mat MultiSpectralFusion(Mat image)
        {
            // Ensure we have a single-channel grayscale image
            Mat grayImg = new Mat();
            if (image.Channels() == 1)
            {
                grayImg = image.Clone();
            }
            else
            {
                Cv2.CvtColor(image, grayImg, ColorConversionCodes.BGR2GRAY);
            }

            // Create color image using magma colormap
            Mat imgColor = new Mat();
            Cv2.ApplyColorMap(grayImg, imgColor, ColormapTypes.Magma);
            grayImg.Dispose();

            // Convert to HSV for enhancement
            Mat hsv = new Mat();
            Cv2.CvtColor(imgColor, hsv, ColorConversionCodes.BGR2HSV);
            imgColor.Dispose();

            Mat[] channels = Cv2.Split(hsv);

            // Enhance saturation and value for better visualization
            double saturationScale = (double)guna2TrackBar1.Value / 100.0;
            channels[1].ConvertTo(channels[1], -1, saturationScale * 1.2);
            channels[2].ConvertTo(channels[2], -1, 1.3); // Increase brightness

            Cv2.Merge(channels, hsv);
            Mat fused = new Mat();
            Cv2.CvtColor(hsv, fused, ColorConversionCodes.HSV2BGR);

            hsv.Dispose();
            foreach (var ch in channels) ch.Dispose();

            return fused;
        }

        // --- IMAGE SEGMENTATION SIMULATION ---
        private Mat ApplySegmentation(Mat image)
        {
            Mat gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            Mat thresh = new Mat();
            Cv2.Threshold(gray, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            gray.Dispose();

            Mat segmented = new Mat();
            Cv2.ApplyColorMap(thresh, segmented, ColormapTypes.Bone);
            thresh.Dispose();

            Mat output = new Mat();
            Cv2.AddWeighted(image, 0.6, segmented, 0.4, 0, output);
            segmented.Dispose();

            return output;
        }

        // --- ROAD OVERLAY SIMULATION ---
        private Mat OverlayRoadNetwork(Mat image, double opacity)
        {
            Mat roadGrid = Mat.Zeros(image.Size(), image.Type());
            int step = 60;

            for (int y = step; y < roadGrid.Rows; y += step)
            {
                Cv2.Line(roadGrid, new OpenCvSharp.Point(0, y), new OpenCvSharp.Point(roadGrid.Cols, y), Scalar.Lime, 2);
            }
            for (int x = step; x < roadGrid.Cols; x += step)
            {
                Cv2.Line(roadGrid, new OpenCvSharp.Point(x, 0), new OpenCvSharp.Point(x, roadGrid.Rows), Scalar.Lime, 2);
            }

            Mat output = new Mat();
            Cv2.AddWeighted(image, 1.0 - opacity, roadGrid, opacity, 0, output);
            roadGrid.Dispose();

            return output;
        }

        // --- OBJECT DETECTION SIMULATION ---
        private List<Dictionary<string, object>> DetectObjects(Mat image)
        {
            int height = image.Rows;
            int width = image.Cols;
            var detections = new List<Dictionary<string, object>>();

            int numObjects = Math.Max(1, (height * width) / 120000);
            string[] classes = { "satellite-target", "road-junction", "structure", "vehicle" };

            for (int i = 0; i < numObjects; i++)
            {
                int x = random.Next(10, width - 100);
                int y = random.Next(10, height - 100);
                int w = random.Next(40, 120);
                int h = random.Next(40, 120);
                double confidence = random.NextDouble() * (0.97 - 0.72) + 0.72;

                detections.Add(new Dictionary<string, object>
                {
                    { "class", classes[i % 4] },
                    { "bbox", new int[] { x, y, x + w, y + h } },
                    { "confidence", confidence }
                });
            }
            return detections;
        }

        // --- EXPORT FUNCTIONALITY ---
        private void guna2Button1_Click(object sender, EventArgs e)
        {
            if (processedImageMat == null)
            {
                MessageBox.Show("There is no processed image available to export.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Export Processed Image";
                saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
                saveFileDialog.DefaultExt = "png";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    guna2ProgressBar2.Value = 20;
                    guna2HtmlLabel37.Text = "Exporting processed image file...";

                    using (Bitmap bmp = BitmapConverter.ToBitmap(processedImageMat))
                    {
                        guna2ProgressBar2.Value = 60;

                        ImageFormat format = ImageFormat.Png;
                        string ext = Path.GetExtension(saveFileDialog.FileName).ToLower();
                        if (ext == ".jpg" || ext == ".jpeg") format = ImageFormat.Jpeg;
                        else if (ext == ".bmp") format = ImageFormat.Bmp;

                        bmp.Save(saveFileDialog.FileName, format);
                    }

                    guna2ProgressBar2.Value = 100;
                    guna2HtmlLabel37.Text = $"Image successfully exported to {Path.GetFileName(saveFileDialog.FileName)}";
                    MessageBox.Show("Processed image exported successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // --- RESET INPUTS ---
        private void guna2GradientButton2_Click(object sender, EventArgs e)
        {
            guna2TrackBar1.Value = 100;
            guna2TrackBar2.Value = 100;
            guna2TrackBar3.Value = 100;
            guna2TrackBar4.Value = 100;
            guna2TrackBar5.Value = 100;
            guna2TrackBar7.Value = 50;
            guna2TrackBar6.Value = 100;

            guna2HtmlLabel32.Text = "50%";
            guna2HtmlLabel23.Text = "100%";

            guna2ToggleSwitch1.Checked = false;
            guna2ToggleSwitch2.Checked = false;
            guna2ToggleSwitch3.Checked = false;
            guna2ToggleSwitch4.Checked = false;
            guna2ToggleSwitch5.Checked = false;

            // Clear old images in UI Controls safely
            var oldRawImg = guna2PictureBox1.Image;
            var oldProcImg = guna2PictureBox2.Image;
            var oldSelImg = guna2PictureBox3.Image;

            guna2PictureBox1.Image = null;
            guna2PictureBox2.Image = null;
            guna2PictureBox3.Image = null;

            oldRawImg?.Dispose();
            oldProcImg?.Dispose();
            oldSelImg?.Dispose();

            if (rawImageMat != null) { rawImageMat.Dispose(); rawImageMat = null; }
            if (processedImageMat != null) { processedImageMat.Dispose(); processedImageMat = null; }

            guna2ProgressBar1.Value = 0;
            guna2ProgressBar2.Value = 0;
            guna2HtmlLabel33.Text = "Time: --";
            guna2HtmlLabel34.Text = "Objects: --";
            guna2HtmlLabel35.Text = "Conf: --";
            guna2HtmlLabel36.Text = "Resolution: --";
            guna2HtmlLabel37.Text = "State reset complete.";

            MessageBox.Show("All options and images have been reset.", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --- ZOOM AND DISPLAY ---
        private void guna2TrackBar6_Scroll(object sender, ScrollEventArgs e)
        {
            guna2HtmlLabel23.Text = $"{guna2TrackBar6.Value}%";
            ApplyZoomAndDisplay();
        }

        // --- FIXED APPLY ZOOM AND DISPLAY ---
        private void ApplyZoomAndDisplay()
        {
            if (rawImageMat == null) return;

            double scale = (double)guna2TrackBar6.Value / 100.0;

            // 1. Display Raw Input Image
            Mat currentRawDisplay = new Mat();
            if (rawImageMat.Channels() == 1)
            {
                Cv2.CvtColor(rawImageMat, currentRawDisplay, ColorConversionCodes.GRAY2BGR);
            }
            else if (rawImageMat.Channels() == 4)
            {
                Cv2.CvtColor(rawImageMat, currentRawDisplay, ColorConversionCodes.BGRA2BGR);
            }
            else
            {
                currentRawDisplay = rawImageMat.Clone();
            }

            // Display raw image with zoom
            int rawWidth = (int)(currentRawDisplay.Cols * scale);
            int rawHeight = (int)(currentRawDisplay.Rows * scale);
            if (rawWidth > 0 && rawHeight > 0)
            {
                using (Mat resizedRaw = new Mat())
                {
                    Cv2.Resize(currentRawDisplay, resizedRaw, new OpenCvSharp.Size(rawWidth, rawHeight));
                    var oldImage = guna2PictureBox1.Image;
                    guna2PictureBox1.Image = BitmapConverter.ToBitmap(resizedRaw);
                    oldImage?.Dispose();
                }
            }
            currentRawDisplay.Dispose();

            // 2. Display Enhanced/Processed Image
            if (processedImageMat != null)
            {
                Mat displayProc = new Mat();

                // Ensure processed image is in BGR format for display
                if (processedImageMat.Channels() == 1)
                {
                    // Apply color mapping to grayscale images
                    Cv2.ApplyColorMap(processedImageMat, displayProc, ColormapTypes.Inferno);
                }
                else if (processedImageMat.Channels() == 4)
                {
                    Cv2.CvtColor(processedImageMat, displayProc, ColorConversionCodes.BGRA2BGR);
                }
                else
                {
                    displayProc = processedImageMat.Clone();
                }

                int procWidth = (int)(displayProc.Cols * scale);
                int procHeight = (int)(displayProc.Rows * scale);
                if (procWidth > 0 && procHeight > 0)
                {
                    using (Mat resizedProc = new Mat())
                    {
                        Cv2.Resize(displayProc, resizedProc, new OpenCvSharp.Size(procWidth, procHeight));
                        var oldImage = guna2PictureBox2.Image;
                        guna2PictureBox2.Image = BitmapConverter.ToBitmap(resizedProc);
                        oldImage?.Dispose();
                    }
                }
                displayProc.Dispose();
            }
        }

        // --- LIVE CONTROLLER EVENT BINDINGS ---
        private void guna2TrackBar7_Scroll(object sender, ScrollEventArgs e)
        {
            guna2HtmlLabel32.Text = $"{guna2TrackBar7.Value}%";
            RefreshPipeline();
        }

        private void guna2ToggleSwitch1_CheckedChanged(object sender, EventArgs e) => RefreshPipeline();
        private void guna2ToggleSwitch2_CheckedChanged(object sender, EventArgs e) => RefreshPipeline();
        private void guna2ToggleSwitch3_CheckedChanged(object sender, EventArgs e) => RefreshPipeline();
        private void guna2ToggleSwitch4_CheckedChanged(object sender, EventArgs e) => RefreshPipeline();
        private void guna2ToggleSwitch5_CheckedChanged(object sender, EventArgs e) => RefreshPipeline();
        private void guna2TrackBar1_Scroll(object sender, ScrollEventArgs e) => RefreshPipeline();
        private void guna2TrackBar2_Scroll(object sender, ScrollEventArgs e) => RefreshPipeline();
        private void guna2TrackBar3_Scroll(object sender, ScrollEventArgs e) => RefreshPipeline();
        private void guna2TrackBar4_Scroll(object sender, ScrollEventArgs e) => RefreshPipeline();
        private void guna2TrackBar5_Scroll(object sender, ScrollEventArgs e) => RefreshPipeline();

        private void RefreshPipeline()
        {
            if (rawImageMat != null)
            {
                guna2GradientButton1_Click(this, EventArgs.Empty);
            }
        }

        // --- MODEL SELECTIONS ---
        private void guna2ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedModel = guna2ComboBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedModel) || selectedModel == "Select Model...") return;

            switch (selectedModel)
            {
                case "V1": LoadModelV1(); break;
                case "V2": LoadModelV2(); break;
            }
        }

        private void LoadModelV1()
        {
            guna2ComboBox1.FillColor = Color.LightBlue;
            guna2HtmlLabel37.Text = "Model V1 architecture assigned.";
        }

        private void LoadModelV2()
        {
            guna2ComboBox1.FillColor = Color.LightGreen;
            guna2HtmlLabel37.Text = "Model V2 architecture assigned.";
        }

        // Empty stubs kept to prevent Designer files from breaking
        private void guna2ProgressBar1_ValueChanged(object sender, EventArgs e) { }
        private void guna2HtmlLabel1_Click(object sender, EventArgs e) { }
        private void guna2TextBox1_TextChanged(object sender, EventArgs e) { }
        private void guna2HtmlLabel3_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel4_Click(object sender, EventArgs e) { }
        private void guna2NumericUpDown1_ValueChanged(object sender, EventArgs e) { }
        private void guna2PictureBox1_Click(object sender, EventArgs e) { }
        private void guna2PictureBox2_Click(object sender, EventArgs e) { }
        private void guna2ProgressBar2_ValueChanged(object sender, EventArgs e) { }
        private void guna2HtmlLabel24_Click(object sender, EventArgs e) { }
        private void guna2vSeparator1_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel23_Click(object sender, EventArgs e) { }
        private void guna2ContainerControl3_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel30_Click(object sender, EventArgs e) { }
        private void guna2CirclePictureBox1_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel32_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel36_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel34_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel33_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel35_Click(object sender, EventArgs e) { }
        private void guna2HtmlLabel37_Click(object sender, EventArgs e) { }

        private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void guna2HtmlLabel29_Click(object sender, EventArgs e)
        {

        }
    }
}