using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Collections.ObjectModel;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;
using NAudio.Wave;
using System.Diagnostics;

namespace MidiHub;

public partial class MainWindow : Window
{
    private InputDevice? _midiDevice;
    private readonly Dictionary<int, (WaveOutEvent waveOut, AudioFileReader reader)> _players = new();
    private readonly Dictionary<int, string> _soundMap = new();
    private readonly ObservableCollection<SoundEntry> _soundEntries = new();
    private const string PreferredDevice = "MIDIIN2";
    private const int MaxLogLines = 100;
    // PC réservés pour JJazzLab (0-based : 3 = fichier "04", 4 = fichier "05")
    private static readonly HashSet<int> JazzLabPc = [3, 4];
    private float _globalVolume = 1.0f;
    private static readonly string VolumeSettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "midihub-volumes.json");
    private int _logLineCount;
    private const int PedalB_CC = 7;
    private readonly Dictionary<int, int> _lastLoggedCcValue = new();

    public MainWindow()
    {
        InitializeComponent();
        SoundsList.ItemsSource = _soundEntries;
        LoadSounds();
        LoadVolumeSettings();
        ConnectMidi();
    }

    // --- Chargement des sons ---
    private void LoadSounds()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Debug.WriteLine($"Base directory: {baseDir}");
        var soundsDir = Path.Combine(baseDir, "Sounds");

        if (!Directory.Exists(soundsDir))
            soundsDir = Path.Combine(Directory.GetCurrentDirectory(), "Sounds");

        if (!Directory.Exists(soundsDir))
        {
            Log("ATTENTION : dossier Sounds/ introuvable");
            return;
        }

        // Charger tous les fichiers audio du dossier, triés par nom
        var files = Directory.GetFiles(soundsDir, "*.*")
            .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToArray();

        int loaded = 0;
        foreach (var file in files)
        {
            var baseName = Path.GetFileNameWithoutExtension(file);

            // Extraire le préfixe numérique (ex: "04_kick" → 4)
            var prefixStr = new string(baseName.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(prefixStr, out int prefix) || prefix < 1 || prefix > 10)
            {
                Log($"  IGNORÉ (nom sans préfixe 01-10) : {Path.GetFileName(file)}");
                continue;
            }

            int pc = prefix - 1; // 0-based

            // Vérifier que ce PC n'est pas réservé pour JJazzLab
            if (JazzLabPc.Contains(pc))
            {
                Log($"  ATTENTION : PC {pc} (fichier \"{prefix:D2}\") réservé à JJazzLab — {Path.GetFileName(file)} ignoré");
                continue;
            }

            _soundMap[pc] = file;
            var entry = new SoundEntry
            {
                ProgramNumber = pc,
                ProgramLabel = $"PC {pc}",
                FileName = baseName,
                PlayState = "---",
                PlayColor = new SolidColorBrush(Color.FromRgb(166, 173, 200)) // gris
            };
            // Quand le volume individuel change, mettre à jour le lecteur actif si besoin
            entry.OnVolumeChanged = (p, _) => ApplyVolume(p);
            _soundEntries.Add(entry);
            Log($"  PC {pc} → {Path.GetFileName(file)}");
            loaded++;
        }

        Log($"{loaded} son(s) chargé(s)");
    }

    // --- Connexion MIDI ---
    private void ConnectMidi()
    {
        var inputDevices = InputDevice.GetAll().ToList();

        if (inputDevices.Count == 0)
        {
            SetStatus(false, "Aucun périphérique MIDI détecté");
            return;
        }

        int idx = inputDevices.FindIndex(d =>
            d.Name.Contains(PreferredDevice, StringComparison.OrdinalIgnoreCase));

        if (idx < 0)
        {
            // Prendre le premier disponible
            idx = 0;
            Log($"MIDIIN2 non trouvé, utilisation de : {inputDevices[0].Name}");
        }

        _midiDevice = inputDevices[idx];
        _midiDevice.EventReceived += OnMidiEvent;
        _midiDevice.StartEventsListening();

        SetStatus(true, _midiDevice.Name);
        Log($"Connecté à : {_midiDevice.Name}");
    }

    // --- Réception MIDI ---
    private void OnMidiEvent(object? sender, MidiEventReceivedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.Event)
            {
                case ProgramChangeEvent pc:
                    int prog = pc.ProgramNumber;
                    Log($"[PC] Canal:{pc.Channel + 1}  Programme:{prog}");
                    if (JazzLabPc.Contains(prog))
                        Log($"  → JJazzLab (direct)");
                    else
                        ToggleSound(prog);
                    break;

                case ControlChangeEvent cc:
                    int ccVal = cc.ControlValue;
                    int ccNum = cc.ControlNumber;

                    bool shouldLog = ccVal == 0 || ccVal == 127 ||
                        !_lastLoggedCcValue.TryGetValue(ccNum, out int lastVal) ||
                        Math.Abs(ccVal - lastVal) >= 10;

                    if (shouldLog)
                    {
                        Log($"[CC] Canal:{cc.Channel + 1}  CC#{ccNum}  Val:{ccVal}");
                        _lastLoggedCcValue[ccNum] = ccVal;
                    }

                    if (ccNum == PedalB_CC)
                        GlobalVolumeSlider.Value = ccVal / 127.0;
                    break;

                default:
                    Log($"[?] {e.Event.GetType().Name}");
                    break;
            }
        });
    }

    // --- Play/Stop toggle ---
    private void ToggleSound(int programNumber)
    {
        // Stop si en cours
        if (_players.TryGetValue(programNumber, out var existing))
        {
            Log($"  ■ STOP PC {programNumber}");
            existing.waveOut.Stop();
            existing.waveOut.Dispose();
            existing.reader.Dispose();
            _players.Remove(programNumber);
            UpdateSoundState(programNumber, false);
            return;
        }

        // Play
        if (!_soundMap.TryGetValue(programNumber, out var filePath) || !File.Exists(filePath))
        {
            Log($"  Pas de son assigné à PC {programNumber}");
            return;
        }

        var reader = new AudioFileReader(filePath);
        reader.Volume = GetEffectiveVolume(programNumber);
        var waveOut = new WaveOutEvent();
        waveOut.Init(reader);

        // Nettoyage quand le son se termine naturellement
        waveOut.PlaybackStopped += (s, ev) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_players.ContainsKey(programNumber) && _players[programNumber].waveOut == waveOut)
                {
                    waveOut.Dispose();
                    reader.Dispose();
                    _players.Remove(programNumber);
                    UpdateSoundState(programNumber, false);
                }
            });
        };

        _players[programNumber] = (waveOut, reader);
        waveOut.Play();
        UpdateSoundState(programNumber, true);
        Log($"  ► PLAY PC {programNumber}");
    }

    // --- Persistance des volumes ---
    private void LoadVolumeSettings()
    {
        if (!File.Exists(VolumeSettingsPath)) return;

        try
        {
            var json = File.ReadAllText(VolumeSettingsPath);
            var settings = JsonSerializer.Deserialize<VolumeSettings>(json);
            if (settings == null) return;

            _globalVolume = settings.GlobalVolume;
            GlobalVolumeSlider.Value = _globalVolume;

            foreach (var entry in _soundEntries)
            {
                if (settings.PcVolumes.TryGetValue(entry.ProgramNumber, out double vol))
                    entry.Volume = vol;
            }
        }
        catch (Exception ex)
        {
            Log($"[Volume] Erreur chargement paramètres : {ex.Message}");
        }
    }

    private void SaveVolumeSettings()
    {
        try
        {
            var settings = new VolumeSettings
            {
                GlobalVolume = _globalVolume,
                PcVolumes = _soundEntries.ToDictionary(e => e.ProgramNumber, e => e.Volume)
            };
            File.WriteAllText(VolumeSettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log($"[Volume] Erreur sauvegarde paramètres : {ex.Message}");
        }
    }

    // --- Volume ---
    private float GetEffectiveVolume(int pc)
    {
        var entry = _soundEntries.FirstOrDefault(s => s.ProgramNumber == pc);
        return (float)(entry?.Volume ?? 1.0) * _globalVolume;
    }

    private void ApplyVolume(int pc)
    {
        if (_players.TryGetValue(pc, out var p))
            p.reader.Volume = GetEffectiveVolume(pc);
    }

    private void GlobalVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _globalVolume = (float)e.NewValue;
        foreach (var pc in _players.Keys)
            ApplyVolume(pc);
    }

    // --- UI helpers ---
    private void SetStatus(bool connected, string text)
    {
        StatusDot.Fill = connected
            ? new SolidColorBrush(Color.FromRgb(166, 227, 161))  // vert
            : new SolidColorBrush(Color.FromRgb(243, 139, 168)); // rouge
        StatusText.Text = text;
    }

    private void UpdateSoundState(int programNumber, bool playing)
    {
        var entry = _soundEntries.FirstOrDefault(s => s.ProgramNumber == programNumber);
        if (entry == null) return;

        entry.PlayState = playing ? "►" : "---";
        entry.PlayColor = playing
            ? new SolidColorBrush(Color.FromRgb(166, 227, 161))  // vert
            : new SolidColorBrush(Color.FromRgb(166, 173, 200)); // gris
    }

    private void Log(string message)
    {
        if (_logLineCount >= MaxLogLines)
        {
            // Garder les 50 dernières lignes
            var lines = LogText.Text.Split('\n');
            LogText.Text = string.Join('\n', lines.Skip(lines.Length - 50));
            _logLineCount = 50;
        }

        LogText.Text += (LogText.Text.Length > 0 ? "\n" : "") + message;
        _logLineCount++;
        LogScroller.ScrollToEnd();
    }

    // --- Toggle log ---
    private void LogToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        bool visible = LogPanel.Visibility == Visibility.Visible;
        LogPanel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        LogToggleBtn.Content = visible ? "Log ▼" : "Log ▲";
    }

    // --- Nettoyage à la fermeture ---
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveVolumeSettings();
        _midiDevice?.StopEventsListening();
        _midiDevice?.Dispose();

        foreach (var (_, (waveOut, reader)) in _players)
        {
            waveOut.Stop();
            waveOut.Dispose();
            reader.Dispose();
        }
    }
}

// --- Modèle pour la liste des sons ---
public class SoundEntry : INotifyPropertyChanged
{
    public int ProgramNumber { get; set; }
    public string ProgramLabel { get; set; } = "";
    public string FileName { get; set; } = "";
    public Action<int, double>? OnVolumeChanged;

    private string _playState = "---";
    public string PlayState
    {
        get => _playState;
        set { _playState = value; OnPropertyChanged(nameof(PlayState)); }
    }

    private SolidColorBrush _playColor = new(Color.FromRgb(166, 173, 200));
    public SolidColorBrush PlayColor
    {
        get => _playColor;
        set { _playColor = value; OnPropertyChanged(nameof(PlayColor)); }
    }

    private double _volume = 1.0;
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeLabel));
            OnVolumeChanged?.Invoke(ProgramNumber, value);
        }
    }

    public string VolumeLabel => $"{_volume:P0}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// --- Persistance des volumes ---
public class VolumeSettings
{
    public float GlobalVolume { get; set; } = 1.0f;
    public Dictionary<int, double> PcVolumes { get; set; } = new();
}
