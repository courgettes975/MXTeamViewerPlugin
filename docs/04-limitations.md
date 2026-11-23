# Limitations TeamViewer et Logitech (constatées, pas supposées)

## TeamViewer 15.83.7

1. **Pas d'API locale.** La Web API (`webapi.teamviewer.com`) gère comptes, appareils et rapports ; elle ne pilote pas
   le client installé. Aucun contrôle micro / audio / enregistrement / zoom / plein écran par API.
2. **Fenêtre de session opaque.** Les fenêtres de session (`sciter-frame`, titre « appareil - TeamViewer ») n'exposent
   que 2 panneaux anonymes à UI Automation : la barre d'outils de session n'est pas automatisable de façon fiable.
   Le clic à des coordonnées a été écarté (fragile, dépend de la langue, du DPI et de la version).
3. **Aucun raccourci clavier documenté** pour plein écran, zoom, enregistrement. Le plugin n'en invente pas :
   ces actions affichent **N/D** tant que vous n'avez pas renseigné `keys.*` ou `scripts.*`.
4. **Enregistrement** : démarrage uniquement via l'interface (« Fichiers et suppléments → Démarrer l'enregistrement »)
   ou l'option « enregistrer automatiquement les sessions ». Le dossier par défaut n'est pas documenté :
   `recordingsPath` permet de le fixer (même dossier que dans les options TeamViewer). L'état « en cours » est déduit
   d'un fichier `.tvs` modifié dans les 6 dernières secondes.
5. **Réunion** : retirée de la version gratuite ; présente ici (menu `id=meeting`). Le plugin teste sa présence à
   chaque utilisation et affiche N/D sinon.
6. **Micro TeamViewer** : coupé via la session audio de capture de `TeamViewer.exe` (Windows Core Audio). La session
   n'existe que lorsque TeamViewer a ouvert le micro. L'interface TeamViewer peut ne pas afficher le micro comme coupé
   (le son est coupé au niveau Windows). Vérifiez une première fois avec un interlocuteur.
7. **Déconnecter** ferme la fenêtre de session (`WM_CLOSE`). TeamViewer peut afficher sa propre confirmation.
8. **Plein écran sans raccourci** = maximiser/restaurer la fenêtre de session (libellé « Fenêtre » sur la touche).
9. **Connexion** : TeamViewer demande le mot de passe ou utilise Easy Access ; le plugin n'en transmet jamais.

## Logitech

1. Le **MX Creative Keypad n'a pas de molette** ; la molette et le roller sont sur le **MX Creative Dialpad**.
   Sur le Keypad, utilisez les touches Zoom +/-, Volume +/- et « Mode molette ».
2. Un plugin **ne peut pas changer de profil Options+** selon un état interne. Le changement automatique est fait
   par les touches **Contexte 1…9** (fonction et icône recalculées en direct).
3. Le SDK n'expose pas au plugin la liste des appareils connectés : le diagnostic l'indique au lieu de l'inventer.
4. Animation : pas d'animation native. La touche « Arrêter » clignote (cadre rouge, 1 Hz) pendant un enregistrement.
