using System;

namespace MediaSlice.Models
{
    public class MediaSegment
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double FadeIn { get; set; }
        public double FadeOut { get; set; }
    }
}
