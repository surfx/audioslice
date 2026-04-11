using System;

namespace MediaSlice.Models
{
    public class AudioStreamInfo
    {
        public int Index { get; set; }
        public string Language { get; set; } = "";
        public string Codec { get; set; } = "";
        public string DisplayName => Index == 0 ? "Áudio (Padrão)" : $"Áudio [{Index}] {Language}";

        public static AudioStreamInfo Default => new() { Index = 0, Language = "pt-BR", Codec = "default" };
    }
}