using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;
using Microsoft.Win32;

namespace DemoApp
{
    /// <summary>
    /// Conversation.xaml 的互動邏輯
    /// </summary>
    public partial class Conversation : Window
    {
        private readonly SpeechConfig speechConfig;
        private ConversationTranscriber conversationTranscriber;
        internal ObservableCollection<ConversationItem> conversationItems { get; set; } = new ObservableCollection<ConversationItem>();
        private Stopwatch stopwatch;

        public Conversation()
        {
            InitializeComponent();
            speechConfig = SpeechConfig.FromSubscription(Configs.speechKey, Configs.speechRegion);
            Configs.conversationLanguages.Sort();
            cbLocale.ItemsSource = Configs.conversationLanguages;
            cbLocale.SelectedItem = "en-US";  // en-US
        }

        private string Locale
        {
            get
            {
                if (cbLocale == null || cbLocale.SelectedIndex < 0)
                    return null;
                return Configs.conversationLanguages[cbLocale.SelectedIndex];
            }
        }

        private void CbLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(tbTimeoutMs.Text, out int timeout) || timeout < 50)
            {
                MessageBox.Show("Invalid timeout number!");
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "WAV files (*.wav)|*.wav| MP3 files (*.mp3)|*.mp3| AAC files (*.aac)|*.aac";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            dialog.Title = "Select a .WAV file";
            if (dialog.ShowDialog() != true)
                return;

            string fileName = FfmpegConverter.ConvertWavFormat(dialog.FileName);
            if (fileName == null)
                return;

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            listbox.Visibility = Visibility.Visible;
            //tbText.Text = "";
            conversationItems.Clear();
            conversationItems.Add(new ConversationItem());
            listbox.ItemsSource = conversationItems;

            stopwatch = new Stopwatch();
            stopwatch.Start();
            timespan.Content = "0 s";

            speechConfig.SpeechRecognitionLanguage = Locale;
            speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, timeout.ToString());
            speechConfig.SetProperty(PropertyId.SpeechServiceResponse_DiarizeIntermediateResults, "true");

            using (AudioConfig audioConfig = AudioConfig.FromWavFileInput(fileName))
            {
                tbDebug.Text = "Loaded " + dialog.FileName;
                // Create a conversation transcriber using audio stream input
                conversationTranscriber = new ConversationTranscriber(speechConfig, audioConfig);

                conversationTranscriber.Transcribing += ConversationTranscriber_Transcribing;
                conversationTranscriber.Transcribed += ConversationTranscriber_Transcribed;
                conversationTranscriber.Canceled += ConversationTranscriber_Canceled;
                conversationTranscriber.SessionStopped += ConversationTranscriber_SessionStopped;
                await conversationTranscriber.StartTranscribingAsync();
            }
        }

        private void ConversationTranscriber_SessionStopped(object sender, SessionEventArgs e)
        {
            void UpdateText()
            {
                tbDebug.Text = "Session stopped.";
                stopwatch.Stop();
                timespan.Content = $"{stopwatch.ElapsedMilliseconds / 1000} s";

                // remove last empty item
                ConversationItem last = conversationItems[conversationItems.Count - 1];
                if (string.IsNullOrEmpty(last.Text))
                    conversationItems.RemoveAt(conversationItems.Count - 1);
            }
            RunOnUi(UpdateText);
            RunOnUi(EnableUi);
        }

        private void ConversationTranscriber_Canceled(object sender, ConversationTranscriptionCanceledEventArgs e)
        {
            void UpdateText()
            {
                tbDebug.Text = $"Cancelled, reason={e.Reason}, error={e.ErrorCode}\n";
                stopwatch.Stop();
                timespan.Content = $"{stopwatch.ElapsedMilliseconds / 1000} s";

                string msg = $"Cancelled, reason={e.Reason}, error={e.ErrorCode}\nDetail={e.ErrorDetails}";
                MessageBox.Show(msg, "Error");
            }
            RunOnUi(UpdateText);
            RunOnUi(EnableUi);
        }

        private void ConversationTranscriber_Transcribed(object sender, ConversationTranscriptionEventArgs e)
        {
            ConversationItem last = conversationItems[conversationItems.Count - 1];
            last.Text = e.Result.Text;
            last.SpeakerId = e.Result.SpeakerId;

            TimeSpan strartTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
            TimeSpan endTime = strartTime + e.Result.Duration;
            last.TimeText = $"{strartTime.ToString(@"mm\:ss")} - {endTime.ToString(@"mm\:ss")}";

            void UpdateText()
            {
                //tbDebug.Text = "Result: " + e.Result.Reason;
                //tbText.Text += $"[Speaker {e.Result.SpeakerId}] {e.Result.Text}\n";
                ConversationItem newItem = new ConversationItem();
                conversationItems.Add(newItem);
                listbox.ScrollIntoView(newItem);
            }
            RunOnUi(UpdateText);
        }

        private void ConversationTranscriber_Transcribing(object sender, ConversationTranscriptionEventArgs e)
        {
            ConversationItem last = conversationItems[conversationItems.Count - 1];
            last.Text = e.Result.Text;
            last.SpeakerId = e.Result.SpeakerId;
            void UpdateText()
            {
                //tbText.Text += $"[Speaker {e.Result.SpeakerId}] {e.Result.Text}\n";
            }
            //RunOnUi(UpdateText);
        }

        private async void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            await conversationTranscriber.StopTranscribingAsync();
            EnableUi();
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

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            string text = "";
            foreach (ConversationItem item in conversationItems)
            {
                text += item.ToString() + "\n";
            }
            Clipboard.Clear();
            Clipboard.SetText(text);
        }
    }

    internal class ConversationItem : INotifyPropertyChanged
    {
        private string _speakerId;
        private string _text;
        private string _timeText;

        public string SpeakerId
        {
            get => _speakerId;
            set
            {
                if (_speakerId != value)
                {
                    _speakerId = value;
                    OnPropertyChanged(nameof(SpeakerId));
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        public string TimeText
        {
            get => _timeText;
            set
            {
                if (_timeText != value)
                {
                    _timeText = value;
                    OnPropertyChanged(nameof(TimeText));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return $"{SpeakerId} [{TimeText}]\n{Text}";
        }
    }
}
