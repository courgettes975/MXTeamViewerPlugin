# Compilation et installation

## Prérequis

| Élément | Version testée |
|---|---|
| Windows 11 | 26300 |
| .NET SDK | 10 ou plus (testé avec 11.0.100 ; runtime cible 10 + WindowsDesktop 10) |
| Logi Options+ / Logi Plugin Service | 6.4.2 (`C:\Program Files\Logi\LogiPluginService\PluginApi.dll`) |
| LogiPluginTool | 6.4.2 — `dotnet tool install --global LogiPluginTool` |
| TeamViewer | 15.83.7 (Full) |

Le projet référence le `PluginApi.dll` **installé** (le service refuse un plugin compilé contre une API plus récente).
Sans Options+ (CI), le paquet NuGet `LogiPluginApi` est utilisé.

## Compiler

```powershell
dotnet build .\src\MXTeamViewerPlugin.csproj -c Debug
```

La compilation publie aussi l'agent dans `bin\Debug\bin\agent\`, crée le lien de développement
`%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\MXTeamViewerPlugin.link` et demande au service de recharger
le plugin (`loupedeck:plugin/MXTeamViewer/reload`). `-p:LinkPlugin=false` désactive le lien.

## Paquet .lplug4

```powershell
.\scripts\Build-Package.ps1
```

Produit `dist\MXTeamViewer_1.0.0.lplug4` (build Release, pack, verify).

## Installation dans Logi Options+

* **Paquet** : `.\scripts\Install.ps1`, ou double-clic sur le `.lplug4`, ou `logiplugintool install dist\MXTeamViewer_1.0.0.lplug4`.
* **Développeur** : `.\scripts\Install.ps1 -Dev` (lien vers `bin\Debug`).

Puis dans Logi Options+ :
1. Sélectionner le **MX Creative Keypad**.
2. Ouvrir le profil voulu (par ex. le profil par défaut / Bureau).
3. Panneau **Actions** → **MX TeamViewer** → glisser les actions sur les touches.
   * Profil statique : glisser « TeamViewer », « Connexion rapide », « Session favorite 1 »…
   * Profil automatique : glisser **Contexte 1 … Contexte 9** sur les 9 touches : elles basculent seules
     entre la disposition « TeamViewer inactif » et « En session » (`profiles` dans config.json).
4. MX Creative **Dialpad** : glisser « Molette TeamViewer » sur la molette.

## Désinstallation

```powershell
.\scripts\Uninstall.ps1             # garde config + logs
.\scripts\Uninstall.ps1 -RemoveData # supprime aussi %LOCALAPPDATA%\MXTeamViewerPlugin
```
