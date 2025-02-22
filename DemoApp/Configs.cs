using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoApp
{
    internal static class Configs
    {
        internal static string speechKey = "";
        internal const string speechRegion = "eastasia";

        internal static readonly string[] AiLanguages =
        [
            "de-DE",
            "en-US",
            "es-ES",
            "fr-FR",
            "it-IT",
            "ja-JP",
            "ko-KR",
            "sv-SE",
            "zh-CN",
            "zh-HK",
            "zh-TW"
        ];

        internal static readonly List<string> pronunciationLanguages = [
            "ar-EG",
            "ar-SA",
            "ca-ES",
            "zh-HK",
            "zh-CN",
            "zh-TW",
            "da-DK",
            "nl-NL",
            "en-AU",
            "en-CA",
            "en-IN",
            "en-GB",
            "en-US",
            "fi-FI",
            "fr-CA",
            "fr-FR",
            "de-DE",
            "hi-IN",
            "it-IT",
            "ja-JP",
            "ko-KR",
            "ms-MY",
            "nb-NO",
            "pl-PL",
            "pt-BR",
            "pt-PT",
            "ru-RU",
            "ex-MX",
            "es-ES",
            "sv-SE",
            "ta-IN",
            "th-TH",
            "vi-VN"
        ];

        internal static readonly List<string> conversationLanguages = [
            "en-US",
            "zh-CN",
            "en-AU",
            "en-CA",
            "en-IN",
            "en-GB",
            "fr-CA",
            "fr-FR",
            "de-DE",
            "it-IT",
            "ja-JP",
            "pt-BR",
            "es-MX",
            "es-ES",
        ];
    }
}
