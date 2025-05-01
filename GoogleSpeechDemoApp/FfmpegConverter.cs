using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GoogleSpeechDemoApp
{
    internal class FfmpegConverter
    {
        public static string ConvertWavFormat(string inputFile)
        {
            string outputFile = "output.wav";
            try
            {
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
                ProcessStartInfo processStartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg-bin/ffmpeg.exe",
                    Arguments = $"-i \"{inputFile}\" -ac 1 -ar 16000 -sample_fmt s16 -c:a pcm_s16le \"{outputFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    //RedirectStandardOutput = true,
                };
                Process process = Process.Start(processStartInfo);
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return outputFile;
                MessageBox.Show("Ffmpeg return error: " + process.ExitCode);
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Convert error: " + ex);
                return null;
            }
        }
    }
}
