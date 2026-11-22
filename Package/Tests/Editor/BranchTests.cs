using System;
using NUnit.Framework;
using UnityEngine;

namespace MaterialDestruction.Tests
{
    public class BranchTests
    {
        [TestCase(-1.24f,"upper")]
        [TestCase(1.24f,"lower")]
        public void OverloadSplitsOuterFibersBeforeDetachment(float peak,string side)
        {
            var trial=new BranchTrial {peak=peak};
            var m=trial.Simulate();
            Assert.That(m.Broken,Is.EqualTo(BranchModel.Fibers));
            Assert.That(m.Events[0].side,Is.EqualTo(side));
            Assert.That(m.Events[0].tick,Is.LessThan(m.Events[m.Events.Count-1].tick));
            Assert.That(JsonUtility.ToJson(m.Events[0]),Is.EqualTo(JsonUtility.ToJson(trial.Simulate().Events[0])));
        }

        [Test]
        public void ShallowLoadBendsAndRecovers()
        {
            var trial=new BranchTrial {peak=-.24f};
            var m=new BranchModel(trial.profile);
            Vector3 rest=m.Centers[BranchModel.Links];
            float distance=0;
            for(int i=0;i<trial.Duration;i++)
            {
                m.Step(trial.DriveAt(i),trial.contact);
                distance=Mathf.Max(distance,Vector3.Distance(m.Centers[BranchModel.Links],rest));
            }
            Assert.That(distance,Is.GreaterThan(.03f));
            Assert.That(Vector3.Distance(m.Centers[BranchModel.Links],rest),Is.LessThan(.001f));
            Assert.That(m.Broken,Is.Zero);
        }

        [Test]
        public void NearClampLoadHasShortLever()
        {
            Assert.That(new BranchTrial {contact=.15f}.Simulate().Broken,Is.Zero);
        }

        [Test]
        public void SampledInputAndParametricTrialAgree()
        {
            var t=new BranchTrial();
            var copy=new BranchTrial {samples=new BranchInput[t.Duration]};
            for(int i=0;i<t.Duration;i++) copy.samples[i]=new BranchInput {drive=t.DriveAt(i),contact=t.contact};
            Assert.That(copy.Simulate().Events,Is.EqualTo(t.Simulate().Events));
        }

        [Test]
        public void RejectInvalidCase()
        {
            Assert.Throws<ArgumentException>(()=>BranchTrial.Read("{\"version\":77}"));
            Assert.Throws<ArgumentException>(()=>new BranchTrial {contact=-1}.Simulate());
        }
        [Test]
        public void ReleaseKeepsPartialDamageAndNewInputContinuesIt()
        {
            var m=new BranchModel(new BranchProfile());
            while(m.Broken==0 && m.Tick<150) m.Step(-1f,.83f);
            Assert.That(m.Broken,Is.InRange(1,BranchModel.Fibers-1));
            int broken=m.Broken;
            for(int i=0;i<100;i++) m.Step(0,.83f);
            Assert.That(m.Broken,Is.EqualTo(broken));
            for(int i=0;i<150;i++) m.Step(-1.3f,.83f);
            Assert.That(m.Detached);
        }

        [Test]
        public void InstantComparisonUsesSameOnsetAndDifferentDuration()
        {
            var staged=new BranchTrial().Simulate();
            var instant=new BranchTrial {releaseAtFirstBreak=true}.Simulate();
            Assert.That(instant.Events[0],Is.EqualTo(staged.Events[0]));
            Assert.That(instant.Events[1].tick,Is.EqualTo(instant.Events[0].tick));
        }
    }
}
