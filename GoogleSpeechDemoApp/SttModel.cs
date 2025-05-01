using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GoogleSpeechDemoApp
{
    internal class SttModelRaw
    {
        [JsonPropertyName("Location")]
        public string Location { get; set; }
        
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("BCP-47")]
        public string LanguageCode { get; set; }

        [JsonPropertyName("Model")]
        public string Model { get; set; }

        [JsonPropertyName("Automatic punctuation")]
        public bool AutoPunctuation { get; set; }

        [JsonPropertyName("Diarization")]
        public bool Diarization { get; set; }

        [JsonPropertyName("Model adaptation")]
        public bool ModelAdaptation { get; set; }

        [JsonPropertyName("Word-level confidence")]
        public bool WordLevelConfidence { get; set; }

        [JsonPropertyName("Profanity filter")]
        public bool ProfanityFilter { get; set; }

        [JsonPropertyName("Spoken punctuation")]
        public bool SpokenPunctuation { get; set; }

        [JsonPropertyName("Spoken emojis")]
        public bool SpokenEmojis { get; set; }
    }

    internal class SttLanguage
    {
        public string Name { get; set; }
        public string LanguageCode { get; set; }
        public List<SttModel> Models { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    internal class SttModel
    {
        public string ModelName { get; set; }
        public bool AutoPunctuation { get; set; }
        public bool Diarization { get; set; }
        public bool ModelAdaptation { get; set; }
        public bool WordLevelConfidence { get; set; }
        public bool ProfanityFilter { get; set; }
        public bool SpokenPunctuation { get; set; }
        public bool SpokenEmojis { get; set; }

        public override string ToString()
        {
            return ModelName.Replace('_', '-');
        }
    }
}
