using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

namespace WhisperAiDemoApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly GgmlType[] AllModels = new GgmlType[]
        {
            GgmlType.Tiny, GgmlType.Base, GgmlType.Small, GgmlType.Medium, GgmlType.LargeV1, GgmlType.LargeV2, GgmlType.LargeV3Turbo
        };

        private string modelPath;
        private readonly List<GgmlType> availableModels = new List<GgmlType>();
        private Stopwatch stopwatch;

        public MainWindow()
        {
            InitializeComponent();
            cbLocale.ItemsSource = AiLanguages;
            cbLocale.SelectedIndex = 18;
            cbModel.ItemsSource = AllModels;
            cbModel.SelectedIndex = 2;

            string assemblyFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            modelPath = System.IO.Path.Combine(assemblyFolder, "model");
            Directory.CreateDirectory(modelPath);
            tbGGMLModel.Text = modelPath;

            FindModelFiles();
        }

        private void BtnSelectModel_OnClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = modelPath;
                dialog.Description = "Select your GGML directory:";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                {
                    tbGGMLModel.Text = modelPath = dialog.SelectedPath;
                    FindModelFiles();
                }
            }
        }

        private async Task DownloadModel(string fileName, GgmlType ggmlType)
        {
            Console.WriteLine($"Downloading Model {fileName}");
            string fullPath = System.IO.Path.Combine(modelPath, fileName);
            if (File.Exists(fullPath))
            {
                var result = MessageBox.Show($"Model {fileName} already exist in:\n{fullPath}\nDo you want to download again?", "Download", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
            using var fileWriter = File.OpenWrite(fullPath);
            await modelStream.CopyToAsync(fileWriter);
            FindModelFiles();
        }

        private string SelectedModelFile
        {
            get
            {
                if (cbSpeechModel.SelectedIndex < 0)
                    return null;
                string fileName = cbSpeechModel.SelectedItem.ToString() + ".ggml";
                return System.IO.Path.Combine(modelPath, fileName);
            }
        }

        private async void BtnDownloadModel_OnClick(object sender, RoutedEventArgs e)
        {
            setupBox.IsEnabled = false;
            string fileName = $"{cbModel.SelectedItem}.ggml";
            try
            {
                await DownloadModel(fileName, (GgmlType)cbModel.SelectedItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            setupBox.IsEnabled = true;
        }

        private void FindModelFiles()
        {
            string[] files = Directory.GetFiles(modelPath, "*.ggml");
            availableModels.Clear();
            foreach (GgmlType ggmlType in AllModels)
            {
                string fileName = $"{ggmlType}.ggml";
                if (files.Contains(System.IO.Path.Combine(modelPath, fileName)))
                {
                    availableModels.Add(ggmlType);
                }
            }
            lbFoundModels.Content = string.Join(", ", availableModels);
            cbSpeechModel.ItemsSource = availableModels;
            if (availableModels.Count > 0)
                cbSpeechModel.SelectedIndex = 0;
        }

        private void CbSpeechModel_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(SelectedModelFile))
            {
                MessageBox.Show("Selected model not exist!\n" + SelectedModelFile);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "WAV files (*.wav)|*.wav";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            dialog.Title = "Select a .WAV file";
            if (dialog.ShowDialog() != true)
                return;

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            tbText.Text = "";
            tbRecognizing.Text = "";

            try
            {
                // Optional logging from the native library
                using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

                // This section creates the whisperFactory object which is used to create the processor object.
                using var whisperFactory = WhisperFactory.FromPath(SelectedModelFile);

                // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
                string locale = cbLocale.SelectedItem.ToString();
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage(locale)
                    .Build();

                tbDebug.Text = "Loaded " + dialog.FileName;
                using var fileStream = File.OpenRead(dialog.FileName);

                // This section processes the audio file and prints the results (start time, end time and text) to the console.
                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
                    tbText.Text += $"{result.Start}->{result.End}: {result.Text}\n";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                tbRecognizing.Text = ex.ToString();
            }
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void CbLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        internal static readonly string[] AiLanguages =
        [
            "af",
            "am",
            "ar",
            "as",
            "az",
            "ba",
            "be",
            "bg",
            "bn",
            "bo",
            "br",
            "bs",
            "ca",
            "cs",
            "cy",
            "da",
            "de",
            "el",
            "en",
            "es",
            "et",
            "eu",
            "fa",
            "fi",
            "fo",
            "fr",
            "gl",
            "gu",
            "ha",
            "haw",
            "he",
            "hi",
            "hr",
            "ht",
            "hu",
            "hy",
            "id",
            "is",
            "it",
            "ja",
            "jw",
            "ka",
            "kk",
            "km",
            "kn",
            "ko",
            "la",
            "lb",
            "ln",
            "lo",
            "lt",
            "lv",
            "mg",
            "mi",
            "mk",
            "ml",
            "mn",
            "mr",
            "ms",
            "mt",
            "my",
            "ne",
            "nl",
            "nn",
            "no",
            "oc",
            "pa",
            "pl",
            "ps",
            "pt",
            "ro",
            "ru",
            "sa",
            "sd",
            "si",
            "sk",
            "sl",
            "sn",
            "so",
            "sq",
            "sr",
            "su",
            "sv",
            "sw",
            "ta",
            "te",
            "tg",
            "th",
            "tk",
            "tl",
            "tr",
            "tt",
            "uk",
            "ur",
            "uz",
            "vi",
            "yi",
            "yo",
            "yue",
            "zh"
        ];
    }
}
