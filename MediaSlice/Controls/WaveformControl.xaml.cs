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
            double startX = (StartTime / Duration) * ActualWidth;
            double endX = (EndTime / Duration) * ActualWidth;
            LeftDimmer.Width = Math.Max(0, startX);
            Canvas.SetLeft(RightDimmer, endX);
            RightDimmer.Width = Math.Max(0, ActualWidth - endX);
            Canvas.SetLeft(LeftMarker, startX);
            Canvas.SetLeft(RightMarker, endX);

            // Linha de progresso da exportação
            if (ProgressPosition >= 0) {
                double progX = (ProgressPosition / Duration) * ActualWidth;
                LeftMarker.Width = 3; LeftMarker.Opacity = 1;
                Canvas.SetLeft(LeftMarker, progX);
            } else {
                LeftMarker.Width = 1; LeftMarker.Opacity = 0.5;
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Duration <= 0 || ActualWidth <= 0) return;
            var pos = e.GetPosition(this);
            double clickedTime = (pos.X / ActualWidth) * Duration;
            double distToStart = Math.Abs(clickedTime - StartTime);
            double distToEnd = Math.Abs(clickedTime - EndTime);
            if (distToStart < distToEnd) StartTime = Math.Min(clickedTime, EndTime - 0.1);
            else EndTime = Math.Max(clickedTime, StartTime + 0.1);
            RequestPreview?.Invoke(clickedTime);
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
