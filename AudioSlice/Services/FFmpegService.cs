using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AudioSlice.Models;

namespace AudioSlice.Services
{
    public class FFmpegService
    {
        public async Task ProcessAudioAsync(string ffmpegPath, string inputPath, string outputPath, List<AudioSegment> segments)
        {
            if (!File.Exists(ffmpegPath))
                throw new FileNotFoundException("FFmpeg não encontrado em: " + ffmpegPath);

            var seg = segments.FirstOrDefault();
            if (seg == null) return;

            // Duração total do trecho cortado
            double cutDuration = seg.EndTime - seg.StartTime;
            
            // Garantir que os fades não sejam maiores que o próprio clipe
            double actualFadeIn = Math.Min(seg.FadeIn, cutDuration / 2);
            double actualFadeOut = Math.Min(seg.FadeOut, cutDuration / 2);
            
            // O Fade Out deve começar em: (Duração Total - Tempo do FadeOut)
            double fadeOutStart = Math.Max(0, cutDuration - actualFadeOut);

            // Construção do filtro afade
            string filter = $"afade=t=in:ss=0:d={actualFadeIn.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}," +
                           $"afade=t=out:st={fadeOutStart.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}:d={actualFadeOut.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";

            string args = $"-y -ss {seg.StartTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-to {seg.EndTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-i \"{inputPath}\" -map_metadata 0 -af \"{filter}\" -c:a libmp3lame -b:a 320k \"{outputPath}\"";

            await RunFFmpegAsync(ffmpegPath, args);
        }

        private Task RunFFmpegAsync(string ffmpegPath, string arguments)
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

            process.Exited += (s, e) =>
            {
                if (process.ExitCode == 0) tcs.SetResult(true);
                else tcs.SetException(new Exception($"FFmpeg erro {process.ExitCode}"));
                process.Dispose();
            };

            process.Start();
            return tcs.Task;
        }
    }
}
