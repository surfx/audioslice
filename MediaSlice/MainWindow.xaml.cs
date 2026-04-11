using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MediaSlice.ViewModels;

namespace MediaSlice
{
    public partial class MainWindow : Window
    {
        private static readonly Color[] ProgressColors = new[]
        {
            Color.FromRgb(0x23, 0xFF, 0xAD),
            Color.FromRgb(0x1A, 0xE6, 0xB8),
            Color.FromRgb(0x12, 0xCD, 0xC3),
            Color.FromRgb(0x0A, 0xB4, 0xCE),
            Color.FromRgb(0x00, 0x9C, 0xD9),
            Color.FromRgb(0x00, 0x84, 0xE4),
            Color.FromRgb(0x00, 0x6C, 0xEF),
            Color.FromRgb(0x54, 0x54, 0xFF),
            Color.FromRgb(0x88, 0x3C, 0xFF),
            Color.FromRgb(0xBC, 0x24, 0xFA)
        };

        private readonly Rectangle[] _progressBars = new Rectangle[50];

        public MainWindow()
        {
            InitializeComponent();
            
            InitializeProgressBars();

            var vm = (MainViewModel)DataContext;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.ExportProgress))
                {
                    UpdateProgressBars(vm.ExportProgress);
                }
            };
            
            vm.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(vm.PlayIcon))
                {
                    if (vm.PlayIcon == "⏸" && vm.IsVideo) VideoPlayer.Play();
                    else if (vm.IsVideo) VideoPlayer.Pause();
                }
            };
            
            WaveformCtrl.RequestPreview += (pos) => 
            {
                if (vm.IsFileLoaded)
                {
                    if (vm.IsVideo)
                    {
                        VideoPlayer.Position = TimeSpan.FromSeconds(pos);
                        VideoPlayer.Play();
                    }
                    if (Math.Abs(pos - vm.StartTime) < 0.1) vm.PlayWithEffects(vm.StartTime, vm.EndTime);
                    else vm.PlayWithEffects(Math.Max(vm.StartTime, vm.EndTime - 2.0), vm.EndTime);
                }
            };
        }

        private void InitializeProgressBars()
        {
            for (int i = 0; i < 50; i++)
            {
                _progressBars[i] = new Rectangle
                {
                    Margin = new Thickness(1, 0, 1, 0),
                    Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x32, 0x50)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                ProgressBars.Items.Add(_progressBars[i]);
            }
        }

        private void UpdateProgressBars(double progress)
        {
            int activeSegments = (int)(progress / 100.0 * 50);
            for (int i = 0; i < 50; i++)
            {
                if (i < activeSegments)
                {
                    int colorIndex = (int)(i / 5.0);
                    _progressBars[i].Fill = new SolidColorBrush(ProgressColors[colorIndex]);
                    _progressBars[i].Height = 20;
                }
                else
                {
                    _progressBars[i].Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x32, 0x50));
                    _progressBars[i].Height = 8;
                }
            }
        }

        private void OnBackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = (MainViewModel)DataContext;

            if (vm.IsFadeInPopupOpen || vm.IsFadeOutPopupOpen)
            {
                vm.IsFadeInPopupOpen = false;
                vm.IsFadeOutPopupOpen = false;
                e.Handled = true;
                return;
            }

            if (!vm.IsFileLoaded) vm.SelectFile();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void OnFilesDropped(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string file = files[0];
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    string[] validExtensions = { ".mp3", ".mp4", ".mkv", ".avi", ".mov", ".wav" };

                    if (Array.Exists(validExtensions, e => e == ext))
                    {
                        var vm = (MainViewModel)DataContext;
                        vm.LoadFile(file);
                    }
                    else
                    {
                        MessageBox.Show("Por favor, arraste arquivos de áudio ou vídeo suportados.", "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
