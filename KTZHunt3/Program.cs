using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Resources;
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

namespace KTZHunt3
{
    partial class Program : MyGridProgram
    {
        const double maxScriptTimeMSPerSec = 0.25; //!

        static public Program gProgram = null;
        public WcPbApi APIWC = null;

        public Program()
        {
            gProgram = this;
            log("BOOT", LT.LOG_N);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            gProgram = null;
        }

        public static int tick = -1;
        BurnoutTrack bt60 = new BurnoutTrack(60, maxScriptTimeMSPerSec);

        static Profiler initP = new Profiler("init");
        static Profiler mainP = new Profiler("main");

        #region premain

        public void Main(string arg, UpdateType upd)
        {
            gProgram = this;
            tick += 1;

            #region burnoutfailsafepre

            if (bt60.burnoutpre()) return;

            #endregion

            if (tick % 20 == 0)
                if (Me.Closed)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    return;
                }
            mainP.start();
            main(arg, upd);
            mainP.stop();
            if (tick % 5 == 0)
            {
                Echo(tick.ToString());
                if (profileLog != null) profileLog.WriteText("name:ms1t:ms60t\n" + Profiler.getAllReports());
                /*if (gInv != null)
                {
                    Echo(gInv.lastStatus);
                }*/
            }
            if (consoleLog != null && tick % 5 == 0)
            {
                if (Logger.loggedMessagesDirty)
                {
                    Logger.updateLoggedMessagesRender();
                    consoleLog.WriteText(Logger.loggedMessagesRender);
                }
            }

            #region burnoutfailsafepost

            if (bt60.burnoutpost()) return;

            #endregion

        }

        #endregion

        void main(string arg, UpdateType upd)
        {
            initP.start();
            var loaded = load(0.05);
            initP.stop();
            if (!loaded) return;

            processRadar(0.025);
        }
    }
}