using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AssemblyAiDemoApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists("key.txt"))
            {
                Keys.API_KEY = File.ReadAllText("key.txt").Trim();
            }
            else
            {
                MessageBox.Show(this, "Cannot load access token from key.txt!");
            }
        }

        private void BtnSTT_OnClick(object sender, RoutedEventArgs e)
        {
            Hide();
            SpeechToText stt = new SpeechToText();
            stt.ShowDialog();
            Show();
        }
    }
}
