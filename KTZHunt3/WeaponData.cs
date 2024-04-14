using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTZHunt3
{
    partial class Program : MyGridProgram
    {
        static public List<RailData> RailDatalist = new List<RailData>();
        static public Dictionary<string, RailData> RailDataSubType = new Dictionary<string, RailData>();

        public class RailData
        {
            public string SubTypeId;
            public int chargeTicks = 0;
            public int DUF = 0;
            public float maxDraw = 0;
            public float ammoVel = 0;

            public RailData(string subtype, int ticks, float maxcharge, int duf, float vel)
            {
                SubTypeId = subtype;
                RailDatalist.Add(this);
                RailDataSubType[SubTypeId] = this;
                chargeTicks = ticks;
                maxDraw = maxcharge;
                DUF = duf;
                ammoVel = vel;
            }
        }



        class WeaponState
        {
            public IMyTerminalBlock b = null;
            public RailData settings = null;
            public bool isCharging = false;

            public float chargeProgress = 0;

            public void setCharging(bool b)
            {
                if (b != isCharging)
                {
                    isCharging = b;
                    if (b) chargeProgress = 0;
                    else chargeProgress = 1;
                }
            }

            float lDraw = 0;
            float lProg = 0;
            public float lastDrawFactor = 0;

            public void update()
            {
                if (lDraw == 0 || tick % 3 == 0)
                {
                    lDraw = gProgram.APIWC.GetCurrentPower(b);
                    setCharging(lDraw > 5);
                }
                if (isCharging)
                {
                    if (tick % 3 == 0)
                    {
                        lastDrawFactor = lDraw / settings.maxDraw;
                        lProg = 1.0f / settings.chargeTicks * lastDrawFactor;
                    }
                    chargeProgress += lProg;
                    if (chargeProgress > 1)
                    {
                        chargeProgress = 1;
                    }
                }
            }
        }

        Dictionary<IMyTerminalBlock, WeaponState> wsdict = new Dictionary<IMyTerminalBlock, WeaponState>();

        WeaponState getWS(IMyTerminalBlock b)
        {
            WeaponState ws = null;
            wsdict.TryGetValue(b, out ws);
            if (ws == null)
            {
                if (RailDataSubType.ContainsKey(b.DefinitionDisplayNameText))
                {
                    ws = wsdict[b] = new WeaponState();
                    ws.settings = RailDataSubType[b.DefinitionDisplayNameText];
                    ws.b = b;
                }
            }
            return ws;
        }
    }
}