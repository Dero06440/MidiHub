# CLAUDE.md - MidiHub

## Projet

**MidiHub** est un hub MIDI central en C# (.NET 8, application console) qui intercepte les événements d'un pédalier **Behringer FCB1010** et les dispatche vers différentes actions et applications.

## Contexte utilisateur

- **Développeur** : maîtrise Java, C#, se débrouille en Python
- **IDE** : VS Code (développement via Claude Code) + Visual Studio (build/debug)
- **OS** : Windows 11 uniquement
- **Projet local** : `C:\Users\bigde\GitCsharp\MidiHub`

## Architecture globale

```
FCB1010 (USB/MIDI)
     │
     ▼
  MidiHub (console C# .NET 8)
     │
     ├──► Module Audio    : jouer/stopper des sons (NAudio)
     ├──► Named Pipe "PartiLive"  ──► PartiLive (WPF) - changement de page
     ├──► Named Pipe "AndroJazz"  ──► AndroJazz (.NET MAUI)
     └──► Port MIDI virtuel       ──► JJazzLab
```

## Matériel MIDI

### Behringer FCB1010
- **10 pédales** (rangée du bas : 1-5, rangée du haut : 6-10)
- **2 pédales d'expression** (A et B) - contrôle continu (volume, wah, etc.)
- **2 banques** navigables via pédales UP/DOWN (banques 0-9 = 100 presets)
- **Messages MIDI envoyés** : Program Change (PC) et/ou Control Change (CC) selon configuration
- **Configuration** : chaque preset peut envoyer jusqu'à 5 CC + 1 PC
- **Connexion** : MIDI 5-pin DIN → interface USB-MIDI → PC

### Configuration MIDI constatée (7 mars 2026)
- **Interface USB-MIDI** : ESI MIDIMATE eX
- **Port d'entrée** : `MIDIIN2 (ESI MIDIMATE eX)` (index [1])
- **Pédales de preset** : envoient **Program Change** sur **Canal 1**
- **Pédale d'expression** : envoie **CC 7** (volume) sur **Canal 1**, valeurs 0-127
- **Pas de Note On/Off**

### Mapping Program Change connu
| Pédale | Message MIDI |
|--------|-------------|
| Pédale testée | PC 1, Canal 1 |
| Pédale testée | PC 2, Canal 1 |
| Pédale testée | PC 3, Canal 1 |
| Pédale testée | PC 7, Canal 1 |
| Pédale testée | PC 92, Canal 1 |

> Le mapping complet des 10 pédales reste à documenter.

## Stack technique

| Composant | Librairie | Usage |
|-----------|-----------|-------|
| MIDI In | DryWetMIDI | Écouter le FCB1010 |
| Audio | NAudio | Jouer/stopper des fichiers son |
| IPC | System.IO.Pipes | Communiquer avec PartiLive/AndroJazz |
| MIDI Out | DryWetMIDI | Renvoyer vers JJazzLab (futur) |
| Config | JSON (System.Text.Json) | Mapping pédales ↔ actions |

## Structure du projet (cible)

```
MidiHub/
├── MidiHub.sln
├── MidiHub.csproj
├── Program.cs                  # Point d'entrée, boucle principale
├── Config/
│   ├── MidiConfig.cs           # Modèle de configuration
│   └── midihub-config.json     # Mapping pédales → actions
├── Midi/
│   ├── MidiListener.cs         # Écoute du pédalier
│   └── MidiRouter.cs           # Dispatch des événements
├── Audio/
│   └── AudioPlayer.cs          # Lecture/stop de sons
├── Ipc/
│   └── PipeServer.cs           # Named Pipes vers PartiLive/AndroJazz
├── Sounds/                     # Fichiers audio (.wav, .mp3)
├── CLAUDE.md
├── SPECS.md
└── README.md
```

## Fichier de configuration (midihub-config.json)

```json
{
  "midiDeviceName": "FCB1010",
  "actions": [
    {
      "name": "Kick",
      "trigger": { "type": "ProgramChange", "channel": 1, "number": 0 },
      "action": { "type": "PlaySound", "file": "Sounds/kick.wav", "toggle": true }
    },
    {
      "name": "Page suivante",
      "trigger": { "type": "ProgramChange", "channel": 1, "number": 1 },
      "action": { "type": "SendPipe", "pipe": "PartiLive", "command": "NEXT_PAGE" }
    },
    {
      "name": "Page précédente",
      "trigger": { "type": "ProgramChange", "channel": 1, "number": 2 },
      "action": { "type": "SendPipe", "pipe": "PartiLive", "command": "PREV_PAGE" }
    }
  ]
}
```

## Phases de développement

### Phase 1 : Écoute MIDI + Play/Stop son ← ON EST ICI
### Phase 2 : Configuration JSON + mapping flexible
### Phase 3 : Named Pipes vers PartiLive
### Phase 4 : Port MIDI virtuel vers JJazzLab
### Phase 5 : Looper / Séquenceur MIDI

## Conventions de code

- **Langue du code** : anglais (noms de classes, méthodes, variables)
- **Langue des commentaires** : français
- **Langue des logs console** : français
- **Nullable** : activé (`<Nullable>enable</Nullable>`)
- **Top-level statements** : oui (Program.cs simplifié)
- **Async/await** : privilégier pour MIDI et audio (non-bloquant)

## Applications clientes

### PartiLive
- **Type** : WPF (.NET)
- **Emplacement** : à confirmer
- **Communication** : Named Pipe `PartiLive`
- **Commandes** : `NEXT_PAGE`, `PREV_PAGE`, `GOTO_PAGE:{n}`

### AndroJazz
- **Type** : .NET MAUI
- **Emplacement** : à confirmer
- **Communication** : Named Pipe `AndroJazz`
- **Commandes** : à définir

### JJazzLab
- **Type** : Application Java externe
- **Communication** : Port MIDI virtuel (via loopMIDI ou virtualMIDI)
- **Le FCB1010 est actuellement utilisé directement par JJazzLab**
- **Objectif** : MidiHub intercepte le MIDI et re-route vers JJazzLab

## Commandes de build

```bash
# Build
dotnet build

# Run
dotnet run

# Publish (exe autonome)
dotnet publish -c Release -r win-x64 --self-contained
```
