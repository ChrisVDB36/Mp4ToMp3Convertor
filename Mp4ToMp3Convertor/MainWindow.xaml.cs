using Mp4ToMp3Convertor.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;
using MessageBox = System.Windows.Forms.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Mp4ToMp3Convertor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Dispatcher.Invoke(() => VerifyFFmpeg());
        }

        // Update controls on listview changes
        private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnDelete.IsEnabled = (lvMediaFiles.SelectedItem is MediaFile) ? true : false;
        }

        // Set output folder path
        private void BtnSetOutputFolderPath(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                // Set path on selection confirmation
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedPath = dialog.SelectedPath;

                    // Only set path if exists
                    if (Directory.Exists(selectedPath))
                    {
                        txtOutputFolderPath.Text = selectedPath;
                    }
                }
            }
        }

        // Add files to listview
        private void BtnBrowseMediaFiles(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select MP4 files",
                Filter = "MP4 files (*.mp4)|*.mp4",
                DefaultExt = ".mp4",
                Multiselect = true
            };

            // Get files
            if (openFileDialog.ShowDialog() == true)
            {
                var fileList = openFileDialog.FileNames.ToList();

                // Check count first
                if (fileList.Count > 0)
                {
                    // Go tru all selected files
                    foreach (var file in fileList)
                    {
                        var mediaFile = new MediaFile
                        {
                            FilePath = file,
                            FileName = file.Split('\\').Last().Replace(".mp4", string.Empty),
                            Action = "waiting",
                            Progress = 0
                        };

                        // Add to listview
                        if (!lvMediaFiles.Items.Contains(mediaFile))
                        {
                            lvMediaFiles.Items.Add(mediaFile);
                        }
                    }

                    // Enable clear button if listview has items
                    btnClear.IsEnabled = (lvMediaFiles.Items.Count > 0) ? true : false;
                }
            }
        }

        // Remove selected file from listview
        private void BtnRemoveSelectedFile(object sender, RoutedEventArgs e)
        {
            // Only remove if selected item is valid file
            if (lvMediaFiles.SelectedItem is MediaFile mediaFile)
            {
                lvMediaFiles.Items.Remove(mediaFile);
                btnClear.IsEnabled = (lvMediaFiles.Items.Count > 0) ? true : false;
            }
        }

        // Clear entire listview
        private void BtnClearList(object sender, RoutedEventArgs e)
        {
            if (lvMediaFiles.Items.Count > 0)
            {
                lvMediaFiles.Items.Clear();
            }

            btnClear.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        // Convert files
        private async void BtnConvertFilesAsync(object sender, RoutedEventArgs e)
        {
            var outputFolderPath = txtOutputFolderPath.Text;
            var fileList = lvMediaFiles.Items.OfType<MediaFile>().ToList();

            // First check if output folder is set
            if (string.IsNullOrWhiteSpace(outputFolderPath) || !Directory.Exists(outputFolderPath))
            {
                MessageBox.Show("No output folder set, cannot convert!", "Missing output folder");
            }
            else
            {
                // Check count first
                if (fileList.Count > 0)
                {
                    // Disable controls while converting
                    btnClear.IsEnabled = false;
                    btnDelete.IsEnabled = false;
                    btnConvertFiles.IsEnabled = false;
                    btnAddFiles.IsEnabled = false;

                    var alertMessage = string.Empty;
                    var totalFailed = 0;

                    // Convert each file from listview
                    foreach (var file in fileList)
                    {
                        try
                        {
                            var outputFileName = string.Concat(Path.Combine(outputFolderPath, file.FileName), ".mp3");
                            var mediaInfo = await MediaInfo.Get(file.FilePath);
                            var audioStream = mediaInfo.AudioStreams.First();

                            // Create new conversion object
                            var conversion = Conversion.New()
                                .AddStream(audioStream)
                                .SetOutput(outputFileName)
                                .SetOverwriteOutput(true)
                                .UseMultiThread(false)
                                .SetPreset(ConversionPreset.UltraFast);

                            // Set progress bar event handling
                            conversion.OnProgress += (s, args) =>
                            {
                                file.Progress = args.Percent;
                                RefreshListView();
                            };

                            // Change state from current file
                            file.Action = "Converting...";
                            file.Progress = 0;

                            // Update listview
                            RefreshListView();

                            //Start conversion
                            await conversion.Start();

                            // Set to completed when done converting
                            file.Action = "Completed";
                            file.Progress = 100;
                        }
                        catch (Exception)
                        {
                            file.Progress = 0;
                            file.Action = "failed";
                            totalFailed++;
                        }

                        // Update listview again
                        RefreshListView();
                    }

                    // Set extra msg if one or more files failed to convert
                    if (totalFailed > 0)
                    {
                        alertMessage = $"\n{totalFailed} {(totalFailed == 1 ? "file" : "files")} failed to convert, it is possible that the file is either corrupt or too large.";
                    }

                    // Confirm message
                    MessageBox.Show(string.Concat("Converting completed.", alertMessage), "Done", MessageBoxButtons.OK);

                    // Enable controls again
                    btnClear.IsEnabled = true;
                    btnConvertFiles.IsEnabled = true;
                    btnAddFiles.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("No files found to convert, converting aborted!", "No files found");
                }
            }
        }

        // Refresh listview when items are been updated
        private void RefreshListView()
        {
            Dispatcher.Invoke(() => lvMediaFiles.Items.Refresh());
        }

        // Verify if FFmpeg is installed
        // Also verify if using the latest version
        private async Task VerifyFFmpeg()
        {
            cpUpdater.Visibility = Visibility.Visible;

            try
            {
                // Try install or update FFmpeg
                FFmpeg.ExecutablesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpeg");
                await FFmpeg.GetLatestVersion(FFmpegVersion.Official);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error happend while trying to verify FFmpeg.\n{ex.Message}", "Something went wrong!", MessageBoxButtons.OK);
            }

            // Fade out effect after install/update completion
            cpUpdater.ContentTemplate = TryFindResource("FFmpegUpdaterCompleted") as DataTemplate;
            cpUpdater.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(2)));

            // Wait few seconds before hiding panel
            await Task.Run(() => Thread.Sleep(2000));

            // Hide panel so controls become available
            cpUpdater.Visibility = Visibility.Collapsed;
        }
    }
}
