using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using MediaSlice.Models;

namespace MediaSlice.Services
{
    public class FFmpegService
    {
        private static readonly Dictionary<string, string> _languageCache = new(StringComparer.OrdinalIgnoreCase);

        private string GetFriendlyLanguageName(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) return "Desconhecido";
            if (_languageCache.TryGetValue(langCode, out var cached)) return cached;

            try 
            {
                // Busca a cultura correspondente ao código ISO
                var culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .FirstOrDefault(c => c.ThreeLetterISOLanguageName.Equals(langCode, StringComparison.OrdinalIgnoreCase) || 
                                         c.TwoLetterISOLanguageName.Equals(langCode, StringComparison.OrdinalIgnoreCase));

                if (culture != null)
                {
                    // Força o nome em Português (ex: "Japonês" em vez de "Japanese" ou "日本語")
                    // .NET DisplayName geralmente segue o idioma do SO, mas garantimos o alfabeto latino
                    string name = culture.EnglishName; // EnglishName é sempre alfabeto latino e seguro
                    
                    // Traduções manuais para os principais para garantir PT-BR perfeito na combo
                    var trad = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                        {"Portuguese", "Português"}, {"English", "Inglês"}, {"Japanese", "Japonês"},
                        {"Italian", "Italiano"}, {"Spanish", "Espanhol"}, {"French", "Francês"},
                        {"German", "Alemão"}, {"Russian", "Russo"}, {"Chinese", "Chinês"}
                    };

                    foreach(var item in trad) if (name.Contains(item.Key)) name = item.Value;

                    name = char.ToUpper(name[0]) + name.Substring(1);
                    _languageCache[langCode] = name;
                    return name;
                }
            } catch { }

            return langCode.ToUpper();
        }

        public async Task ProcessMediaAsync(string ffmpegPath, string inputPath, string outputPath, List<MediaSegment> segments, IProgress<double>? progress = null, int audioStreamIndex = 0, List<int>? subtitleIndices = null)
        {
            if (!File.Exists(ffmpegPath)) throw new FileNotFoundException("FFmpeg não encontrado");
            var seg = segments.FirstOrDefault();
            if (seg == null) return;

            bool isVideo = IsVideoFile(inputPath);
            double totalDuration = seg.EndTime - seg.StartTime;
            string filter = $"afade=t=in:ss=0:d={seg.FadeIn.ToString("0.0", CultureInfo.InvariantCulture)}," +
                           $"afade=t=out:st={(totalDuration - seg.FadeOut).ToString("0.0", CultureInfo.InvariantCulture)}:d={seg.FadeOut.ToString("0.0", CultureInfo.InvariantCulture)}";

            string codecArgs = isVideo ? "-c:v copy -c:a aac -b:a 192k" : "-c:a libmp3lame -b:a 320k";
            
            // Mapeamento: Vídeo + Áudio selecionado
            string mapArgs = isVideo ? $"-map 0:v -map 0:{audioStreamIndex}" : "";
            
            // Mapeia apenas as legendas PTBR e ENG se existirem
            if (isVideo && subtitleIndices != null && subtitleIndices.Count > 0)
            {
                foreach (int idx in subtitleIndices) mapArgs += $" -map 0:{idx}";
                mapArgs += " -disposition:s 0"; // Nenhuma ativa por padrão
            }

            if (isVideo) {
                string ext = Path.GetExtension(outputPath).ToLower();
                if (ext == ".mp4" || ext == ".m4v") codecArgs += " -c:s mov_text";
                else codecArgs += " -c:s copy";
            }

            string args = $"-y -ss {seg.StartTime.ToString("0.0", CultureInfo.InvariantCulture)} " +
                          $"-to {seg.EndTime.ToString("0.0", CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" {mapArgs} -af \"{filter}\" {codecArgs} \"{outputPath}\"";

            await RunFFmpegWithProgressAsync(ffmpegPath, args, totalDuration, progress);
        }

        public async Task ExtractAudioAsync(string ffmpegPath, string inputPath, string outputPath, double startTime, double endTime, IProgress<double>? progress = null)
        {
            double duration = endTime - startTime;
            string args = $"-y -ss {startTime.ToString("0.0", CultureInfo.InvariantCulture)} " +
                          $"-to {endTime.ToString("0.0", CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" -vn -c:a libmp3lame -b:a 320k \"{outputPath}\"";
            await RunFFmpegWithProgressAsync(ffmpegPath, args, duration, progress);
        }

        private bool IsVideoFile(string path) => new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" }.Contains(Path.GetExtension(path).ToLower());

        private Task RunFFmpegWithProgressAsync(string ffmpegPath, string arguments, double totalDuration, IProgress<double>? progress = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = ffmpegPath, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true },
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null && progress != null && totalDuration > 0)
                {
                    var match = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+)\.(\d+)");
                    if (match.Success)
                    {
                        double currentTime = int.Parse(match.Groups[1].Value) * 3600 +
                                           int.Parse(match.Groups[2].Value) * 60 +
                                           int.Parse(match.Groups[3].Value) +
                                           int.Parse(match.Groups[4].Value) / 100.0;
                        double percent = (currentTime / totalDuration) * 100;
                        progress.Report(Math.Min(100, percent));
                    }
                }
            };

            process.Exited += (s, e) => { if (process.ExitCode == 0) tcs.SetResult(true); else tcs.SetException(new Exception("FFmpeg falhou")); process.Dispose(); };
            
            process.Start();
            process.BeginErrorReadLine();
            return tcs.Task;
        }

        public async Task<(List<MediaStreamInfo> Streams, double Duration)> GetMediaInfoAsync(string ffmpegPath, string inputPath)
        {
            var streams = new List<MediaStreamInfo>();
            double duration = 0;
            if (!File.Exists(ffmpegPath) || !File.Exists(inputPath)) return (streams, 0);

            var process = new Process {
                StartInfo = new ProcessStartInfo { FileName = ffmpegPath, Arguments = $"-i \"{inputPath}\"", UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }
            };
            process.Start();
            string output = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            var durMatch = Regex.Match(output, @"Duration: (\d+):(\d+):(\d+)\.(\d+)");
            if (durMatch.Success) duration = int.Parse(durMatch.Groups[1].Value) * 3600 + int.Parse(durMatch.Groups[2].Value) * 60 + int.Parse(durMatch.Groups[3].Value);

            var streamMatches = Regex.Matches(output, @"Stream #0:(?<idx>\d+)(?:\((?<lang>.*?)\))?.*?: (?<type>Audio|Subtitle): (?<codec>\w+)");
            
            foreach (Match m in streamMatches)
            {
                var type = m.Groups["type"].Value == "Audio" ? StreamType.Audio : StreamType.Subtitle;
                var langCode = m.Groups["lang"].Value;
                
                streams.Add(new MediaStreamInfo {
                    Index = int.Parse(m.Groups["idx"].Value),
                    LanguageCode = string.IsNullOrEmpty(langCode) ? "und" : langCode,
                    LanguageName = GetFriendlyLanguageName(langCode),
                    Codec = m.Groups["codec"].Value,
                    Type = type
                });
            }

            return (streams, duration);
        }
    }
}