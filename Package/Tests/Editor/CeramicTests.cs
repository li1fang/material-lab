using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MaterialDestruction.Tests
{
    public class CeramicTests
    {
        Vector3 At(int sector) {
            float angle=(sector+.5f)*2*Mathf.PI/CeramicRim.Cells;
            return new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*CeramicRim.Radius*.97f;
        }
        [Test]
        public void LightImpactAndCoreContactLeaveRimWhole()
        {
            var rim=new CeramicRim();
            Assert.IsEmpty(rim.Strike(At(1),Vector3.down,.08f));
            Assert.IsEmpty(rim.Strike(Vector3.zero,Vector3.down,.8f));
            Assert.AreEqual(0,rim.Missing);
        }
        [Test]
        public void ContactsCanCrossSeamAndReleaseEachCellOnlyOnce()
        {
            var rim=new CeramicRim();
            Assert.AreEqual(new[]{35,0,1},rim.Strike(At(0),Vector3.down,.45f));
            Assert.IsEmpty(rim.Strike(At(0),Vector3.down,.45f));
            Assert.AreEqual(new[]{17,18,19},rim.Strike(At(18),Vector3.down,.45f));
            Assert.AreEqual(6,rim.Missing);
        }
        [Test]
        public void ContactLogDetectsChangedReleaseIds()
        {
            var hit=new CeramicContact {step=9,impulseNs=.25f,source="wood",localPoint=At(5),localDirection=Vector3.down};
            hit.released=new CeramicRim().Strike(hit.localPoint,hit.localDirection,hit.impulseNs);
            var log=new CeramicResult{steps=10,contacts=new[]{hit},missingCells=1,fragments=1};
            log.VerifyReplay();
            hit.released[0]++;
            Assert.Throws<InvalidOperationException>(log.VerifyReplay);
        }
    }
}
