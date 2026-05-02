using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MediaSlice.ViewModels;

namespace MediaSlice
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_COMMAND = 0x0111;
        // Comandos do MPC-HC (podem variar, mas estes são os padrão para busca)
        private const int MPC_PLAY = 887;
        private const int MPC_PAUSE = 888;
        private const int MPC_STOP = 890;
        private const int MPC_SEEK_BAR = 905;

        private Process? _mpcProcess;
        private IntPtr _mpcHwnd;

        public MainWindow()
        {
            InitializeComponent();
            
            var vm = (MainViewModel)DataContext;
            
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.InputFilePath) || e.PropertyName == nameof(vm.StartTime))
                {
                    if (!string.IsNullOrEmpty(vm.InputFilePath) && vm.IsVideo)
                    {
                        if (e.PropertyName == nameof(vm.InputFilePath) || _mpcHwnd == IntPtr.Zero)
                        {
                            StartEmbeddedMPC(vm.InputFilePath, vm.MPCPath, vm.StartTime);
                        }
                        else if (e.PropertyName == nameof(vm.StartTime))
                        {
                            // Se apenas o tempo mudou, tenta sincronizar
                            SyncMPCPosition(vm.StartTime);
                        }
                    }
                    else if (string.IsNullOrEmpty(vm.InputFilePath))
                    {
                        StopMPC();
                    }
                }
                else if (e.PropertyName == nameof(vm.PlayIcon))
                {
                    if (_mpcHwnd != IntPtr.Zero)
                    {
                        if (vm.PlayIcon == "⏸") SendMessage(_mpcHwnd, WM_COMMAND, (IntPtr)MPC_PLAY, IntPtr.Zero);
                        else SendMessage(_mpcHwnd, WM_COMMAND, (IntPtr)MPC_PAUSE, IntPtr.Zero);
                    }
                }
            };

            vm.RequestSync += (pos) => 
            {
                if (vm.IsVideo && _mpcHwnd != IntPtr.Zero) 
                {
                    SyncMPCPosition(pos);
                }
            };

            this.SizeChanged += (s, e) => UpdateMPCLayout();
            this.Closed += (s, e) => StopMPC();
        }

        private void SyncMPCPosition(double seconds)
        {
            if (_mpcHwnd == IntPtr.Zero || _mpcProcess == null || _mpcProcess.HasExited) return;

            // Para manter a sincronização perfeita com o áudio interno do MediaSlice,
            // reiniciamos o MPC na posição correta se a diferença for maior que 1 segundo,
            // ou se o usuário estiver buscando manualmente.
            var vm = (MainViewModel)DataContext;
            StartEmbeddedMPC(vm.InputFilePath, vm.MPCPath, seconds);
        }

        private void StartEmbeddedMPC(string videoPath, string mpcPath, double startTime)
        {
            StopMPC();

            if (!System.IO.File.Exists(mpcPath)) return;

            var ts = TimeSpan.FromSeconds(startTime);
            // Formato estrito hh:mm:ss conforme ajuda do MPC-HC
            string startPos = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";

            // Usando apenas comandos que apareceram no seu log de ajuda:
            // /viewpreset 1 (Modo Mínimo) e /slave (para embutir)
            string args = $"\"{videoPath}\" /startpos {startPos} /play /viewpreset 1 /slave {PlayerPanel.Handle}";

            _mpcProcess = new Process();
            _mpcProcess.StartInfo = new ProcessStartInfo
            {
                FileName = mpcPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            _mpcProcess.Start();

            // Aguarda a janela ser criada
            Task.Run(() => {
                int attempts = 0;
                while (_mpcProcess != null && !_mpcProcess.HasExited && attempts < 50)
                {
                    _mpcProcess.Refresh();
                    if (_mpcProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        _mpcHwnd = _mpcProcess.MainWindowHandle;
                        Dispatcher.Invoke(() => {
                            SetParent(_mpcHwnd, PlayerPanel.Handle);
                            
                            // Remove bordas e barras da janela do MPC
                            int style = GetWindowLong(_mpcHwnd, GWL_STYLE);
                            SetWindowLong(_mpcHwnd, GWL_STYLE, (style & ~WS_CAPTION & ~WS_BORDER) | WS_CHILD);
                            
                            UpdateMPCLayout();
                        });
                        break;
                    }
                    Thread.Sleep(100);
                    attempts++;
                }
            });
        }

        private void UpdateMPCLayout()
        {
            if (_mpcHwnd != IntPtr.Zero)
            {
                MoveWindow(_mpcHwnd, 0, 0, (int)PlayerPanel.Width, (int)PlayerPanel.Height, true);
            }
        }

        private void StopMPC()
        {
            if (_mpcProcess != null && !_mpcProcess.HasExited)
            {
                try { _mpcProcess.Kill(); } catch { }
            }
            _mpcProcess = null;
            _mpcHwnd = IntPtr.Zero;
        }

        private void OnBackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            if (vm.IsFadeInPopupOpen || vm.IsFadeOutPopupOpen) { vm.IsFadeInPopupOpen = false; vm.IsFadeOutPopupOpen = false; e.Handled = true; return; }
            if (!vm.IsFileLoaded) vm.SelectFile();
        }

        private void OnDragOver(object sender, System.Windows.DragEventArgs e) => e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;

        private void OnFilesDropped(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
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
