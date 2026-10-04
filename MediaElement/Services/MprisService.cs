// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Services;

/// <summary>A request from the desktop's media controls (media keys, the shell's player widget).</summary>
internal enum MprisCommand
{
    Play,
    Pause,
    PlayPause,
    Stop,
    /// <summary>Seek to an absolute position (value: seconds).</summary>
    SeekTo,
    /// <summary>value: 0 to 1.</summary>
    SetVolume,
    SetRate,
    /// <summary>value: 1 to loop the track, 0 not to.</summary>
    SetLoop,
}

/// <summary>A MediaElement that can be the published player.</summary>
internal interface IMprisPlayer
{
    /// <summary>Carries out a command; called on a D-Bus thread, so it dispatches to the UI thread itself.</summary>
    void Execute(MprisCommand command, double value);
}

/// <summary>What the desktop is shown about the published player (an immutable snapshot taken on the UI thread).</summary>
internal sealed record MprisState(
    string Title,
    string Artist,
    string ArtworkUrl,
    TimeSpan Duration,
    string PlaybackStatus,
    double Volume,
    double Rate,
    bool Loop,
    bool CanSeek)
{
    public static readonly MprisState Empty = new("", "", "", TimeSpan.Zero, "Stopped", 1.0, 1.0, false, false);

    /// <summary>MPRIS PlaybackStatus for a toolkit state.</summary>
    public static string StatusOf(CommunityToolkit.Maui.Core.MediaElementState state) => state switch
    {
        CommunityToolkit.Maui.Core.MediaElementState.Playing => "Playing",
        CommunityToolkit.Maui.Core.MediaElementState.Buffering => "Playing",
        CommunityToolkit.Maui.Core.MediaElementState.Paused => "Paused",
        _ => "Stopped",
    };
}

[DBusInterface("org.mpris.MediaPlayer2", GetPropertyMethod = "GetRootAsync", GetAllPropertiesMethod = "GetAllRootAsync",
    SetPropertyMethod = "SetRootAsync", WatchPropertiesMethod = "WatchRootPropertiesAsync")]
internal interface IMprisRoot : IDBusObject
{
    Task RaiseAsync();
    Task QuitAsync();
    Task<object> GetRootAsync(string prop);
    Task<IDictionary<string, object>> GetAllRootAsync();
    Task SetRootAsync(string prop, object val);
    Task<IDisposable> WatchRootPropertiesAsync(Action<PropertyChanges> handler);
}

[DBusInterface("org.mpris.MediaPlayer2.Player", GetPropertyMethod = "GetPlayerAsync", GetAllPropertiesMethod = "GetAllPlayerAsync",
    SetPropertyMethod = "SetPlayerAsync", WatchPropertiesMethod = "WatchPlayerPropertiesAsync")]
internal interface IMprisPlayerInterface : IDBusObject
{
    Task NextAsync();
    Task PreviousAsync();
    Task PauseAsync();
    Task PlayPauseAsync();
    Task StopAsync();
    Task PlayAsync();
    Task SeekAsync(long offset);
    Task SetPositionAsync(ObjectPath trackId, long position);
    Task OpenUriAsync(string uri);
    Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception>? onError = null);
    Task<object> GetPlayerAsync(string prop);
    Task<IDictionary<string, object>> GetAllPlayerAsync();
    Task SetPlayerAsync(string prop, object val);
    Task<IDisposable> WatchPlayerPropertiesAsync(Action<PropertyChanges> handler);
}

/// <summary>
/// Publishes the playing MediaElement's metadata (MetadataTitle,
/// MetadataArtist, MetadataArtworkUrl) and state over MPRIS
/// (org.mpris.MediaPlayer2 on the session bus), the Linux counterpart of the
/// SystemMediaTransportControls display the toolkit updates on Windows. The
/// desktop then shows the player in its media widget and routes the media
/// keys (play, pause, play/pause, stop) to it, as the toolkit's Windows
/// Metadata class routes the SMTC Play and Pause buttons.
///
/// One player per process, as one SMTC session is shown per app: the
/// MediaElement that last opened or played media with metadata. Elements
/// without any metadata (a muted background video) are never published.
/// Set <c>OPENMAUI_MPRIS=0</c> to turn publishing off.
/// </summary>
internal sealed class MprisService
{
    internal const string ObjectPathValue = "/org/mpris/MediaPlayer2";
    internal const string EnvironmentVariable = "OPENMAUI_MPRIS";

    private readonly Func<Task<Connection>> _connect;
    private readonly Lock _gate = new();
    private readonly MprisObject _object;
    private IMprisPlayer? _active;
    private Task _chain = Task.CompletedTask;
    private Connection? _connection;
    private string? _busName;
    private bool _registered;

    public MprisService(Func<Task<Connection>>? connect = null)
    {
        _connect = connect ?? (async () => (await SessionBus.GetAsync().ConfigureAwait(false)).Connection);
        _object = new MprisObject(this);
    }

    private static MprisService? s_shared;

    /// <summary>The process's service; tests replace it.</summary>
    internal static MprisService Shared
    {
        get => s_shared ??= new MprisService();
        set => s_shared = value;
    }

    internal static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) != "0";

    /// <summary>The D-Bus object (tests call it directly).</summary>
    internal MprisObject DBusObject => _object;

    /// <summary>The published player, or null.</summary>
    internal IMprisPlayer? Active { get { lock (_gate) return _active; } }

    /// <summary>The bus name owned while a player is published (org.mpris.MediaPlayer2.*).</summary>
    internal string? BusName { get { lock (_gate) return _busName; } }

    /// <summary>Makes <paramref name="player"/> the published player with this state.</summary>
    public void Publish(IMprisPlayer player, MprisState state)
    {
        bool start;
        lock (_gate)
        {
            start = _active == null;
            _active = player;
        }
        _object.SetState(state, force: true);
        if (start)
            Enqueue(RegisterAsync);
    }

    /// <summary>New state for <paramref name="player"/>; ignored unless it is the published one.</summary>
    public void Update(IMprisPlayer player, MprisState state)
    {
        if (Active != player) return;
        _object.SetState(state, force: false);
    }

    /// <summary>The playback position (polled by the desktop; not signalled).</summary>
    public void UpdatePosition(IMprisPlayer player, TimeSpan position)
    {
        if (Active != player) return;
        _object.Position = position;
    }

    /// <summary>The position jumped (a seek): the desktop's progress bar follows.</summary>
    public void NotifySeeked(IMprisPlayer player, TimeSpan position)
    {
        if (Active != player) return;
        _object.Position = position;
        _object.RaiseSeeked(position);
    }

    /// <summary>The player goes away (its handler disconnected): the desktop stops showing it.</summary>
    public void Withdraw(IMprisPlayer player)
    {
        lock (_gate)
        {
            if (_active != player) return;
            _active = null;
        }
        _object.SetState(MprisState.Empty, force: true);
        Enqueue(UnregisterAsync);
    }

    internal void Execute(MprisCommand command, double value) => Active?.Execute(command, value);

    /// <summary>Waits for queued bus work (tests).</summary>
    internal Task WhenSettled() { lock (_gate) return _chain; }

    private void Enqueue(Func<Task> work)
    {
        lock (_gate)
        {
            _chain = _chain.ContinueWith(async _ =>
            {
                try { await work().ConfigureAwait(false); }
                catch (Exception ex) { DiagnosticLog.Debug("MediaElement", $"MPRIS: {ex.Message}"); }
            }, TaskScheduler.Default).Unwrap();
        }
    }

    private async Task RegisterAsync()
    {
        if (Active == null || _busName != null) return;
        var connection = _connection ??= await _connect().ConfigureAwait(false);
        if (!_registered)
        {
            await connection.RegisterObjectAsync(_object).ConfigureAwait(false);
            _registered = true;
        }
        var name = BusNameFor(MediaScreenWakeLock.ApplicationName, Environment.ProcessId);
        await connection.RegisterServiceAsync(name, ServiceRegistrationOptions.None).ConfigureAwait(false);
        lock (_gate) _busName = name;
        DiagnosticLog.Debug("MediaElement", $"MPRIS: published as {name}");
    }

    private async Task UnregisterAsync()
    {
        if (Active != null || _busName == null || _connection == null) return;
        var name = _busName;
        lock (_gate) _busName = null;
        await _connection.UnregisterServiceAsync(name).ConfigureAwait(false);
    }

    /// <summary>
    /// org.mpris.MediaPlayer2.&lt;app&gt;.instance&lt;pid&gt;: the app name reduced
    /// to the characters a bus name element allows, never starting with a digit.
    /// </summary>
    internal static string BusNameFor(string appName, int pid)
    {
        var chars = (appName ?? "").Where(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_').ToArray();
        var element = new string(chars);
        if (element.Length == 0) element = "openmaui";
        if (char.IsAsciiDigit(element[0])) element = "_" + element;
        return $"org.mpris.MediaPlayer2.{element}.instance{pid.ToString(CultureInfo.InvariantCulture)}";
    }
}

/// <summary>The object at /org/mpris/MediaPlayer2 (both MPRIS interfaces).</summary>
internal sealed class MprisObject : IMprisRoot, IMprisPlayerInterface
{
    private readonly MprisService _service;
    private readonly Lock _gate = new();
    private MprisState _state = MprisState.Empty;
    private int _track;
    private Action<PropertyChanges>? _playerChanged;
    private Action<long>? _seeked;
    private long _positionUs;

    public MprisObject(MprisService service) => _service = service;

    public ObjectPath ObjectPath => new(MprisService.ObjectPathValue);

    internal TimeSpan Position
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _positionUs) * 10);
        set => Interlocked.Exchange(ref _positionUs, value.Ticks / 10);
    }

    internal MprisState State { get { lock (_gate) return _state; } }

    internal ObjectPath TrackId { get { lock (_gate) return TrackPath(_track); } }

    private static ObjectPath TrackPath(int track) => new($"/org/openmaui/MediaElement/track{track}");

    internal void SetState(MprisState state, bool force)
    {
        Dictionary<string, object> changed;
        lock (_gate)
        {
            var old = _state;
            if (!force && old == state) return;
            if (old.Title != state.Title || old.Artist != state.Artist || old.ArtworkUrl != state.ArtworkUrl)
                _track++;
            _state = state;
            changed = DiffPlayer(old, state, force);
        }
        if (changed.Count > 0)
            _playerChanged?.Invoke(new PropertyChanges(changed.ToArray()));
    }

    internal void RaiseSeeked(TimeSpan position) => _seeked?.Invoke(position.Ticks / 10);

    private Dictionary<string, object> DiffPlayer(MprisState old, MprisState now, bool force)
    {
        var all = PlayerProperties(now);
        var before = PlayerProperties(old);
        var changed = new Dictionary<string, object>();
        foreach (var (key, value) in all)
        {
            if (key == "Position") continue; // never signalled (the spec says so)
            if (force || !Equal(before[key], value))
                changed[key] = value;
        }
        return changed;
    }

    private static bool Equal(object a, object b) => (a, b) switch
    {
        (IDictionary<string, object> x, IDictionary<string, object> y) => x.Count == y.Count && x.All(kv =>
            y.TryGetValue(kv.Key, out var v) && (kv.Value is string[] sa && v is string[] sb ? sa.SequenceEqual(sb) : Equals(kv.Value, v))),
        _ => Equals(a, b),
    };

    private Dictionary<string, object> PlayerProperties(MprisState s)
    {
        var metadata = new Dictionary<string, object>
        {
            ["mpris:trackid"] = TrackPath(_track),
        };
        if (s.Duration > TimeSpan.Zero) metadata["mpris:length"] = s.Duration.Ticks / 10;
        if (!string.IsNullOrEmpty(s.Title)) metadata["xesam:title"] = s.Title;
        if (!string.IsNullOrEmpty(s.Artist)) metadata["xesam:artist"] = new[] { s.Artist };
        if (!string.IsNullOrEmpty(s.ArtworkUrl)) metadata["mpris:artUrl"] = s.ArtworkUrl;

        bool hasPlayer = _service.Active != null;
        return new Dictionary<string, object>
        {
            ["PlaybackStatus"] = s.PlaybackStatus,
            ["LoopStatus"] = s.Loop ? "Track" : "None",
            ["Rate"] = s.Rate,
            ["Shuffle"] = false,
            ["Metadata"] = metadata,
            ["Volume"] = s.Volume,
            ["Position"] = Interlocked.Read(ref _positionUs),
            ["MinimumRate"] = Math.Min(0.25, s.Rate),
            ["MaximumRate"] = Math.Max(2.0, s.Rate),
            ["CanGoNext"] = false,
            ["CanGoPrevious"] = false,
            ["CanPlay"] = hasPlayer,
            ["CanPause"] = hasPlayer,
            ["CanSeek"] = hasPlayer && s.CanSeek,
            ["CanControl"] = true,
        };
    }

    private static Dictionary<string, object> RootProperties() => new()
    {
        ["CanQuit"] = false,
        ["CanRaise"] = false,
        ["HasTrackList"] = false,
        ["Identity"] = MediaScreenWakeLock.ApplicationName,
        ["SupportedUriSchemes"] = Array.Empty<string>(),
        ["SupportedMimeTypes"] = Array.Empty<string>(),
    };

    // ---- org.mpris.MediaPlayer2 -------------------------------------------

    public Task RaiseAsync() => Task.CompletedTask;
    public Task QuitAsync() => Task.CompletedTask;

    public Task<object> GetRootAsync(string prop)
        => RootProperties().TryGetValue(prop, out var value)
            ? Task.FromResult(value)
            : Task.FromException<object>(new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop));

    public Task<IDictionary<string, object>> GetAllRootAsync() => Task.FromResult<IDictionary<string, object>>(RootProperties());

    public Task SetRootAsync(string prop, object val)
        => Task.FromException(new DBusException("org.freedesktop.DBus.Error.PropertyReadOnly", prop));

    public Task<IDisposable> WatchRootPropertiesAsync(Action<PropertyChanges> handler)
        => Task.FromResult<IDisposable>(new Subscription(() => { }));

    // ---- org.mpris.MediaPlayer2.Player ----------------------------------

    public Task NextAsync() => Task.CompletedTask;      // CanGoNext is false: MediaElement has no playlist
    public Task PreviousAsync() => Task.CompletedTask;  // CanGoPrevious is false
    public Task OpenUriAsync(string uri) => Task.CompletedTask;

    public Task PauseAsync() => Run(MprisCommand.Pause);
    public Task PlayPauseAsync() => Run(MprisCommand.PlayPause);
    public Task StopAsync() => Run(MprisCommand.Stop);
    public Task PlayAsync() => Run(MprisCommand.Play);

    public Task SeekAsync(long offset)
    {
        var target = Math.Max(0, Interlocked.Read(ref _positionUs) + offset);
        var duration = State.Duration.Ticks / 10;
        if (duration > 0 && target > duration)
            return Run(MprisCommand.Stop); // past the end: the next track; with none, stop (the spec)
        return Run(MprisCommand.SeekTo, target / 1_000_000.0);
    }

    public Task SetPositionAsync(ObjectPath trackId, long position)
    {
        if (!trackId.Equals(TrackId) || position < 0) return Task.CompletedTask; // stale track: ignored (the spec)
        var duration = State.Duration.Ticks / 10;
        if (duration > 0 && position > duration) return Task.CompletedTask;
        return Run(MprisCommand.SeekTo, position / 1_000_000.0);
    }

    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception>? onError = null)
    {
        _seeked += handler;
        return Task.FromResult<IDisposable>(new Subscription(() => _seeked -= handler));
    }

    public Task<object> GetPlayerAsync(string prop)
    {
        Dictionary<string, object> properties;
        lock (_gate) properties = PlayerProperties(_state);
        return properties.TryGetValue(prop, out var value)
            ? Task.FromResult(value)
            : Task.FromException<object>(new DBusException("org.freedesktop.DBus.Error.UnknownProperty", prop));
    }

    public Task<IDictionary<string, object>> GetAllPlayerAsync()
    {
        lock (_gate) return Task.FromResult<IDictionary<string, object>>(PlayerProperties(_state));
    }

    public Task SetPlayerAsync(string prop, object val)
    {
        switch (prop)
        {
            case "Volume" when val is double volume:
                return Run(MprisCommand.SetVolume, Math.Clamp(volume, 0, 1));
            case "Rate" when val is double rate && rate > 0:
                return Run(MprisCommand.SetRate, rate);
            case "LoopStatus" when val is string loop:
                return Run(MprisCommand.SetLoop, loop == "None" ? 0 : 1);
            case "Shuffle":
                return Task.CompletedTask;
            default:
                return Task.FromException(new DBusException("org.freedesktop.DBus.Error.PropertyReadOnly", prop));
        }
    }

    public Task<IDisposable> WatchPlayerPropertiesAsync(Action<PropertyChanges> handler)
    {
        _playerChanged += handler;
        return Task.FromResult<IDisposable>(new Subscription(() => _playerChanged -= handler));
    }

    private Task Run(MprisCommand command, double value = 0)
    {
        _service.Execute(command, value);
        return Task.CompletedTask;
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
