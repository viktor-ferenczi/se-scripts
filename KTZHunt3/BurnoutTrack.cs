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
        const int PBLIMIT_STARTUPTICKS = 0; //20 by default

        class BurnoutTrack
        {
            public double maxmspersec = 0.25;
            public static double[] defertrack;
            public int len = 60;

            public BurnoutTrack(int l, double ms)
            {
                len = l;
                maxmspersec = ms;
                defertrack = new double[len];
            }

            int defercalls = 0;
            int deferpos = 0;
            static bool hangflag = false;
            int hangticks = 0;
            int hangtick = 0;
            bool fsdbg = false;
            DateTime bf = DateTime.Now;

            public bool burnoutpre()
            {
                bf = DateTime.Now;
                if (hangflag)
                {
                    if (tick > hangtick)
                    {
                        double avg = 0;
                        foreach (var d in defertrack) avg += d;
                        avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
                        if (avg > maxmspersec * len / 60)
                        {
                            defertrack[deferpos] = 0;
                            defercalls += 1;
                            deferpos = (deferpos + 1) % defertrack.Length;
                            return true;
                        }
                        else
                        {
                            hangflag = false;
                            //log("Resuming after " + (hangticks / 60.0d).ToString("0.0") + "s", LT.LOG_N);
                        }
                    }
                }
                return hangflag;
            }

            public double avg()
            {
                double avg = 0;
                foreach (var d in defertrack) avg += d;
                avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
                return avg;
            }

            public void setwait(int ticks)
            {
                hangticks = ticks;
                hangtick = tick + ticks;
                hangflag = true;
            }

            public bool burnoutpost()
            {
                double ms = (DateTime.Now - bf).TotalMilliseconds;
                defertrack[deferpos] = ms;
                defercalls += 1;
                deferpos = (deferpos + 1) % defertrack.Length;
                if (!hangflag)
                {
                    double p_avg = 0;
                    foreach (var d in defertrack) p_avg += d;
                    int divisor = defercalls > defertrack.Length ? defertrack.Length : defercalls;
                    var avg = p_avg / divisor;
                    var mtch = maxmspersec * len / 60;
                    if (avg > mtch)
                    {
                        int tickstodoom = PBLIMIT_STARTUPTICKS - tick;
                        if (tickstodoom > 0 && tickstodoom * maxmspersec < avg) return false;

                        int waitticks = 0;
                        while (p_avg / (divisor + waitticks) > mtch) waitticks++;

                        hangticks = waitticks;
                        hangtick = tick + waitticks;
                        hangflag = true;


                        var lstr = tick + ": " + avg.ToString("0.00") + ">" + (mtch).ToString("0.00") + "ms/s exec. Sleeping " + (hangticks / 60.0d).ToString("0.0") + "s";
                        log(lstr, LT.LOG_N);
                        /*var c = getCtrl();
                        if (c != null)
                        {
                            if (!fsdbg)
                            {
                                c.CustomData = "";
                                fsdbg = true;
                            }
                            c.CustomData += "\n\n" + lstr + "\n\n" + Profiler.getAllReports();
                        }
                        else getCtrlTick = -9000;*/

                        return true;
                    }
                }
                else return true;
                return false;
            }
        }
    }
}