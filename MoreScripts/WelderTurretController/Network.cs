using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRageMath;

namespace WelderTurretController
{
	partial class Program : MyGridProgram
	{
		enum MSG
		{
			SETTARGET,
			TURRETPING,
		}
		class Network
		{
			Program p;
			IMyBroadcastListener listener = null;
			static public string tag = "WelderTurrets";
			public Network(Program p)
			{
				listener = p.IGC.RegisterBroadcastListener(tag);
				this.p = p;
			}
			public void upd()
			{
				p.culldead();
				p.sendpings();
				while (listener.HasPendingMessage)
				{
					MyIGCMessage myIGCMessage = listener.AcceptMessage();
					if (myIGCMessage.Tag != tag) continue;
					var d = myIGCMessage.Data;
					try
					{
						if (d == null) continue;

						if (d is MyTuple<int, int, Vector3D>)
						{
							var dat = (MyTuple<int, int, Vector3D>)d;
							var m = dat.Item1;
							if (m == (int)MSG.SETTARGET)
							{
								foreach (var t in p.turrets)
								{
									if (!t.remote && t.ID == dat.Item2)
									{
										t.setTarget(dat.Item3);
										break;
									}
								}
							}else if(m == (int)MSG.TURRETPING)
							{
								p.recping(dat.Item2);
							}
						}
					}
					catch (Exception) { }
				}
			}
		}
	}
}
