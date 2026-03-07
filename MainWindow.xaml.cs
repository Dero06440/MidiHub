using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Collections.ObjectModel;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;
using NAudio.Wave;

namespace MidiHub;

public partial class MainWindow : Window
{
    private InputDevice? _midiDevice;
    private readonly Dictionary<int, (WaveOutEvent waveOut, AudioFileReader reader)> _players = new();
    private readonly Dictionary<int, string> _soundMap = new();
    private readonly ObservableCollection<SoundEntry> _soundEntries = new();
    private const string PreferredDevice = "MIDIIN2";
    private const int MaxLogLines = 100;
    private int _logLineCount;

    public MainWindow()
    {
        InitializeComponent();
        SoundsList.ItemsSource = _soundEntries;
        LoadSounds();
        ConnectMidi();
    }

    // --- Chargement des sons ---
    private void LoadSounds()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
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

        for (int i = 0; i < files.Length; i++)
        {
            _soundMap[i] = files[i];
            _soundEntries.Add(new SoundEntry
            {
                ProgramNumber = i,
                ProgramLabel = $"PC {i}",
                FileName = Path.GetFileNameWithoutExtension(files[i]),
                PlayState = "---",
                PlayColor = new SolidColorBrush(Color.FromRgb(166, 173, 200)) // gris
            });
            Log($"  PC {i} → {Path.GetFileName(files[i])}");
        }

        Log($"{files.Length} son(s) chargé(s)");
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
                    ToggleSound(prog);
                    break;

                case ControlChangeEvent cc:
                    // Ignorer les CC (pédale d'expression) pour éviter le spam
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

    // --- Nettoyage à la fermeture ---
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
