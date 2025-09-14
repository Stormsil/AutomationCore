// VideoRecorder (extracted from monolith)
// Специализированный сервис для записи видео
using System;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Models;
using AutomationCore.Infrastructure.Capture;
using OpenCvSharp;

namespace AutomationCore.Features.Recording
{
    /// <summary>
    /// Кодеки для видео записи
    /// </summary>
    public enum VideoCodec
    {
        H264,
        MJPEG,
        XVID,
        MP4V
    }

    /// <summary>
    /// Сервис записи видео из capture session
    /// </summary>
    public sealed class VideoRecorder : IDisposable
    {
        private readonly LegacyCaptureSession _session;
        private readonly string _outputPath;
        private readonly int _fps;
        private readonly VideoCodec _codec;
        private VideoWriter? _writer;
        private CancellationTokenSource? _recordingCts;
        private Task? _recordingTask;
        private DateTime _recordingStartTime;

        public bool IsRecording { get; private set; }
        public TimeSpan Duration => IsRecording ? DateTime.UtcNow - _recordingStartTime : TimeSpan.Zero;
        public long FramesRecorded { get; private set; }

        public VideoRecorder(LegacyCaptureSession session, string outputPath, int fps = 30, VideoCodec codec = VideoCodec.H264)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _outputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
            _fps = fps > 0 ? fps : throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be positive");
            _codec = codec;
        }

        public async Task StartAsync()
        {
            if (IsRecording) return;

            var firstFrame = await _session.GetNextFrameAsync().ConfigureAwait(false);
            if (firstFrame == null)
                throw new InvalidOperationException("No frames available from capture session");

            var fourcc = _codec switch
            {
                VideoCodec.H264 => VideoWriter.FourCC('H', '2', '6', '4'),
                VideoCodec.MJPEG => VideoWriter.FourCC('M', 'J', 'P', 'G'),
                VideoCodec.XVID => VideoWriter.FourCC('X', 'V', 'I', 'D'),
                _ => VideoWriter.FourCC('M', 'P', '4', 'V')
            };

            _writer = new VideoWriter(_outputPath, fourcc, _fps,
                new OpenCvSharp.Size(firstFrame.Width, firstFrame.Height));

            if (!_writer.IsOpened())
                throw new InvalidOperationException($"Failed to open video writer for {_outputPath}");

            IsRecording = true;
            _recordingStartTime = DateTime.UtcNow;
            FramesRecorded = 0;
            _recordingCts = new CancellationTokenSource();

            _recordingTask = Task.Run(async () => await RecordingLoop(_recordingCts.Token).ConfigureAwait(false));
        }

        private async Task RecordingLoop(CancellationToken cancellationToken)
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _fps);
            var lastFrameTime = DateTime.UtcNow;

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsRecording)
                {
                    var frame = await _session.GetNextFrameAsync(cancellationToken).ConfigureAwait(false);
                    if (frame != null && _writer != null)
                    {
                        // Конвертируем CaptureFrame в OpenCV Mat
                        using var mat = ConvertFrameToMat(frame);
                        _writer.Write(mat);
                        FramesRecorded++;

                        // Ограничиваем FPS
                        var elapsed = DateTime.UtcNow - lastFrameTime;
                        if (elapsed < frameInterval)
                        {
                            var delay = frameInterval - elapsed;
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        }
                        lastFrameTime = DateTime.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
            }
            catch (Exception ex)
            {
                // TODO: Добавить логирование ошибок
                Console.WriteLine($"Recording error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (!IsRecording) return;

            IsRecording = false;
            _recordingCts?.Cancel();

            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ожидаемое исключение при отмене
                }
            }

            _writer?.Release();
            _writer?.Dispose();
            _writer = null;

            _recordingCts?.Dispose();
            _recordingCts = null;
            _recordingTask = null;
        }

        private static Mat ConvertFrameToMat(Core.Models.CaptureFrame frame)
        {
            // TODO: Реализовать конвертацию из CaptureFrame в OpenCV Mat
            // Пока заглушка для компиляции
            return new Mat(frame.Height, frame.Width, MatType.CV_8UC3);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}