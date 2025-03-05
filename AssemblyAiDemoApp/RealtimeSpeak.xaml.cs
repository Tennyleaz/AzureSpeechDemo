using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AssemblyAI;
using AssemblyAI.Realtime;
using AssemblyAI.Transcripts;

namespace AssemblyAiDemoApp
{
    /// <summary>
    /// RealtimeSpeak.xaml 的互動邏輯
    /// </summary>
    public partial class RealtimeSpeak : Window
    {
        private CancellationTokenSource cts;
        private RealtimeTranscriber transcriber;
        private const int sampleRate = 16_000;

        public RealtimeSpeak()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // check if SoX is installed
            try
            {
                Process.Start("sox", "--version").WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("SoX is required to run this demo.\nPlease install it from https://sourceforge.net/projects/sox/files/latest");
                tbRecognizing.Text = ex.ToString();
                btnStart.IsEnabled = false;
                Process.Start("https://sourceforge.net/projects/sox/files/latest/");
                return;
            }

            try
            {
                transcriber = new RealtimeTranscriber()
                {
                    ApiKey = Keys.API_KEY,
                    SampleRate = sampleRate
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                tbRecognizing.Text = ex.ToString();
                return;
            }

            transcriber.SessionBegins.Subscribe(OnSessionBegin);
            transcriber.PartialTranscriptReceived.Subscribe(OnPartialReceived);
            transcriber.FinalTranscriptReceived.Subscribe(OnFinalReceived);
            transcriber.ErrorReceived.Subscribe(OnError);
        }

        private void OnSessionBegin(SessionBegins message)
        {
            Console.WriteLine($"Session begins: \n- Session ID: {message.SessionId}\n- Expires at: {message.ExpiresAt}");

            RunOnUi(() =>
            {
                tbRecognizing.Text = "Begin speaking...";
            });
        }

        private void OnPartialReceived(PartialTranscript transcript)
        {
            // don't do anything if nothing was said
            if (string.IsNullOrEmpty(transcript.Text)) 
                return;

            Console.WriteLine($"Partial: {transcript.Text}");
            RunOnUi(() =>
            {
                tbRecognizing.Text = transcript.Text;
            });
        }

        private void OnFinalReceived(FinalTranscript transcript)
        {
            Console.WriteLine($"Final: {transcript.Text}");
            RunOnUi(() =>
            {
                tbText.Text += transcript.Text;
            });
        }

        private void OnError(RealtimeError error)
        {
            Console.WriteLine($"Real-time error: {error.Error}");
            RunOnUi(() =>
            {
                tbRecognizing.Text = "Error: " + error.Error;
            });
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            tbRecognizing.Text = "";
            tbText.Text = "";
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            progress.Visibility = Visibility.Visible;

            cts = new CancellationTokenSource();
            try
            {
                await transcriber.ConnectAsync();
                string soxArguments = string.Join("", [
                    // --default-device doesn't work on Windows
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
                "--no-show-progress",
                $"--rate {sampleRate}",
                "--channels 1",
                "--encoding signed-integer",
                "--bits 16",
                "--type wav",
                "-" // pipe
                ]);

                using Process soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sox",
                        Arguments = soxArguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                soxProcess.Start();
                Stream soxOutputStream = soxProcess.StandardOutput.BaseStream;
                byte[] buffer = new byte[4096];
                while (await soxOutputStream.ReadAsync(buffer, 0, buffer.Length, cts.Token) > 0)
                {
                    if (cts.Token.IsCancellationRequested)
                        break;
                    await transcriber.SendAudioAsync(buffer);
                }

                soxProcess.Kill();
                await transcriber.CloseAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                tbRecognizing.Text = ex.ToString();
            }

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            progress.Visibility = Visibility.Collapsed;
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
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
