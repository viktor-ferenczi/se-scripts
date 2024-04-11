using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.GUI.TextPanel;

namespace WelderTurretController
{
	partial class Program : MyGridProgram
	{

		List<WeldTurret> turrets = new List<WeldTurret>();
		List<IMyProjector> projectors = new List<IMyProjector>();
		IMyTextPanel LCD = null;
        IMyRadioAntenna antenna = null;
		void load()
		{
			mgp = new MultigridProjectorProgrammableBlockAgent(Me);
			if (mgp.Available) Echo("Multigrid Projector API ready!");
            var lc = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(Cfg.MLCD);
            var bg = GridTerminalSystem.GetBlockGroupWithName(Cfg.ToolGrouName);
			if (bg != null)
			{
				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				bg.GetBlocks(blocks);

				List<IMyMotorAdvancedStator> hinges = new List<IMyMotorAdvancedStator>();
				List<IMyTerminalBlock> tools = new List<IMyTerminalBlock>();
				foreach (var b in blocks)
				{
					var st = b.BlockDefinition.SubtypeId;
					if (st == "LargeHinge" || st == "SmallHinge" || st == "MediumHinge")
					{
						hinges.Add((IMyMotorAdvancedStator)b);
					}
					else if (lc is IMyTextPanel)
					{
						LCD = (IMyTextPanel)lc;
						if (LCD.ContentType != ContentType.TEXT_AND_IMAGE)
						{
							LCD.ContentType = ContentType.TEXT_AND_IMAGE;
							LCD.FontSize = 0.666F;
						}
					}
					else if (!(b is IMyMotorAdvancedStator))
					{
						tools.Add(b);
					}
				}
				foreach (var b in blocks)
				{
					var st = b.BlockDefinition.SubtypeId;
					if (st == "LargeAdvancedStator" || st == "SmallAdvancedStatorSmall" || st == "SmallAdvancedStator")
					{
						var r = (IMyMotorAdvancedStator)b;
						IMyMotorAdvancedStator hi = null;//var t = new WeldTurret();
						//t.rotor = r;
						foreach (var h in hinges)
						{
							if (h.CubeGrid == r.TopGrid)
							{
								hi = h;
						
								break;
							}
						}
						if (hi != null)
						{
							var t = new WeldTurret(r, hi); 
							foreach (var w in tools)
							{
								if (w.CubeGrid == t.hinge.TopGrid && w is IMyFunctionalBlock)
									t.tools.Add((IMyFunctionalBlock)w);
							}
							turrets.Add(t);
						}
					}
				}
			}
			Echo("Detected " + turrets.Count + " turret.");
			List<IMyRadioAntenna> ant = new List<IMyRadioAntenna>();
			GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(ant, b => b.Enabled);
			if (ant.Count > 0)
			{
				antenna = ant[0];
				Echo("Antenna found.");
			}
			else
			{
				Runtime.UpdateFrequency = UpdateFrequency.None;
				Echo("No antenna found.");
			}
			projectors.Clear();
			GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors);

		}
	}
}
