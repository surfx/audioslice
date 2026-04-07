using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;

namespace AudioSlice.Services
{
    public class AudioService
    {
        public async Task<float[]> GetWaveformDataAsync(string filePath, int points)
        {
            return await Task.Run(() =>
            {
                var waveform = new float[points];
                try
                {
                    using (var reader = new AudioFileReader(filePath))
                    {
                        var sampleProvider = reader.ToSampleProvider();
                        int samplesPerPoint = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8) / points);
                        if (samplesPerPoint <= 0) samplesPerPoint = 1;

                        var buffer = new float[samplesPerPoint];
                        for (int i = 0; i < points; i++)
                        {
                            int read = reader.Read(buffer, 0, samplesPerPoint);
                            if (read > 0)
                            {
                                waveform[i] = buffer.Take(read).Max(Math.Abs);
                            }
                        }
                    }
                }
                catch { /* Ignorar erros de leitura */ }
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
