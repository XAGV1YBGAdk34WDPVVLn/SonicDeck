using System.Globalization;
using System.IO;
using System.Text;
using SonicDeck.Models;

namespace SonicDeck.Audio.EqEngines;

public class EqualizerApoService
{
    private const string DefaultApoConfigDir = @"C:\Program Files\EqualizerAPO\config";
    private const string SonicDeckIncludeFile = "sonicdeck_eq.txt";

    private string _configDir;
    private DateTime _lastWriteTime = DateTime.MinValue;
    private readonly object _writeLock = new();

    public EqualizerApoService()
    {
        if (Directory.Exists(DefaultApoConfigDir))
        {
            _configDir = DefaultApoConfigDir;
            IsApoInstalled = true;
        }
        else
        {
            _configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EqualizerAPO_Export");
            Directory.CreateDirectory(_configDir);
            IsApoInstalled = false;
        }
    }

    public bool IsApoInstalled { get; private set; }
    public string ConfigDirectory => _configDir;
    public string ActiveIncludeFilePath => Path.Combine(_configDir, SonicDeckIncludeFile);
    public string StatusMessage { get; private set; } = "Ready";

    public void SetCustomDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            _configDir = path;
            IsApoInstalled = File.Exists(Path.Combine(path, "config.txt")) ||
                             File.Exists(Path.Combine(Directory.GetParent(path)?.FullName ?? "", "Editor.exe"));
            StatusMessage = $"Target: {_configDir}";
        }
    }

    public string BuildApoConfigContent(
        bool isEnabled,
        double preampDb,
        double bassBoostDb,
        double trebleTiltDb,
        double globalQ,
        double balance,
        IReadOnlyList<EqBand> bands)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("# SonicDeck Live System Equalizer Profile");
        sb.AppendLine($"# Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        if (!isEnabled)
        {
            sb.AppendLine("# Equalizer Bypassed");
            sb.AppendLine("Preamp: 0.0 dB");
            return sb.ToString();
        }

        sb.AppendLine(string.Format(inv, "Preamp: {0:0.00} dB", preampDb));

        if (Math.Abs(balance) > 0.01)
        {
            double leftAtten = balance > 0 ? -20.0 * Math.Log10(Math.Max(0.01, 1.0 - balance)) : 0.0;
            double rightAtten = balance < 0 ? -20.0 * Math.Log10(Math.Max(0.01, 1.0 + balance)) : 0.0;
            sb.AppendLine("Channel: L");
            sb.AppendLine(string.Format(inv, "Preamp: -{0:0.00} dB", leftAtten));
            sb.AppendLine("Channel: R");
            sb.AppendLine(string.Format(inv, "Preamp: -{0:0.00} dB", rightAtten));
            sb.AppendLine("Channel: all");
        }

        // Sub-Bass Shelf
        if (Math.Abs(bassBoostDb) > 0.05)
        {
            sb.AppendLine(string.Format(inv, "Filter 1: ON LSC Fc 75.0 Hz Gain {0:0.00} dB Q 0.75", bassBoostDb));
        }

        // 10 Parametric / Shelf Bands
        for (int i = 0; i < bands.Count; i++)
        {
            var b = bands[i];
            double q = Math.Clamp(b.QFactor * globalQ, 0.25, 10.0);
            string type = i == 0 ? "LSC" : i == bands.Count - 1 ? "HSC" : "PK";
            sb.AppendLine(string.Format(
                inv,
                "Filter {0}: ON {1} Fc {2:0.0} Hz Gain {3:0.00} dB Q {4:0.00}",
                i + 2, type, b.Frequency, b.GainDb, q));
        }

        // Treble Air Tilt Shelf
        if (Math.Abs(trebleTiltDb) > 0.05)
        {
            sb.AppendLine(string.Format(inv, "Filter 12: ON HSC Fc 6500.0 Hz Gain {0:0.00} dB Q 0.71", trebleTiltDb));
        }

        return sb.ToString();
    }

    public bool SyncToApo(
        bool isEnabled,
        double preampDb,
        double bassBoostDb,
        double trebleTiltDb,
        double globalQ,
        double balance,
        IReadOnlyList<EqBand> bands,
        bool force = false)
    {
        lock (_writeLock)
        {
            if (!force && (DateTime.UtcNow - _lastWriteTime).TotalMilliseconds < 18)
                return true;

            try
            {
                Directory.CreateDirectory(_configDir);
                string content = BuildApoConfigContent(isEnabled, preampDb, bassBoostDb, trebleTiltDb, globalQ, balance, bands);
                File.WriteAllText(ActiveIncludeFilePath, content, Encoding.UTF8);

                // Ensure main config.txt includes sonicdeck_eq.txt
                string mainConfigPath = Path.Combine(_configDir, "config.txt");
                string includeLine = $"Include: {SonicDeckIncludeFile}";
                if (File.Exists(mainConfigPath))
                {
                    string existing = File.ReadAllText(mainConfigPath);
                    if (!existing.Contains(SonicDeckIncludeFile, StringComparison.OrdinalIgnoreCase))
                    {
                        File.AppendAllText(mainConfigPath, Environment.NewLine + includeLine + Environment.NewLine);
                    }
                }
                else
                {
                    File.WriteAllText(mainConfigPath, includeLine + Environment.NewLine, Encoding.UTF8);
                }

                _lastWriteTime = DateTime.UtcNow;
                StatusMessage = IsApoInstalled
                    ? $"Equalizer APO Live ({_configDir})"
                    : $"Profile Saved ({ActiveIncludeFilePath})";
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to local export directory if Program Files is locked without Admin permissions
                _configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EqualizerAPO_Export");
                Directory.CreateDirectory(_configDir);
                string content = BuildApoConfigContent(isEnabled, preampDb, bassBoostDb, trebleTiltDb, globalQ, balance, bands);
                File.WriteAllText(ActiveIncludeFilePath, content, Encoding.UTF8);
                StatusMessage = "Saved to App Folder (Run as Admin for Program Files write)";
                return false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"APO Write Warning: {ex.Message}";
                return false;
            }
        }
    }
}
