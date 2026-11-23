# Tableau des fonctions et méthodes

Légende : **oui** = utilisé, *config* = possible si vous le configurez, — = n'existe pas.

| Fonction (id) | API | CLI / URI | Raccourci clavier | UI Automation | Fallback retenu |
|---|---|---|---|---|---|
| TeamViewer `tv.open` | — | **oui** `TeamViewer.exe` | — | — | Win32 restaurer + premier plan |
| Nouvelle session `tv.newsession` | — | lancement | **Ctrl+K** (affiché par l'UI) | **oui** `search-input-field` | Ctrl+K |
| Connexion rapide `tv.quick` | — | **oui** `-i <ID>` / `teamviewer10://control?device=` | — | — | sans ID : écran nouvelle session |
| Créer réunion `tv.meeting` | — | — | (mode raccourci Ctrl+/ M+E, non utilisé) | **oui** `id=meeting` | désactivé (N/D) si absent |
| Micro `tv.mic` | — | — | — | — (fenêtre opaque) | **Core Audio** session capture TeamViewer.exe ; option micro Windows |
| Micro Windows `tv.mic.windows` | — | — | — | — | **Core Audio** endpoint par défaut |
| Son distant `tv.audio` | — | — | — | — | **Core Audio** session rendu TeamViewer.exe |
| Volume distant `vol.up/down`, molette | — | — | — | — | **Core Audio** volume de session |
| Enregistrements `tv.recordings` | — | — | — | — | **Explorateur** (dossier configuré / détecté, dernier .tvs sélectionné) |
| Enregistrer `tv.record` | — | — | *config* `keys.recordStart/Stop` | — | *config* script .ps1/.ahk ; détection .tvs ; anti double démarrage |
| Capture `tv.capture` | — | — | — | — | **PrintWindow** de la fenêtre de session → PNG |
| Favoris `fav.1..5` | — | **oui** `-i <ID>` | — | — | — |
| Déconnecter `tv.disconnect` | — | — | — | — | **WM_CLOSE** fenêtre de session ; double appui 2 s |
| Plein écran `tv.fullscreen` | — | — | *config* `keys.fullscreen` | — | maximiser/restaurer la fenêtre |
| Zoom `zoom.in/out/100`, molette | — | — | *config* `keys.zoom*` | — | *config* script ; sinon N/D |
| Navigation molette | — | — | — | — | **WM_MOUSEWHEEL** vers la session |
| Diagnostic `tv.diagnostics`, `tv.selftest` | ping jeton (option) | — | — | dump arbre | rapport texte |

## Touches contextuelles

| Slot | TeamViewer inactif | En session |
|---|---|---|
| Contexte 1 | TeamViewer | Micro |
| Contexte 2 | Connexion rapide | Audio |
| Contexte 3 | PC Bureau | Capture |
| Contexte 4 | PC Portable | Enregistrer |
| Contexte 5 | Enregistrements | Plein écran |
| Contexte 6 | Nouvelle session | Zoom + |
| Contexte 7 | Serveur | Déconnecter |
| Contexte 8 | Réunion | Zoom - |
| Contexte 9 | Diagnostic | TeamViewer |

## États visuels (LCD)

| État | Rendu |
|---|---|
| Micro actif / coupé / inconnu | micro vert / micro rouge barré / gris |
| Son distant actif / coupé | haut-parleur vert / rouge barré |
| Enregistrement inactif / actif | cercle rouge / carré rouge + cadre clignotant |
| Favori connecté / prêt / sans ID | icône PC verte + cadre / blanche / grise « ID? » |
| Déconnecter | fond rouge (rouge sombre sans session) |
| Action non disponible | icône grise + badge orange « N/D » |
| Échec d'une action | cadre orange + message 2,5 s |

Spécification des pictogrammes : dessinés par `src\Rendering\Icons.cs` (vectoriel, toutes tailles LCD) ;
icône du paquet 256×256 générée par `scripts\New-PackageIcon.ps1`. Pas de logo TeamViewer (marque déposée).
