using System;

namespace MediaSlice.Models
{
    public enum StreamType { Audio, Subtitle }
    public class MediaStreamInfo
    {
        public int Index { get; set; }
        public string LanguageCode { get; set; } = "und"; // ISO 639-2 (ex: por, eng, jpn)
        public string LanguageName { get; set; } = "Desconhecido";
        public string Codec { get; set; } = "";
        public StreamType Type { get; set; }
        
        public string DisplayName => Type == StreamType.Audio 
            ? $"Áudio [{Index}] {LanguageName}" 
            : $"Legenda [{Index}] {LanguageName}";

        public bool IsPortuguese => LanguageCode.Equals("por", StringComparison.OrdinalIgnoreCase) || 
                                    LanguageCode.Equals("ptb", StringComparison.OrdinalIgnoreCase);
        
        public bool IsEnglish => LanguageCode.Equals("eng", StringComparison.OrdinalIgnoreCase);
    }
}