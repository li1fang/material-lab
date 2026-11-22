using System;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public struct RibbonStroke
    {
        public int side;
        // Peak opening displacement in metres, after projecting the hand motion.
        public float peakOpening;
        public int before,after;
    }

    [Serializable]
    public sealed class RibbonTake
    {
        public int version=1;
        public RibbonStroke[] grips;

        public int Replay()
        {
            if(version!=1 || grips==null) throw new ArgumentException("Invalid ribbon recording.");
            var ribbon=new RibbonTear();
            foreach(var stroke in grips)
            {
                if(stroke.before!=ribbon.Broken) throw new InvalidOperationException("Grip start differs.");
                ribbon.Grab(stroke.side);
                ribbon.Open(stroke.peakOpening);
                ribbon.Release();
                if(stroke.after!=ribbon.Broken) throw new InvalidOperationException("Grip end differs.");
            }
            return ribbon.Broken;
        }
    }

    // Reduced displacement-controlled tear. Each cross-yarn bundle consumes a little
    // opening work. Energy remaining below the next rupture relaxes on unloading.
    public sealed class RibbonTear
    {
        public const int Bundles=64, Starter=6;
        public const float Length=.48f, Width=.124f, Slack=.012f;
        public int Broken { get; private set; }
        public int Side { get; private set; }
        public float Peak { get; private set; }
        public float Load { get; private set; }
        public float Front => (Bundles-Starter-Broken)/(float)Bundles;
        public bool Split => Broken==Bundles-Starter;
        int start;
        float consumed;

        public void Grab(int side)
        {
            if(side!=1 && side!=-1) throw new ArgumentOutOfRangeException(nameof(side));
            Side=side; start=Broken; Peak=Load=0; consumed=0;
        }
        static float Cost(int i) => .0042f*(1+.20f*Mathf.Sin(i*1.73f)+.11f*Mathf.Cos(i*.83f));

        public void Open(float opening)
        {
            if(!float.IsFinite(opening) || opening<0) throw new ArgumentOutOfRangeException(nameof(opening));
            if(Side==0) return;
            Peak=Mathf.Max(opening,Peak);
            float available=opening-Slack;
            while(!Split && available>=consumed+Cost(Broken))
            {
                consumed+=Cost(Broken);
                Broken++;
            }
            Load=Split?0:Mathf.Clamp01((available-consumed)/Cost(Broken));
        }

        public RibbonStroke Release()
        {
            var receipt=new RibbonStroke {side=Side,peakOpening=Peak,before=start,after=Broken};
            Side=0;Load=0;
            return receipt;
        }
    }
}
