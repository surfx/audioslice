using System;
using System.Windows;
using System.Windows.Input;
using AudioSlice.ViewModels;

namespace AudioSlice
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            var vm = (MainViewModel)DataContext;
            
            // O WaveformControl passa o ponto clicado e se foi início ou fim
            WaveformCtrl.RequestPreview += (pos) => 
            {
                if (vm.IsFileLoaded)
                {
                    // Se o ponto clicado for o início (StartTime), toca do início até o fim da seleção
                    // Se for o fim (EndTime), toca os últimos 2 segundos antes do fim
                    if (Math.Abs(pos - vm.StartTime) < 0.1)
                    {
                        vm.PlayWithEffects(vm.StartTime, vm.EndTime);
                    }
                    else
                    {
                        vm.PlayWithEffects(Math.Max(vm.StartTime, vm.EndTime - 2.0), vm.EndTime);
                    }
                }
            };
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

            if (!vm.IsFileLoaded)
            {
                vm.SelectFile();
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
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
                    if (file.ToLower().EndsWith(".mp3"))
                    {
                        var vm = (MainViewModel)DataContext;
                        vm.LoadFile(file);
                    }
                    else
                    {
                        MessageBox.Show("Por favor, arraste apenas arquivos MP3.", "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
