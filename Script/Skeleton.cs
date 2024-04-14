﻿// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable RedundantUsingDirective
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
// ReSharper disable CheckNamespace

// Import everything available for PB scripts in-game
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;


// TODO: Change the namespace name to something meaningful,
//       Put all the code which needs to be deployed as part of
//       your script into this same namespace!
namespace ScriptSkeleton
{
    // TODO: See the README.md for more: Hints and ScriptDev client plugin
    // TODO: Add your supporting code in separate classes and source files

    // ReSharper disable once UnusedType.Global
    class Program : MyGridProgram
    {
        public Program()
        {
            // TODO: One-time initialization executed when the PB program is loaded
        }

        // ReSharper disable once UnusedMember.Global
        public void Main(string argument, UpdateType updateSource)
        {
            // TODO: This is executed when the PB is run
        }

        // ReSharper disable once UnusedMember.Global
        public void Save()
        {
            // TODO: You can run code here before the game is saved (optional method)
        }
    }
}