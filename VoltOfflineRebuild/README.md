# Bouzelouf / VoltOfflineRebuild

Rebuild local de Volt pour etude et tests offline. L'application s'appelle maintenant **Bouzelouf** dans l'interface, mais le projet garde le nom technique `VoltOfflineRebuild`.

Ce projet n'est pas une recompilation exacte du Volt originel. C'est une recreation fonctionnelle basee sur l'analyse du binaire original, avec une DLL native locale (`VoltNative.dll`) et une interface WinForms moderne.

## Lancer l'application

Executable principal :

```powershell
VoltOfflineRebuild\bin\Release\net8.0-windows\VoltOfflineRebuild.exe
```

Important : ne copie pas uniquement le `.exe`. L'application a besoin des fichiers a cote.

Pour transferer sur un autre Windows, copie tout le dossier :

```text
VoltOfflineRebuild\bin\Release\net8.0-windows\
```

Fichiers attendus dans le dossier :

- `VoltOfflineRebuild.exe`
- `VoltOfflineRebuild.dll`
- `VoltOfflineRebuild.deps.json`
- `VoltOfflineRebuild.runtimeconfig.json`
- `VoltNative.dll`
- `Assets\bouzelouf_logo.png`

Si l'application ne demarre pas sur l'autre PC, installe :

```text
.NET Desktop Runtime 8 x64
```

## Build depuis les sources

Depuis le dossier `VoltOfflineRebuild` :

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '..\.dotnet_home').Path
dotnet build .\VoltOfflineRebuild.csproj -c Release
```

Si la DLL native a ete modifiee :

```powershell
cd .\native
$env:PATH='C:\Users\Manzu\Desktop\mingw\w64devkit\bin;' + $env:PATH
g++.exe -shared -static -s -O2 -o VoltNative.dll VoltNative.cpp -luser32 -lwinmm
cd ..
dotnet build .\VoltOfflineRebuild.csproj -c Release
```

## Utilisation rapide

1. Lance Minecraft / AZ Launcher et arrive en jeu.
2. Lance `VoltOfflineRebuild.exe`.
3. Va dans `Settings` puis clique sur `Connect to Minecraft` si la connexion automatique n'a pas trouve la fenetre.
4. Va dans `Left clicker` ou `Right clicker`.
5. Coche `Enabled`.
6. Maintiens le bouton de souris correspondant en jeu.

Raccourcis :

- `F4` active/desactive le left clicker.
- `F7` active/desactive le right clicker.

## Onglet Left / Right

### CPS

`Clicks per second` regle la vitesse cible.

- Left : jusqu'a 200 CPS.
- Right : jusqu'a 300 CPS.

Le CPS reel peut etre plus bas selon Minecraft, la fenetre cible, le mode de randomisation, les sleeps Windows, et les options activees.

### Modes

`Normal` :

- Intervalle plus disperse.
- Peut avoir des variations plus fortes.

`Butterfly` :

- Mode par defaut.
- Alterne des intervalles courts et plus longs pour se rapprocher d'un comportement butterfly.

`Blatant` :

- Plus direct.
- Quand la randomisation est activee, la cible CPS change plus lentement au lieu de changer a chaque clic.

### Randomization

Active la variation du rythme.

`Random spread` regle l'intensite de variation.

### Jitter

Ajoute un petit mouvement souris pendant l'autoclick, comme le Volt originel qui expose `jitter` + `jitterpower` et importe `mouse_event`.

Comportement actuel :

- `Jitter` active/desactive le mouvement.
- `Jitter strength` regle la puissance.
- Le mouvement utilise `mouse_event(MOUSEEVENTF_MOVE)`, pas `SendInput`.
- Le clic continue d'utiliser la DLL native et les messages Windows comme avant.

Conseil : commence bas, par exemple 5-10%. A 20-25%, le mouvement peut devenir tres visible en jeu.

### Ignore menus

Essaie d'arreter l'autoclick quand un menu/inventaire est ouvert, en se basant sur la visibilite du curseur.

Limite connue : selon le mode fenetre, la version de Minecraft ou AZ Launcher, la detection peut etre imparfaite.

### Break block

Seulement cote left clicker.

Objectif : permettre de casser un bloc tout en gardant l'autoclick actif au CPS configure.

Comportement actuel :

- Si tu maintiens le clic gauche assez longtemps, le mode breakblock envoie un maintien de minage.
- L'autoclick continue a envoyer des pulses au rythme configure.
- Quand tu relaches, le release est envoye.

Si un bloc ne se casse pas :

1. Verifie que `Break block` est active.
2. Baisse le CPS pour tester, par exemple 10-20 CPS.
3. Augmente `Break hold`, par exemple 300-450 ms.
4. Verifie que la fenetre Minecraft est bien focus.

### Spikes

Seulement cote left clicker.

Le spike ne doit pas tourner en continu. Il se declenche quand :

- `Spikes` est coche.
- Tu fais un nouveau clic physique gauche.

Parametres :

- `Spike CPS` : nombre de clics dans la montee.
- `Spike time` : duree totale de la montee en millisecondes.

Exemple : `Spike CPS = 4`, `Spike time = 75 ms` envoie une petite rafale repartie sur environ 75 ms.

### Slots whitelist

Permet d'autoriser l'autoclick seulement sur certains slots.

Utilisation :

1. Coche `Slots whitelist`.
2. Clique sur les boutons `1` a `9` pour choisir les slots autorises.
3. Mets ton item sur un slot autorise.
4. Maintiens le clic en jeu.
5. Change de slot : l'autoclick doit s'arreter si le nouveau slot n'est pas autorise.

Important : Left et Right ont maintenant chacun leur propre whitelist cote C#.

## Probleme connu : slot whitelist encore imparfait

Le slot whitelist n'est pas encore aussi fiable que Volt original.

Ce qui est implemente :

- Detection des touches `1` a `9` cote C#.
- Detection des touches `1` a `9` cote hook natif.
- Detection de la molette via `WM_MOUSEWHEEL` cote hook natif.
- Verification de la whitelist juste avant les clics.
- Recheck regulier pendant les pauses entre clics.
- Whitelist separee pour left et right cote runtime.

Ce qui peut encore poser probleme :

- Changement de slot par molette selon la facon dont Minecraft/AZ traite les messages.
- Changement de slot pendant que clic gauche et clic droit sont maintenus ensemble.
- Latence entre le changement de slot et la mise a jour du `currentSlot`.
- Cas ou Minecraft consomme l'evenement avant que le hook le voie correctement.
- Cas ou le slot change par une autre methode qu'une touche 1-9 ou la molette.

Si la whitelist continue de cliquer sur un slot non autorise :

1. Teste d'abord uniquement avec les touches `1` a `9`, sans molette.
2. Teste uniquement left clicker, right clicker desactive.
3. Teste uniquement right clicker, left clicker desactive.
4. Active `Only focused`.
5. Desactive `Break block` et `Spikes` le temps du test.
6. Mets un seul slot whitelist, par exemple slot 1.
7. Maintiens le clic en slot 1, puis passe slot 2 avec la touche `2`.

Resultat attendu :

- L'autoclick doit s'arreter presque immediatement.

Si le bug arrive seulement avec la molette :

- Le probleme vient probablement du suivi `WM_MOUSEWHEEL`.
- La prochaine correction logique serait d'ajouter une lecture plus directe de l'etat du slot depuis Minecraft ou de reconstituer plus precisement le mecanisme natif de Volt.

Si le bug arrive avec left + right en meme temps :

- Le probleme vient probablement de la synchronisation entre les deux workers.
- La whitelist est separee, mais le `currentSlot` reste commun car le slot Minecraft est global.
- Il faut verifier si un worker garde un clic en cours pendant que l'autre detecte deja le changement.

## Onglet Settings

`Connect to Minecraft` :

- Cherche une fenetre Minecraft / LWJGL.
- Configure la fenetre cible pour la DLL.
- Reattache le hook natif.

Si rien ne se passe :

1. Lance Minecraft avant Bouzelouf.
2. Mets Minecraft en fenetre ou fenetre plein ecran.
3. Va en jeu, pas seulement dans le launcher.
4. Clique `Refresh target`.
5. Regarde l'onglet `Logs`.

## Onglet Logs

Affiche les evenements internes :

- chargement de `VoltNative.dll`
- connexion a la fenetre cible
- activation/desactivation left/right
- erreurs de chargement DLL

Si l'application se lance mais ne clique pas, l'onglet `Logs` est le premier endroit a regarder.

## Problemes frequents

### L'application ne se lance pas sur un autre PC

Cause probable : tu as copie seulement le `.exe`.

Solution :

- Copie tout le dossier `net8.0-windows`.
- Installe `.NET Desktop Runtime 8 x64`.

### `VoltNative.dll` introuvable

Cause : la DLL native n'est pas dans le meme dossier que l'executable.

Solution :

- Verifie que `VoltNative.dll` est a cote de `VoltOfflineRebuild.exe`.

### L'UI s'ouvre mais aucun clic ne part

Checklist :

1. `Enabled` est coche.
2. La fenetre Minecraft est detectee dans `Settings`.
3. Minecraft est au premier plan.
4. Tu maintiens bien le bouton de souris correspondant.
5. `Ignore menus` ne bloque pas parce que le curseur est visible.
6. `Slots whitelist` ne bloque pas le slot courant.

### Le CPS est trop bas

Ca peut venir de :

- mode `Butterfly` avec randomisation activee ;
- `Jitter` trop eleve ;
- `Simulate exhaust` ;
- Minecraft qui limite ou ignore certains messages ;
- timing Windows ;
- fenetre pas focus.

Pour tester le CPS brut :

1. Mode `Blatant`.
2. `Randomization` off.
3. `Jitter` off.
4. `Simulate exhaust` off.
5. `Slots whitelist` off.
6. `Break block` off.

### Ignore menus ne marche pas toujours

La detection actuelle utilise surtout la visibilite du curseur.

Ce n'est pas exactement le systeme original de Volt. Selon la version de Minecraft, ca peut continuer a cliquer dans certains menus.

## Differences exactes avec Volt originel

### Ce qui est proche de Volt

- Interface reconstruite autour des options principales left/right.
- DLL native locale nommee `VoltNative.dll`.
- Exports compatibles avec les noms retrouves : `sendClick`, `sendRightClick`, `sendBreakBlockClick`, `oneClick`, `isClicking`, `attach`, `dettach`, etc.
- Clics envoyes par messages Windows vers la fenetre cible, pas par `SendInput`.
- Utilisation d'un hook natif `WH_GETMESSAGE`.
- Modes de randomisation reconstruits : `Normal`, `Butterfly`, `Blatant`.
- Spikes reconstruits d'apres l'IL : rafale sur nouveau clic physique, avec `Spike CPS` et `Spike time`.
- Breakblock rapproche du comportement observe : ne coupe pas volontairement l'autoclick.

### Ce qui n'est pas identique

- Ce n'est pas la DLL originale de Volt.
- Ce n'est pas une reconstruction byte-for-byte.
- Le code original est obfusque avec `VM_DISPATCH`, donc certaines fonctions sont reinterpretees.
- L'auth, HWID, profils serveur et systeme de login original ne sont pas repris.
- L'UI est une refonte Bouzelouf, pas l'UI Volt exacte.
- La detection menu/inventory est approximative.
- Le son de clic est expose dans l'UI mais pas completement implemente comme Volt.
- Les options Misc comme TNT macro sont surtout des placeholders UI pour l'instant.
- Slot whitelist n'est pas encore aussi fiable que l'original.

### Pourquoi ne pas utiliser directement la DLL originale

La DLL originale depend du client original, de ses offsets, de son loader, et de son contexte runtime. Dans ce rebuild, on utilise une DLL locale compatible avec les exports analyses, mais controlee et recompilable.

Avantages :

- Debogable.
- Modifiable.
- Plus simple a transferer.
- Pas besoin du systeme d'auth original.

Inconvenients :

- Certaines subtilites natives de Volt doivent etre recopiees manuellement.
- Certaines features ne sont que des approximations.

## Etat actuel des fonctionnalites

| Fonction | Etat |
| --- | --- |
| Left autoclick | Fonctionnel |
| Right autoclick | Fonctionnel |
| CPS slider | Fonctionnel |
| Randomization | Fonctionnel, approximatif |
| Butterfly mode | Fonctionnel, approximatif |
| Blatant mode | Fonctionnel, approximatif |
| Spikes | Fonctionnel, reconstruit |
| Break block | Fonctionnel, encore a tester selon Minecraft |
| Ignore menus | Partiel |
| Slots whitelist | Partiel / bug restant possible |
| Click sound | UI presente, logique incomplete |
| Misc macros | UI presente, logique incomplete |
| Login/auth Volt | Non repris |

## Conseils pour continuer le reverse

Si tu veux rapprocher encore plus de Volt original :

1. Reanalyser le `ClickerThreadFunc` original autour de `sendBreakBlockClickDelegate`.
2. Reconstituer precisement `TimeUtils.performantSleep`.
3. Retrouver comment Volt original detecte le slot courant.
4. Verifier si la DLL originale lit directement la hotbar ou se contente des messages clavier/souris.
5. Comparer le comportement en jeu avec logs : touche 1-9, molette, left+right simultanes.

Les fichiers utiles :

- `tools\VoltMetaProbe\Program.cs`
- `tools\move_next_full_il.txt`
- `VoltOfflineRebuild\MainForm.cs`
- `VoltOfflineRebuild\native\VoltNative.cpp`
