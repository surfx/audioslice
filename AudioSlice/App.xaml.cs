using System;
using System.Windows;

namespace AudioSlice
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, ev) => 
            {
                MessageBox.Show($"Erro não tratado: {ev.Exception.Message}", "Erro Crítico");
                ev.Handled = true;
            };
        }
    }
}
