using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PDFtoImage;
using SkiaSharp;
using LightOnOCRWrapper;

namespace LightOnOCR_UI
{
    public class OcrJobItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string ResultText { get; set; } = "";

        // Store original image bytes for bbox cropping
        public byte[] ImageBytes { get; set; }

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanDelete)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
            }
        }

        public string StatusColor => Status == "Pending" ? "#9CA3AF" : Status == "Processing" ? "#F59E0B" : Status == "Error" ? "#EF4444" : "#10B981";
        public bool CanDelete => Status != "Processing";
        public bool CanSave => Status == "Completed";

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<OcrJobItem> JobQueue { get; set; } = new ObservableCollection<OcrJobItem>();
        private bool _isProcessing = false;
        private bool _isDarkMode = false;
        private OcrEngine _ocrEngine;

        public MainWindow()
        {
            InitializeComponent();
            FileQueueList.ItemsSource = JobQueue;
            this.Closed += MainWindow_Closed;

            // Immediately update UI to show it's loading, before doing any heavy lifting
            UpdateStatus("Initializing UI...", "⏳");
            StartBtn.IsEnabled = false; // Prevent user from starting OCR before model is ready
            StartBtn.Content = "LOADING AI MODEL...";

            UpdateExportButtonsUI(); // Initialize buttons

            // Call the async init method without blocking the UI thread
            _ = InitializeOcrEngineAsync();
        }

        private async Task InitializeOcrEngineAsync()
        {
            try
            {
                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model", "LightOnOCR-2-1B-bbox-BF16.gguf");
                string projPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model", "mmproj-F32.gguf");

                if (File.Exists(modelPath) && File.Exists(projPath))
                {
                    UpdateStatus("Warming up AI Engine...", "⚙️");

                    // Make sure overlay is visible when starting
                    Dispatcher.Invoke(() => LoadingOverlay.Visibility = Visibility.Visible);

                    // Run the heavy model loading on a background thread
                    await Task.Run(() =>
                    {
                        _ocrEngine = new OcrEngine(modelPath, projPath);
                    });

                    // Re-enable UI and hide the overlay when done
                    Dispatcher.Invoke(() =>
                    {
                        // 🔥 HIDE THE OVERLAY 🔥
                        LoadingOverlay.Visibility = Visibility.Collapsed;

                        StartBtn.IsEnabled = JobQueue.Count > 0;
                        StartBtn.Content = "START PROCESSING";
                        UpdateStatus("System Ready. Please select documents...", "✅");
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Show error state on the overlay
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        StartBtn.Content = "MODEL NOT FOUND";
                        UpdateStatus($"ERROR: Model files not found in {Path.GetDirectoryName(modelPath)}", "❌");
                        MessageBox.Show("AI Model files are missing. Please place them in the 'model' folder.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    StartBtn.Content = "INITIALIZATION FAILED";
                    UpdateStatus($"Initialization Error: {ex.Message}", "❌");
                });
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_ocrEngine != null) { _ocrEngine.Dispose(); _ocrEngine = null; }
        }

        private void UpdateStatus(string message, string icon = "ℹ️")
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                StatusIcon.Text = icon;
            });
        }

        // --- DYNAMIC EXPORT BUTTONS UI ---
        private void UpdateExportButtonsUI()
        {
            int successCount = JobQueue.Count(j => j.Status == "Completed");

            if (successCount == 0)
            {
                SaveFilesBtn.IsEnabled = false;
                SaveFilesBtn.Content = "💾 Save File (.docx)";
                CombineFilesBtn.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(SaveFilesBtn, 3); // Stretch to fill
                Dispatcher.Invoke(() => OutputTextBox.Clear()); // Clear output when no successful jobs
            }
            else if (successCount == 1)
            {
                SaveFilesBtn.IsEnabled = true;
                SaveFilesBtn.Content = "💾 Save File (.docx)";
                CombineFilesBtn.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(SaveFilesBtn, 3); // Stretch to fill
            }
            else // > 1
            {
                SaveFilesBtn.IsEnabled = true;
                SaveFilesBtn.Content = "📦 Save as ZIP (.zip)";
                CombineFilesBtn.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(SaveFilesBtn, 1); // Shrink to share space
            }
        }

        // --- 1. THEME SWITCHING ---
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ThemeToggleBtn.Content = _isDarkMode ? "☀️ Light Mode" : "🌙 Dark Mode";

            this.Resources["AppBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#0F172A" : "#F3F4F6"));
            this.Resources["PanelBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#1E293B" : "#FFFFFF"));
            this.Resources["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#F8FAFC" : "#111827"));
            this.Resources["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#94A3B8" : "#6B7280"));
            this.Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#334155" : "#E5E7EB"));
            this.Resources["StatusBarBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isDarkMode ? "#0B1120" : "#E5E7EB"));
        }

        // --- 2. FILE SELECTION ---
        private void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Supported Images/PDFs|*.png;*.jpg;*.jpeg;*.pdf",
                Title = "Select Documents for OCR"
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    JobQueue.Add(new OcrJobItem { FileName = Path.GetFileName(file), FullPath = file });
                }
                UpdateStatus($"{JobQueue.Count} file(s) in queue.", "📁");
            }

            StartBtn.IsEnabled = JobQueue.Count > 0 && _ocrEngine != null;
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is OcrJobItem item)
            {
                // Allow removing files at any time, then update the UI
                JobQueue.Remove(item);
                UpdateStatus($"{JobQueue.Count} file(s) in queue.", "📁");
                UpdateExportButtonsUI();
            }

            StartBtn.IsEnabled = JobQueue.Count > 0 && _ocrEngine != null;
        }

        // --- CLEAR ALL QUEUE ---
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                MessageBox.Show("Please wait for processing to finish before clearing the queue.", "Processing Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            JobQueue.Clear();
            OutputTextBox.Clear();
            UpdateExportButtonsUI();
            UpdateStatus("Queue cleared.", "🧹");

            StartBtn.IsEnabled = false;
        }

        // --- INDIVIDUAL FILE SAVE ---
        private void SaveSingleFile_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is OcrJobItem item && item.Status == "Completed")
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = $"Save {item.FileName} Output",
                    Filter = "Word Document|*.docx|Markdown File|*.md|Text File|*.txt",
                    FileName = Path.GetFileNameWithoutExtension(item.FileName) + ".docx"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        var converter = new MarkdownToWordConverter();

                        if (dlg.FileName.EndsWith(".docx"))
                        {
                            converter.ConvertMarkdownToDocx(item.ResultText, dlg.FileName);
                            converter.CleanupTempImages();
                        }
                        else if (dlg.FileName.EndsWith(".md"))
                        {
                            File.WriteAllText(dlg.FileName, item.ResultText);
                        }
                        else
                        {
                            File.WriteAllText(dlg.FileName, item.ResultText);
                        }

                        ShowSuccessDialog($"Successfully saved {Path.GetFileName(dlg.FileName)}!\nWould you like to view it now?", dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}", "❌");
                    }
                }
            }
        }

        // --- 3. LIST REORDERING ---
        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                // Ensure we don't start a drag if the user is clicking a button (like Delete or Save)
                if (e.OriginalSource is FrameworkElement el && el.TemplatedParent is Button) return;

                var point = e.GetPosition(listBox);
                var hitTestResult = VisualTreeHelper.HitTest(listBox, point);

                if (hitTestResult != null)
                {
                    ListBoxItem item = FindAncestor<ListBoxItem>(hitTestResult.VisualHit);
                    if (item != null && item.DataContext is OcrJobItem dataItem)
                    {
                        DragDrop.DoDragDrop(listBox, dataItem, DragDropEffects.Move);
                    }
                }
            }
        }

        private void List_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(OcrJobItem)) is OcrJobItem droppedData)
            {
                var point = e.GetPosition(FileQueueList);
                var hitTestResult = VisualTreeHelper.HitTest(FileQueueList, point);

                ListBoxItem targetItemContainer = FindAncestor<ListBoxItem>(hitTestResult?.VisualHit);

                int targetIdx = JobQueue.Count - 1; // Default to end
                if (targetItemContainer?.DataContext is OcrJobItem targetData)
                {
                    targetIdx = JobQueue.IndexOf(targetData);
                }

                int removedIdx = JobQueue.IndexOf(droppedData);
                if (removedIdx != targetIdx && removedIdx != -1)
                {
                    JobQueue.RemoveAt(removedIdx);
                    JobQueue.Insert(targetIdx, droppedData);
                }
            }
        }

        // Helper to find the ListBoxItem visual container
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        // --- 4. OCR PROCESSING ---
        private async void StartOcr_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing || JobQueue.Count == 0 || _ocrEngine == null) return;

            _isProcessing = true;
            UpdateExportButtonsUI();
            StartBtn.Content = "PROCESSING...";
            Dispatcher.Invoke(() => OutputTextBox.Clear());

            foreach (var job in JobQueue)
            {
                if (job.Status != "Pending") continue;
                job.Status = "Processing";
                UpdateStatus($"Extracting text from: {job.FileName}", "⏳");

                Decoder utf8Decoder = Encoding.UTF8.GetDecoder();

                Action<byte[]> onTokenGenerated = (bytes) =>
                {
                    char[] chars = new char[utf8Decoder.GetCharCount(bytes, 0, bytes.Length)];
                    int charCount = utf8Decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
                    string token = new string(chars, 0, charCount);

                    if (!string.IsNullOrEmpty(token))
                    {
                        job.ResultText += token;
                        Dispatcher.InvokeAsync(() => { OutputTextBox.AppendText(token); OutputTextBox.ScrollToEnd(); });
                    }
                };

                try
                {
                    await Task.Run(() =>
                    {
                        var converter = new MarkdownToWordConverter();

                        if (Path.GetExtension(job.FullPath).ToLower() == ".pdf")
                        {
                            // For PDFs, we completely replace the job text with the fully processed (and cropped) text
                            string processedPdfText = ProcessPdf(job.FullPath, onTokenGenerated);

                            // We override the live-streamed ResultText with the version that has local image paths
                            job.ResultText = processedPdfText;
                        }
                        else
                        {
                            // For single images
                            byte[] imageBytes = File.ReadAllBytes(job.FullPath);

                            // We need to capture the raw text to crop it
                            string rawText = "";
                            Action<byte[]> imageTokenCallback = (bytes) => {
                                Decoder decoder = Encoding.UTF8.GetDecoder();
                                char[] chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length)];
                                int charCount = decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
                                string token = new string(chars, 0, charCount);

                                rawText += token;
                                onTokenGenerated(bytes); // Update UI
                            };

                            // Run OCR
                            _ocrEngine.ProcessImageBytes(imageBytes, imageTokenCallback);

                            // Crop and replace text
                            string processedText = converter.ProcessBboxAndCropImages(rawText, imageBytes);

                            // Override with cropped version
                            job.ResultText = processedText;
                        }
                    });

                    job.Status = "Completed";
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}", "❌");
                    job.Status = "Error";
                }
            }

            _isProcessing = false;
            StartBtn.Content = "START PROCESSING";
            UpdateStatus("Processing complete! Ready to save.", "✅");
            UpdateExportButtonsUI();
        }

        private string ProcessPdf(string pdfPath, Action<byte[]> onToken)
        {
            int pageCount = 0;
            StringBuilder finalPdfText = new StringBuilder();
            var converter = new MarkdownToWordConverter(); // Instantiate the cropper

            using (var tempStream = File.OpenRead(pdfPath))
            {
                pageCount = Conversion.GetPageCount(tempStream);
            }

            for (int i = 0; i < pageCount; i++)
            {
                using (var stream = File.OpenRead(pdfPath))
                using (SKBitmap bitmap = Conversion.ToImage(stream, page: i, options: new PDFtoImage.RenderOptions { Dpi = 300 }))
                {
                    if (bitmap == null) continue;

                    using (var imageStream = new MemoryStream())
                    {
                        // 1. Get the bytes for THIS specific page
                        bitmap.Encode(imageStream, SKEncodedImageFormat.Png, 100);
                        byte[] pageBytes = imageStream.ToArray();

                        // We need to capture the text for just this page to crop it
                        string pageText = "";

                        // Wrap the original token callback to also capture the full page string
                        Action<byte[]> pageTokenCallback = (bytes) =>
                        {
                            Decoder decoder = Encoding.UTF8.GetDecoder();
                            char[] chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length)];
                            int charCount = decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
                            string token = new string(chars, 0, charCount);

                            pageText += token; // Build the page text locally
                            onToken(bytes);    // Still update the UI live
                        };

                        // 2. Run the OCR engine on THIS page
                        _ocrEngine.ProcessImageBytes(pageBytes, pageTokenCallback);

                        // 3. Process the bounding boxes and crop images for THIS page immediately
                        string croppedPageText = converter.ProcessBboxAndCropImages(pageText, pageBytes);

                        // 4. Append the fully processed page text to the final document
                        finalPdfText.AppendLine(croppedPageText);

                        // add page break if not last page (native Microsoft Word XML tag)
                        if (i < pageCount - 1)
                        {
                            finalPdfText.AppendLine("\n\n```{=openxml}\n<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>\n```\n\n");
                        }
                    }
                }
            }
            return finalPdfText.ToString();
        }

        // --- 5. EXPORT LOGIC ---
        private void SaveFiles_Click(object sender, RoutedEventArgs e)
        {
            var completedJobs = JobQueue.Where(j => j.Status == "Completed").ToList();
            if (completedJobs.Count == 0) return;

            if (completedJobs.Count == 1)
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = "Save Document",
                    Filter = "Word Document|*.docx|Markdown File|*.md|Text File|*.txt",
                    FileName = Path.GetFileNameWithoutExtension(completedJobs[0].FileName) + ".docx"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        var converter = new MarkdownToWordConverter();
                        converter.ConvertMarkdownToDocx(completedJobs[0].ResultText, dlg.FileName);
                        converter.CleanupTempImages();
                        ShowSuccessDialog($"Successfully saved {Path.GetFileName(dlg.FileName)}!\nWould you like to view it now?", dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}", "❌");
                    }
                }
            }
            else
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = "Save as ZIP",
                    Filter = "ZIP Archive|*.zip",
                    FileName = "OCR_Results.zip"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        if (File.Exists(dlg.FileName))
                            File.Delete(dlg.FileName);

                        using (var zipStream = new FileStream(dlg.FileName, FileMode.Create))
                        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                        {
                            foreach (var job in completedJobs)
                            {
                                if (string.IsNullOrWhiteSpace(job.ResultText)) continue;

                                var converter = new MarkdownToWordConverter();
                                
                                string filename = Path.GetFileNameWithoutExtension(job.FileName) + ".docx";
                                var entry = archive.CreateEntry(filename);

                                using (var entryStream = entry.Open())
                                using (var tempFile = new MemoryStream())
                                {
                                    // Hack: use temp file path for in-memory DOCX
                                    string tempPath = Path.Combine(Path.GetTempPath(), filename);
                                    converter.ConvertMarkdownToDocx(job.ResultText, tempPath);
                                    byte[] docxBytes = File.ReadAllBytes(tempPath);
                                    entryStream.Write(docxBytes, 0, docxBytes.Length);
                                    File.Delete(tempPath);
                                }

                                converter.CleanupTempImages();
                            }
                        }
                        ShowSuccessDialog($"Successfully saved {Path.GetFileName(dlg.FileName)}!\n", dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}", "❌");
                    }
                }
            }
        }

        private void CombineFiles_Click(object sender, RoutedEventArgs e)
        {
            var completedJobs = JobQueue.Where(j => j.Status == "Completed").ToList();
            if (completedJobs.Count == 0) return;

            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Save Combined Output",
                Filter = "Word Document|*.docx|Markdown File|*.md",
                FileName = "Combined_OCR_Results.docx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var converter = new MarkdownToWordConverter();
                    StringBuilder combined = new StringBuilder();

                    foreach (var job in completedJobs)
                    {
                        if (string.IsNullOrWhiteSpace(job.ResultText)) continue;

                        combined.AppendLine(job.ResultText);
                    }

                    if (dlg.FileName.EndsWith(".docx"))
                    {
                        converter.ConvertMarkdownToDocx(combined.ToString(), dlg.FileName);
                        converter.CleanupTempImages();
                    }
                    else
                    {
                        File.WriteAllText(dlg.FileName, combined.ToString());
                    }

                    // 🚀 SHOW THE BEAUTIFUL SUCCESS DIALOG 🚀
                    ShowSuccessDialog($"Successfully saved {Path.GetFileName(dlg.FileName)}!\nWould you like to view it now?", dlg.FileName);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}", "❌");
                }
            }
        }

        private void ShowSuccessDialog(string message, string filePath)
        {
            // 1. Apply a subtle blur and dim the main window slightly
            var blurEffect = new System.Windows.Media.Effects.BlurEffect { Radius = 5 };
            MainContainer.Effect = blurEffect;
            MainContainer.Opacity = 0.8; // Dims the background slightly to make the popup pop!

            try
            {
                // 2. Create and show the dialog
                SuccessDialog successDialog = new SuccessDialog(message, filePath)
                {
                    Owner = this // Centers it over this window
                };

                successDialog.ShowDialog(); // This blocks until the user closes the popup
            }
            finally
            {
                // 3. REMOVE the blur and restore opacity as soon as the popup closes
                MainContainer.Effect = null;
                MainContainer.Opacity = 1.0;
            }
        }
    }
}
