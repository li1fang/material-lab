using System;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public sealed class CeramicResult
    {
        public int version=1;
        public string trial;
        public Vector3 dishPosition;
        public int detachStep=-1, steps, missingCells, fragments;
        public float maxChipTravelM;
        public CeramicContact[] contacts;

        public void VerifyReplay()
        {
            if(version!=1 || contacts==null) throw new ArgumentException("Expected a version 1 ceramic contact recording.");
            var rim=new CeramicRim();
            int last=-1, groups=0;
            foreach(var hit in contacts)
            {
                if(hit==null || hit.released==null || hit.step<last || hit.step>steps)
                    throw new InvalidOperationException("Invalid contact sequence.");
                int[] ids=rim.Strike(hit.localPoint,hit.localDirection,hit.impulseNs);
                if(ids.Length!=hit.released.Length) throw new InvalidOperationException("Release count differs.");
                for(int i=0;i<ids.Length;i++)
                {
                    if(ids[i]!=hit.released[i]) throw new InvalidOperationException("Release ID differs.");
                    if(i==0 || (ids[i-1]+1)%CeramicRim.Cells!=ids[i]) groups++;
                }
                last=hit.step;
            }
            if(rim.Missing!=missingCells || fragments!=groups) throw new InvalidOperationException("Final rim or fragment count differs.");
        }
    }
}
