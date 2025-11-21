@echo off

mkdir ScriptingReferences 2>NUL

set DLL_DIR=packages\SpaceEngineers.ScriptingReferences.1.3.0\lib\net46
set OPTIONS=--project --languageversion CSharp6 --referencepath %DLL_DIR%

ilspycmd %OPTIONS% -o ScriptingReferences\Sandbox.Common %DLL_DIR%\Sandbox.Common.dll
ilspycmd %OPTIONS% -o ScriptingReferences\Sandbox.Game %DLL_DIR%\Sandbox.Game.dll
ilspycmd %OPTIONS% -o ScriptingReferences\Sandbox.Graphics %DLL_DIR%\Sandbox.Graphics.dll
ilspycmd %OPTIONS% -o ScriptingReferences\SpaceEngineers.Game %DLL_DIR%\SpaceEngineers.Game.dll
ilspycmd %OPTIONS% -o ScriptingReferences\SpaceEngineers.ObjectBuilders %DLL_DIR%\SpaceEngineers.ObjectBuilders.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Audio %DLL_DIR%\VRage.Audio.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage %DLL_DIR%\VRage.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Game %DLL_DIR%\VRage.Game.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Input %DLL_DIR%\VRage.Input.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Library %DLL_DIR%\VRage.Library.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Math %DLL_DIR%\VRage.Math.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Render %DLL_DIR%\VRage.Render.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Render11 %DLL_DIR%\VRage.Render11.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Scripting %DLL_DIR%\VRage.Scripting.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.Steam %DLL_DIR%\VRage.Steam.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.UserInterface %DLL_DIR%\VRage.UserInterface.dll
ilspycmd %OPTIONS% -o ScriptingReferences\VRage.XmlSerializers %DLL_DIR%\VRage.XmlSerializers.dll

pause
