================================================
  MidiHub - Installation
================================================

CONTENU DU DOSSIER
------------------
  MidiHub.exe     → Application principale
  Sounds\         → Fichiers audio (wav/mp3)

INSTALLATION
------------
1. Copier le dossier complet (MidiHub.exe + Sounds\) ou tu veux
2. Pas d'installation requise, pas de .NET a installer

PREREQUIS
---------
  - Windows 10 / 11 (64 bits)
  - Interface USB-MIDI branchee (ESI MIDIMATE eX ou autre)
  - Pilote de l'interface MIDI installe

NOMMAGE DES FICHIERS SONS
--------------------------
  Les fichiers dans Sounds\ doivent commencer par un numero 01 a 10.
  Exemples : 01_kick.wav, 02_snare.wav, 03_hihat.mp3

  PC 04 et 05 sont RESERVES pour JJazzLab (ne pas mettre de son dessus).

  Pédales valides : 01, 02, 03, 06, 07, 08, 09, 10

VOLUMES
-------
  Les reglages de volume sont sauvegardes dans :
  midihub-volumes.json (cree automatiquement a la fermeture)
