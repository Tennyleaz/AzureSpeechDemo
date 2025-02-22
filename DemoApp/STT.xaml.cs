using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.PronunciationAssessment;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DemoApp
{
    /// <summary>
    /// STT.xaml 的互動邏輯
    /// </summary>
    public partial class STT : Window
    {
        private readonly SpeechConfig speechConfig;
        private SpeechRecognizer recognizer;
        private Stopwatch stopwatch;
        private List<PronunciationAssessmentWordResult> recognizedWords;
        private int score;

        public STT()
        {
            InitializeComponent();
            speechConfig = SpeechConfig.FromSubscription(Configs.speechKey, Configs.speechRegion);
            Configs.pronunciationLanguages.Sort();
            cbLocale.ItemsSource = Configs.pronunciationLanguages;
            cbLocale.SelectedItem = "en-US";  // en-US
        }

        private string Locale
        {
            get
            {
                if (cbLocale == null || cbLocale.SelectedIndex < 0)
                    return null;
                return Configs.pronunciationLanguages[cbLocale.SelectedIndex];
            }
        }

        private bool IsPhoneme
        {
            get
            {
                if (checkPhoneme == null)
                    return false;
                return checkPhoneme.IsChecked == true;
            }
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(tbTimeoutMs.Text, out int timeout) || timeout < 50)
            {
                MessageBox.Show("Invalid timeout number!");
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
            scrollViewer.Visibility = Visibility.Collapsed;
            tbText.Text = "";
            tbRecognizing.Text = "";
            recognizedWords = new List<PronunciationAssessmentWordResult>();

            speechConfig.SpeechRecognitionLanguage = Locale;
            speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, timeout.ToString());

            using (AudioConfig audioConfig = AudioConfig.FromWavFileInput(dialog.FileName))
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
                timespan.Content = "0 s";
                grades.Content = "0/100";

                tbDebug.Text = "Loaded " + dialog.FileName;
                recognizer = new SpeechRecognizer(speechConfig, audioConfig);

                if (IsPhoneme)
                {
                    var pronunciationAssessmentConfig = new PronunciationAssessmentConfig(
                        referenceText: "",
                        gradingSystem: GradingSystem.HundredMark,
                        granularity: Granularity.Phoneme,
                        enableMiscue: false);
                    if (Locale == "en-US")
                    {
                        // only available in the en-US locale
                        pronunciationAssessmentConfig.EnableProsodyAssessment();
                        // only available in the en-US locale
                        pronunciationAssessmentConfig.EnableContentAssessmentWithTopic("Chat");
                    }
                    pronunciationAssessmentConfig.PhonemeAlphabet = Locale == "zh-CN" ? "SAPI" : "IPA";
                    pronunciationAssessmentConfig.ApplyTo(recognizer);
                }

                recognizer.Recognizing += SpeechRecognizer_Recognizing;
                recognizer.Recognized += Recognizer_Recognized;
                recognizer.Canceled += Recognizer_Canceled;
                recognizer.SessionStopped += Recognizer_SessionStopped;
                //var speechRecognitionResult = await recognizer.RecognizeOnceAsync();  // 15s only
                await recognizer.StartContinuousRecognitionAsync();
            }
        }

        private void Recognizer_SessionStopped(object sender, SessionEventArgs e)
        {
            stopwatch.Stop();
            
            // show final result
            RunOnUi(FinalUpdate);
            RunOnUi(EnableUi);
            return;

            void FinalUpdate()
            {
                timespan.Content = $"{stopwatch.ElapsedMilliseconds / 1000} s";

                // Syllable only support english
                if (IsPhoneme && (Locale == "en-US" || Locale == "zh-CN"))
                {
                    //tbText.Visibility = Visibility.Collapsed;
                    scrollViewer.Visibility = Visibility.Visible;
                    wrapPanel.Children.Clear();
                    if (Locale == "zh-CN")
                    {
                        foreach (var word in recognizedWords)
                        {
                            string speakedWord = string.Join("·", word.Phonemes.Select(x => x.Phoneme));
                            SyllableTextItem item = new SyllableTextItem(word.Word, $"/{speakedWord}/");
                            wrapPanel.Children.Add(item);
                        }
                    }
                    else
                    {
                        // English has Syllable group
                        foreach (var word in recognizedWords)
                        {
                            string speakedWord = null;
                            if (word.Syllables != null)
                                speakedWord = string.Join("·", word.Syllables.Select(x => x.Syllable));
                            else
                                speakedWord = string.Join("", word.Phonemes.Select(x => x.Phoneme));
                            SyllableTextItem item = new SyllableTextItem(word.Word, $"/{speakedWord}/");
                            wrapPanel.Children.Add(item);
                        }
                    }
                }
                if (checkGrade.IsChecked == true)
                {
                    grades.Content = $"{score}/100";
                }
            }
        }

        private void Recognizer_Canceled(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            void updateText()
            {

            }
        }

        private async void Recognizer_Recognized(object sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var pronunciationAssessmentResult = PronunciationAssessmentResult.FromResult(e.Result);
                if (pronunciationAssessmentResult?.Words != null)
                {
                    recognizedWords.AddRange(pronunciationAssessmentResult.Words);
                }

                if (pronunciationAssessmentResult != null)
                    score = (int)pronunciationAssessmentResult.AccuracyScore;

                RunOnUi(FinalTextResult);
            }
            else
            {
                Console.WriteLine("Recognized error: " + e.Result.Reason);
            }

            //if (string.IsNullOrEmpty(e.Result.Text))
            //{
            //    // we stop recognize here
            //    await recognizer.StopContinuousRecognitionAsync();
            //}

            return;

            void FinalTextResult()
            {
                Console.WriteLine("Recognized: " + e.Result.Text);
                tbText.Text += e.Result.Text + "\n";
            }

            
        }

        private void SpeechRecognizer_Recognizing(object sender, SpeechRecognitionEventArgs e)
        {
            RunOnUi(updateText);
            return;

            void updateText()
            {
                tbRecognizing.Text = e.Result.Text;
            }
        }

        private async void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            await recognizer.StopContinuousRecognitionAsync();
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess())
                action();
            else
                Dispatcher.Invoke(action);
        }

        private void EnableUi()
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
        }

        private void OutputSpeechRecognitionResult(SpeechRecognitionResult speechRecognitionResult)
        {
            switch (speechRecognitionResult.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    Debug.WriteLine($"RECOGNIZED: Text={speechRecognitionResult.Text}");
                    break;
                case ResultReason.NoMatch:
                    Debug.WriteLine($"NOMATCH: Speech could not be recognized.");
                    break;
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(speechRecognitionResult);
                    Debug.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Debug.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Debug.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Debug.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
            }

            RunOnUi(EnableUi);
            RunOnUi(updateResult);
            return;

            void updateResult()
            {
                stopwatch.Stop();
                timespan.Content = $"{stopwatch.ElapsedMilliseconds / 1000} s";
                tbDebug.Text = "Result: " + speechRecognitionResult.Reason;

                // The pronunciation assessment result as a Speech SDK object
                var pronunciationAssessmentResult = PronunciationAssessmentResult.FromResult(speechRecognitionResult);
                if (pronunciationAssessmentResult?.Words != null)
                {
                    // Syllable only support english
                    if (IsPhoneme && (Locale == "en-US" || Locale == "zh-CN"))
                    {
                        tbText.Visibility = Visibility.Collapsed;
                        scrollViewer.Visibility = Visibility.Visible;
                        wrapPanel.Children.Clear();
                        if (Locale == "zh-CN")
                        {
                            foreach (var word in pronunciationAssessmentResult.Words)
                            {
                                string speakedWord = string.Join("·", word.Phonemes.Select(x => x.Phoneme));
                                SyllableTextItem item = new SyllableTextItem(word.Word, $"/{speakedWord}/");
                                wrapPanel.Children.Add(item);
                            }
                        }
                        else
                        {
                            // English has Syllable group
                            foreach (var word in pronunciationAssessmentResult.Words)
                            {
                                string speakedWord = string.Join("·", word.Syllables.Select(x => x.Syllable));
                                SyllableTextItem item = new SyllableTextItem(word.Word, $"/{speakedWord}/");
                                wrapPanel.Children.Add(item);
                            }
                        }
                    }
                    if (checkGrade.IsChecked == true)
                    {
                        grades.Content = $"{pronunciationAssessmentResult.AccuracyScore}/100";
                    }
                }
                else
                {
                    tbText.Text = speechRecognitionResult.Text;
                    tbText.Visibility = Visibility.Visible;
                    scrollViewer.Visibility = Visibility.Collapsed;
                    grades.Content = "";
                }
            }
        }

        private void CbLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Locale == "en-US")
            {
                tbLocaleFeature.Text = "Locale features:\nPronunciation: \tO\nProsody: \tO\nContent: \tO\nPhoneme: \tIPA";
                checkPhoneme.IsEnabled = true;
                checkPhoneme.IsChecked = true;
            }
            else if (Locale == "zh-CN")
            {
                tbLocaleFeature.Text = "Locale features:\nPronunciation: \tO\nProsody: \tX\nContent: \tX\nPhoneme: \tSAPI";
                checkPhoneme.IsEnabled = true;
                checkPhoneme.IsChecked = true;
            }
            else
            {
                tbLocaleFeature.Text = "Locale features:\nPronunciation: \tO\nProsody: \tX\nContent: \tX\nPhoneme: \tX";
                checkPhoneme.IsEnabled = false;
                checkPhoneme.IsChecked = false;
            }
        }
    }
}
