using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MediaSlice.Controls
{
    public partial class WaveformControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty PointsProperty = DependencyProperty.Register("Points", typeof(float[]), typeof(WaveformControl), new PropertyMetadata(null, OnPointsChanged));
        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(WaveformControl), new PropertyMetadata(0.0, OnSelectionChanged));
        public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register("StartTime", typeof(double), typeof(WaveformControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));
        public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register("EndTime", typeof(double), typeof(WaveformControl), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));
        public static readonly DependencyProperty FadeInProperty = DependencyProperty.Register("FadeIn", typeof(double), typeof(WaveformControl), new PropertyMetadata(0.0, OnSelectionChanged));
        public static readonly DependencyProperty FadeOutProperty = DependencyProperty.Register("FadeOut", typeof(double), typeof(WaveformControl), new PropertyMetadata(0.0, OnSelectionChanged));

        public float[] Points { get => (float[])GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
        public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public double StartTime { get => (double)GetValue(StartTimeProperty); set => SetValue(StartTimeProperty, value); }
        public double EndTime { get => (double)GetValue(EndTimeProperty); set => SetValue(EndTimeProperty, value); }
        public double FadeIn { get => (double)GetValue(FadeInProperty); set => SetValue(FadeInProperty, value); }
        public double FadeOut { get => (double)GetValue(FadeOutProperty); set => SetValue(FadeOutProperty, value); }

        public string StartTimeStr => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss\.f");
        public string EndTimeStr => TimeSpan.FromSeconds(EndTime).ToString(@"mm\:ss\.f");

        private bool _isDraggingStart;
        private bool _isDraggingEnd;

        public event Action<double>? RequestPreview;
        public event PropertyChangedEventHandler? PropertyChanged;

        public WaveformControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => { DrawWaveform(); UpdateOverlay(); };
        }

        private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).DrawWaveform();
        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (WaveformControl)d;
            ctrl.DrawWaveform();
            ctrl.UpdateOverlay();
            ctrl.OnPropertyChanged(nameof(StartTimeStr));
            ctrl.OnPropertyChanged(nameof(EndTimeStr));
        }

        private void DrawWaveform()
        {
            if (Points == null || Points.Length == 0 || ActualWidth <= 0 || Duration <= 0) return;

            WaveformPolyline.Points.Clear();
            double midY = ActualHeight / 2;
            double xScale = ActualWidth / Points.Length;

            for (int i = 0; i < Points.Length; i++)
            {
                double x = i * xScale;
                double t = (double)i / Points.Length * Duration;
                float originalValue = Points[i];
                double factor = 1.0;

                // Aplica o Fade apenas dentro do trecho selecionado
                if (t >= StartTime && t <= EndTime)
                {
                    if (t < StartTime + FadeIn && FadeIn > 0)
                    {
                        factor = (t - StartTime) / FadeIn;
                    }
                    else if (t > EndTime - FadeOut && FadeOut > 0)
                    {
                        factor = (EndTime - t) / FadeOut;
                    }
                }

                double yOffset = originalValue * factor * midY;
                WaveformPolyline.Points.Add(new Point(x, midY - yOffset));
                WaveformPolyline.Points.Add(new Point(x, midY + yOffset));
            }
        }

        private void UpdateOverlay()
        {
            if (Duration <= 0 || ActualWidth <= 0) return;
            double startX = (StartTime / Duration) * ActualWidth;
            double endX = (EndTime / Duration) * ActualWidth;
            
            Canvas.SetLeft(LeftHandleGroup, startX - 5);
            Canvas.SetLeft(RightHandleGroup, endX - 5);

            LeftDimmer.Width = Math.Max(0, startX);
            Canvas.SetLeft(RightDimmer, endX);
            RightDimmer.Width = Math.Max(0, ActualWidth - endX);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Duration <= 0) return;
            var pos = e.GetPosition(this);
            double time = (pos.X / ActualWidth) * Duration;

            if (Math.Abs(time - StartTime) < Math.Abs(time - EndTime)) _isDraggingStart = true;
            else _isDraggingEnd = true;
            
            CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStart || _isDraggingEnd)
            {
                double previewPos = _isDraggingStart ? StartTime : Math.Max(StartTime, EndTime - 2.0);
                RequestPreview?.Invoke(previewPos);
            }
            _isDraggingStart = false;
            _isDraggingEnd = false;
            ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseCaptured && Duration > 0)
            {
                var pos = e.GetPosition(this);
                double time = Math.Clamp((pos.X / ActualWidth) * Duration, 0, Duration);

                if (_isDraggingStart) StartTime = Math.Min(time, EndTime - 0.1);
                else if (_isDraggingEnd) EndTime = Math.Max(time, StartTime + 0.1);
            }
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
