using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using PFD.Shared.Models;

namespace PFD.Blazor.Services;

/// <summary>
/// Azure Speech Service implementation for audio transcription.
/// Adapted from VoicePal's AzureSpeechEngine.
/// </summary>
public class AzureSpeechService : IAzureSpeechService
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private readonly ILogger<AzureSpeechService> _logger;

    public AzureSpeechService(string subscriptionKey, string region, ILogger<AzureSpeechService> logger)
    {
        _subscriptionKey = subscriptionKey;
        _region = region;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region);

    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        if (!IsConfigured)
        {
            return (false, "Azure Speech not configured. Please set AzureSpeech:Key and AzureSpeech:Region.");
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var tokenEndpoint = $"https://{_region}.api.cognitive.microsoft.com/sts/v1.0/issueToken";
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            request.Content = new StringContent("");

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Connection successful!");
            }

            var statusCode = (int)response.StatusCode;
            return statusCode switch
            {
                401 => (false, "Invalid subscription key."),
                403 => (false, "Access forbidden. Check subscription status."),
                404 => (false, "Invalid region."),
                _ => (false, $"Connection failed: {response.ReasonPhrase}")
            };
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Speech connection test failed");
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    public async Task<TranscribeAudioResponse> TranscribeAsync(
        byte[] audioData,
        string mimeType = "audio/webm",
        string? locale = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return TranscribeAudioResponse.Failed("Azure Speech not configured", "NOT_CONFIGURED");
        }

        if (audioData == null || audioData.Length == 0)
        {
            return TranscribeAudioResponse.Failed("No audio data provided", "EMPTY_AUDIO");
        }

        try
        {
            _logger.LogInformation("Transcribing audio: {Length} bytes, type: {MimeType}", audioData.Length, mimeType);

            // Convert audio to WAV format if needed
            byte[] wavData;
            if (mimeType.Contains("webm") || mimeType.Contains("ogg") || mimeType.Contains("opus"))
            {
                wavData = await ConvertToWavAsync(audioData, ct);
            }
            else if (mimeType.Contains("wav"))
            {
                wavData = audioData;
            }
            else
            {
                // Try to convert anyway
                wavData = await ConvertToWavAsync(audioData, ct);
            }

            if (wavData == null || wavData.Length < 44)
            {
                return TranscribeAudioResponse.Failed("Failed to process audio", "CONVERSION_FAILED");
            }

            // Extract PCM and resample to 16kHz
            var pcmData = ExtractPcmFromWav(wavData);
            if (pcmData == null || pcmData.Length == 0)
            {
                return TranscribeAudioResponse.Failed("Failed to extract audio data", "PCM_EXTRACTION_FAILED");
            }

            _logger.LogInformation("Prepared {Length} bytes of PCM data for Azure", pcmData.Length);

            // Create Azure Speech config
            var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            config.SpeechRecognitionLanguage = locale ?? "en-US";
            config.OutputFormat = OutputFormat.Detailed;

            // Create audio stream
            using var pushStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
            pushStream.Write(pcmData);
            pushStream.Close();

            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(config, audioConfig);

            // Perform recognition
            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason switch
            {
                ResultReason.RecognizedSpeech => TranscribeAudioResponse.Ok(
                    result.Text,
                    GetConfidence(result),
                    result.Properties.GetProperty(PropertyId.SpeechServiceConnection_RecoLanguage) ?? locale),

                ResultReason.NoMatch => TranscribeAudioResponse.Failed("No speech detected", "NO_SPEECH"),

                ResultReason.Canceled => HandleCancellation(result),

                _ => TranscribeAudioResponse.Failed($"Unexpected result: {result.Reason}", "UNKNOWN_ERROR")
            };
        }
        catch (OperationCanceledException)
        {
            return TranscribeAudioResponse.Failed("Transcription cancelled", "CANCELLED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return TranscribeAudioResponse.Failed($"Transcription failed: {ex.Message}", "TRANSCRIPTION_ERROR");
        }
    }

    private async Task<byte[]> ConvertToWavAsync(byte[] inputData, CancellationToken ct)
    {
        // For WebM/Opus, we need to write to a temp file since MediaFoundationReader requires a file path
        string? tempInputFile = null;
        string? tempOutputFile = null;

        try
        {
            // Write input to temp file
            tempInputFile = Path.GetTempFileName() + ".webm";
            await File.WriteAllBytesAsync(tempInputFile, inputData, ct);

            // Try MediaFoundationReader for WebM/Opus
            try
            {
                using var reader = new MediaFoundationReader(tempInputFile);

                // Resample to 16kHz mono
                var targetFormat = new WaveFormat(16000, 16, 1);
                using var resampler = new MediaFoundationResampler(reader, targetFormat);
                resampler.ResamplerQuality = 60;

                tempOutputFile = Path.GetTempFileName() + ".wav";
                WaveFileWriter.CreateWaveFile(tempOutputFile, resampler);

                return await File.ReadAllBytesAsync(tempOutputFile, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MediaFoundationReader failed, returning original data");
                return inputData;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio conversion failed");
            return inputData; // Return original and hope for the best
        }
        finally
        {
            // Clean up temp files
            if (tempInputFile != null && File.Exists(tempInputFile))
            {
                try { File.Delete(tempInputFile); } catch { }
            }
            if (tempOutputFile != null && File.Exists(tempOutputFile))
            {
                try { File.Delete(tempOutputFile); } catch { }
            }
        }
    }

    private static double? GetConfidence(SpeechRecognitionResult result)
    {
        try
        {
            var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
            if (!string.IsNullOrEmpty(json) && json.Contains("confidence"))
            {
                // Simple extraction - could use JSON parser
                var confIndex = json.IndexOf("\"confidence\":");
                if (confIndex > 0)
                {
                    var start = confIndex + 13;
                    var end = json.IndexOfAny(new[] { ',', '}' }, start);
                    if (end > start && double.TryParse(json[start..end], out var conf))
                    {
                        return conf;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private TranscribeAudioResponse HandleCancellation(SpeechRecognitionResult result)
    {
        var cancellation = CancellationDetails.FromResult(result);

        if (cancellation.Reason == CancellationReason.Error)
        {
            var (message, code) = cancellation.ErrorCode switch
            {
                CancellationErrorCode.AuthenticationFailure => ("Authentication failed", "AUTH_FAILED"),
                CancellationErrorCode.ConnectionFailure => ("Connection failed", "CONNECTION_FAILED"),
                CancellationErrorCode.ServiceTimeout => ("Service timed out", "TIMEOUT"),
                _ => (cancellation.ErrorDetails, "AZURE_ERROR")
            };

            _logger.LogError("Azure Speech error: {Code} - {Details}", cancellation.ErrorCode, cancellation.ErrorDetails);
            return TranscribeAudioResponse.Failed(message, code);
        }

        return TranscribeAudioResponse.Failed("Recognition cancelled", "CANCELLED");
    }

    private static byte[]? ExtractPcmFromWav(byte[] wavBytes)
    {
        try
        {
            if (wavBytes.Length < 44) return null;
            if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F')
                return null;

            var channels = BitConverter.ToInt16(wavBytes, 22);
            var sampleRate = BitConverter.ToInt32(wavBytes, 24);
            var bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

            if (channels <= 0) channels = 1;
            if (sampleRate <= 0) sampleRate = 44100;
            if (bitsPerSample <= 0) bitsPerSample = 16;

            // Find data chunk
            var dataOffset = -1;
            var dataSize = 0;
            for (var i = 12; i < wavBytes.Length - 8; i++)
            {
                if (wavBytes[i] == 'd' && wavBytes[i + 1] == 'a' && wavBytes[i + 2] == 't' && wavBytes[i + 3] == 'a')
                {
                    dataSize = BitConverter.ToInt32(wavBytes, i + 4);
                    dataOffset = i + 8;
                    break;
                }
            }

            if (dataOffset < 0) return null;

            var availableData = Math.Min(dataSize, wavBytes.Length - dataOffset);
            var bytesPerSample = bitsPerSample / 8;
            var numSamples = availableData / bytesPerSample / channels;

            // Convert to float samples (mono)
            var floatSamples = new float[numSamples];
            for (var i = 0; i < numSamples; i++)
            {
                var offset = dataOffset + (i * bytesPerSample * channels);
                if (offset + bytesPerSample > wavBytes.Length) break;

                if (bitsPerSample == 16)
                    floatSamples[i] = BitConverter.ToInt16(wavBytes, offset) / 32768f;
                else if (bitsPerSample == 32)
                    floatSamples[i] = BitConverter.ToInt32(wavBytes, offset) / 2147483648f;
                else if (bitsPerSample == 8)
                    floatSamples[i] = (wavBytes[offset] - 128) / 128f;
            }

            // Resample to 16kHz if necessary
            if (sampleRate != 16000)
            {
                floatSamples = Resample(floatSamples, sampleRate, 16000);
            }

            // Convert to 16-bit PCM bytes
            var pcmBytes = new byte[floatSamples.Length * 2];
            for (var i = 0; i < floatSamples.Length; i++)
            {
                var sample = (short)(Math.Clamp(floatSamples[i], -1f, 1f) * 32767f);
                pcmBytes[i * 2] = (byte)(sample & 0xFF);
                pcmBytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return pcmBytes;
        }
        catch
        {
            return null;
        }
    }

    private static float[] Resample(float[] input, int inputRate, int outputRate)
    {
        var ratio = (double)outputRate / inputRate;
        var outputLength = (int)(input.Length * ratio);
        var output = new float[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var srcIndex = i / ratio;
            var srcIndexInt = (int)srcIndex;
            var frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < input.Length)
            {
                output[i] = (float)(input[srcIndexInt] * (1 - frac) + input[srcIndexInt + 1] * frac);
            }
            else if (srcIndexInt < input.Length)
            {
                output[i] = input[srcIndexInt];
            }
        }

        return output;
    }
}

