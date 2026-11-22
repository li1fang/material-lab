using System;

namespace MaterialDestruction
{
    [Serializable]
    public sealed class BranchReport
    {
        public int tick, joint, broken;
        public bool detached;
        public FiberEvent[] events;
        public float peakDeflectionM, residualDeflectionM, fragmentFallM;
    }
}
