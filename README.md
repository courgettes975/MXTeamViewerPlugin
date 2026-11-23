# MX TeamViewer — plugin Logi Options+ pour MX Creative Keypad / Dialpad

Pilote TeamViewer depuis les touches LCD du MX Creative Keypad (et la molette du MX Creative Dialpad) :
lancement, nouvelle session, connexions favorites, micro, son distant, captures, enregistrements, plein écran,
zoom, déconnexion protégée, touches contextuelles et diagnostic.

**Aucun mot de passe TeamViewer n'est stocké ni transmis.** Les ID TeamViewer (non secrets) sont dans `config.json` ;
le jeton Web API optionnel est chiffré par DPAPI.

## Démarrage rapide

```powershell
.\scripts\Install.ps1            # build Release + paquet + installation dans Logi Options+
```

1. Logi Options+ → MX Creative Keypad → Actions → **MX TeamViewer** → glisser les actions (ou « Contexte 1…9 »).
2. Éditer `%LOCALAPPDATA%\MXTeamViewerPlugin\config.json` (ID des favoris, voir `config.sample.json`) : rechargé à chaud.
3. Touche **Diagnostic** / **Tester les actions** pour vérifier ce qui fonctionne sur votre poste.

## Ce qui marche et comment (TeamViewer 15.83.7)

| Fonction | Méthode réelle |
|---|---|
| Lancer / premier plan / restaurer | TeamViewer.exe + Win32 |
| Connexion à un ID / favoris | CLI documentée `TeamViewer.exe -i <ID>` (ou URI `teamviewer10://`) |
| Nouvelle session | UI Automation (champ « Rechercher et se connecter ») puis Ctrl+K |
| Créer réunion | UI Automation (menu `meeting`), désactivée si absente |
| Micro TeamViewer | Windows Core Audio : session de capture de TeamViewer.exe (option : micro Windows) |
| Son distant + volume | Windows Core Audio : session de lecture de TeamViewer.exe |
| Capture | PNG de la fenêtre de session → `Documents\TeamViewer Captures\TeamViewer_YYYY-MM-DD_HH-mm-ss.png` |
| Enregistrements | ouverture du dossier (configurable), détection du `.tvs` en cours |
| Démarrer/arrêter enregistrement, zoom | **pas de méthode officielle** : raccourci ou script à configurer, sinon N/D |
| Plein écran | raccourci configuré, sinon maximiser la fenêtre de session |
| Déconnecter | fermeture de la fenêtre de session uniquement, double appui en 2 s |

Détails : [faisabilité](docs/01-faisabilite.md) · [architecture](docs/02-architecture.md) ·
[compilation et installation](docs/03-compilation-installation.md) · [limitations](docs/04-limitations.md) ·
[tableau des fonctions](docs/05-fonctions.md)

## Arborescence

```
src/          plugin (Plugin, Actions, Dial, TeamViewer, Automation, Configuration, Security, Logging, Rendering)
agent/        MXTeamViewerAgent.exe (UI Automation, Core Audio, capture) — hors du process Logi
shared/       protocole JSON plugin <-> agent
scripts/      Build-Package, Install, Uninstall, Set-ApiToken, New-PackageIcon
docs/         documentation
```

## Favoris

```json
"favorites": [
  { "name": "PC Bureau", "teamviewerId": "123 456 789", "alias": "BUREAU-PC", "icon": "desktop" }
]
```

`icon` : `desktop`, `laptop`, `server`, `client`, `star`. `alias` = nom de l'appareil tel qu'affiché dans le titre de
la fenêtre de session (« BUREAU-PC - TeamViewer ») : la touche passe au vert quand cette session est ouverte.
Un ID invalide (lettres, longueur) est ignoré : il ne peut pas injecter d'argument de ligne de commande.

## Sécurité

* Pas de mot de passe TeamViewer, nulle part (pas de paramètre `-p`).
* Jeton Web API (optionnel, diagnostic uniquement) : `.\scripts\Set-ApiToken.ps1` → DPAPI CurrentUser,
  `%LOCALAPPDATA%\MXTeamViewerPlugin\secrets\`.
* Logs : `%LOCALAPPDATA%\MXTeamViewerPlugin\logs\plugin-YYYYMMDD.log` (7 jours, niveaux DEBUG/INFO/WARNING/ERROR,
  masquage automatique des motifs password/token/bearer).
* Scripts utilisateur : chemins absolus de config.json uniquement, aucun argument construit à partir de texte.

## Dépannage

| Symptôme | Action |
|---|---|
| Touche « Micro ? » / « Audio ? » | Aucun flux audio TeamViewer ouvert (normal hors session/réunion). |
| N/D sur Zoom / Enregistrer | Renseigner `keys.*` ou `scripts.*` (aucun raccourci officiel). |
| Sessions non détectées | Diagnostic → section « Fenêtres TeamViewer visibles » → ajuster `windows.*`. |
| Élément UIA introuvable (autre langue/version) | Tester les actions → arbre UIA → ajuster `uia.*`. |
| Agent indisponible | `bin\agent\MXTeamViewerAgent.exe audio` doit répondre ; réinstaller le paquet. |
