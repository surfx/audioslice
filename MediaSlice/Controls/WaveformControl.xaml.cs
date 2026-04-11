using System;
using System.ComponentModel;
using System.Linq;
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
        public static readonly DependencyProperty ProgressPositionProperty = DependencyProperty.Register("ProgressPosition", typeof(double), typeof(WaveformControl), new PropertyMetadata(-1.0, OnSelectionChanged));

        public float[] Points { get => (float[])GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
        public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public double StartTime { get => (double)GetValue(StartTimeProperty); set => SetValue(StartTimeProperty, value); }
        public double EndTime { get => (double)GetValue(EndTimeProperty); set => SetValue(EndTimeProperty, value); }
        public double FadeIn { get => (double)GetValue(FadeInProperty); set => SetValue(FadeInProperty, value); }
        public double FadeOut { get => (double)GetValue(FadeOutProperty); set => SetValue(FadeOutProperty, value); }
        public double ProgressPosition { get => (double)GetValue(ProgressPositionProperty); set => SetValue(ProgressPositionProperty, value); }

        public event Action<double>? RequestPreview;
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isDraggingStart;
        private bool _isDraggingEnd;

        public WaveformControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => InvalidateVisuals();
            Loaded += (s, e) => InvalidateVisuals();
        }

        private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).InvalidateVisuals();
        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformControl)d).UpdateOverlay();

        private void InvalidateVisuals()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { DrawWaveform(); UpdateOverlay(); }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void DrawWaveform()
        {
            if (Points == null || Points.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                double midY = ActualHeight / 2;
                double xScale = ActualWidth / (Points.Length - 1);
                float maxVal = Points.Max();
                if (maxVal <= 0) maxVal = 1;
                ctx.BeginFigure(new Point(0, midY), true, true);
                for (int i = 0; i < Points.Length; i++) {
                    double t = (double)i / (Points.Length - 1) * Duration;
                    double factor = GetFadeFactor(t);
                    double h = Math.Max(1, (Points[i] / maxVal) * (ActualHeight * 0.7) / 2 * factor);
                    ctx.LineTo(new Point(i * xScale, midY - h), true, false);
                }
                for (int i = Points.Length - 1; i >= 0; i--) {
                    double t = (double)i / (Points.Length - 1) * Duration;
                    double factor = GetFadeFactor(t);
                    double h = Math.Max(1, (Points[i] / maxVal) * (ActualHeight * 0.7) / 2 * factor);
                    ctx.LineTo(new Point(i * xScale, midY + h), true, false);
                }
            }
            geometry.Freeze();
            WaveformPath.Data = geometry;
            WaveformPath.Stroke = new SolidColorBrush(Color.FromRgb(35, 255, 173));
            WaveformPath.StrokeThickness = 0.5;
        }

        private double GetFadeFactor(double t)
        {
            if (Duration <= 0 || t < StartTime || t > EndTime) return 0.3;
            double factor = 1.0;
            if (FadeIn > 0 && t < StartTime + FadeIn) factor = (t - StartTime) / FadeIn;
            else if (FadeOut > 0 && t > EndTime - FadeOut) factor = (EndTime - t) / FadeOut;
            return Math.Clamp(factor, 0.0, 1.0);
        }

        private void UpdateOverlay()
        {
            if (Duration <= 0 || ActualWidth <= 0) return;
            
            double handleOffset = LeftMarker.ActualWidth / 2;
            
            double startX = (StartTime / Duration) * ActualWidth;
            double endX = (EndTime / Duration) * ActualWidth;
            
            LeftDimmer.Width = Math.Max(0, startX);
            Canvas.SetLeft(RightDimmer, endX);
            RightDimmer.Width = Math.Max(0, ActualWidth - endX);
            
            // Posiciona as alças centralizadas no ponto do tempo
            Canvas.SetLeft(LeftMarker, startX - handleOffset);
            Canvas.SetLeft(RightMarker, endX - handleOffset);

            if (ProgressPosition >= 0) {
                double progX = (ProgressPosition / Duration) * ActualWidth;
                ProgressMarker.Visibility = Visibility.Visible;
                Canvas.SetLeft(ProgressMarker, Math.Clamp(progX, 0, ActualWidth - 2));
            } else {
                ProgressMarker.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMarkerDown(object sender, MouseButtonEventArgs e)
        {
            var marker = sender as FrameworkElement;
            if (marker == null) return;

            if (marker.Name == "LeftMarker") _isDraggingStart = true;
            else if (marker.Name == "RightMarker") _isDraggingEnd = true;

            marker.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Duration <= 0 || ActualWidth <= 0) return;
            var pos = e.GetPosition(this);
            double clickedTime = (pos.X / ActualWidth) * Duration;
            
            double distToStart = Math.Abs(clickedTime - StartTime);
            double distToEnd = Math.Abs(clickedTime - EndTime);
            
            if (distToStart < distToEnd) { StartTime = Math.Min(clickedTime, EndTime - 0.1); RequestPreview?.Invoke(StartTime); }
            else { EndTime = Math.Max(clickedTime, StartTime + 0.1); RequestPreview?.Invoke(Math.Max(StartTime, EndTime - 3.0)); }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingStart && !_isDraggingEnd) return;

            var pos = e.GetPosition(this);
            double newTime = Math.Clamp((pos.X / ActualWidth) * Duration, 0, Duration);

            if (_isDraggingStart) { StartTime = Math.Min(newTime, EndTime - 0.1); RequestPreview?.Invoke(StartTime); }
            else if (_isDraggingEnd) { EndTime = Math.Max(newTime, StartTime + 0.1); }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStart) { _isDraggingStart = false; LeftMarker.ReleaseMouseCapture(); }
            if (_isDraggingEnd) { _isDraggingEnd = false; RightMarker.ReleaseMouseCapture(); RequestPreview?.Invoke(Math.Max(StartTime, EndTime - 3.0)); }
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
