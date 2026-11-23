# Phase 2 — Architecture technique

```
Logi Options+ / Logi Plugin Service (process hôte .NET 10)
└── MXTeamViewerPlugin.dll (net10.0)
    ├── Plugin/        MXTeamViewerPlugin (point d'entrée), TeamViewerApplication (stub requis), DiagnosticsReport
    ├── Actions/       ActionCatalog (table de toutes les actions), TeamViewerKeys (PluginDynamicCommand)
    ├── Dial/          TeamViewerDial (PluginDynamicAdjustment : zoom / volume / navigation)
    ├── Profiles       (config.profiles + touches « Contexte 1..9 » dans TeamViewerKeys)
    ├── TeamViewer/    ITeamViewerController
    │                  TeamViewerController        composite + boucle d'état 1 s
    │                  TeamViewerCliController     exe, -i <ID>, URI teamviewer10://
    │                  TeamViewerUiAutomationController  (via agent)
    │                  WindowsAudioController      Core Audio (via agent)
    │                  SessionWindowController     Win32 : WM_CLOSE, maximiser, molette, raccourcis, capture
    │                  TeamViewerApiController     Web API (diagnostic du jeton uniquement)
    │                  TeamViewerLocator / TeamViewerWindows   détection chemin, version, fenêtres, .tvs
    ├── Automation/    Win32, KeyChord, ScriptRunner (.ps1/.ahk/.exe), AgentClient
    ├── Configuration/ PluginConfig, ConfigStore (config.json, rechargement à chaud)
    ├── Security/      SecretStore (DPAPI CurrentUser)
    ├── Logging/       TvLog (fichier + log du service, masquage des secrets)
    └── Rendering/     Icons (pictogrammes dessinés, aucun asset)
        │  stdin/stdout JSON (une ligne = une requête)
        ▼
bin\agent\MXTeamViewerAgent.exe (net10.0-windows, WPF UIAutomationClient, WinForms/System.Drawing)
    ├── Uia.cs          FindFirst par AutomationId puis par nom, Invoke/Select/Toggle/Focus, Dump
    ├── CoreAudio.cs    sessions audio par processus (capture / rendu), mute micro Windows
    └── WindowCapture.cs  PrintWindow(PW_RENDERFULLCONTENT) → PNG
```

## Pourquoi un agent séparé

* Le Logi Plugin Service ne charge que `Microsoft.NETCore.App` : UI Automation managée (WPF) et
  System.Drawing nécessitent `Microsoft.WindowsDesktop.App`.
* Un blocage UIA (fenêtre qui ne répond pas) ou un plantage COM n'affecte jamais Options+ :
  timeout côté plugin, agent tué et relancé à la demande.

## Routage des méthodes (ordre de préférence de la spec)

`TeamViewerController` choisit pour chaque appel la première méthode réellement disponible :
API officielle → CLI/URI → raccourci clavier (config) → UI Automation → Core Audio / Win32 → script.
Remplacer une méthode = modifier une ligne dans le composite ; les actions ne changent pas.

## Flux d'état

1. Timer 1 s : `TeamViewerWindows.Run()` (EnumWindows, coût négligeable).
2. Toutes les 2 s si TeamViewer tourne : `audio.get` (agent).
3. En session : détection `.tvs` dans le dossier d'enregistrements.
4. Empreinte (`Fingerprint`) comparée : les touches ne sont redessinées que si quelque chose de visible change.
