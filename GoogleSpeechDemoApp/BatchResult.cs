using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleSpeechDemoApp
{
    internal class BatchResult
    {
        public List<Result> results { get; set; }
    }

    internal class Result
    {
        public List<Alternative> alternatives { get; set; }
        public double resultEndOffset { get; set; }
        public string languageCode { get; set; }
    }

    internal class Alternative
    {
        public string transcript { get; set; }
        public double confidence { get; set; }
    }
}
