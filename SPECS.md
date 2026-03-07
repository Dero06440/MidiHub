# MidiHub - Cahier des charges

## 1. Présentation

**MidiHub** transforme un pédalier MIDI Behringer FCB1010 en contrôleur multifonction capable de :
- Jouer/stopper des sons
- Piloter des applications tierces (PartiLive, AndroJazz, JJazzLab)
- (Futur) Fonctionner comme looper/séquenceur MIDI

## 2. Matériel

| Élément | Détail |
|---------|--------|
| Pédalier | Behringer FCB1010 |
| Connexion | MIDI 5-pin DIN → Interface USB-MIDI → PC |
| OS | Windows 11 |
| Pédales | 10 pédales de preset (2 rangées de 5) |
| Expression | 2 pédales d'expression (A, B) |
| Banques | 10 banques (0-9), 10 presets par banque = 100 presets |

## 3. Roadmap

### Phase 1 - Fondation ← ACTUELLE
**Objectif** : écouter le pédalier et jouer/stopper un son

| Fonctionnalité | Détail |
|----------------|--------|
| Détection MIDI | Lister les ports MIDI disponibles |
| Écoute MIDI | Recevoir tous les messages du FCB1010 |
| Log MIDI | Afficher en console le type, canal, numéro, valeur de chaque message |
| Play/Stop son | Un appui = joue le son, un 2e appui = stop |
| Format audio | WAV (priorité) et MP3 |

**Livrable** : application console qui joue un son quand on appuie sur une pédale.

---

### Phase 2 - Configuration
**Objectif** : rendre le mapping pédale → action configurable

| Fonctionnalité | Détail |
|----------------|--------|
| Fichier JSON | `midihub-config.json` pour mapper chaque pédale à une action |
| Hot-reload | Recharger la config sans redémarrer |
| Types d'action | `PlaySound`, `SendPipe`, `SendMidi` |
| Mode apprentissage | Appuyer sur une pédale → MidiHub enregistre le message MIDI |

---

### Phase 3 - Communication avec PartiLive
**Objectif** : piloter PartiLive (WPF) depuis le pédalier

| Fonctionnalité | Détail |
|----------------|--------|
| Named Pipe serveur | MidiHub expose un pipe nommé `PartiLive` |
| Commandes | `NEXT_PAGE`, `PREV_PAGE`, `GOTO_PAGE:{n}` |
| Côté PartiLive | Ajouter un client Named Pipe qui écoute les commandes |
| Reconnexion | Si PartiLive redémarre, le pipe se reconnecte |

---

### Phase 4 - Routage MIDI vers JJazzLab
**Objectif** : partager le FCB1010 entre MidiHub et JJazzLab

| Fonctionnalité | Détail |
|----------------|--------|
| Port MIDI virtuel | Utiliser loopMIDI pour créer un port virtuel |
| Routage sélectif | Certaines pédales → MidiHub, d'autres → JJazzLab |
| MIDI Through | Retransmettre les messages non-capturés vers JJazzLab |

---

### Phase 5 - Looper / Séquenceur (exploratoire)
**Objectif** : fonctions audio/MIDI avancées

| Fonctionnalité | Détail |
|----------------|--------|
| Looper audio | Enregistrer/superposer des boucles audio |
| Séquenceur MIDI | Envoyer des séquences MIDI pré-programmées |
| Sync tempo | Synchronisation BPM avec JJazzLab |

---

## 4. Exigences techniques

### Performance
- **Latence MIDI → action** : < 10ms (impératif pour le jeu en live)
- **Latence audio** : < 20ms (utiliser WASAPI via NAudio si nécessaire)
- **Consommation CPU** : minimale (application en arrière-plan)

### Fiabilité
- Ne doit jamais planter si un client (PartiLive, JJazzLab) est absent
- Reconnexion automatique si le pédalier est débranché/rebranché
- Logs clairs en console pour le diagnostic

### Configuration
- Tout le mapping dans un fichier JSON éditable
- Pas besoin de recompiler pour changer l'assignation des pédales

## 5. Contraintes

- **Windows 11 uniquement** : pas de portabilité Linux/Mac requise
- **Pas de GUI pour MidiHub** : application console (les clients ont leur propre GUI)
- **Pas de framework web** : tout est local
- **Dépendances NuGet uniquement** : DryWetMIDI, NAudio

## 6. Questions ouvertes

- [ ] Configuration actuelle du FCB1010 : quels messages MIDI envoie-t-il ? (PC, CC, quel canal ?)
- [ ] Emplacement des fichiers sons à jouer ?
- [ ] Emplacement du projet PartiLive ? (pour la phase 3)
- [ ] Interface USB-MIDI utilisée ? (nom du device tel qu'il apparaît dans Windows)
- [ ] loopMIDI déjà installé ? (pour la phase 4)
