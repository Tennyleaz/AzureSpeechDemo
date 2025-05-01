using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Google.Api.Gax;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Speech.V2;
using Google.Cloud.Storage.V1;
using Google.LongRunning;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Win32;

namespace GoogleSpeechDemoApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private readonly string _apiKey;
        private const string PROJECT_ID = "voltaic-signal-457006-p5";  // id of "My First Project"
        private const string BUCKET_NAME = "stt-demo-app";  // GCS bucket name
        private string LOCATION = "global";
        private SpeechClient speechClient;
        private GoogleCredential credential;
        private StorageClient storageClient;
        private List<SttLanguage> sttLanguages;
        private CancellationTokenSource cts;

        public MainWindow()
        {
            InitializeComponent();
            IsEnabled = false;
            if (File.Exists("service-key.json"))
            {
                //_apiKey = File.ReadAllText("google-key.txt").Trim();
            }
            else
            {
                MessageBox.Show(this, "Cannot load access token from service-key.json!");
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                credential = GoogleCredential.FromFile("service-key.json").
                    CreateScoped("https://www.googleapis.com/auth/cloud-platform");
                storageClient = StorageClient.Create(credential);
                //CreateSpeechClient();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message);
                return;
            }

            // load json from resource
            cbRegion.SelectedIndex = 0;
            IsEnabled = true;
        }

        private void CreateSpeechClient()
        {
            SpeechClientBuilder builder = new SpeechClientBuilder
            {
                Credential = credential,
            };
            if (LOCATION != "global")
                builder.Endpoint = $"{LOCATION}-speech.googleapis.com";
            speechClient = builder.Build();
        }

        private RecognitionConfig CreateConfig()
        {
            RecognitionConfig confing = new RecognitionConfig
            {
                AutoDecodingConfig = new AutoDetectDecodingConfig(),
                LanguageCodes = { SelectedLanguage.LanguageCode },
                Model = SelectedModel.ModelName,
                Features = new RecognitionFeatures
                {
                    EnableAutomaticPunctuation = SelectedModel.AutoPunctuation,
                }
            };
            if (chkSpeaker.IsChecked == true && cbSpeakerCount.SelectedIndex > 1)
            {
                confing.Features.DiarizationConfig = new SpeakerDiarizationConfig
                {
                    MaxSpeakerCount = cbSpeakerCount.SelectedIndex + 1
                };
            }
            return confing;
        }

        private async Task<string> CreateRecognizer()
        {
            // Return default if region is global
            if (LOCATION == "global")
            {
                WriteLog("Using default recognizer.");
                return $"projects/{PROJECT_ID}/locations/{LOCATION}/recognizers/_";
            }

            string recognizerId = $"stt-demo-{SelectedLanguage.LanguageCode.ToLower()}-{SelectedModel.ToString().ToLower()}";
            string recognizerName = $"My recognizer {SelectedLanguage.LanguageCode} ({SelectedModel})";
            // Get existing recognizer
            try
            {
                string name = $"projects/{PROJECT_ID}/locations/{LOCATION}/recognizers/{recognizerId}";
                Recognizer recognizer = await speechClient.GetRecognizerAsync(name);
                WriteLog($"Found recognizer: {recognizer.DisplayName}");
                return recognizer.Name;
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }

            try
            {
                // create one if not exists
                CreateRecognizerRequest createRequest = new CreateRecognizerRequest
                {
                    Recognizer = new Recognizer
                    {
                        DisplayName = recognizerName,
                        DefaultRecognitionConfig = CreateConfig()
                    },
                    RecognizerId = recognizerId,
                    ParentAsLocationName = new LocationName(PROJECT_ID, LOCATION)
                };
                var createResult = await speechClient.CreateRecognizerAsync(createRequest);
                if (createResult.IsCompleted)
                {
                    WriteLog($"Created recognizer: {createResult.Result.DisplayName}");
                    return createResult.Result.Name;
                }
                WriteLog("Cannot create recognizer!");
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }
            return null;
        }

        private async Task Recognize(string fileName, string recognizerId)
        {
            await using FileStream fs = new FileStream(fileName, FileMode.Open);
            RecognizeRequest request = new RecognizeRequest
            {
                Config = CreateConfig(),
                Content = await ByteString.FromStreamAsync(fs),
                Recognizer = recognizerId
            };
            RecognizeResponse response = await speechClient.RecognizeAsync(request);
            tbText.Text = "";
            if (response.Results != null)
            {
                foreach (var result in response.Results)
                {
                    if (result.Alternatives.Count > 0)
                        tbText.Text += result.Alternatives[0].Transcript + "\n";
                }

                WriteLog($"Total cost seconds: {response.Metadata.TotalBilledDuration}");
            }
        }

        private async Task<BatchRecognizeResults> BatchRecognize(string fileName, string recognizerId)
        {
            Google.Apis.Storage.v1.Data.Object obj = await UploadGoogleStorage(fileName);
            string gcsUri = $"gs://{obj.Bucket}/{obj.Name}";
            // Initialize request argument(s)
            BatchRecognizeRequest request = new BatchRecognizeRequest
            {
                Recognizer = recognizerId,
                Files =
                {
                    new BatchRecognizeFileMetadata
                    {
                        Uri = gcsUri,
                    }
                },
                Config = CreateConfig(),
                ConfigMask = new FieldMask(),
                RecognitionOutputConfig = new RecognitionOutputConfig()
                {
                    //GcsOutputConfig = new GcsOutputConfig  // use this option for multiple files, to GCS path
                    //{
                    //    Uri = $"gs://{obj.Bucket}/transcripts/",
                    //},
                    InlineResponseConfig = new InlineOutputConfig()  // use this option to output 1 file directly
                },
                ProcessingStrategy = BatchRecognizeRequest.Types.ProcessingStrategy.Unspecified,
            };
            // Make the request
            Operation<BatchRecognizeResponse, OperationMetadata> batchResult = await speechClient.BatchRecognizeAsync(request, cts.Token);
            // Poll until the returned long-running operation is complete
            WriteLog("Pulling for result, please wait...");
            Operation<BatchRecognizeResponse, OperationMetadata> completedResponse = await batchResult.PollUntilCompletedAsync();
            // Delete the file from GCS
            await DeleteGoogleStorage(obj);
            // Retrieve the operation result
            BatchRecognizeResponse result = completedResponse.Result;
            WriteLog("Done.");
            if (result.Results != null)
            {
                WriteLog("Billed duration: " + result.TotalBilledDuration + "\n");
                foreach (var file in result.Results.Values)
                {
                    if (file.Error != null )
                    {
                        WriteLog(file.Error.ToString());
                        return null;
                    }
                    //Console.WriteLine(file.CloudStorageResult);
                    //tbRecognizing.Text += file.CloudStorageResult.Uri + "\n";
                    return file.InlineResult.Transcript;
                    //return file.CloudStorageResult.Uri;
                }
            }
            WriteLog("No result!");
            return null;
        }

        private async Task<Google.Apis.Storage.v1.Data.Object> UploadGoogleStorage(string fileName)
        {

            //Bucket bucket = await storageClient.GetBucketAsync(BUCKET_NAME);
            //FileInfo fileInfo = new FileInfo(fileName);
            //string contentType;
            //if (fileInfo.Extension == ".wav")
            //    contentType = "audio/wav";
            //else if (fileInfo.Extension == ".mp3")
            //    contentType = "audio/mpeg";
            //else if (fileInfo.Extension == ".aac")
            //    contentType = "audio/aac";
            //else
            //    throw new Exception("Unsupported file type");

            WriteLog("Conver with ffmpeg...");
            string outFileName = FfmpegConverter.ConvertWavFormat(fileName);
            string contentType = "audio/wav";
            FileInfo fileInfo = new FileInfo(outFileName);

            WriteLog("Uploading audio file to GCS...");
            await using FileStream fs = new FileStream(outFileName, FileMode.Open);
            var gcsObject = await storageClient.UploadObjectAsync(BUCKET_NAME, $"audio-files/{fileInfo.Name}", contentType, fs);
            return gcsObject;
        }

        private async Task DeleteGoogleStorage(Google.Apis.Storage.v1.Data.Object obj)
        {
            try
            {
                await storageClient.DeleteObjectAsync(obj);
                WriteLog("Deleted file from GCS.");
            }
            catch (Exception ex)
            {
                WriteLog("Error deleting file: " + ex.Message);
            }
        }

        /*private async Task FetchGoogleStorage(string fileUri)
        {
            string[] parts = fileUri.Split('/');
            string fileName = parts[parts.Length - 1];
            string bucketName = parts[2];
            string objectName = $"transcripts/{fileName}";
            using MemoryStream stream = new MemoryStream();
            await storageClient.DownloadObjectAsync(bucketName, objectName, stream);
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            //tbText.Text += "\nText:\n" + json;
            BatchResult speechResult = JsonSerializer.Deserialize<BatchResult>(json);
            Duration startTime = new Duration();
            foreach (Result result in speechResult.results)
            {
                Duration endTime = startTime + result.resultEndOffset;
                tbText.Text += $"[{startTime}-{endTime}]\n{result.alternatives[0].transcript}\n";
                startTime = endTime;
            }
        }*/

        private void PrintResults(BatchRecognizeResults transcripts)
        {
            tbText.Text = "";
            TimeSpan startTime = TimeSpan.Zero;
            List<SpeechRecognitionResult> results = transcripts.Results.Where(x => x.Alternatives.Count > 0).ToList();
            results.Sort(CompareResult);
            foreach (SpeechRecognitionResult result in results)
            {
                //if (result.Alternatives.Count > 0)
                {
                    TimeSpan endTime = startTime + result.ResultEndOffset.ToTimeSpan();
                    tbText.Text += $"[{startTime.ToString(@"mm\:ss")}-{endTime.ToString(@"mm\:ss")}]\n{result.Alternatives[0].Transcript}\n";
                    startTime = endTime;
                }
            }
        }

        private static int CompareResult(SpeechRecognitionResult x, SpeechRecognitionResult y)
        {
            int xEnd = x.ResultEndOffset.ToTimeSpan().Milliseconds;
            int yEnd = y.ResultEndOffset.ToTimeSpan().Milliseconds;
            return xEnd.CompareTo(yEnd);
        }

        internal SttLanguage SelectedLanguage
        {
            get
            {
                if (cbLocale.SelectedIndex >= 0)
                    return cbLocale.SelectedItem as SttLanguage;
                return null;
            }
        }

        internal SttModel SelectedModel
        {
            get
            {
                if (cbModel.SelectedIndex >= 0)
                    return cbModel.SelectedItem as SttModel;
                return null;
            }
        }

        private void CbLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLocale.SelectedIndex >= 0)
            {
                cbModel.ItemsSource = SelectedLanguage.Models;
                cbModel.SelectedIndex = 0;
            }
            else
            {
                cbModel.ItemsSource = null;
                cbModel.SelectedIndex = -1;
            }
        }

        private void CbModel_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedModel != null)
            {
                chkSpeaker.IsChecked = SelectedModel.Diarization;
            }
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "WAV files (*.wav)|*.wav| MP3 files (*.mp3)|*.mp3| AAC files (*.aac)|*.aac";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            dialog.Title = "Select an audio file";
            if (dialog.ShowDialog() != true)
                return;

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            progress.Visibility = Visibility.Visible;
            WriteLog("Batch Processing...");
            cts = new CancellationTokenSource();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                string recognizerId = await CreateRecognizer();
                BatchRecognizeResults results = await BatchRecognize(dialog.FileName, recognizerId);
                if (results != null)
                    PrintResults(results);
                // fetch file from GCS
                //await FetchGoogleStorage(fileUri);
            }
            catch (Exception ex)
            {
                tbText.Text = ex.ToString();
            }

            stopwatch.Stop();
            timespan.Content = $"{stopwatch.Elapsed.TotalSeconds} s";
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            progress.Visibility = Visibility.Collapsed;
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetText(tbText.Text);
        }

        private void CbRegion_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLocale == null) 
                return;
            string resourceName;
            int index;
            if (cbRegion.SelectedIndex == 1)
            {
                resourceName = "google stt sea.json";
                index = 21;  // zh-tw
                LOCATION = "asia-southeast1";
            }
            else
            {
                resourceName = "google stt global.json";
                index = 31;  // en-us
                LOCATION = "global";
            }
            CreateSpeechClient();
            sttLanguages = LoadLanguages(resourceName);
            cbLocale.ItemsSource = sttLanguages;
            cbLocale.SelectedIndex = index;
        }

        private List<SttLanguage> LoadLanguages(string resourceName)
        {
            // load json from resource
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream($"GoogleSpeechDemoApp.{resourceName}");
            using StreamReader reader = new StreamReader(stream);
            string jsonString = reader.ReadToEnd();
            List<SttModelRaw> rawModels = JsonSerializer.Deserialize<List<SttModelRaw>>(jsonString);
            // convert to 2d list
            List<SttLanguage> stts = new List<SttLanguage>();
            foreach (SttModelRaw raw in rawModels)
            {
                SttLanguage sttLanguage = stts.Find(x => x.LanguageCode == raw.LanguageCode);
                if (sttLanguage == null)
                {
                    sttLanguage = new SttLanguage
                    {
                        Name = raw.Name,
                        LanguageCode = raw.LanguageCode,
                        Models = new List<SttModel>()
                    };
                    stts.Add(sttLanguage);
                }

                sttLanguage.Models.Add(new SttModel
                {
                    ModelName = raw.Model,
                    AutoPunctuation = raw.AutoPunctuation,
                    Diarization = raw.Diarization,
                    ModelAdaptation = raw.ModelAdaptation,
                    WordLevelConfidence = raw.WordLevelConfidence,
                    ProfanityFilter = raw.ProfanityFilter,
                    SpokenPunctuation = raw.SpokenPunctuation,
                    SpokenEmojis = raw.SpokenEmojis
                });
            }
            return stts;
        }

        private void WriteLog(string message)
        {
            tbRecognizing.Text += "\n" + message;
            tbRecognizing.ScrollToEnd();
        }
    }
}