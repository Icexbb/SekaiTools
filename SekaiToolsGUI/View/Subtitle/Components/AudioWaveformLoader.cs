using System.IO;
using NAudio.Wave;

namespace SekaiToolsGUI.View.Subtitle.Components;

internal sealed record AudioWaveformEnvelope(
    short[] Minimum,
    short[] Maximum,
    double BucketMilliseconds);

internal static class AudioWaveformLoader
{
    private const double BucketMilliseconds = 5;

    public static Task<AudioWaveformEnvelope> LoadAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        return Task.Run(() => LoadCore(videoPath, cancellationToken), cancellationToken);
    }

    private static AudioWaveformEnvelope LoadCore(
        string videoPath,
        CancellationToken cancellationToken)
    {
        using var reader = new MediaFoundationReader(videoPath);
        var sampleProvider = reader.ToSampleProvider();
        var format = sampleProvider.WaveFormat;
        var channels = format.Channels;
        var sampleRate = format.SampleRate;
        if (channels <= 0 || sampleRate <= 0)
            throw new InvalidDataException("音频流格式无效");

        var bucketFrameCount = Math.Max(
            1,
            (int)Math.Round(sampleRate * BucketMilliseconds / 1000));
        var minimum = new List<short>();
        var maximum = new List<short>();
        var framesInBucket = 0;
        var bucketMinimum = short.MaxValue;
        var bucketMaximum = short.MinValue;
        var channelIndex = 0;
        var channelSum = 0d;
        var bufferLength = Math.Max(channels, 16 * 1024 / channels * channels);
        var buffer = new float[bufferLength];

        void AddFrame(float monoSample)
        {
            var sample = (short)Math.Round(
                Math.Clamp(monoSample, -1f, 1f) * short.MaxValue);
            bucketMinimum = Math.Min(bucketMinimum, sample);
            bucketMaximum = Math.Max(bucketMaximum, sample);
            framesInBucket++;
            if (framesInBucket < bucketFrameCount)
                return;

            minimum.Add(bucketMinimum);
            maximum.Add(bucketMaximum);
            framesInBucket = 0;
            bucketMinimum = short.MaxValue;
            bucketMaximum = short.MinValue;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = sampleProvider.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            for (var index = 0; index < read; index++)
            {
                channelSum += buffer[index];
                channelIndex++;
                if (channelIndex < channels)
                    continue;

                AddFrame((float)(channelSum / channels));
                channelIndex = 0;
                channelSum = 0;
            }
        }

        if (channelIndex > 0)
            AddFrame((float)(channelSum / channelIndex));
        if (framesInBucket > 0)
        {
            minimum.Add(bucketMinimum);
            maximum.Add(bucketMaximum);
        }

        if (minimum.Count == 0)
            throw new InvalidDataException("视频中没有可用的音频波形");

        return new AudioWaveformEnvelope(
            minimum.ToArray(),
            maximum.ToArray(),
            bucketFrameCount * 1000d / sampleRate);
    }
}
