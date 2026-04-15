using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LightOnOCR_UI
{
    public partial class SuccessDialog : Window
    {
        private string _savedFilePath;

        public SuccessDialog(string message, string filePath)
        {
            InitializeComponent();
            MessageText.Text = message;
            _savedFilePath = filePath;

            // Share the colors from the main window
            this.Resources = Application.Current.MainWindow.Resources;

            // Only show the "Open File" button if it's a Word Doc or text file that exists
            if (File.Exists(_savedFilePath) &&
               (_savedFilePath.EndsWith(".docx") || _savedFilePath.EndsWith(".md") || _savedFilePath.EndsWith(".txt")))
            {
                OpenFileBtn.Visibility = Visibility.Visible;
            }
            else
            {
                // If it's a ZIP or we shouldn't open it, make the Close button span the whole width
                System.Windows.Controls.Grid.SetColumnSpan(CloseBtn, 3);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Launch the file using the default Windows application (e.g. MS Word)
                Process.Start(new ProcessStartInfo
                {
                    FileName = _savedFilePath,
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Close();
            }
        }
    }
}