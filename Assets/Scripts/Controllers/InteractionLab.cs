using UnityEngine;

namespace ALS.HDT
{
    public class InteractionLab : MonoBehaviour
    {
        public GameObject resistanceGate;
        public Rigidbody movableObject;
        public Collider supportSurface;
        public float ExpectedObjectRestY => supportSurface.bounds.max.y + movableObject.GetComponent<Collider>().bounds.extents.y;
        public Vector3 objectStartPosition;
        public bool GateEnabled => resistanceGate && resistanceGate.activeSelf;
        public void SetGate(bool active) => resistanceGate.SetActive(active);
        public void ToggleGate() => SetGate(!GateEnabled);
        public void ResetObject()
        {
            movableObject.linearVelocity = movableObject.angularVelocity = Vector3.zero;
            movableObject.position = objectStartPosition; movableObject.rotation = Quaternion.identity;
            movableObject.WakeUp();
        }
    }
}
