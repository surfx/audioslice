using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;

namespace MediaSlice.Services
{
    public class MediaService
    {
        public async Task<float[]> GetWaveformDataAsync(string filePath, int points, string ffmpegPath = "", int audioStreamIndex = 0, double duration = 0)
        {
            return await Task.Run(() =>
            {
                var waveform = new float[points];
                if (!File.Exists(filePath)) return waveform;

                try
                {
                    // Se o arquivo for WAV (extraído pelo FFmpeg no ViewModel), lemos direto.
                    // Se for MP3, a NAudio lê nativamente.
                    // Apenas se for um vídeo e não tivermos o ffmpegPath vazio é que tentaríamos extrair (mas o ViewModel já faz isso).
                    
                    using (var reader = new AudioFileReader(filePath))
                    {
                        long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                        if (totalSamples <= 0) return waveform;

                        int samplesPerPoint = (int)Math.Max(1, totalSamples / points);
                        var buffer = new float[samplesPerPoint];

                        for (int i = 0; i < points; i++)
                        {
                            int read = reader.Read(buffer, 0, samplesPerPoint);
                            if (read > 0)
                            {
                                float max = 0;
                                for (int j = 0; j < read; j++)
                                {
                                    float abs = Math.Abs(buffer[j]);
                                    if (abs > max) max = abs;
                                }
                                waveform[i] = max;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Erro ao ler waveform: " + ex.Message);
                }
                return waveform;
            });
        }

        public double GetDuration(string filePath)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    return reader.TotalTime.TotalSeconds;
                }
            }
            catch { return 0; }
        }
    }
}
