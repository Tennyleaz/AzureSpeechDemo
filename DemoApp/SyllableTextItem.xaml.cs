using System;
using System.Collections.Generic;
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

namespace DemoApp
{
    /// <summary>
    /// SyllableTextItem.xaml 的互動邏輯
    /// </summary>
    public partial class SyllableTextItem : UserControl
    {
        public SyllableTextItem(string text, string syllable)
        {
            InitializeComponent();
            lbText.Content = text;
            lbSyllable.Content = syllable;
        }
    }
}
