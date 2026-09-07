# Dictée universelle

Application Windows de dictée vocale universelle : un raccourci global (clavier ou bouton de souris) démarre l'enregistrement depuis n'importe quelle application, la transcription se fait via l'API OpenAI, et le texte est automatiquement collé dans le champ actif — sans extension à installer, sans changer d'outil.

## Fonctionnalités

- **Déclencheur configurable** : raccourci clavier ou bouton de souris (milieu, latéral).
- **Transcription** via l'API OpenAI (`gpt-transcribe` par défaut), catalogue de modèles éditable.
- **Collage universel** : le texte est copié dans le presse-papiers puis collé automatiquement dans la fenêtre qui avait le focus au démarrage de l'enregistrement.
- **Overlay discret** : indicateur d'enregistrement, dernière transcription avec bouton Copier, opacité réglable, raccourci dédié pour l'afficher/masquer.
- **Paramètres** : microphone, durée max d'enregistrement, langue, thème clair/sombre avec couleur d'accent, lancement au démarrage de Windows.
- **Clé API stockée chiffrée** (DPAPI, liée au compte Windows) — jamais en clair sur le disque.

## Installation

Télécharge le dernier installeur depuis l'onglet [Releases](../../releases) de ce dépôt, exécute-le (installation par utilisateur, sans droits admin requis) et lance l'application. Elle démarre réduite dans la zone de notification.

> L'installeur n'est pas signé : Windows (SmartScreen / Smart App Control) peut afficher un avertissement au premier lancement — clique sur *Informations complémentaires → Exécuter quand même*.

Ouvre ensuite **Paramètres…** depuis l'icône de la zone de notification pour renseigner ta clé API OpenAI.

## Compiler depuis les sources

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download), Windows 10/11.

```powershell
dotnet build src/VoiceToText/VoiceToText.csproj
dotnet run --project src/VoiceToText/VoiceToText.csproj
```

Pour construire l'installeur localement (nécessite [Inno Setup 6](https://jrsoftware.org/isinfo.php)) :

```powershell
dotnet publish src/VoiceToText/VoiceToText.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion=0.1.0 installer\VoiceToTextDictation.iss
```

## Documentation

La spécification fonctionnelle complète est dans [`docs/specification-v1.md`](docs/specification-v1.md).

## Licence

[MIT](LICENSE)
