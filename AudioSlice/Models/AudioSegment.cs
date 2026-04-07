using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioSlice.Models
{
    public class AudioSegment : INotifyPropertyChanged
    {
        private double _startTime;
        private double _endTime;
        private double _fadeIn;
        private double _fadeOut;

        public double StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); }
        }

        public double EndTime
        {
            get => _endTime;
            set { _endTime = value; OnPropertyChanged(); }
        }

        public double FadeIn
        {
            get => _fadeIn;
            set { _fadeIn = value; OnPropertyChanged(); }
        }

        public double FadeOut
        {
            get => _fadeOut;
            set { _fadeOut = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
