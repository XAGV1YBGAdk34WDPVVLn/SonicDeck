using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using SonicDeck.Audio.EqEngines;

namespace SonicDeck.Audio;

public class AppAudioSessionViewModel : INotifyPropertyChanged
{
    private readonly AudioSessionControl _session;
    private float _volume;
    private bool _isMuted;
    private float _peakLevel;

    public AppAudioSessionViewModel(AudioSessionControl session, int processId, string displayName)
    {
        _session = session;
        ProcessId = processId;
        DisplayName = displayName;
        try
        {
            _volume = Math.Clamp(_session.SimpleAudioVolume.Volume * 100f, 0f, 100f);
            _isMuted = _session.SimpleAudioVolume.Mute;
        }
        catch
        {
            _volume = 100f;
        }
    }

    public int ProcessId { get; }
    public string DisplayName { get; }

    public float Volume
    {
        get => _volume;
        set
        {
            float clamped = Math.Clamp(value, 0f, 100f);
            if (Math.Abs(_volume - clamped) > 0.1f)
            {
                _volume = clamped;
                try
                {
                    _session.SimpleAudioVolume.Volume = _volume / 100f;
                }
                catch { }
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    public string VolumeText => $"{_volume:0}%";

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                try
                {
                    _session.SimpleAudioVolume.Mute = _isMuted;
                }
                catch { }
                OnPropertyChanged();
            }
        }
    }

    public float PeakLevel
    {
        get => _peakLevel;
        set
        {
            if (Math.Abs(_peakLevel - value) > 0.005f)
            {
                _peakLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public void RefreshLiveMetrics()
    {
        try
        {
            PeakLevel = Math.Clamp(_session.AudioMeterInformation.MasterPeakValue, 0f, 1f);
            float sysVol = _session.SimpleAudioVolume.Volume * 100f;
            if (Math.Abs(sysVol - _volume) > 1.0f)
            {
                _volume = sysVol;
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumeText));
            }
            bool sysMute = _session.SimpleAudioVolume.Mute;
            if (sysMute != _isMuted)
            {
                _isMuted = sysMute;
                OnPropertyChanged(nameof(IsMuted));
            }
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SystemAudioMixerService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _activeRenderDevice;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_STOP = 0xB2;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public SystemAudioMixerService()
    {
        RefreshDefaultDevice();
    }

    public string ActiveDeviceName => _activeRenderDevice?.FriendlyName ?? "No Output Device";

    public void RefreshDefaultDevice(string? deviceId = null)
    {
        try
        {
            _activeRenderDevice?.Dispose();
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                _activeRenderDevice = _enumerator.GetDevice(deviceId);
            }
            else
            {
                _activeRenderDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
        }
        catch
        {
            _activeRenderDevice = null;
        }
    }

    public MMDevice? GetDeviceById(string? id, DataFlow flow = DataFlow.Render)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(id))
                return _enumerator.GetDevice(id);
            return _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public List<AudioDeviceItem> GetOutputDevices()
    {
        var list = new List<AudioDeviceItem>();
        try
        {
            string? defaultId = null;
            try
            {
                defaultId = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)?.ID;
            }
            catch { }

            foreach (var dev in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new AudioDeviceItem
                {
                    Id = dev.ID,
                    Name = dev.FriendlyName,
                    IsLoopback = false,
                    IsDefault = dev.ID == defaultId
                });
            }
        }
        catch { }
        return list;
    }

    public List<AudioDeviceItem> GetInputAndLoopbackDevices()
    {
        var list = new List<AudioDeviceItem>();
        try
        {
            foreach (var dev in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                list.Add(new AudioDeviceItem
                {
                    Id = dev.ID,
                    Name = $"[Input] {dev.FriendlyName}",
                    IsLoopback = false
                });
            }
            foreach (var dev in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new AudioDeviceItem
                {
                    Id = dev.ID,
                    Name = $"[Loopback] {dev.FriendlyName}",
                    IsLoopback = true
                });
            }
        }
        catch { }
        return list;
    }

    public float GetMasterVolumePercent()
    {
        try
        {
            if (_activeRenderDevice != null)
                return _activeRenderDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
        }
        catch { }
        return 80f;
    }

    public void SetMasterVolumePercent(float percent)
    {
        try
        {
            if (_activeRenderDevice != null)
                _activeRenderDevice.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent / 100f, 0f, 1f);
        }
        catch { }
    }

    public bool GetMasterMute()
    {
        try
        {
            return _activeRenderDevice?.AudioEndpointVolume.Mute ?? false;
        }
        catch
        {
            return false;
        }
    }

    public void SetMasterMute(bool mute)
    {
        try
        {
            if (_activeRenderDevice != null)
                _activeRenderDevice.AudioEndpointVolume.Mute = mute;
        }
        catch { }
    }

    public List<AppAudioSessionViewModel> GetActiveAppSessions(out string detectedNowPlaying)
    {
        detectedNowPlaying = string.Empty;
        var results = new List<AppAudioSessionViewModel>();
        if (_activeRenderDevice == null)
            return results;

        try
        {
            var sessions = _activeRenderDevice.AudioSessionManager.Sessions;
            var seenPids = new HashSet<int>();

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                int pid = (int)s.GetProcessID;

                if (s.IsSystemSoundsSession)
                {
                    results.Add(new AppAudioSessionViewModel(s, 0, "System Sounds"));
                    continue;
                }

                if (pid <= 0 || seenPids.Contains(pid))
                    continue;

                string label = s.DisplayName;
                string windowTitle = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    windowTitle = proc.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(label) || label.StartsWith("@"))
                    {
                        label = string.IsNullOrWhiteSpace(windowTitle)
                            ? FormatProcessName(proc.ProcessName)
                            : $"{FormatProcessName(proc.ProcessName)} — {Truncate(windowTitle, 32)}";
                    }

                    if (string.IsNullOrWhiteSpace(detectedNowPlaying) &&
                        !string.IsNullOrWhiteSpace(windowTitle) &&
                        IsMediaProcess(proc.ProcessName))
                    {
                        detectedNowPlaying = $"{FormatProcessName(proc.ProcessName)}: {windowTitle}";
                    }
                }
                catch
                {
                    if (string.IsNullOrWhiteSpace(label))
                        continue;
                }

                seenPids.Add(pid);
                results.Add(new AppAudioSessionViewModel(s, pid, label));
            }
        }
        catch { }

        return results;
    }

    public void SendMediaPlayPause() => SendMediaKey(VK_MEDIA_PLAY_PAUSE);
    public void SendMediaNext() => SendMediaKey(VK_MEDIA_NEXT_TRACK);
    public void SendMediaPrevious() => SendMediaKey(VK_MEDIA_PREV_TRACK);
    public void SendMediaStop() => SendMediaKey(VK_MEDIA_STOP);

    private static void SendMediaKey(byte vk)
    {
        try
        {
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }

    private static bool IsMediaProcess(string procName)
    {
        string n = procName.ToLowerInvariant();
        return n.Contains("spotify") || n.Contains("chrome") || n.Contains("msedge") ||
               n.Contains("firefox") || n.Contains("vlc") || n.Contains("foobar") ||
               n.Contains("musicbee") || n.Contains("itunes") || n.Contains("aimp") ||
               n.Contains("winamp") || n.Contains("tidal") || n.Contains("wmplayer");
    }

    private static string FormatProcessName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Application";
        return char.ToUpperInvariant(raw[0]) + raw[1..];
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    public void Dispose()
    {
        _activeRenderDevice?.Dispose();
        _enumerator.Dispose();
    }
}
