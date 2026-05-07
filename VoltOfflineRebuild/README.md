# VoltOfflineRebuild

Rebuild local inspire de `Anydesk--1-`, avec une interface Windows lancable.

## Organisation

- l'application ouvre directement la fenetre principale
- l'interface ne demande pas d'auth et n'affiche pas de HWID/MAC
- les modeles `Config`, `UserConfig`, `Profile`, `LibOffsets` restent proches du decompile
- `ClickerLibrary` garde les points d'integration (`Attach`, `Detach`, `SendClick`, `SendRightClick`, offsets) sous forme d'adaptateur local visible dans `Log`

## Build

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '..\\.dotnet_home').Path
dotnet build .\\VoltOfflineRebuild.csproj -c Release
```

Executable:

```powershell
.\\bin\\Release\\net8.0-windows\\VoltOfflineRebuild.exe
```

## Interface

- `Clicker` expose les reglages left/right CPS et les ticks
- `Misc` expose slots, jitter et options de profil
- `Settings` expose le target handle et le bridge local
- `Profiles` gere des profils en memoire
- `Artefacts` liste les DLL du dossier `reconstructed_dlls_complete`
- `Log` affiche les evenements internes

## Limites

`Anydesk--1-readable-analysis.cs` n'est pas recompilable tel quel: les corps importants sont remplaces par `VM_DISPATCH(...)` et plusieurs noms generes par l'obfuscateur ne sont pas des identifiants C# valides.

Ce projet est donc une recreation propre pour etude offline, pas une reconstitution byte-for-byte de la DLL ou du client original.
