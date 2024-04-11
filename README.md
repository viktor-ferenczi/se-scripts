## Space Engineers Ingame Scripts

- Scripts are merged using the [ScriptMerge](https://github.com/viktor-ferenczi/se-script-merge) tool
- Merged scripts are deployed to their respective folders under `%AppData%\SpaceEngineers\IngameScripts\local`
- Scripts are automatically loaded into their respective PBs using the [ScriptDev](https://github.com/viktor-ferenczi/se-script-dev) plugin

### Example commands

#### OmniBeam Arm Controller

**Debug**

```sh
IngameScriptMergeTool.exe -n OmniBeam -d OmniBeam
```

**Release**

```sh
IngameScriptMergeTool.exe -n OmniBeam -d OmniBeam -maur
```
