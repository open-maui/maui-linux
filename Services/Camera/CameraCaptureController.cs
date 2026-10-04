// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>A running camera pipeline as the capture controller sees it (tests substitute one).</summary>
internal interface ICameraSession : IDisposable
{
    event Action? FrameArrived;

    event Action<string>? Failed;

    long FrameCount { get; }

    SKBitmap? SnapshotLatest();

    Task<bool> StopAsync(bool finish, TimeSpan timeout);
}

/// <summary>Where a capture is in its flow.</summary>
internal enum CameraCaptureState
{
    /// <summary>The camera is opening; no frame yet.</summary>
    Starting,

    /// <summary>The live preview is showing.</summary>
    Live,

    /// <summary>A photo was taken and is shown for the user to keep or retake.</summary>
    Review,

    /// <summary>A video is being recorded.</summary>
    Recording,

    /// <summary>The recording is being finished (the file completed).</summary>
    Finishing,

    /// <summary>The camera failed; <see cref="CameraCaptureController.ErrorMessage"/> says why.</summary>
    Failed,

    /// <summary>The capture ended (kept or cancelled).</summary>
    Done,
}

/// <summary>
/// The capture flow behind the camera dialog, without the drawing: the live preview, then for a
/// photo Take / Retake / Use photo (as Windows' camera capture UI), for a video Record / Stop.
/// Completes <see cref="Result"/> with the captured file, or null when cancelled. Files are named
/// as the Windows build names them: capture.jpg / capture.mp4 in the cache directory's
/// ".Microsoft.Maui.Media.MediaPicker" folder, made unique.
/// </summary>
internal sealed class CameraCaptureController : IDisposable
{
    internal const string CacheFolderName = ".Microsoft.Maui.Media.MediaPicker";
    internal const string CacheFileName = "capture";

    /// <summary>JPEG quality of a captured photo.</summary>
    internal const int PhotoQuality = 95;

    private readonly object _gate = new();
    private readonly CameraSource _source;
    private readonly string _outputDirectory;
    private readonly CameraVideoProfile? _profile;
    private readonly Func<string, ICameraSession> _sessionFactory;
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ICameraSession? _session;
    private SKBitmap? _review;
    private SKBitmap? _frame;
    private long _frameNumber = -1;
    private string? _recordingPath;
    private DateTime _recordingStarted;

    public CameraCaptureController(CameraSource source, bool photo, string outputDirectory, CameraVideoProfile? profile, Func<string, ICameraSession> sessionFactory)
    {
        if (!photo && profile == null)
            throw new ArgumentNullException(nameof(profile), "A video capture needs a recording profile.");
        _source = source;
        IsPhoto = photo;
        _outputDirectory = outputDirectory;
        _profile = profile;
        _sessionFactory = sessionFactory;
    }

    public bool IsPhoto { get; }

    public CameraCaptureState State { get; private set; } = CameraCaptureState.Starting;

    public string? ErrorMessage { get; private set; }

    /// <summary>How long the current recording has run.</summary>
    public TimeSpan RecordingElapsed => State == CameraCaptureState.Recording ? DateTime.UtcNow - _recordingStarted : TimeSpan.Zero;

    /// <summary>Raised (on any thread) when the state or the preview frame changes.</summary>
    public event Action? Changed;

    /// <summary>The captured file, or null when the user cancelled.</summary>
    public Task<string?> Result => _result.Task;

    /// <summary>Opens the camera for the live preview.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (State != CameraCaptureState.Starting || _session != null)
                return;
            OpenSession(CameraPipelines.Preview(_source));
        }
    }

    /// <summary>
    /// The image to draw: the photo under review, else the latest preview frame (null before the
    /// first). The controller owns it; it stays valid until the next call.
    /// </summary>
    public SKBitmap? CurrentImage()
    {
        lock (_gate)
        {
            if (State == CameraCaptureState.Review)
                return _review;
            var session = _session;
            if (session == null)
                return _frame;
            var number = session.FrameCount;
            if (number != _frameNumber)
            {
                var latest = session.SnapshotLatest();
                if (latest != null)
                {
                    _frame?.Dispose();
                    _frame = latest;
                    _frameNumber = number;
                }
            }
            return _frame;
        }
    }

    /// <summary>Takes the photo shown in the preview and shows it for review.</summary>
    public void TakePhoto()
    {
        lock (_gate)
        {
            if (!IsPhoto || State != CameraCaptureState.Live || _session == null)
                return;
            var snapshot = _session.SnapshotLatest();
            if (snapshot == null)
                return;
            _review?.Dispose();
            _review = snapshot;
            State = CameraCaptureState.Review;
        }
        RaiseChanged();
    }

    /// <summary>Discards the photo under review and goes back to the preview.</summary>
    public void Retake()
    {
        lock (_gate)
        {
            if (State != CameraCaptureState.Review)
                return;
            _review?.Dispose();
            _review = null;
            State = _session != null ? CameraCaptureState.Live : CameraCaptureState.Starting;
        }
        RaiseChanged();
    }

    /// <summary>Keeps the photo under review: writes it as JPEG and completes with its path.</summary>
    public async Task AcceptAsync()
    {
        SKBitmap? photo;
        lock (_gate)
        {
            if (State != CameraCaptureState.Review || _review == null)
                return;
            photo = _review;
            _review = null;
            State = CameraCaptureState.Done;
        }
        RaiseChanged();
        string? path = null;
        try
        {
            path = await Task.Run(() =>
            {
                using (photo)
                {
                    var file = NewOutputPath(".jpg");
                    using var image = SKImage.FromBitmap(photo);
                    using var data = image.Encode(SKEncodedImageFormat.Jpeg, PhotoQuality);
                    using var stream = File.Create(file);
                    data.SaveTo(stream);
                    return file;
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Camera", $"Saving the photo failed: {ex.Message}", ex);
        }
        await CloseSessionAsync(finish: false).ConfigureAwait(false);
        _result.TrySetResult(path);
    }

    /// <summary>Starts recording: the camera is reopened with the recording pipeline (preview kept).</summary>
    public async Task StartRecordingAsync()
    {
        ICameraSession? preview;
        lock (_gate)
        {
            if (IsPhoto || State != CameraCaptureState.Live)
                return;
            preview = _session;
            _session = null;
            State = CameraCaptureState.Starting;
        }
        RaiseChanged();
        if (preview != null)
        {
            Detach(preview);
            await preview.StopAsync(finish: false, TimeSpan.Zero).ConfigureAwait(false);
        }

        var path = NewOutputPath(_profile!.Extension);
        lock (_gate)
        {
            if (State == CameraCaptureState.Done)
                return;
            _recordingPath = path;
            // With sound first; a machine whose microphone cannot be opened records without.
            if (!OpenSession(CameraPipelines.Recording(_source, _profile, path, withAudio: true), reportFailure: _profile.AudioEncoder == null)
                && _profile.AudioEncoder != null)
            {
                TryDelete(path);
                OpenSession(CameraPipelines.Recording(_source, _profile, path, withAudio: false));
            }
            if (_session != null)
            {
                _recordingStarted = DateTime.UtcNow;
                State = CameraCaptureState.Recording;
            }
        }
        RaiseChanged();
    }

    /// <summary>Stops recording, finishes the file and completes with its path.</summary>
    public async Task StopRecordingAsync()
    {
        string? path;
        lock (_gate)
        {
            if (State != CameraCaptureState.Recording)
                return;
            State = CameraCaptureState.Finishing;
            path = _recordingPath;
        }
        RaiseChanged();
        var completed = await CloseSessionAsync(finish: true).ConfigureAwait(false);
        lock (_gate)
            State = CameraCaptureState.Done;
        RaiseChanged();
        if (!completed || path == null || !File.Exists(path) || new FileInfo(path).Length == 0)
        {
            if (path != null)
                TryDelete(path);
            _result.TrySetResult(null);
            return;
        }
        _result.TrySetResult(path);
    }

    /// <summary>Cancels the capture: the camera closes, a partial recording is deleted, the result is null.</summary>
    public async Task CancelAsync()
    {
        string? partial;
        lock (_gate)
        {
            if (State == CameraCaptureState.Done)
                return;
            State = CameraCaptureState.Done;
            partial = _recordingPath;
        }
        RaiseChanged();
        await CloseSessionAsync(finish: false).ConfigureAwait(false);
        if (partial != null)
            TryDelete(partial);
        _result.TrySetResult(null);
    }

    public void Dispose()
    {
        _ = CancelAsync();
        lock (_gate)
        {
            _frame?.Dispose();
            _frame = null;
            _review?.Dispose();
            _review = null;
        }
    }

    /// <summary>A new file in the capture folder: capture.ext, then "capture (2).ext" and so on.</summary>
    internal string NewOutputPath(string extension)
    {
        var folder = Path.Combine(_outputDirectory, CacheFolderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, CacheFileName + extension);
        for (int n = 2; File.Exists(path); n++)
            path = Path.Combine(folder, $"{CacheFileName} ({n}){extension}");
        // Reserve the name.
        using (File.Create(path)) { }
        return path;
    }

    private bool OpenSession(string description, bool reportFailure = true)
    {
        try
        {
            var session = _sessionFactory(description);
            session.FrameArrived += OnFrame;
            session.Failed += OnFailed;
            _session = session;
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("Camera", $"Camera pipeline failed: {ex.Message}");
            if (reportFailure)
            {
                ErrorMessage = ex.Message;
                State = CameraCaptureState.Failed;
            }
            return false;
        }
    }

    private void Detach(ICameraSession session)
    {
        session.FrameArrived -= OnFrame;
        session.Failed -= OnFailed;
    }

    private async Task<bool> CloseSessionAsync(bool finish)
    {
        ICameraSession? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }
        if (session == null)
            return !finish;
        Detach(session);
        return await session.StopAsync(finish, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }

    private void OnFrame()
    {
        lock (_gate)
        {
            if (State == CameraCaptureState.Starting && _session != null)
                State = CameraCaptureState.Live;
        }
        // Every frame repaints the preview.
        RaiseChanged();
    }

    private void OnFailed(string message)
    {
        lock (_gate)
        {
            if (State == CameraCaptureState.Done)
                return;
            ErrorMessage = message;
            State = CameraCaptureState.Failed;
        }
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("Camera", $"Capture change handler failed: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
