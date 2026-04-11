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
        public MainWindow()
        {
            InitializeComponent();
            
            var vm = (MainViewModel)DataContext;
            
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.InputFilePath))
                {
                    if (!string.IsNullOrEmpty(vm.InputFilePath) && vm.IsVideo)
                    {
                        VideoPlayer.Source = new Uri(vm.InputFilePath);
                    }
                    else
                    {
                        VideoPlayer.Source = null;
                    }
                }
                else if (e.PropertyName == nameof(vm.PlayIcon))
                {
                    if (vm.PlayIcon == "⏸" && vm.IsVideo) VideoPlayer.Play();
                    else if (vm.IsVideo) VideoPlayer.Pause();
                }
            };

            vm.RequestSync += (pos) => 
            {
                if (vm.IsVideo) 
                {
                    // Só sincroniza forçadamente se estiver pausado ou exportando (ExportTimePosition >= 0)
                    if (vm.PlayIcon == "▶" || vm.IsProcessing)
                    {
                        VideoPlayer.Position = TimeSpan.FromSeconds(pos);
                    }
                }
            };
            
            WaveformCtrl.RequestPreview += (pos) => 
            {
                if (vm.IsFileLoaded)
                {
                    if (vm.IsVideo) VideoPlayer.Position = TimeSpan.FromSeconds(pos);
                    vm.PlayWithEffects(pos, vm.EndTime);
                }
            };
        }

        private void OnBackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            if (vm.IsFadeInPopupOpen || vm.IsFadeOutPopupOpen) { vm.IsFadeInPopupOpen = false; vm.IsFadeOutPopupOpen = false; e.Handled = true; return; }
            if (!vm.IsFileLoaded) vm.SelectFile();
        }

        private void OnDragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        private void OnFilesDropped(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    string ext = System.IO.Path.GetExtension(files[0]).ToLower();
                    if (Array.Exists(new[] { ".mp3", ".mp4", ".mkv", ".avi", ".mov", ".wav" }, e => e == ext))
                        ((MainViewModel)DataContext).LoadFile(files[0]);
                }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
