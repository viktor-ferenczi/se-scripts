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

### Preparations for use with coding agents

If you want to use coding agents (aka "AI") in your IDE, then do the following: 

1. Install .NET 9.0 SDK (ILSpy dependency)
2. Install ILSpy by running `SetupILSpy.bat`
3. Open the solution in an IDE and force a NuGet Restore
4. Run `DecompileScriptingReferences.bat` to provide the interfaces for the coding agents (also useful for RAG)

A working setup: VSCode + Copilot + Cline with the VS Code LM API provider.

Good models: Claude 4.5, GPT 5.x, Gemini 3

Smaller models may also succeed on simpler tasks.
