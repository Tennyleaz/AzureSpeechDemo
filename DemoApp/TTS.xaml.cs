using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using System.Xml.Linq;

namespace DemoApp
{
    /// <summary>
    /// TTS.xaml 的互動邏輯
    /// </summary>
    public partial class TTS : Window
    {
        public TTS()
        {
            InitializeComponent();
            InitializeComponent();
            cbLocale.ItemsSource = Configs.AiLanguages;
            cbLocale.SelectedIndex = 1;
            cbLocale.SelectionChanged += CbLocale_SelectionChanged;
            cbVoice.ItemsSource = Voices;
        }

        private SpeechSynthesizer speechSynthesizer = null;
        private SpeechConfig speechConfig;
        private readonly ObservableCollection<string> Voices = new ObservableCollection<string>();

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //string locale = AiLanguages[cbLocale.SelectedIndex];
            speechConfig = SpeechConfig.FromSubscription(Configs.speechKey, Configs.speechRegion);
            string pitch = speechConfig.GetProperty(PropertyId.SpeechSynthesisRequest_Pitch);
            string rate = speechConfig.GetProperty(PropertyId.SpeechSynthesisRequest_Rate);
            await GetVoicesFromLocale();
            //speechConfig.SpeechSynthesisLanguage = "en-US";
            //using (speechSynthesizer = new SpeechSynthesizer(speechConfig))
            //{
            //    SynthesisVoicesResult result = await speechSynthesizer.GetVoicesAsync("en-US");
            //    Debug.WriteLine(result.Voices);
            //}
        }

        private async void CbLocale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await GetVoicesFromLocale();
        }

        private async Task GetVoicesFromLocale()
        {
            string locale = Configs.AiLanguages[cbLocale.SelectedIndex];
            speechConfig.SpeechSynthesisLanguage = locale;
            using (speechSynthesizer = new SpeechSynthesizer(speechConfig))
            {
                SynthesisVoicesResult result = await speechSynthesizer.GetVoicesAsync(locale);
                Voices.Clear();
                foreach (VoiceInfo v in result.Voices)
                {
                    Voices.Add(v.ShortName);
                }

                cbVoice.SelectedIndex = 0;
            }

            // try get sample text
            string fileName = locale + ".txt";
            if (File.Exists(fileName))
            {
                tbText.Text = File.ReadAllText(fileName);
            }
            else
            {
                tbText.Text = string.Empty;
            }
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbText.Text))
                return;
            btnStart.IsEnabled = false;
            btnCancel.IsEnabled = true;
            tbText.IsReadOnly = true;
            tbText.IsHitTestVisible = false;
            progress.Visibility = Visibility.Visible;

            // The neural multilingual voice can speak different languages based on the input text.
            string locale = Configs.AiLanguages[cbLocale.SelectedIndex];
            string voice = Voices[cbVoice.SelectedIndex];
            Debug.WriteLine($"locale={locale}, voice={voice}");
            // All neural voices are multilingual and fluent in their own language and English.
            speechConfig.SpeechSynthesisLanguage = locale;
            // SpeechSynthesisLanguage is ignored if name is set
            speechConfig.SpeechSynthesisVoiceName = voice;
            //speechConfig.SetProperty(PropertyId.SpeechSynthesisRequest_Rate, "0.65");
            using (speechSynthesizer = new SpeechSynthesizer(speechConfig))
            {
                string text = tbText.Text;
                string rate = (cbRate.SelectedItem as ComboBoxItem).Content.ToString();
                string volume = (cbVolume.SelectedItem as ComboBoxItem).Content.ToString();
                // create a xml document
                XNamespace ns = "https://www.w3.org/2001/10/synthesis"; // Default namespace
                XDocument document = new XDocument(
                    new XElement(ns + "speak",
                        new XAttribute("version", "1.0"),
                        new XAttribute(XNamespace.Xml + "lang", locale),
                        new XElement(ns + "voice",
                            new XAttribute("name", voice),
                            new XElement(ns + "prosody",
                                new XAttribute("rate", rate),
                                new XAttribute("volume", volume),
                                text
                            )
                        )
                    )
                );
                string ssml = document.ToString();
                Debug.WriteLine(ssml);

                speechSynthesizer.WordBoundary += SpeechSynthesizer_WordBoundary;
                //speechSynthesizer.SynthesisCompleted += SpeechSynthesizer_SynthesisCompleted;
                //speechSynthesizer.SynthesisCanceled += SpeechSynthesizer_SynthesisCanceled;
                //speechSynthesizer.Synthesizing += SpeechSynthesizer_Synthesizing;
                SpeechSynthesisResult speechSynthesisResult = await speechSynthesizer.SpeakSsmlAsync(ssml);
                OutputSpeechSynthesisResult(speechSynthesisResult, text);
            }
        }

        private void SpeechSynthesizer_SynthesisCanceled(object sender, SpeechSynthesisEventArgs e)
        {
            //RunOnUi(EnableUi);
        }

        private void SpeechSynthesizer_SynthesisCompleted(object sender, SpeechSynthesisEventArgs e)
        {
            //RunOnUi(EnableUi);
        }

        private void EnableUi()
        {
            progress.Visibility = Visibility.Collapsed;
            btnStart.IsEnabled = true;
            btnCancel.IsEnabled = false;
            tbText.IsReadOnly = false;
            tbText.IsHitTestVisible = true;
        }

        private void SpeechSynthesizer_WordBoundary(object sender, SpeechSynthesisWordBoundaryEventArgs e)
        {
            // this event fires at background thread
            Dispatcher.Invoke(() =>
            {
                tbText.Focus();
                tbText.SelectionStart = (int)e.TextOffset;
                tbText.SelectionLength = (int)e.WordLength;
            });
        }

        //private void SpeechSynthesizer_Synthesizing(object? sender, SpeechSynthesisEventArgs e)
        //{

        //}

        private void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
        {
            switch (speechSynthesisResult.Reason)
            {
                case ResultReason.SynthesizingAudioCompleted:
                    Debug.WriteLine($"Speech synthesized completed in {speechSynthesisResult.AudioDuration}");
                    break;
                case ResultReason.Canceled:
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    Debug.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Debug.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Debug.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        Debug.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
                default:
                    break;
            }
            speechSynthesisResult.Dispose();
            speechSynthesizer = null;

            EnableUi();
        }

        private async void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("BtnCancel_OnClick");
            if (speechSynthesizer != null)
            {
                await speechSynthesizer.StopSpeakingAsync();
                Debug.WriteLine("Stopped");
            }
            else
            {
                Debug.WriteLine("speechSynthesizer is null");
            }
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess())
                action();
            else
                Dispatcher.Invoke(action);
        }
    }
}
