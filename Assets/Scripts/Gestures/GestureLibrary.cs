using System;
using UnityEngine;

namespace ALS.HDT
{
    public enum GestureType { Idle, Handshake, Wave, DoubleThumbsUp }
    public enum GestureState { Idle, BlendingIn, Performing, Returning }

    [Serializable]
    public class ArmPose
    {
        public Vector3 shoulder, wrist;
        public float elbow, pronation, fingerCurl, thumbCurl, thumbSpread;
        public float[] fingerCurls;
        public float FingerCurl(int index) => fingerCurls != null && fingerCurls.Length == 4 ? fingerCurls[index] : fingerCurl;
        public ArmPose Copy() { var copy = (ArmPose)MemberwiseClone(); copy.fingerCurls = fingerCurls == null ? null : (float[])fingerCurls.Clone(); return copy; }
        public static ArmPose Blend(ArmPose a, ArmPose b, float t) => new ArmPose {
            shoulder = Vector3.Lerp(a.shoulder, b.shoulder, t), wrist = Vector3.Lerp(a.wrist, b.wrist, t),
            elbow = Mathf.Lerp(a.elbow, b.elbow, t), pronation = Mathf.Lerp(a.pronation, b.pronation, t),
            fingerCurl = Mathf.Lerp(a.fingerCurl, b.fingerCurl, t), thumbCurl = Mathf.Lerp(a.thumbCurl, b.thumbCurl, t),
            thumbSpread = Mathf.Lerp(a.thumbSpread, b.thumbSpread, t),
            fingerCurls = new[] { Mathf.Lerp(a.FingerCurl(0), b.FingerCurl(0), t), Mathf.Lerp(a.FingerCurl(1), b.FingerCurl(1), t),
                Mathf.Lerp(a.FingerCurl(2), b.FingerCurl(2), t), Mathf.Lerp(a.FingerCurl(3), b.FingerCurl(3), t) }
        };
    }

    [Serializable]
    public class UpperBodyPose
    {
        public ArmPose left = new ArmPose(), right = new ArmPose();
        public UpperBodyPose Copy() => new UpperBodyPose { left = left.Copy(), right = right.Copy() };
        public static UpperBodyPose Blend(UpperBodyPose a, UpperBodyPose b, float t)
            => new UpperBodyPose { left = ArmPose.Blend(a.left, b.left, t), right = ArmPose.Blend(a.right, b.right, t) };
    }

    [Serializable]
    public class GestureDefinition
    {
        public GestureType type;
        public float duration = 3.8f;
        [Range(.1f, .4f)] public float blendFraction = .23f;
        public int cycles = 3;
        public float amplitude = 14;
        public UpperBodyPose pose = new UpperBodyPose();
    }

    [CreateAssetMenu(menuName = "ALS HDT/Gesture Library")]
    public class GestureLibrary : ScriptableObject
    {
        public UpperBodyPose idle = new UpperBodyPose();
        public float returnDuration = .65f;
        public GestureDefinition[] gestures;
        public GestureDefinition Find(GestureType type) => Array.Find(gestures, item => item.type == type);
    }
}
