using System;
using UnityEngine;

namespace MaterialDestruction
{
    public static class BranchFragment
    {
        // The fragment mesh and both centerlines use the target object's local coordinates.
        public static Rigidbody Attach(GameObject target,BranchModel model,Vector3[] previousCenters,float mass)
        {
            if (!model.Detached || previousCenters==null || previousCenters.Length!=model.Centers.Length ||
                !float.IsFinite(mass) || mass<=0 || target.GetComponent<Rigidbody>())
                throw new ArgumentException("Expected a newly detached span, two poses and a positive mass.");

            int joint=model.CrackJoint;
            var space=target.transform;
            for(int a=joint;a<BranchModel.Links;a+=3)
            {
                int b=Mathf.Min(a+3,BranchModel.Links);
                Vector3 delta=model.Centers[b]-model.Centers[a];
                var child=new GameObject("Wood collision");
                child.transform.SetParent(space,false);
                child.transform.localPosition=(model.Centers[a]+model.Centers[b])*.5f;
                child.transform.localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                var capsule=child.AddComponent<CapsuleCollider>();
                capsule.radius=model.Radius((float)a/BranchModel.Links);
                capsule.height=delta.magnitude+2*capsule.radius;
            }
            var body=target.AddComponent<Rigidbody>();
            body.mass=mass;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            var end=model.Centers[BranchModel.Links];
            var endVelocity=(end-previousCenters[BranchModel.Links])/BranchModel.StepSeconds;
            var baseVelocity=(model.Centers[joint]-previousCenters[joint])/BranchModel.StepSeconds;
            var lever=end-model.Centers[joint];
            Vector3 spin=Vector3.Cross(lever,endVelocity-baseVelocity)/Mathf.Max(.0001f,lever.sqrMagnitude);
            body.angularVelocity=space.TransformDirection(spin);
            body.linearVelocity=space.TransformVector(endVelocity+Vector3.Cross(spin,
                space.InverseTransformPoint(body.worldCenterOfMass)-end));
            return body;
        }
    }
}
