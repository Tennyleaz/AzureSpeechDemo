﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AssemblyAI;
using AssemblyAI.Transcripts;
using Microsoft.Win32;

namespace AssemblyAiDemoApp
{
    /// <summary>
    /// SpeechToText.xaml 的互動邏輯
    /// </summary>
    public partial class SpeechToText : Window
    {
        private AssemblyAIClient client;
        private readonly List<string> localesNano = new List<string>();
        private readonly List<string> localesBest;
        private CancellationTokenSource cts;
        internal ObservableCollection<ConversationItem> conversationItems { get; set; } = new ObservableCollection<ConversationItem>();
        private Stopwatch stopwatch;

        public string Locale
        {
            get
            {
                if (cbModel.SelectedIndex == 1)  // nano model
                {
                    return localesNano[cbLocale.SelectedIndex];
                }
                else  // best model
                {
                    return localesBest[cbLocale.SelectedIndex];
                }
            }
        }

        public SpeechToText()
        {
            InitializeComponent();

            var languageValues = Enum.GetValues(typeof(TranscriptLanguageCode));
            foreach (var lang in languageValues)
            {
                localesNano.Add(lang.ToString());
            }

            localesBest = new List<string>()
            {
                "En",
                "EnAu",
                "EnUk",
                "EnUs",
                "Es",
                "Fr",
                "De",
                "It",
                "Pt",
                "Nl",
                "Hi",
                "Ja",
                "Zh",
                "Fi",
                "Ko",
                "Pl",
                "Ru",
                "Tr",
                "Uk",
                "Vi"
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                client = new AssemblyAIClient(Keys.API_KEY);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                tbRecognizing.Text = ex.ToString();
            }

            cbLocale.ItemsSource = localesBest;
            cbLocale.SelectedIndex = 3;  // EnUs
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            //Uri uri;
            //try
            //{
            //    uri = new Uri(tbText.Text);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show("Audio URL error: " + ex.Message);
            //    return;
            //}

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "WAV files (*.wav)|*.wav| MP3 files (*.mp3)|*.mp3| AAC files (*.aac)|*.aac";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            dialog.Title = "Select an audio file";
            if (dialog.ShowDialog() != true)
                return;

            // Transcribe file at remote URL
            TranscriptLanguageCode transcriptLanguageCode = (TranscriptLanguageCode)Enum.Parse(typeof(TranscriptLanguageCode), Locale, true);
            TranscriptOptionalParams tp = new TranscriptOptionalParams
            {
                //AudioUrl = uri.ToString(),
                LanguageCode = transcriptLanguageCode,
                LanguageDetection = chkAutoDetect.IsChecked,
                SpeechModel = cbModel.SelectedIndex == 1 ? SpeechModel.Nano : SpeechModel.Best,
                SpeakerLabels = chkSpeaker.IsChecked,
                FormatText = true,
                AutoChapters = chkAutoChapter.IsChecked,
                Summarization = chkSummary.IsChecked,
                Punctuate = true,
                //Disfluencies = true
            };

            if (chkSpeaker.IsChecked == true && cbSpeakerCount.SelectedIndex > 0)
            {
                tp.SpeakersExpected = cbSpeakerCount.SelectedIndex + 1;
            }

            if (chkSummary.IsChecked == true)
            {
                tp.SummaryType = SummaryType.Bullets;
                tp.SummaryModel = SummaryModel.Informative;
            }

            FileInfo fileInfo = new FileInfo(dialog.FileName);
            cts = new CancellationTokenSource();
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            Cursor = Cursors.Wait;
            tbText.Text = "";
            conversationItems.Clear();
            listbox.ItemsSource = conversationItems;
            listbox.Visibility = Visibility.Collapsed;
            progress.Visibility = Visibility.Visible;
            tbRecognizing.Text = "Recognizing...";
            timespan.Content = "0 s";

            try
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
                var transcript = await client.Transcripts.TranscribeAsync(fileInfo, tp, null, cts.Token);

                // checks if transcript.Status == TranscriptStatus.Completed, throws an exception if not
                transcript.EnsureStatusCompleted();
                ShowResult(transcript);
                // delete it on server
                //transcript = await client.Transcripts.DeleteAsync(transcript.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Transcribe error: " + ex.Message);
                tbRecognizing.Text = ex.ToString();
            }

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            Cursor = Cursors.Arrow;
            progress.Visibility = Visibility.Collapsed;
            cts.Dispose();
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            stopwatch?.Stop();
            //btnStart.IsEnabled = true;
            //btnStop.IsEnabled = false;
            //Cursor = Cursors.Arrow;
        }

        private void ShowResult(Transcript transcript)
        {
            stopwatch.Stop();
            timespan.Content = $"{stopwatch.Elapsed.TotalSeconds} s";

            if (transcript.Utterances != null && transcript.Utterances.Any())
            {
                foreach (TranscriptUtterance tu in transcript.Utterances)
                {
                    TimeSpan start = TimeSpan.FromMilliseconds(tu.Start);
                    TimeSpan end = TimeSpan.FromMilliseconds(tu.End);
                    string timeText = $"[{start.ToString(@"mm\:ss")}-{end.ToString(@"mm\:ss")}]";
                    tbText.Text += timeText + tu.Text + "\n";

                    ConversationItem newItem = new ConversationItem();
                    newItem.Text = tu.Text;
                    newItem.SpeakerId = tu.Speaker;
                    newItem.TimeText = timeText;
                    newItem.Start = tu.Start;
                    newItem.End = tu.End;
                    conversationItems.Add(newItem);
                }
                if (transcript.Chapters != null && transcript.Chapters.Any())
                {
                    int cIndex = 0;
                    int tIndex = 0;
                    foreach (Chapter chapter in transcript.Chapters)
                    {
                        cIndex++;
                        for (; tIndex < conversationItems.Count; tIndex++)
                        {
                            if (conversationItems[tIndex].Start >= chapter.Start)
                            {
                                break;
                            }
                        }

                        TimeSpan start = TimeSpan.FromMilliseconds(chapter.Start);
                        TimeSpan end = TimeSpan.FromMilliseconds(chapter.End);
                        string timeText = $"[{start.ToString(@"mm\:ss")}-{end.ToString(@"mm\:ss")}]";

                        ConversationItem newItem = new ConversationItem();
                        newItem.Text = chapter.Headline;
                        newItem.SpeakerId = $"Chapter {cIndex}";
                        newItem.TimeText = timeText;
                        newItem.Start = chapter.Start;
                        newItem.End = chapter.End;
                        conversationItems.Insert(tIndex, newItem);
                        if (tIndex < conversationItems.Count - 1)
                            tIndex++;
                    }
                }
                if (!string.IsNullOrEmpty(transcript.Summary))
                {
                    ConversationItem summaryItem = new ConversationItem();
                    summaryItem.Text = transcript.Summary;
                    summaryItem.SpeakerId = "Summary";
                    conversationItems.Insert(0, summaryItem);
                }
                listbox.Visibility = Visibility.Visible;
            }
            else
            {
                if (!string.IsNullOrEmpty(transcript.Summary))
                {
                    tbText.Text += $"[Summary]\n{transcript.Summary}\n\n";
                }
                if (transcript.Chapters != null && transcript.Chapters.Any())
                {
                    int index = 0;
                    foreach (Chapter chapter in transcript.Chapters)
                    {
                        index++;
                        tbText.Text += $"[Chapter {index}]\n{chapter.Headline}\n";
                    }
                }
                tbText.Text += transcript.Text;
            }

            //btnStart.IsEnabled = true;
            //btnStop.IsEnabled = false;
            //Cursor = Cursors.Arrow;
            tbRecognizing.Text = "Done.";
        }

        private void CbLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // disable speaker id for some languages in nano
            bool isSupported = false;
            if (cbModel.SelectedIndex == 1)
            {
                isSupported = localesBest.Contains(Locale);
            }
            else
            {
                isSupported = true;  // best model always support speaker id
            }
            chkSpeaker.IsEnabled = isSupported;
            if (!isSupported)
                chkSpeaker.IsChecked = false;
        }

        private void CbModel_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModel.SelectedIndex == 1)
            {
                cbLocale.ItemsSource = localesNano;
            }
            else
            {
                cbLocale.ItemsSource = localesBest;
                cbLocale.SelectedIndex = 3;
            }
        }

        private void Speaker_OnChecked(object sender, RoutedEventArgs e)
        {
            if (cbSpeakerCount.SelectedIndex == 0)
                cbSpeakerCount.SelectedIndex = 1;
        }

        private void Speaker_OnUnChecked(object sender, RoutedEventArgs e)
        {
            cbSpeakerCount.SelectedIndex = 0;
        }

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            string text = "";
            if (conversationItems.Count > 0)
            {
                foreach (ConversationItem item in conversationItems)
                {
                    text += item.ToString() + "\n";
                }
            }
            else
            {
                text = tbText.Text;
            }
            Clipboard.Clear();
            Clipboard.SetText(text);
        }

        private void ChkAutoChapter_OnChecked(object sender, RoutedEventArgs e)
        {
            chkSummary.IsChecked = false;
        }

        private void ChkSummary_OnChecked(object sender, RoutedEventArgs e)
        {
            chkAutoChapter.IsChecked = false;
        }
    }

    internal class ConversationItem : INotifyPropertyChanged
    {
        private string _speakerId;
        private string _text;
        private string _timeText;

        public int Start, End;

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
