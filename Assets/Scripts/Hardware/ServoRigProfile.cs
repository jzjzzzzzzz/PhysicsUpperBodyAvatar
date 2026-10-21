using System;
using UnityEngine;

namespace ALS.HDT
{
    // Logical order, NOT a claim about the physical controller's port numbers.
    public enum ServoFunction { ShoulderFlexion, ShoulderRotation, ShoulderAbduction, ElbowFlexion,
        ForearmRotation, WristFlexion, WristDeviation, IndexCurl, MiddleCurl, RingCurl, LittleCurl, ThumbCurl, ThumbSpread }

    [Serializable]
    public class ServoChannel
    {
        public string id, label;
        public ServoFunction function;
        public int controllerPort = -1;
        public float inputMin = 0, inputMax = 180;
        public bool reversed;
        public float targetMin, targetMax;
        public float maxInputDegreesPerSecond = 180;

        public float Decode(float input)
        {
            float u = Mathf.InverseLerp(inputMin, inputMax, input);
            return Mathf.Lerp(targetMin, targetMax, reversed ? 1 - u : u);
        }
        public float Encode(float target)
        {
            float u = Mathf.InverseLerp(targetMin, targetMax, target);
            return Mathf.Lerp(inputMin, inputMax, reversed ? 1 - u : u);
        }
    }

    [CreateAssetMenu(menuName = "ALS HDT/13-channel Servo Rig")]
    public class ServoRigProfile : ScriptableObject
    {
        public string schema = "als-hdt-servo-rig-v1";
        [TextArea] public string calibrationNotes = "PROVISIONAL: 7 arm + 6 hand per side. Port IDs, endpoint angles, direction, handedness and linkage ratios are not measured from hardware. 0..180 is a virtual input example, not a verified servo travel range.";
        public bool hardwareCalibrated;
        public BodySide side;
        public ServoChannel[] channels;
        [Tooltip("Desired flexion degrees per unit curl; each ray remains physically articulated.")]
        public Vector3 fingerFlexion = new Vector3(75, 85, 55);
        public Vector3 thumbFlexion = new Vector3(45, 55, 55);

        public static ServoRigProfile CreateDefault(BodySide side)
        {
            var result = CreateInstance<ServoRigProfile>(); result.side = side;
            string[] ids = { "A1", "A2", "A3", "A4", "A5", "A6", "A7", "H1", "H2", "H3", "H4", "H5", "H6" };
            string[] names = { "Shoulder flex", "Shoulder rotate", "Shoulder raise", "Elbow flex", "Forearm rotate", "Wrist flex", "Wrist deviate", "Index curl", "Middle curl", "Ring curl", "Little curl", "Thumb curl", "Thumb spread" };
            float[] mins = { -105, -100, -112, -145, -90, -45, -28, 0, 0, 0, 0, 0, -35 };
            float[] maxs = { 65, 100, 112, 0, 90, 45, 28, 1, 1, 1, 1, 1, 35 };
            result.channels = new ServoChannel[13];
            for (int i = 0; i < 13; i++) result.channels[i] = new ServoChannel {
                id = ids[i], label = names[i], function = (ServoFunction)i, targetMin = mins[i], targetMax = maxs[i]
            };
            return result;
        }

        public void Validate()
        {
            if (side == BodySide.None || channels == null || channels.Length != 13) throw new ArgumentException("Exactly 13 ordered channels and a left/right side are required.");
            for (int i = 0; i < 13; i++) {
                var c = channels[i];
                if (c == null || c.function != (ServoFunction)i || !float.IsFinite(c.inputMin) || !float.IsFinite(c.inputMax)
                    || !float.IsFinite(c.targetMin) || !float.IsFinite(c.targetMax) || !float.IsFinite(c.maxInputDegreesPerSecond)
                    || c.inputMax <= c.inputMin || c.targetMax <= c.targetMin || c.maxInputDegreesPerSecond <= 0)
                    throw new ArgumentException("Invalid channel calibration at index " + i);
            }
            for (int i = 0; i < 3; i++) if (!float.IsFinite(fingerFlexion[i]) || fingerFlexion[i] < 0 || fingerFlexion[i] > 100
                || !float.IsFinite(thumbFlexion[i]) || thumbFlexion[i] < 0 || thumbFlexion[i] > (i == 0 ? 70 : 100))
                throw new ArgumentException("Finger synergy exceeds joint limits.");
            // Keep remapping endpoints inside the built-in physical rig's anatomy.
            var defaults = CreateDefault(side);
            try { for (int i = 0; i < 13; i++)
                if (channels[i].targetMin < defaults.channels[i].targetMin || channels[i].targetMax > defaults.channels[i].targetMax)
                    throw new ArgumentException("Target range exceeds the prototype anatomy at channel " + i);
            } finally { if (Application.isPlaying) Destroy(defaults); else DestroyImmediate(defaults); }
        }

        public float[] Encode(ArmPose pose)
        {
            float[] logical = { pose.shoulder.x, pose.shoulder.y, pose.shoulder.z, pose.elbow, pose.pronation,
                pose.wrist.x, pose.wrist.z, pose.FingerCurl(0), pose.FingerCurl(1), pose.FingerCurl(2), pose.FingerCurl(3), pose.thumbCurl, pose.thumbSpread };
            var result = new float[13]; for (int i = 0; i < 13; i++) result[i] = channels[i].Encode(logical[i]); return result;
        }
        public ArmPose Decode(float[] values)
        {
            var q = new float[13]; for (int i = 0; i < 13; i++) q[i] = channels[i].Decode(values[i]);
            return new ArmPose { shoulder = new Vector3(q[0], q[1], q[2]), elbow = q[3], pronation = q[4],
                wrist = new Vector3(q[5], 0, q[6]), fingerCurls = new[] { q[7], q[8], q[9], q[10] }, thumbCurl = q[11], thumbSpread = q[12] };
        }
    }
}
