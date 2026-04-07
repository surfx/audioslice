using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AudioSlice.Services;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioSlice.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AudioService _audioService = new();
        private readonly FFmpegService _ffmpegService = new();
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _audioFileReader;
        private readonly DispatcherTimer _stopTimer;

        private string _inputFilePath = "";
        private string _status = "Clique para selecionar um MP3";
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

        public string InputFilePath { get => _inputFilePath; set { _inputFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFileLoaded)); } }
        public bool IsFileLoaded => !string.IsNullOrEmpty(InputFilePath);
        public string FileName => IsFileLoaded ? System.IO.Path.GetFileName(InputFilePath) : "";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public bool IsProcessing { get => _isProcessing; set { _isProcessing = value; OnPropertyChanged(); } }
        public float[] WaveformPoints { get => _waveformPoints; set { _waveformPoints = value; OnPropertyChanged(); } }
        public double Duration { get => _duration; set { _duration = value; OnPropertyChanged(); } }
        public double StartTime { get => _startTime; set { _startTime = Math.Max(0, Math.Min(value, EndTime - 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(StartTimeFormatted)); } }
        public double EndTime { get => _endTime; set { _endTime = Math.Min(Duration, Math.Max(value, StartTime + 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(EndTimeFormatted)); } }
        public double FadeIn { get => _fadeIn; set { _fadeIn = value; OnPropertyChanged(); } }
        public double FadeOut { get => _fadeOut; set { _fadeOut = value; OnPropertyChanged(); } }
        public string PlayIcon { get => _playIcon; set { _playIcon = value; OnPropertyChanged(); } }
        
        public string StartTimeFormatted => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss\.f");
        public string EndTimeFormatted => TimeSpan.FromSeconds(EndTime).ToString(@"mm\:ss\.f");

        public bool IsFadeInPopupOpen { get => _isFadeInPopupOpen; set { _isFadeInPopupOpen = value; OnPropertyChanged(); if(value) IsFadeOutPopupOpen = false; } }
        public bool IsFadeOutPopupOpen { get => _isFadeOutPopupOpen; set { _isFadeOutPopupOpen = value; OnPropertyChanged(); if(value) IsFadeInPopupOpen = false; } }

        public ICommand SelectFileCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand ProcessCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ToggleFadeInCommand { get; }
        public ICommand ToggleFadeOutCommand { get; }

        public MainViewModel()
        {
            SelectFileCommand = new RelayCommand(_ => SelectFile());
            PlayCommand = new RelayCommand(_ => TogglePlay(), _ => IsFileLoaded);
            ProcessCommand = new RelayCommand(async _ => await ProcessAudio(), _ => !IsProcessing && IsFileLoaded);
            ResetCommand = new RelayCommand(_ => ResetSelection());
            ToggleFadeInCommand = new RelayCommand(_ => IsFadeInPopupOpen = !IsFadeInPopupOpen);
            ToggleFadeOutCommand = new RelayCommand(_ => IsFadeOutPopupOpen = !IsFadeOutPopupOpen);

            _stopTimer = new DispatcherTimer();
            _stopTimer.Tick += (s, e) => StopAudio();
        }

        public void SelectFile()
        {
            var openFileDialog = new OpenFileDialog { Filter = "Arquivos MP3 (*.mp3)|*.mp3" };
            if (openFileDialog.ShowDialog() == true)
            {
                InputFilePath = openFileDialog.FileName;
                LoadAudioData();
            }
        }

        private async void LoadAudioData()
        {
            Status = "Carregando...";
            Duration = _audioService.GetDuration(InputFilePath);
            StartTime = 0;
            EndTime = Duration;
            WaveformPoints = await _audioService.GetWaveformDataAsync(InputFilePath, 1000);
            Status = "Pronto";
            OnPropertyChanged(nameof(FileName));
        }

        private void ResetSelection() { StartTime = 0; EndTime = Duration; FadeIn = 0; FadeOut = 0; }

        private void TogglePlay()
        {
            if (_wavePlayer != null && _wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                StopAudio();
            }
            else
            {
                PlayWithEffects(StartTime, EndTime);
            }
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
                
                // Aplicar Fade In/Out visual e sonoro
                if (FadeIn > 0 || FadeOut > 0)
                {
                    // Começa silenciando apenas se houver Fade In configurado
                    var fadeProvider = new FadeInOutSampleProvider(provider, FadeIn > 0);
                    if (FadeIn > 0) fadeProvider.BeginFadeIn(FadeIn * 1000);
                    
                    // Lógica para disparar o Fade Out no tempo certo
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
                if (playTime > 0)
                {
                    _stopTimer.Interval = TimeSpan.FromSeconds(playTime);
                    _stopTimer.Start();
                }
            }
            catch (Exception ex) 
            { 
                Status = "Erro ao reproduzir";
                Application.Current.Dispatcher.Invoke(() => PlayIcon = "▶");
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                PlayIcon = "▶";
                _stopTimer.Stop();
            });
        }

        private void StopAudio()
        {
            _stopTimer.Stop();
            if (_wavePlayer != null)
            {
                _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
                _wavePlayer.Stop();
                _wavePlayer.Dispose();
                _wavePlayer = null;
            }
            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
            Application.Current.Dispatcher.Invoke(() => PlayIcon = "▶");
        }

        private async Task ProcessAudio()
        {
            var defaultFileName = $"cut_{System.IO.Path.GetFileName(InputFilePath)}";
            var saveFileDialog = new SaveFileDialog { Filter = "Arquivo MP3 (*.mp3)|*.mp3", FileName = defaultFileName };
            if (saveFileDialog.ShowDialog() == true)
            {
                IsProcessing = true;
                Status = "Exportando...";
                try
                {
                    var segments = new System.Collections.Generic.List<Models.AudioSegment> 
                    { 
                        new Models.AudioSegment { StartTime = StartTime, EndTime = EndTime, FadeIn = FadeIn, FadeOut = FadeOut } 
                    };
                    await _ffmpegService.ProcessAudioAsync(InputFilePath, saveFileDialog.FileName, segments);
                    Status = "Corte salvo com sucesso!";
                }
                catch (Exception ex) { Status = "Erro"; MessageBox.Show("Erro ao salvar: " + ex.Message); }
                finally { IsProcessing = false; }
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
