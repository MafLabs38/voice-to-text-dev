# Specification fonctionnelle - Dictée universelle Windows

**Statut :** validé le 2026-09-06 — prêt pour initialisation du dépôt et développement  
**Date :** 2026-09-06  
**Produit :** application WPF Windows de dictée vocale universelle

## 1. Objectif

Créer une application Windows autonome permettant d'enregistrer une dictée à partir d'un raccourci clavier global, de la transcrire via un service de transcription distant (API compatible Whisper ou l'un de ses successeurs), puis de coller automatiquement le texte obtenu dans le contrôle qui avait le focus avant l'enregistrement.

L'application doit proposer une expérience plus simple que l'ancienne extension VS Code : aucune extension à installer, une sélection claire du périphérique audio, une clé API et un modèle de transcription configurables (clé stockée chiffrée) et une interface de réglages durable.

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

### 4.4 Transcription distante

- Transcription réalisée via l'API OpenAI (endpoint fixe, non configurable dans le MVP). Le modèle est choisi dans un catalogue éditable (`models.json`). La clé API est saisie dans les Paramètres et stockée chiffrée (DPAPI, liée au compte Windows) dans un fichier séparé des réglages généraux — jamais en clair, jamais dans le fichier JSON de settings.
- Le MVP implémente un seul fournisseur, derrière une interface permettant d'en ajouter d'autres par la suite sans réécrire le reste de l'application.
- Langue configurable, avec `Français` par défaut et détection automatique disponible si le fournisseur le permet.
- Préservation de la ponctuation produite par le modèle.
- État visible pendant la transcription : `Transcription en cours`.
- En cas d'échec (réseau, authentification, quota dépassé), aucun collage ne doit être déclenché et l'erreur doit être visible depuis l'overlay ou la zone de notification.
- Le fichier audio temporaire envoyé au fournisseur est supprimé localement après la transcription (cf. §6).

### 4.5 Collage universel

- Au début de l'enregistrement, mémoriser la fenêtre qui avait le focus.
- Après transcription, réactiver cette fenêtre lorsque Windows l'autorise, déposer le texte dans le presse-papiers puis émettre `Ctrl+V`.
- Cette stratégie vise tous les contrôles compatibles avec le collage Windows standard.
- Une copie du texte est toujours conservée dans le presse-papiers, même si la réactivation ou le collage automatique échoue.
- La transcription ne doit jamais être injectée par frappe simulée caractère par caractère dans le MVP.

### 4.6 Overlay

L'overlay est une fenêtre WPF sans bordure, épinglable au-dessus des autres fenêtres et déplaçable par glisser-déposer. Sa position est mémorisée.

Trois modules indépendants sont affichables ou masquables :

1. **Indicateur d'enregistrement**
   - Petit fond noir carré ou très compact.
   - Pastille rouge `REC` clignotante pendant la capture.
   - État distinct pour la transcription et l'erreur.

2. **Dernière transcription**
   - Petite fenêtre de texte lisible présentant la dernière transcription.
   - Bouton Copier.
   - Bouton fermer/masquer.
   - État vide discret avant la première transcription.
   - Réutilisée pour afficher une entrée sélectionnée depuis le module Historique (cf. ci-dessous).

3. **Historique**
   - Case à cocher activant ou désactivant l'historisation locale des dictées.
   - Bouton ouvrant le dossier d'historique dans l'explorateur Windows.
   - Liste déroulante énumérant les dictées historisées, les plus récentes en premier ; sélectionner une entrée l'affiche dans le module Dernière transcription, avec son bouton Copier.

La visibilité des trois modules et le statut « toujours au-dessus » sont réglables indépendamment.

### 4.7 Paramètres

Les paramètres sont persistés par utilisateur Windows et accessibles sans arrêter le service.

- Raccourci global.
- Microphone d'entrée.
- Durée maximale d'enregistrement.
- Clé API OpenAI (stockée chiffrée) et modèle de transcription.
- Langue de transcription.
- Affichage de l'indicateur `REC`.
- Affichage de la dernière transcription.
- Historisation des dictées activée ou non.
- Affichage du module Historique.
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
- **Transcription :** client HTTP .NET vers l'API OpenAI, derrière une interface permettant d'ajouter d'autres fournisseurs par la suite.
- **Raccourci global :** API Windows `RegisterHotKey` via interop contrôlée (ou hook souris bas niveau pour un déclencheur bouton de souris).
- **Fenêtre active et collage :** Win32 (`GetForegroundWindow`, `SetForegroundWindow`, presse-papiers WPF, envoi de `Ctrl+V`).
- **Zone de notification :** composant compatible WPF, choisi après vérification de compatibilité .NET 10.
- **Configuration :** fichier JSON dans le répertoire applicatif utilisateur (`LocalApplicationData`) pour les réglages généraux (aucune clé API dedans). La clé API est chiffrée via `ProtectedData` (DPAPI, portée utilisateur courant) dans un fichier séparé.
- **Historique :** un fichier texte horodaté par dictée, dans un sous-dossier dédié du répertoire applicatif utilisateur.

Les fichiers audio temporaires doivent être supprimés après transcription, sauf option explicite de diagnostic ajoutée ultérieurement.

## 7. Architecture envisagée

```text
WPF shell / zone de notification / overlays
                  |
            ViewModels MVVM
                  |
          DictationCoordinator
     /       |          |          |          \
Hotkey  AudioRecorder TranscriptionClient TextPaster HistoryStore
                  |
             SettingsStore
```

`DictationCoordinator` est le propriétaire de la machine à états et garantit qu'une seule dictée est traitée à la fois. `TranscriptionClient` encapsule l'appel au fournisseur distant configuré. `HistoryStore` écrit et liste les fichiers d'historique lorsque l'historisation est activée.

## 8. Critères d'acceptation

- Depuis un champ de texte Chrome, une dictée de moins de la durée maximale est transcrite et collée dans ce même champ après arrêt de l'enregistrement.
- Le même scénario fonctionne depuis un éditeur ouvert dans VS Code.
- Le microphone sélectionné est utilisé après redémarrage de l'application.
- Modifier la durée maximale modifie bien l'arrêt automatique de l'enregistrement.
- L'indicateur `REC` apparaît au démarrage de la capture, clignote, puis disparaît ou change d'état à l'arrêt selon la préférence configurée.
- Le résultat reste récupérable via le bouton Copier, y compris lorsqu'une autre fenêtre reçoit le focus pendant la transcription.
- Une erreur de microphone, d'authentification ou de réseau vers le fournisseur de transcription est affichée clairement et ne provoque pas de collage parasite.
- Fermer l'overlay ne quitte pas l'application; l'application reste disponible via la zone de notification.
- Décocher la case d'historisation empêche la création de nouveaux fichiers d'historique ; la recocher reprend l'historisation dès la dictée suivante.
- Sélectionner une entrée dans la liste déroulante d'historique l'affiche dans le module Dernière transcription et permet de la copier.
- Le bouton d'ouverture de dossier ouvre le dossier d'historique dans l'explorateur Windows.

## 9. Limites connues

- Windows peut empêcher une application non privilégiée de donner le focus ou d'envoyer un collage à une fenêtre exécutée avec des privilèges administrateur. Le comportement doit alors rester sûr : texte conservé dans le presse-papiers et message visible.
- Certaines applications n'acceptent pas le collage standard ou le filtrent : elles ne pourront pas être garanties par le MVP.
- La transcription nécessite une connexion réseau active et un accès valide (clé API) au fournisseur configuré ; en cas d'indisponibilité réseau ou de quota dépassé, aucune transcription n'est produite.
- L'audio de chaque dictée quitte la machine et est envoyé au fournisseur de transcription configuré ; le choix d'un fournisseur de confiance reste sous la responsabilité de l'utilisateur.
- La clé API du fournisseur est stockée chiffrée (DPAPI, liée au compte Windows courant) dans un fichier dédié, jamais en clair et jamais mélangée aux réglages généraux.
- Les performances et la latence perçue dépendent du fournisseur de transcription choisi et de la qualité de la connexion réseau plutôt que du matériel local.

## 10. Décisions validées le 2026-09-06

1. Raccourci initial : `Ctrl+Alt+Space`.
2. Transcription exclusivement distante (Whisper API et successeurs, pas de modèle local), configurée par fichier comme l'ancienne extension VS Code. Un seul fournisseur implémenté au MVP, derrière une interface extensible.
3. Mode dictée : appuyer/appuyer uniquement (pas de « maintenir pour parler » au MVP).
4. Overlay : une fenêtre unique à modules indépendants (REC, Dernière transcription, Historique).
5. Historique local activable : un fichier texte horodaté par dictée, texte seul (pas d'audio), consultable via la liste déroulante du module Historique et le bouton d'ouverture de dossier.
6. Socle : .NET 10 LTS.

## 11. Hors périmètre du MVP

- Transcription en direct mot par mot pendant la parole.
- Commandes vocales.
- Recherche indexée dans l'historique, export structuré (CSV/JSON) et synchronisation cloud de l'historique.
- Correction linguistique ou reformulation par IA.
- Installation/distribution automatisée et signature de l'exécutable.
- Support de plusieurs utilisateurs Windows ou de plusieurs systèmes d'exploitation.
- Mode « maintenir pour parler » et fournisseurs de transcription multiples simultanés.