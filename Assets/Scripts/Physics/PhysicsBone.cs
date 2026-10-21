using UnityEngine;

namespace ALS.HDT
{
    [DisallowMultipleComponent]
    public class PhysicsBone : MonoBehaviour
    {
        public string id;
        public BodyRole role;
        public BodySide side;
        public JointKind jointKind;
        public int fingerIndex = -1, phalanxIndex;
        public float length;
        public Rigidbody body;
        public ConfigurableJoint joint;
        public Transform target;
        public Quaternion bindRelative = Quaternion.identity, targetRest = Quaternion.identity;
        public Vector3 resetPosition;
        public Quaternion resetRotation = Quaternion.identity;

        public Quaternion CurrentRelative => joint && joint.connectedBody
            ? Quaternion.Inverse(joint.connectedBody.rotation) * body.rotation : body.rotation;
        public Vector3 Angles => JointSpace.SignedEuler(Quaternion.Inverse(bindRelative) * CurrentRelative);
        public float TrackingError => joint && target ? Quaternion.Angle(CurrentRelative, target.localRotation) : 0;
        public float AnchorError => joint && joint.connectedBody
            ? Vector3.Distance(body.position + body.rotation * joint.anchor,
                joint.connectedBody.position + joint.connectedBody.rotation * joint.connectedAnchor) : 0;
    }
}
