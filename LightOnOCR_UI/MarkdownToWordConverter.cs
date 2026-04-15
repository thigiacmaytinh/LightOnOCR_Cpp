using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Windows;
using System.Diagnostics;

namespace LightOnOCR_UI
{
    public class MarkdownToWordConverter
    {
        private readonly string _tempImageDir;

        /// <summary>
        /// Initialize with a temp directory for cropped images
        /// </summary>
        public MarkdownToWordConverter(string tempImageDir = null)
        {
            _tempImageDir = tempImageDir ?? Path.Combine(Path.GetTempPath(), $"ocr_img_{Guid.NewGuid().ToString().Substring(0, 8)}");

            if (!Directory.Exists(_tempImageDir))
                Directory.CreateDirectory(_tempImageDir);
        }

        /// <summary>
        /// Process markdown: crop bbox images from source bytes, save to disk, 
        /// and replace with local file path references
        /// </summary>
        public string ProcessBboxAndCropImages(string markdownContent, byte[] sourceImageBytes)
        {
            if (string.IsNullOrWhiteSpace(markdownContent) || sourceImageBytes == null)
                return markdownContent;

            using (SKBitmap sourceBitmap = SKBitmap.Decode(sourceImageBytes))
            {
                if (sourceBitmap == null)
                {
                    MessageBox.Show("Failed to decode source image for cropping.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return markdownContent;
                }

                int w = sourceBitmap.Width;
                int h = sourceBitmap.Height;
                const int padding = 5;

                // Pattern: ![image](image_1.png) 150,200,300,450
                string pattern = @"!\[.*?\]\((image_\d+\.png)\)\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)";

                return Regex.Replace(markdownContent, pattern, match =>
                {
                    try
                    {
                        int x1 = int.Parse(match.Groups[2].Value);
                        int y1 = int.Parse(match.Groups[3].Value);
                        int x2 = int.Parse(match.Groups[4].Value);
                        int y2 = int.Parse(match.Groups[5].Value);

                        // Denormalize [0, 1000] -> pixels
                        int px1 = Math.Max(0, (x1 * w / 1000) - padding);
                        int py1 = Math.Max(0, (y1 * h / 1000) - padding);
                        int px2 = Math.Min(w, (x2 * w / 1000) + padding);
                        int py2 = Math.Min(h, (y2 * h / 1000) + padding);

                        if (px1 >= px2 || py1 >= py2) return match.Value;

                        // Crop using SkiaSharp
                        SKRectI cropRect = new SKRectI(px1, py1, px2, py2);
                        using (SKImage fullImage = SKImage.FromBitmap(sourceBitmap))
                        using (SKImage croppedImage = fullImage.Subset(cropRect))
                        using (SKData data = croppedImage.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            // Save to disk
                            string filename = $"crop_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
                            string filepath = Path.Combine(_tempImageDir, filename);
                            if (!Directory.Exists(_tempImageDir)) Directory.CreateDirectory(_tempImageDir);
                            File.WriteAllBytes(filepath, data.ToArray());

                            //string uriPath = new Uri(filepath).AbsoluteUri;

                            // Return markdown with LOCAL file path (not base64)
                            return $"![Cropped region]({filepath})";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error cropping: {ex.Message}");
                        return match.Value;
                    }
                });
            }
        }

        /// <summary>
        /// Convert markdown (with local image file paths) directly to DOCX
        /// Fast because: no base64 encoding/decoding, direct file loading
        /// </summary>
        public void ConvertMarkdownToDocx(string markdownContent, string outputPath)
        {
            string processedMarkdown = markdownContent;
            string imagePattern = @"!\[.*?\]\((.*?\.png)\)";

            processedMarkdown = Regex.Replace(processedMarkdown, imagePattern, match =>
            {
                string localImagePath = match.Groups[1].Value;
                string safePath = localImagePath.Replace("\\", "/");
                return $"![Cropped region]({safePath})";
            });

            string tempMdPath = Path.Combine(_tempImageDir, $"temp_ocr_{Guid.NewGuid().ToString("N")}.md");
            File.WriteAllText(tempMdPath, processedMarkdown);

            try
            {
                // Get the absolute path to where your application is currently running
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Combine it to find the local pandoc.exe
                string pandocPath = Path.Combine(appDirectory, "pandoc.exe");

                // Verify it actually exists before trying to run it
                if (!File.Exists(pandocPath))
                {
                    throw new Exception($"Could not find pandoc.exe at: {pandocPath}. Please ensure it is copied to the output directory.");
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = pandocPath, // 👈 Tell it exactly where the local executable is!
                    Arguments = $"\"{tempMdPath}\" -o \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"Pandoc failed with exit code {process.ExitCode}. Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Conversion failed: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempMdPath))
                {
                    File.Delete(tempMdPath);
                }
            }
        }

        /// <summary>
        /// Clean up temporary image files
        /// </summary>
        public void CleanupTempImages()
        {
            try
            {
                if (Directory.Exists(_tempImageDir))
                    Directory.Delete(_tempImageDir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }

        /// <summary>
        /// Get temp directory path (for debugging)
        /// </summary>
        public string GetTempImageDir() => _tempImageDir;
    }
}