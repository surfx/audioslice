using System;

namespace MediaSlice.Models
{
    public class SubtitleItem
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = "";
    }
}