using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WelderTurretController
{
	partial class Program : MyGridProgram
	{
		int lstst = 0;
		void genStatus()
		{
			if (tick - lstst > 60 * 2)
			{

				lstst = tick;
				bool welding = scanner != null && scanner.weldTargets.Count > 0;

				StringBuilder b = new StringBuilder();
				bapp(b, "WELDER TURRET CONTROLLER\n\n");
				bapp(b, "Status:", welding ? "Welding" : "Idle", "\n");
				int l = 0;
				int r = 0;
				foreach (var t in turrets)
				{
					if (t.remote) r++;
					else l++;
				}
				bapp(b, "Turrets: ", turrets.Count, " (", l, " local, ", r, " remote)\n");
				bapp(b, "\n");
				var n = 0;
				foreach (var t in turrets)
				{
					bapp(b, "t#", n++, ">", v2ss(t.target), "\n");
				}
			
					if (welding)
				{
					
					//if (REMOTE == false)
					{
						IMyProjector c = null;
						foreach (var p in projectors)
						{
							if (p.IsProjecting && p.Enabled)
							{
								c = p;
								break;
							}
						}
						if (c != null)
						{
							int tb = c.TotalBlocks;
							int rb = c.RemainingBlocks;
							int ra = c.RemainingArmorBlocks;
							double perc = 100 - ((double)rb * 100 / tb);
							bapp(b, "Weld progress ", perc.ToString("0.0"), "%\n");
							bapp(b, "Total blocks in blueprint: ", tb, "\n");
							bapp(b, "Functional blocks remaining: ", rb - ra, "\n");
							bapp(b, "Armor blocks remaining: ", ra, "\n");

						}
					}
				}
				string w = b.ToString();
				if (w != lastwrite)
				{
					lastwrite = w;
					LCD.WriteText(w);
				}
			}
		}

		string lastwrite = "";

		static void bapp(StringBuilder b, params object[] args)
		{
			foreach (object a in args)
			{
				b.Append(a.ToString());
			}
		}
	}
}
