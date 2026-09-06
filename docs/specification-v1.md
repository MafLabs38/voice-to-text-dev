# Specification fonctionnelle - Dictée universelle Windows

**Statut :** à valider avant initialisation du dépôt et développement  
**Date :** 2026-09-06  
**Produit :** application WPF Windows de dictée vocale universelle

## 1. Objectif

Créer une application Windows autonome permettant d'enregistrer une dictée à partir d'un raccourci clavier global, de la transcrire localement avec Whisper, puis de coller automatiquement le texte obtenu dans le contrôle qui avait le focus avant l'enregistrement.

L'application doit proposer une expérience plus simple que l'ancienne extension VS Code : aucune extension à installer, une sélection claire du périphérique audio, un modèle Whisper explicitement sélectionnable et une interface de réglages durable.

## 2. Utilisateur cible et périmètre

L'utilisateur travaille dans différentes applications Windows (Chrome, VS Code, applications métier, messagerie, etc.) et veut dicter dans leur champ de saisie actif sans changer d'outil.

Le périmètre couvre Windows 10 et Windows 11. Il ne couvre pas macOS, Linux, la dictée en continu ni la commande vocale de l'ordinateur.

## 3. Parcours principal

1. L'application est démarrée une fois et reste active dans la zone de notification Windows.
2. L'utilisateur place le curseur dans n'importe quel champ où le collage standard (`Ctrl+V`) fonctionne.
3. Il déclenche le raccourci global configurable pour commencer l'enregistrement.
4. L'application mémorise la fenêtre et le contrôle qui détenaient le focus, puis affiche l'indicateur `REC`.
5. Il utilise le même raccourci pour arrêter l'enregistrement, ou l'enregistrement s'arrête au délai maximal configuré.
6. L'audio est transcrit par Whisper.
7. Le texte est placé dans le presse-papiers puis collé dans la cible mémorisée par la séquence de collage Windows.
8. L'overlay de texte affiche le résultat et permet de le recopier manuellement si le collage automatique a échoué ou a visé la mauvaise fenêtre.

## 4. Fonctionnalités du MVP

### 4.1 Service en arrière-plan

- Application WPF démarrant réduite dans la zone de notification.
- Icône de notification avec menu : afficher/masquer l'overlay, ouvrir les paramètres, consulter la dernière transcription, quitter.
- Fonctionnement sans fenêtre principale persistante obligatoire.
- Démarrage automatique avec Windows : option présente mais désactivée par défaut.

### 4.2 Raccourci global et enregistrement

- Un raccourci global configurable déclenche le début et l'arrêt d'un enregistrement.
- Valeur initiale proposée : `Ctrl+Alt+Space`.
- Le raccourci doit fonctionner lorsque Chrome, VS Code ou une autre application a le focus.
- L'état d'enregistrement est unique : aucune seconde capture ne peut démarrer pendant une transcription.
- Un délai maximal d'enregistrement est configurable, avec une valeur initiale de 60 secondes.
- À l'échéance, l'enregistrement s'arrête automatiquement et la transcription commence.
- L'utilisateur peut également arrêter manuellement avant ce délai.

### 4.3 Source audio

- Liste claire des microphones d'entrée disponibles, avec libellé du périphérique Windows.
- Sélection du périphérique actif dans les paramètres.
- Pré-sélection du périphérique d'entrée Windows par défaut.
- Indication d'erreur utile si le périphérique choisi est indisponible ou déjà inaccessible.

### 4.4 Transcription Whisper

- Transcription locale, sans envoyer les données audio à un service distant.
- Moteur envisagé : `whisper.cpp` ou un wrapper .NET éprouvé autour de ce moteur.
- Sélection du modèle dans les paramètres, au minimum : `tiny`, `base`, `small`, `medium`.
- Langue configurable, avec `Français` par défaut et détection automatique disponible.
- Préservation de la ponctuation produite par le modèle.
- État visible pendant la transcription : `Transcription en cours`.
- En cas d'échec, aucun collage ne doit être déclenché et l'erreur doit être visible depuis l'overlay ou la zone de notification.

### 4.5 Collage universel

- Au début de l'enregistrement, mémoriser la fenêtre qui avait le focus.
- Après transcription, réactiver cette fenêtre lorsque Windows l'autorise, déposer le texte dans le presse-papiers puis émettre `Ctrl+V`.
- Cette stratégie vise tous les contrôles compatibles avec le collage Windows standard.
- Une copie du texte est toujours conservée dans le presse-papiers, même si la réactivation ou le collage automatique échoue.
- La transcription ne doit jamais être injectée par frappe simulée caractère par caractère dans le MVP.

### 4.6 Overlay

L'overlay est une fenêtre WPF sans bordure, épinglable au-dessus des autres fenêtres et déplaçable par glisser-déposer. Sa position est mémorisée.

Deux modules indépendants sont affichables ou masquables :

1. **Indicateur d'enregistrement**
   - Petit fond noir carré ou très compact.
   - Pastille rouge `REC` clignotante pendant la capture.
   - État distinct pour la transcription et l'erreur.

2. **Dernière transcription**
   - Petite fenêtre de texte lisible présentant la dernière transcription.
   - Bouton Copier.
   - Bouton fermer/masquer.
   - État vide discret avant la première transcription.

La visibilité des deux modules et le statut « toujours au-dessus » sont réglables indépendamment.

### 4.7 Paramètres

Les paramètres sont persistés par utilisateur Windows et accessibles sans arrêter le service.

- Raccourci global.
- Microphone d'entrée.
- Durée maximale d'enregistrement.
- Modèle Whisper.
- Langue de transcription.
- Affichage de l'indicateur `REC`.
- Affichage de la dernière transcription.
- Position des overlays.
- Toujours au-dessus.
- Lancement au démarrage de Windows.

## 5. États applicatifs

```text
Prêt
  -> Enregistrement
  -> Transcription
  -> Collage automatique
  -> Prêt

Une erreur depuis Enregistrement, Transcription ou Collage automatique
  -> Erreur visible
  -> Prêt
```

Pendant `Enregistrement`, un nouvel appui sur le raccourci arrête la capture. Pendant `Transcription` ou `Collage automatique`, il est ignoré et l'interface signale que l'application est occupée.

## 6. Contraintes techniques proposées

- **Plateforme :** .NET 10 LTS et WPF, architecture MVVM.
- **Capture audio :** NAudio, en WASAPI capture ou capture du périphérique sélectionné.
- **Transcription :** wrapper .NET maintenu de `whisper.cpp`, exécuté localement.
- **Raccourci global :** API Windows `RegisterHotKey` via interop contrôlée.
- **Fenêtre active et collage :** Win32 (`GetForegroundWindow`, `SetForegroundWindow`, presse-papiers WPF, envoi de `Ctrl+V`).
- **Zone de notification :** composant compatible WPF, choisi après vérification de compatibilité .NET 10.
- **Configuration :** fichier JSON dans le répertoire applicatif utilisateur (`LocalApplicationData`).

Les fichiers audio temporaires doivent être supprimés après transcription, sauf option explicite de diagnostic ajoutée ultérieurement.

## 7. Architecture envisagée

```text
WPF shell / zone de notification / overlays
                  |
            ViewModels MVVM
                  |
          DictationCoordinator
     /          |          |          \
Hotkey     AudioRecorder  WhisperTranscriber  TextPaster
                  |
             SettingsStore
```

`DictationCoordinator` est le propriétaire de la machine à états et garantit qu'une seule dictée est traitée à la fois.

## 8. Critères d'acceptation

- Depuis un champ de texte Chrome, une dictée de moins de la durée maximale est transcrite et collée dans ce même champ après arrêt de l'enregistrement.
- Le même scénario fonctionne depuis un éditeur ouvert dans VS Code.
- Le microphone sélectionné est utilisé après redémarrage de l'application.
- Modifier la durée maximale modifie bien l'arrêt automatique de l'enregistrement.
- L'indicateur `REC` apparaît au démarrage de la capture, clignote, puis disparaît ou change d'état à l'arrêt selon la préférence configurée.
- Le résultat reste récupérable via le bouton Copier, y compris lorsqu'une autre fenêtre reçoit le focus pendant la transcription.
- Une erreur de microphone ou de modèle indisponible est affichée clairement et ne provoque pas de collage parasite.
- Fermer l'overlay ne quitte pas l'application; l'application reste disponible via la zone de notification.

## 9. Limites connues

- Windows peut empêcher une application non privilégiée de donner le focus ou d'envoyer un collage à une fenêtre exécutée avec des privilèges administrateur. Le comportement doit alors rester sûr : texte conservé dans le presse-papiers et message visible.
- Certaines applications n'acceptent pas le collage standard ou le filtrent : elles ne pourront pas être garanties par le MVP.
- La première utilisation d'un modèle Whisper peut demander son téléchargement ou son installation. La stratégie exacte de distribution des modèles est à décider avant développement.
- Les performances et la consommation mémoire dépendent fortement du modèle choisi et du matériel.

## 10. Décisions à valider

1. Le raccourci proposé `Ctrl+Alt+Space` convient-il comme valeur initiale ?
2. Le MVP doit-il fonctionner uniquement avec des modèles Whisper téléchargés localement, ou proposer aussi une transcription distante optionnelle ?
3. Souhaites-tu une diction « appuyer une fois pour démarrer, une fois pour arrêter » uniquement, ou aussi un mode « maintenir pour parler » dans une version ultérieure ?
4. L'overlay doit-il être une seule fenêtre avec deux modules, ou deux petites fenêtres réellement indépendantes ? La présente spécification retient une fenêtre unique à modules indépendants.
5. Veux-tu conserver un historique local des transcriptions ? La présente spécification ne conserve que la dernière.
6. Est-ce que .NET 10 est le bon socle pour le projet, ou veux-tu rester sur .NET 8 LTS pour une compatibilité plus conservatrice ?

## 11. Hors périmètre du MVP

- Transcription en direct mot par mot pendant la parole.
- Commandes vocales.
- Historique, recherche et export des transcriptions.
- Synchronisation cloud.
- Correction linguistique ou reformulation par IA.
- Installation/distribution automatisée et signature de l'exécutable.
- Support de plusieurs utilisateurs Windows ou de plusieurs systèmes d'exploitation.