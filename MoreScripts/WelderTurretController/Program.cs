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

namespace WelderTurretController
{
	partial class Program : MyGridProgram
	{
		public class Cfg
		{
			public static string MLCD = "MainLCD";
			public static string ToolGrouName = "WelderTurrets";
		}

        public static Program gProgram = null;
        public Program()
        {
            gProgram = this;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            network = new Network(this);
        }
        Network network = null;

        public void Save()
        {

        }

        static MultigridProjectorProgrammableBlockAgent mgp = null;



        int tick = 0;
        bool fr = true;
        public void Main(string argument, UpdateType updateSource)
        {
            tick += 10;
            if (fr)
            {
                fr = false;
                load();
            }
            network.upd();

            if (scanner != null)
            {
                scanner.upd();
                scanner.updateWeldAssignments();
                if (scanner.weldTargets.Count == 0) scanner = null;
            }
            foreach (var t in turrets)
            {
                t.update();
            }

            if (argument == "weld")
            {
                weld();
            }
            genStatus();
        }

        WeldTargetComp scanner = null;
        void weld()
        {
            scanner = new WeldTargetComp(this);

        }

    }
}
