/*
 * This is a skeleton for developing Space Engineers programmable block scripts
 *
 * Auto-completion needs Space Engineers being installed to its standard location in Steam.
 *
 * Make a copy of this script first.
 * Change the name of the namespace to your topic.
 * Edit your script in JetBrains Rider or Microsoft Visual Studio.
 * Fill in the missing code where you see "TODO" below.
 * Make sure your IDE does not detect any errors, look for red/yellow highlights.
 * Copy-paste the contents of the CodeEditor region into the programmable block in Space Engineers.
 * Check the code in Space Engineers, it should be ready to run if no compilation errors reported.
 *
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
using ContentType = VRage.Game.GUI.TextPanel.ContentType;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace GarageCranes
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        private IMyTextSurface Surface
        {
            get
            {
                return Me.GetSurface(0);
            }
        }

        private void Cls()
        {
            Surface.WriteText("");
        }

        private void Print(string text)
        {
            Surface.WriteText(text, true);
        }

        private IMyBlockGroup CranePistons => GridTerminalSystem.GetBlockGroupWithName("Crane Pistons");
        private Dictionary<string, IList<IMyPistonBase>> _pistons = new Dictionary<string, IList<IMyPistonBase>>(8);

        public Program()
        {
            var pistons = new List<IMyPistonBase>();
            CranePistons?.GetBlocksOfType(pistons);

            foreach (var piston in pistons)
            {
                if (!_pistons.ContainsKey(piston.CustomName))
                    _pistons[piston.CustomName] = new List<IMyPistonBase>(3);
                _pistons[piston.CustomName].Add(piston);
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "")
            {
                PrintPistons();
                return;
            }

            if (argument.Length != 2)
                return;

            var number = argument[0];
            var direction = argument[1];

            switch (direction)
            {
                case '+':
                    ExtendPiston(number);
                    break;

                case '-':
                    RetractPiston(number);
                    break;
            }
        }

        private void PrintPistons()
        {
            var names = _pistons.Keys.ToList();
            names.SortNoAlloc(string.CompareOrdinal);

            Cls();
            foreach (var name in names)
                Print($"{_pistons[name].Count} {name}\n");
        }

        private void ExtendPiston(char number)
        {
            foreach (var piston in _pistons[$"Crane Piston {number}"])
            {
                var left = 2.0f - piston.CurrentPosition;
                piston.MaxLimit = Math.Min(2.0f, piston.CurrentPosition + 0.1f + left * 0.05f);
                piston.Velocity = 1;
            }
        }

        private void RetractPiston(char number)
        {
            foreach (var piston in _pistons[$"Crane Piston {number}"])
            {
                piston.MinLimit = Math.Min(2.0f, piston.CurrentPosition - 0.2f);
                piston.Velocity = -1;
            }
        }

        #endregion
    }
}