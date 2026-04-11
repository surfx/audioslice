using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text.RegularExpressions;
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
        private readonly DispatcherTimer _playbackTimer;
        private readonly string _settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.dat");
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
        private bool _isLoading;
        private double _exportTimePosition = -1;
        private string _extractedAudioFile = "";
        private string _currentSubtitleText = "";
        private readonly List<SubtitleItem> _activeSubtitles = new();
        private List<MediaStreamInfo> _allStreams = new();
        private ObservableCollection<MediaStreamInfo> _audioStreams = new();
        private MediaStreamInfo? _selectedAudioStream;
        private double _volume = 1.0;

        public event Action<double>? RequestSync;

        public string InputFilePath { get => _inputFilePath; set { _inputFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFileLoaded)); OnPropertyChanged(nameof(IsVideo)); OnPropertyChanged(nameof(FileName)); } }
        public string FFmpegPath { get => _ffmpegPath; set { _ffmpegPath = value; OnPropertyChanged(); SaveSettings(); } }
        public bool IsFileLoaded => !string.IsNullOrEmpty(InputFilePath);
        public string FileName => IsFileLoaded ? Path.GetFileName(InputFilePath) : "";
        public bool IsVideo => IsFileLoaded && (new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" }.Contains(Path.GetExtension(InputFilePath).ToLower()));
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public bool IsProcessing { get => _isProcessing; set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); } }
        public bool CanEdit => !IsProcessing && IsFileLoaded;
        public float[] WaveformPoints { get => _waveformPoints; set { _waveformPoints = value; OnPropertyChanged(); } }
        public double Duration { get => _duration; set { _duration = value; OnPropertyChanged(); } }
        public double StartTime { get => _startTime; set { _startTime = Math.Max(0, Math.Min(value, EndTime - 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(StartTimeFormatted)); } }
        public double EndTime { get => _endTime; set { _endTime = Math.Min(Duration, Math.Max(value, StartTime + 0.1)); OnPropertyChanged(); OnPropertyChanged(nameof(EndTimeFormatted)); } }
        public double FadeIn { get => _fadeIn; set { _fadeIn = value; OnPropertyChanged(); } }
        public double FadeOut { get => _fadeOut; set { _fadeOut = value; OnPropertyChanged(); } }
        public string PlayIcon { get => _playIcon; set { _playIcon = value; OnPropertyChanged(); } }
        public double ExportProgress { get => _exportProgress; set { _exportProgress = value; OnPropertyChanged(); } }
        public double ExportTimePosition { get => _exportTimePosition; set { _exportTimePosition = value; OnPropertyChanged(); } }
        public string CurrentSubtitleText { get => _currentSubtitleText; set { _currentSubtitleText = value; OnPropertyChanged(); } }
        public ObservableCollection<MediaStreamInfo> AudioStreams { get => _audioStreams; set { _audioStreams = value; OnPropertyChanged(); } }
        
        public MediaStreamInfo? SelectedAudioStream 
        { 
            get => _selectedAudioStream; 
            set { if (_selectedAudioStream != value) { double pos = _audioFileReader?.CurrentTime.TotalSeconds ?? 0; bool wasPlaying = _wavePlayer?.PlaybackState == PlaybackState.Playing; StopAudio(); _selectedAudioStream = value; OnPropertyChanged(); if (IsFileLoaded) _ = LoadAudioDataAsync(true, wasPlaying, pos); } } 
        }

        public double Volume { get => _volume; set { _volume = value; OnPropertyChanged(); if (_wavePlayer != null) _wavePlayer.Volume = (float)_volume; } }
        public bool ShowProgress { get => _showProgress; set { _showProgress = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
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
            _ffmpegPath = _defaultFFmpeg; LoadSettings();
            SelectFileCommand = new RelayCommand(_ => SelectFile());
            BrowseFFmpegCommand = new RelayCommand(_ => BrowseFFmpeg());
            ResetFFmpegCommand = new RelayCommand(_ => FFmpegPath = _defaultFFmpeg);
            PlayCommand = new RelayCommand(_ => TogglePlay(), _ => CanEdit);
            ProcessCommand = new RelayCommand(async _ => await ProcessMedia(), _ => CanEdit);
            ExportAudioCommand = new RelayCommand(async _ => await ExportAudioOnly(), _ => CanEdit && IsVideo);
            ResetCommand = new RelayCommand(_ => ResetSelection());
            ToggleFadeInCommand = new RelayCommand(_ => IsFadeInPopupOpen = !IsFadeInPopupOpen);
            ToggleFadeOutCommand = new RelayCommand(_ => IsFadeOutPopupOpen = !IsFadeOutPopupOpen);
            
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _playbackTimer.Tick += (s, e) => {
                if (_audioFileReader != null) {
                    double pos = _audioFileReader.CurrentTime.TotalSeconds;
                    
                    if (pos >= EndTime - 0.01) {
                        StopAudio();
                        return;
                    }

                    ExportTimePosition = pos;
                    UpdateSubtitle(pos);
                    // Durante o play, o vídeo roda sozinho via MediaElement.
                    // Só precisamos sincronizar se estivermos em modo de exportação (visualização do corte).
                    if (IsProcessing) RequestSync?.Invoke(pos);
                }
            };
        }

        public void LoadFile(string filePath) { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) { InputFilePath = filePath; Status = "Carregando..."; AudioStreams.Clear(); _selectedAudioStream = null; _allStreams.Clear(); _ = LoadAudioDataAsync(false, false, 0); } }
        public void SelectFile() { var dialog = new OpenFileDialog { Filter = "Mídias|*.mp3;*.mp4;*.mkv;*.avi;*.mov;*.wav|Todos|*.*" }; if (dialog.ShowDialog() == true) LoadFile(dialog.FileName); }
        private void BrowseFFmpeg() { var dialog = new OpenFileDialog { Filter = "ffmpeg.exe|ffmpeg.exe" }; if (dialog.ShowDialog() == true) FFmpegPath = dialog.FileName; }
        private void LoadSettings() { if (File.Exists(_settingsFile)) try { _ffmpegPath = File.ReadAllText(_settingsFile); } catch { } }
        private void SaveSettings() { try { File.WriteAllText(_settingsFile, FFmpegPath); } catch { } }

        private async Task LoadAudioDataAsync(bool isStreamSwitch, bool wasPlaying, double resumePos)
        {
            IsLoading = true; Status = isStreamSwitch ? "Trocando áudio..." : "Carregando mídia...";
            if (!isStreamSwitch) WaveformPoints = Array.Empty<float>();
            _activeSubtitles.Clear(); CurrentSubtitleText = "";
            try {
                if (IsVideo) {
                    if (!File.Exists(FFmpegPath)) { Status = "Configure o FFmpeg!"; IsLoading = false; return; }
                    var info = await _ffmpegService.GetMediaInfoAsync(FFmpegPath, InputFilePath);
                    Duration = info.Duration;
                    _allStreams = info.Streams;
                    
                    if (AudioStreams.Count == 0) foreach (var s in _allStreams.Where(x => x.Type == StreamType.Audio)) AudioStreams.Add(s);
                    if (SelectedAudioStream == null) { _selectedAudioStream = AudioStreams.FirstOrDefault(s => s.IsPortuguese) ?? AudioStreams.FirstOrDefault(); OnPropertyChanged(nameof(SelectedAudioStream)); }
                    
                    int idx = SelectedAudioStream?.Index ?? 0;
                    _extractedAudioFile = Path.Combine(Path.GetTempPath(), $"mediaslice_{Guid.NewGuid()}.wav");
                    var audioProc = Process.Start(new ProcessStartInfo { FileName = FFmpegPath, Arguments = $"-y -i \"{InputFilePath}\" -map 0:{idx} -vn -ac 1 -ar 22050 -c:a pcm_s16le -f wav \"{_extractedAudioFile}\"", UseShellExecute = false, CreateNoWindow = true });
                    await audioProc!.WaitForExitAsync();

                    if (SelectedAudioStream != null && !SelectedAudioStream.IsPortuguese) {
                        var ptSub = _allStreams.FirstOrDefault(s => s.Type == StreamType.Subtitle && s.IsPortuguese);
                        var enSub = _allStreams.FirstOrDefault(s => s.Type == StreamType.Subtitle && s.IsEnglish);
                        var subToExtract = ptSub ?? enSub;
                        
                        if (subToExtract != null) {
                            string subFile = Path.Combine(Path.GetTempPath(), $"sub_{Guid.NewGuid()}.srt");
                            var subProc = Process.Start(new ProcessStartInfo { FileName = FFmpegPath, Arguments = $"-y -i \"{InputFilePath}\" -map 0:{subToExtract.Index} \"{subFile}\"", UseShellExecute = false, CreateNoWindow = true });
                            await subProc!.WaitForExitAsync();
                            if (File.Exists(subFile)) { ParseSubtitle(subFile); try { File.Delete(subFile); } catch {} }
                        }
                    }
                } else { Duration = _mediaService.GetDuration(InputFilePath); _extractedAudioFile = InputFilePath; }
                
                if (Duration <= 0) Duration = 1;
                if (!isStreamSwitch) { StartTime = 0; EndTime = Duration; }
                if (File.Exists(_extractedAudioFile)) { WaveformPoints = await _mediaService.GetWaveformDataAsync(_extractedAudioFile, 2000, "", 0, Duration); Status = "Pronto";
                    if (wasPlaying || !isStreamSwitch) { double startAt = isStreamSwitch ? resumePos : 0; RequestSync?.Invoke(startAt); PlayWithEffects(startAt, EndTime); }
                }
            } catch { Status = "Erro"; } finally { IsLoading = false; OnPropertyChanged(nameof(FileName)); }
        }

        private void ParseSubtitle(string path)
        {
            try {
                string content = File.ReadAllText(path);
                var matches = Regex.Matches(content, @"(?<idx>\d+)\s+(?<start>\d{2}:\d{2}:\d{2},\d{3}) --> (?<end>\d{2}:\d{2}:\d{2},\d{3})\s+(?<text>.*?)(?=\n\n|\n\s*\n|$)", RegexOptions.Singleline);
                foreach (Match m in matches) {
                    _activeSubtitles.Add(new SubtitleItem {
                        Start = TimeSpan.Parse(m.Groups["start"].Value.Replace(',', '.')),
                        End = TimeSpan.Parse(m.Groups["end"].Value.Replace(',', '.')),
                        Text = Regex.Replace(m.Groups["text"].Value, @"<[^>]*>", "").Trim()
                    });
                }
            } catch { }
        }

        private void UpdateSubtitle(double seconds)
        {
            if (_activeSubtitles.Count == 0) return;
            var ts = TimeSpan.FromSeconds(seconds);
            var sub = _activeSubtitles.FirstOrDefault(s => ts >= s.Start && ts <= s.End);
            CurrentSubtitleText = sub?.Text ?? "";
        }

        private void ResetSelection() { StartTime = 0; EndTime = Duration; FadeIn = 0; FadeOut = 0; }
        private void TogglePlay() { if (_wavePlayer?.PlaybackState == PlaybackState.Playing) StopAudio(); else { Status = "Pronto"; PlayWithEffects(_audioFileReader?.CurrentTime.TotalSeconds ?? StartTime, EndTime); } }

        public void PlayWithEffects(double startPos, double endPos)
        {
            if (!IsFileLoaded || string.IsNullOrEmpty(_extractedAudioFile) || !File.Exists(_extractedAudioFile)) return;
            
            // Sincronização visual imediata ANTES de reiniciar o player
            ExportTimePosition = startPos;
            RequestSync?.Invoke(startPos);
            UpdateSubtitle(startPos);

            StopAudio();
            try {
                _audioFileReader = new AudioFileReader(_extractedAudioFile);
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(startPos);
                ISampleProvider provider = _audioFileReader.ToSampleProvider();
                if (FadeIn > 0) { var f = new FadeInOutSampleProvider(provider, true); f.BeginFadeIn(FadeIn * 1000); provider = f; }
                _wavePlayer = new WaveOutEvent { Volume = (float)Volume };
                _wavePlayer.Init(provider); _wavePlayer.Play(); PlayIcon = "⏸";
                _playbackTimer.Start();
            } catch { }
        }

        private void StopAudio() { _playbackTimer.Stop(); _wavePlayer?.Stop(); _wavePlayer?.Dispose(); _wavePlayer = null; _audioFileReader?.Dispose(); _audioFileReader = null; PlayIcon = "▶"; CurrentSubtitleText = ""; ExportTimePosition = -1; }

        private async Task ProcessMedia()
        {
            var saveDialog = new SaveFileDialog { Filter = "Original|*" + Path.GetExtension(InputFilePath), FileName = "corte_" + FileName };
            if (saveDialog.ShowDialog() == true) {
                StopAudio(); IsProcessing = true; ShowProgress = true; ExportProgress = 0; ExportTimePosition = StartTime;
                var subtitleIndices = _allStreams.Where(s => s.Type == StreamType.Subtitle && (s.IsPortuguese || s.IsEnglish)).Select(s => s.Index).ToList();
                var progress = new Progress<double>(p => { ExportProgress = p; ExportTimePosition = StartTime + (p / 100.0 * (EndTime - StartTime)); RequestSync?.Invoke(ExportTimePosition); });
                try {
                    await _ffmpegService.ProcessMediaAsync(FFmpegPath, InputFilePath, saveDialog.FileName, new() { new() { StartTime = StartTime, EndTime = EndTime, FadeIn = FadeIn, FadeOut = FadeOut } }, progress, SelectedAudioStream?.Index ?? 0, subtitleIndices);
                    Status = "Concluído!";
                } catch { Status = "Erro"; } finally { IsProcessing = false; ShowProgress = false; ExportTimePosition = -1; }
            }
        }

        private async Task ExportAudioOnly()
        {
            var saveDialog = new SaveFileDialog { Filter = "MP3|*.mp3", FileName = "audio_" + Path.GetFileNameWithoutExtension(FileName) + ".mp3" };
            if (saveDialog.ShowDialog() == true) {
                StopAudio(); IsProcessing = true; ShowProgress = true; ExportProgress = 0;
                var progress = new Progress<double>(p => { ExportProgress = p; ExportTimePosition = StartTime + (p / 100.0 * (EndTime - StartTime)); });
                try { await _ffmpegService.ExtractAudioAsync(FFmpegPath, InputFilePath, saveDialog.FileName, StartTime, EndTime, progress); Status = "Concluído!"; } catch { Status = "Erro"; }
                finally { IsProcessing = false; ShowProgress = false; ExportTimePosition = -1; }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _e; private readonly Predicate<object?>? _c;
        public RelayCommand(Action<object?> e, Predicate<object?>? c = null) { _e = e; _c = c; }
        public bool CanExecute(object? p) => _c == null || _c(p);
        public void Execute(object? p) => _e(p);
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}