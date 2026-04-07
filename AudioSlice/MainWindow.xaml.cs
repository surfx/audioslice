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

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
