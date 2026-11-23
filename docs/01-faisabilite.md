# Phase 1 — Rapport de faisabilité

Analyse réalisée le 2026-11-23 sur le poste cible (Windows 11 Pro 26300) :

| Composant | Version détectée | Méthode |
|---|---|---|
| TeamViewer (Full, nouvelle UI, `UIVersion=4`) | **15.83.7** | `HKLM\SOFTWARE\TeamViewer\Version`, `C:\Program Files\TeamViewer\TeamViewer.exe` |
| Langue TeamViewer | `fr` | `HKLM\SOFTWARE\TeamViewer\LastSelectedLanguage` |
| Logi Plugin Service (Options+) | PluginApi **6.4.2.3414** | `C:\Program Files\Logi\LogiPluginService\PluginApi.dll` |
| .NET | SDK 11.0.100, runtimes 8 / 10 / 11 (+ WindowsDesktop) | `dotnet --list-runtimes` |

## 1. Logitech (Actions SDK / Logi Plugin Service)

* Plugins C# .NET chargés par le **Logi Plugin Service** : classes `Plugin`, `ClientApplication`,
  `PluginDynamicCommand` (touches), `PluginDynamicAdjustment` (molettes), images via `BitmapBuilder`.
* Paquet `.lplug4` décrit par `metadata/LoupedeckPackage.yaml`. En développement, un fichier
  `.link` dans `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\` pointe vers le dossier de build.
* Les touches LCD sont **dynamiques** : le plugin redessine une touche via `ActionImageChanged()`.
* **Limite importante : le MX Creative Keypad n'a pas de molette.** La molette (dial) et le roller
  appartiennent au **MX Creative Dialpad**. Les modes « molette » sont donc des `PluginDynamicAdjustment`
  utilisables sur le Dialpad (et les encodeurs Loupedeck). Sur le Keypad seul, les mêmes fonctions
  existent en touches (`Zoom +`, `Zoom -`, `Volume +`…).
* **Le SDK ne permet pas à un plugin de changer la disposition des touches ni de basculer de profil.**
  Options+ change de profil selon l'application au premier plan, pas selon un état interne.
  → Solution retenue : des **touches contextuelles** (« Contexte 1…9 ») dont la fonction et l'icône
  changent automatiquement selon l'état TeamViewer (inactif / en session).

## 2. TeamViewer — ce qui existe réellement

| Besoin | Disponible officiellement ? | Constat |
|---|---|---|
| Lancer / premier plan | Oui (exécutable) | Chemin dans le registre ; Win32 pour restaurer/activer la fenêtre. |
| Connexion vers un ID | **Oui — CLI** `TeamViewer.exe -i <ID>` ([KB officielle][cli]) | Le mot de passe est saisi dans TeamViewer, jamais transmis par le plugin. |
| URI `teamviewer10://control?device=<ID>` | Gestionnaire **enregistré localement** (`HKCR\teamviewer10`, `tvcontrol1`…) | Utilisé en méthode alternative (configurable). |
| Écran « se connecter » | UI | Champ UIA « Rechercher et se connecter » (`AutomationId=search-input-field`) et raccourci **Ctrl+K** affiché par l'UI. |
| Réunion | **Présente dans cette installation** | Menu UIA `AutomationId=meeting` (« Réunion »). La fonction n'est plus dans la version gratuite ([communauté][meet]) : le plugin **teste sa présence** et désactive l'action sinon. |
| Web API (`webapi.teamviewer.com/api/v1`) | Oui | Gestion de comptes/appareils/rapports. **Aucun contrôle du client local** (micro, audio, enregistrement…). Le plugin l'utilise seulement pour le diagnostic du jeton (`/ping`). |
| Micro / son distant / enregistrement / plein écran / zoom dans la session | **Pas d'API, pas de CLI, pas de raccourci documenté** | La fenêtre de session (`sciter-frame`) n'expose presque rien à UI Automation (2 éléments). |
| Enregistrement | UI uniquement (« Fichiers et suppléments → Démarrer l'enregistrement ») + option « enregistrer automatiquement » ([KB][rec]) | Fichiers `.tvs` ; dossier configurable dans TeamViewer ; dossier par défaut non documenté. |

## 3. Fallbacks retenus (les plus robustes possibles)

| Fonction | Fallback | Pourquoi c'est fiable |
|---|---|---|
| Micro TeamViewer | **Session audio Windows du processus TeamViewer.exe sur les périphériques de capture** (Core Audio `ISimpleAudioVolume`) | Ne coupe que ce que TeamViewer capture. État lisible. *À valider sur votre poste via le diagnostic.* |
| Micro Windows (option) | Mute du périphérique de capture par défaut (`IAudioEndpointVolume`) | Clairement séparé, désactivé par défaut. |
| Son distant | **Session audio de rendu de TeamViewer.exe** (mute + volume) | Coupe le son distant joué localement, sans toucher aux haut-parleurs Windows. |
| Déconnecter | `WM_CLOSE` sur la **fenêtre de session** uniquement | TeamViewer reste ouvert. Double appui configurable. |
| Capture | Capture de la fenêtre de session (`PrintWindow`/écran) en PNG | Indépendant de l'UI TeamViewer. |
| Plein écran / Ajuster | Raccourci configurable, sinon maximiser/restaurer la fenêtre de session | Libellé honnête : « Fenêtre » si aucun raccourci TeamViewer n'est configuré. |
| Zoom | Raccourci configurable uniquement (aucun raccourci documenté) | Action affichée « N/D » tant que non configurée. |
| Enregistrement | Raccourci/script configurable ; détection d'état par fichier `.tvs` en cours d'écriture | Protection anti-double démarrage. |
| Navigation molette | `WM_MOUSEWHEEL` envoyé à la fenêtre de session | TeamViewer transmet le défilement à l'appareil distant. |

Les méthodes « UI Automation » tournent dans un **agent séparé** (`MXTeamViewerAgent.exe`) : UIA, Core Audio
et capture d'écran n'ont aucune influence sur la stabilité du Logi Plugin Service (si l'agent plante, il est relancé).

[cli]: https://www.teamviewer.com/en/global/support/knowledge-base/teamviewer-classic/for-developers/command-line-parameters
[meet]: https://community.teamviewer.com/English/discussion/130679/is-meetings-no-longer-part-of-the-new-interfase
[rec]: https://www.teamviewer.com/en/global/support/knowledge-base/teamviewer-classic/remote-control/in-session-features/record-a-remote-session/
