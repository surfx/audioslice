using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaSlice.Models;

namespace MediaSlice.Services
{
    public class FFmpegService
    {
        public async Task ProcessMediaAsync(string ffmpegPath, string inputPath, string outputPath, List<MediaSegment> segments, IProgress<double>? progress = null)
        {
            if (!File.Exists(ffmpegPath))
                throw new FileNotFoundException("FFmpeg não encontrado em: " + ffmpegPath);

            var seg = segments.FirstOrDefault();
            if (seg == null) return;

            bool isVideo = IsVideoFile(inputPath);
            double cutDuration = seg.EndTime - seg.StartTime;
            double actualFadeIn = Math.Min(seg.FadeIn, cutDuration / 2);
            double actualFadeOut = Math.Min(seg.FadeOut, cutDuration / 2);
            double fadeOutStart = Math.Max(0, cutDuration - actualFadeOut);

            string filter = $"afade=t=in:ss=0:d={actualFadeIn.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}," +
                           $"afade=t=out:st={fadeOutStart.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}:d={actualFadeOut.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";

            string codecArgs = isVideo 
                ? "-c:v copy -c:a aac -b:a 192k" 
                : "-c:a libmp3lame -b:a 320k";

            string args = $"-y -ss {seg.StartTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-to {seg.EndTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" -map_metadata 0 -af \"{filter}\" {codecArgs} \"{outputPath}\"";

            await RunFFmpegAsync(ffmpegPath, args, progress);
        }

        public async Task ExtractAudioAsync(string ffmpegPath, string inputPath, string outputPath, double startTime, double endTime, IProgress<double>? progress = null)
        {
            if (!File.Exists(ffmpegPath))
                throw new FileNotFoundException("FFmpeg não encontrado em: " + ffmpegPath);

            double cutDuration = endTime - startTime;

            string args = $"-y -ss {startTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-to {endTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" -map_metadata 0 -vn -c:a libmp3lame -b:a 320k \"{outputPath}\"";

            await RunFFmpegAsync(ffmpegPath, args, progress);
        }

        private bool IsVideoFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" }.Contains(ext);
        }

        private Task RunFFmpegAsync(string ffmpegPath, string arguments, IProgress<double>? progress = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            var duration = ParseDuration(arguments);
            
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var timeMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+)\.(\d+)");
                    if (timeMatch.Success)
                    {
                        double currentTime = int.Parse(timeMatch.Groups[1].Value) * 3600 +
                                            int.Parse(timeMatch.Groups[2].Value) * 60 +
                                            int.Parse(timeMatch.Groups[3].Value) +
                                            int.Parse(timeMatch.Groups[4].Value) / 10.0;
                        if (duration > 0)
                            progress?.Report(Math.Min(99, (currentTime / duration) * 100));
                    }
                }
            };

            process.Exited += (s, e) =>
            {
                progress?.Report(100);
                if (process.ExitCode == 0) tcs.SetResult(true);
                else tcs.SetException(new Exception($"FFmpeg erro {process.ExitCode}"));
                process.Dispose();
            };

            process.Start();
            process.BeginErrorReadLine();
            return tcs.Task;
        }

        private double ParseDuration(string arguments)
        {
            try
            {
                var ssMatch = System.Text.RegularExpressions.Regex.Match(arguments, @"-ss\s+([\d.]+)");
                var toMatch = System.Text.RegularExpressions.Regex.Match(arguments, @"-to\s+([\d.]+)");
                if (ssMatch.Success && toMatch.Success)
                {
                    double start = double.Parse(ssMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double end = double.Parse(toMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    return end - start;
                }
            }
            catch { }
            return 0;
        }
    }
}
