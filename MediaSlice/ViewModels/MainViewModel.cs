using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MediaSlice.Services;
using MediaSlice.Models;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MediaSlice.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly MediaService _mediaService = new();
        private readonly FFmpegService _ffmpegService = new();
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _audioFileReader;
        private readonly DispatcherTimer _stopTimer;
        private readonly string _settingsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.dat");
        private readonly string _defaultFFmpeg = @"D:\programas\executaveis\ffmpeg\bin\ffmpeg.exe";

        private string _inputFilePath = "";
        private string _ffmpegPath = "";
        private string _status = "Clique para selecionar sua mídia";
        private bool _isProcessing;
        private float[] _waveformPoints = Array.Empty<float>();
        private double _duration;
        private double _startTime;
        private double _endTime;
        private double _fadeIn = 0;
        private double _fadeOut = 0;
        private string _playIcon = "▶";
        private bool _isFadeInPopupOpen;
        private bool _isFadeOutPopupOpen;
        private double _exportProgress;
        private bool _showProgress;

        public string InputFilePath { get => _inputFilePath; set { _inputFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFileLoaded)); OnPropertyChanged(nameof(IsVideo)); OnPropertyChanged(nameof(FileName)); } }
        public string FFmpegPath { get => _ffmpegPath; set { _ffmpegPath = value; OnPropertyChanged(); SaveSettings(); } }
        public bool IsFileLoaded => !string.IsNullOrEmpty(InputFilePath);
        public string FileName => IsFileLoaded ? System.IO.Path.GetFileName(InputFilePath) : "";
        public bool IsVideo => IsFileLoaded && (InputFilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                                InputFilePath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) || 
                                                InputFilePath.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
                                                InputFilePath.EndsWith(".mov", StringComparison.OrdinalIgnoreCase));
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public bool IsProcessing { get => _isProcessing; set { _isProcessing = value; OnPropertyChanged(); } }
        public float[] WaveformPoints { get => _waveformPoints; set { _waveformPoints = value; OnPropertyChanged(); } }
        public double Duration { get => _duration; set { _duration = value; OnPropertyChanged(); } }
        public double StartTime { get => _startTime; set { _startTime = Math.Max(0, Math.Min(value, EndTime - 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(StartTimeFormatted)); } }
        public double EndTime { get => _endTime; set { _endTime = Math.Min(Duration, Math.Max(value, StartTime + 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(EndTimeFormatted)); } }
        public double FadeIn { get => _fadeIn; set { _fadeIn = value; OnPropertyChanged(); } }
        public double FadeOut { get => _fadeOut; set { _fadeOut = value; OnPropertyChanged(); } }
        public string PlayIcon { get => _playIcon; set { _playIcon = value; OnPropertyChanged(); } }
        public double ExportProgress { get => _exportProgress; set { _exportProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExportProgressSegments)); } }
        public bool ShowProgress { get => _showProgress; set { _showProgress = value; OnPropertyChanged(); } }
        
        public bool[] ExportProgressSegments => new bool[50];
        
        public string StartTimeFormatted => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss\.f");
        public string EndTimeFormatted => TimeSpan.FromSeconds(EndTime).ToString(@"mm\:ss\.f");

        public bool IsFadeInPopupOpen { get => _isFadeInPopupOpen; set { _isFadeInPopupOpen = value; OnPropertyChanged(); if(value) IsFadeOutPopupOpen = false; } }
        public bool IsFadeOutPopupOpen { get => _isFadeOutPopupOpen; set { _isFadeOutPopupOpen = value; OnPropertyChanged(); if(value) IsFadeInPopupOpen = false; } }

        public ICommand SelectFileCommand { get; }
        public ICommand BrowseFFmpegCommand { get; }
        public ICommand ResetFFmpegCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand ProcessCommand { get; }
        public ICommand ExportAudioCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ToggleFadeInCommand { get; }
        public ICommand ToggleFadeOutCommand { get; }

        public MainViewModel()
        {
            _ffmpegPath = _defaultFFmpeg;
            LoadSettings();
            SelectFileCommand = new RelayCommand(_ => SelectFile());
            BrowseFFmpegCommand = new RelayCommand(_ => BrowseFFmpeg());
            ResetFFmpegCommand = new RelayCommand(_ => FFmpegPath = _defaultFFmpeg);
            PlayCommand = new RelayCommand(_ => TogglePlay(), _ => IsFileLoaded);
            ProcessCommand = new RelayCommand(async _ => await ProcessMedia(), _ => !IsProcessing && IsFileLoaded);
            ExportAudioCommand = new RelayCommand(async _ => await ExportAudioOnly(), _ => !IsProcessing && IsVideo);
            ResetCommand = new RelayCommand(_ => ResetSelection());
            ToggleFadeInCommand = new RelayCommand(_ => IsFadeInPopupOpen = !IsFadeInPopupOpen);
            ToggleFadeOutCommand = new RelayCommand(_ => IsFadeOutPopupOpen = !IsFadeOutPopupOpen);

            _stopTimer = new DispatcherTimer();
            _stopTimer.Tick += (s, e) => StopAudio();
        }

        public void LoadFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;
            
            InputFilePath = filePath;
            LoadAudioData();
        }

        public void SelectFile()
        {
            var filter = "Mídias Suportadas|*.mp3;*.mp4;*.mkv;*.avi;*.mov;*.wav|Todos os arquivos|*.*";
            var openFileDialog = new OpenFileDialog { Filter = filter };
            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private void BrowseFFmpeg()
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executável FFmpeg (ffmpeg.exe)|ffmpeg.exe" };
            if (!string.IsNullOrEmpty(FFmpegPath))
            {
                var dir = System.IO.Path.GetDirectoryName(FFmpegPath);
                if (System.IO.Directory.Exists(dir)) openFileDialog.InitialDirectory = dir;
            }
            if (string.IsNullOrEmpty(openFileDialog.InitialDirectory))
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (openFileDialog.ShowDialog() == true) FFmpegPath = openFileDialog.FileName;
        }

        private void LoadSettings()
        {
            if (System.IO.File.Exists(_settingsFile))
            {
                try { using var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(_settingsFile)); _ffmpegPath = reader.ReadString(); } catch { }
            }
        }

        private void SaveSettings()
        {
            try { using var writer = new System.IO.BinaryWriter(System.IO.File.Open(_settingsFile, System.IO.FileMode.Create)); writer.Write(FFmpegPath); } catch { }
        }

        private async void LoadAudioData()
        {
            Status = "Carregando...";
            Duration = _mediaService.GetDuration(InputFilePath);
            StartTime = 0;
            EndTime = Duration;
            WaveformPoints = await _mediaService.GetWaveformDataAsync(InputFilePath, 1000);
            Status = "Pronto";
            OnPropertyChanged(nameof(FileName));
        }

        private void ResetSelection() { StartTime = 0; EndTime = Duration; FadeIn = 0; FadeOut = 0; }

        private void TogglePlay()
        {
            if (_wavePlayer != null && _wavePlayer.PlaybackState == PlaybackState.Playing) StopAudio();
            else PlayWithEffects(StartTime, EndTime);
        }

        public void PlayWithEffects(double startPos, double endPos)
        {
            StopAudio();
            if (!IsFileLoaded) return;

            try
            {
                _audioFileReader = new AudioFileReader(InputFilePath);
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(Math.Max(0, startPos));

                ISampleProvider provider = _audioFileReader.ToSampleProvider();
                if (FadeIn > 0 || FadeOut > 0)
                {
                    var fadeProvider = new FadeInOutSampleProvider(provider, FadeIn > 0);
                    if (FadeIn > 0) fadeProvider.BeginFadeIn(FadeIn * 1000);
                    double totalPlayDuration = endPos - startPos;
                    if (FadeOut > 0 && totalPlayDuration > FadeOut)
                    {
                        var fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(totalPlayDuration - FadeOut) };
                        fadeOutTimer.Tick += (s, e) => { fadeProvider.BeginFadeOut(FadeOut * 1000); fadeOutTimer.Stop(); };
                        fadeOutTimer.Start();
                    }
                    provider = fadeProvider;
                }

                _wavePlayer = new WaveOutEvent();
                _wavePlayer.Init(provider);
                _wavePlayer.PlaybackStopped += OnPlaybackStopped;
                _wavePlayer.Play();
                Application.Current.Dispatcher.Invoke(() => PlayIcon = "⏸");
                double playTime = endPos - startPos;
                if (playTime > 0) { _stopTimer.Interval = TimeSpan.FromSeconds(playTime); _stopTimer.Start(); }
            }
            catch { Status = "Erro ao reproduzir"; Application.Current.Dispatcher.Invoke(() => PlayIcon = "▶"); }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => { PlayIcon = "▶"; _stopTimer.Stop(); });
        }

        private void StopAudio()
        {
            _stopTimer.Stop();
            if (_wavePlayer != null) { _wavePlayer.PlaybackStopped -= OnPlaybackStopped; _wavePlayer.Stop(); _wavePlayer.Dispose(); _wavePlayer = null; }
            if (_audioFileReader != null) { _audioFileReader.Dispose(); _audioFileReader = null; }
            Application.Current.Dispatcher.Invoke(() => PlayIcon = "▶");
        }

        private async Task ProcessMedia()
        {
            var ext = System.IO.Path.GetExtension(InputFilePath);
            var defaultFileName = $"cut_{System.IO.Path.GetFileNameWithoutExtension(InputFilePath)}{ext}";
            var filter = $"Arquivo Original (*{ext})|*{ext}|Todos os arquivos|*.*";
            var saveFileDialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName };
            if (saveFileDialog.ShowDialog() == true)
            {
                IsProcessing = true;
                ShowProgress = true;
                ExportProgress = 0;
                Status = "Exportando...";
                try
                {
                    var segments = new System.Collections.Generic.List<MediaSegment> { new MediaSegment { StartTime = StartTime, EndTime = EndTime, FadeIn = FadeIn, FadeOut = FadeOut } };
                    var progress = new Progress<double>(p => { ExportProgress = p; });
                    await _ffmpegService.ProcessMediaAsync(FFmpegPath, InputFilePath, saveFileDialog.FileName, segments, progress);
                    Status = "Corte salvo com sucesso!";
                }
                catch (Exception ex) { Status = "Erro"; MessageBox.Show("Erro ao salvar: " + ex.Message); }
                finally { IsProcessing = false; ShowProgress = false; }
            }
        }

        private async Task ExportAudioOnly()
        {
            var defaultFileName = $"audio_{System.IO.Path.GetFileNameWithoutExtension(InputFilePath)}.mp3";
            var filter = "MP3|*.mp3|Todos os arquivos|*.*";
            var saveFileDialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName };
            if (saveFileDialog.ShowDialog() == true)
            {
                IsProcessing = true;
                ShowProgress = true;
                ExportProgress = 0;
                Status = "Exportando áudio...";
                try
                {
                    var progress = new Progress<double>(p => { ExportProgress = p; });
                    await _ffmpegService.ExtractAudioAsync(FFmpegPath, InputFilePath, saveFileDialog.FileName, StartTime, EndTime, progress);
                    Status = "Áudio salvo com sucesso!";
                }
                catch (Exception ex) { Status = "Erro"; MessageBox.Show("Erro ao salvar: " + ex.Message); }
                finally { IsProcessing = false; ShowProgress = false; }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}
