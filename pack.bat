@echo off
echo ================================================
echo   MidiHub - Packaging release
echo ================================================
echo.

set OUTPUT=dist

:: Nettoyer le dossier precedent
if exist %OUTPUT% (
    echo Nettoyage de %OUTPUT%\...
    rd /s /q %OUTPUT%
)

:: Build + publish single-file self-contained win-x64
echo Publication en cours...
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o %OUTPUT%

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERREUR : la publication a echoue.
    pause
    exit /b 1
)

:: Supprimer les fichiers inutiles pour la distribution
del /q %OUTPUT%\*.pdb 2>nul

echo.
echo ================================================
echo   Package pret dans : %OUTPUT%\
echo ================================================
echo.
dir /b %OUTPUT%
echo.
echo Contenu requis pour l'installation :
echo   - MidiHub.exe
echo   - Sounds\  (dossier)
echo.
echo Pas besoin d'installer .NET sur le PC cible.
echo.
pause
