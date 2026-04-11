using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaSlice.Models;

namespace MediaSlice.Services
{
    public class FFmpegService
    {
        public async Task ProcessMediaAsync(string ffmpegPath, string inputPath, string outputPath, List<MediaSegment> segments, IProgress<double>? progress = null, int audioStreamIndex = 0)
        {
            if (!File.Exists(ffmpegPath)) throw new FileNotFoundException("FFmpeg não encontrado");
            var seg = segments.FirstOrDefault();
            if (seg == null) return;

            bool isVideo = IsVideoFile(inputPath);
            double totalDuration = seg.EndTime - seg.StartTime;
            string filter = $"afade=t=in:ss=0:d={seg.FadeIn.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}," +
                           $"afade=t=out:st={(totalDuration - seg.FadeOut).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}:d={seg.FadeOut.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";

            string codecArgs = isVideo ? "-c:v copy -c:a aac -b:a 192k" : "-c:a libmp3lame -b:a 320k";
            string mapArgs = isVideo ? $"-map 0:v -map 0:{audioStreamIndex}" : "";

            string args = $"-y -ss {seg.StartTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-to {seg.EndTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" {mapArgs} -af \"{filter}\" {codecArgs} \"{outputPath}\"";

            await RunFFmpegWithProgressAsync(ffmpegPath, args, totalDuration, progress);
        }

        public async Task ExtractAudioAsync(string ffmpegPath, string inputPath, string outputPath, double startTime, double endTime, IProgress<double>? progress = null)
        {
            double duration = endTime - startTime;
            string args = $"-y -ss {startTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-to {endTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
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
                    // Parse: time=00:00:05.12
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

        public async Task<(List<AudioStreamInfo> Streams, double Duration)> GetMediaInfoAsync(string ffmpegPath, string inputPath)
        {
            var streams = new List<AudioStreamInfo>();
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

            var audioMatches = Regex.Matches(output, @"Stream #0:(\d+)(?:\((.*?)\))?.*?: Audio: (\w+)");
            foreach (Match match in audioMatches)
            {
                int idx = int.Parse(match.Groups[1].Value);
                string langTag = match.Groups[2].Value.ToLower();
                string langName = langTag switch { "por" => "Português", "ptb" => "Português", "eng" => "Inglês", "spa" => "Espanhol", _ => string.IsNullOrEmpty(langTag) ? "Padrão" : langTag.ToUpper() };
                streams.Add(new AudioStreamInfo { Index = idx, Language = langName, Codec = match.Groups[3].Value });
            }
            return (streams, duration);
        }
    }
}
