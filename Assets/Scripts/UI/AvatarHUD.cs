using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ALS.HDT
{
    [RequireComponent(typeof(UIDocument))]
    public class AvatarHUD : MonoBehaviour
    {
        public AvatarController avatar;
        public InteractionLab lab;
        public StyleSheet stylesheet;
        private Label state, physics, diagnostics, gateState, servoState;
        private VisualElement debugPanel;
        private Button pause;
        private float lastSample, nextUpdate, hz;
        private int lastSteps;
        private bool showDebug;
        public bool SuppressNativeInputForVerification { get; set; }
        public bool DispatchingVerificationInput { get; set; }
        private BodySide servoSide = BodySide.Right;
        private readonly System.Collections.Generic.List<Slider> servoSliders = new System.Collections.Generic.List<Slider>();

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (stylesheet) root.styleSheets.Add(stylesheet);
            root.pickingMode = PickingMode.Ignore;
            var title = new VisualElement { name = "title" }; title.pickingMode = PickingMode.Ignore;
            title.Add(new Label("ALS / HUMAN DIGITAL TWIN") { name = "eyebrow" });
            title.Add(new Label("Physical presence.") { name = "headline" });
            title.Add(new Label("UPPER-BODY ACTIVE RAGDOLL   /   PHYSX") { name = "subtitle" });
            root.Add(title);
            var panel = new ScrollView { name = "sidebar" };
            panel.Add(new Label("SIMULATION CONSOLE") { name = "panel-title" });
            state = new Label(); state.AddToClassList("state"); panel.Add(state);
            physics = new Label(); physics.AddToClassList("metric"); panel.Add(physics);
            if (avatar.servoBridge) BuildServoPanel(panel);
            panel.Add(new Label("CONTACT LAB") { name = "lab-title" });
            panel.Add(new Label("Hands react to solid objects.\nThe target does not override contact.") { name = "description" });
            AddButton(panel, "Toggle resistance gate", () => lab.ToggleGate());
            AddButton(panel, "Apply hand impulse", avatar.ApplyTestImpulse);
            AddButton(panel, "Reset test object", lab.ResetObject);
            gateState = new Label(); gateState.AddToClassList("metric"); panel.Add(gateState);
            AddButton(panel, "Physics Debug", () => { showDebug = !showDebug; debugPanel.style.display = showDebug ? DisplayStyle.Flex : DisplayStyle.None; });
            debugPanel = new ScrollView { name = "debug-panel" };
            debugPanel.style.display = DisplayStyle.None;
            diagnostics = new Label(); diagnostics.AddToClassList("diagnostics"); debugPanel.Add(diagnostics); panel.Add(debugPanel);
            root.Add(panel);
            var bottom = new VisualElement { name = "actions" };
            AddButton(bottom, "Handshake", () => avatar.PlayGesture(GestureType.Handshake));
            AddButton(bottom, "Wave", () => avatar.PlayGesture(GestureType.Wave));
            AddButton(bottom, "Double Thumbs-Up", () => avatar.PlayGesture(GestureType.DoubleThumbsUp));
            AddButton(bottom, "Return to Idle", avatar.ReturnToIdle);
            AddButton(bottom, "Reset Physics", () => { avatar.ResetPhysics(); lab.ResetObject(); });
            pause = AddButton(bottom, "Pause", () => { avatar.TogglePause(); pause.text = avatar.IsPaused ? "Resume" : "Pause"; });
            root.Add(bottom);
            var hint = new Label("DRAG TO ORBIT  ·  SCROLL TO ZOOM  ·  LOCAL COMMANDS :8765") { name = "camera-hint" };
            hint.pickingMode = PickingMode.Ignore; root.Add(hint);
        }

        private Button AddButton(VisualElement parent, string label, System.Action action)
        {
            var button = new Button(() => { if (!SuppressNativeInputForVerification || DispatchingVerificationInput) action(); }) { text = label }; parent.Add(button); return button;
        }

        private void BuildServoPanel(VisualElement panel)
        {
            servoState = new Label(); servoState.AddToClassList("metric"); panel.Add(servoState);
            var fold = new Foldout { text = "Servo mapping / 13 channels", value = false, name = "servo-panel" }; panel.Add(fold);
            fold.Add(new Label("Virtual input only. Port IDs unset.\nEndpoint/direction calibration pending.") { name = "servo-note" });
            var side = new DropdownField("Side", new System.Collections.Generic.List<string> { "Right", "Left" }, 0);
            side.AddToClassList("servo-side");
            side.RegisterValueChangedCallback(e => { servoSide = e.newValue == "Left" ? BodySide.Left : BodySide.Right; RefreshServoSliders(); }); fold.Add(side);
            for (int i = 0; i < 13; i++) {
                int channel = i; var c = avatar.servoBridge.Profile(servoSide).channels[i];
                var slider = new Slider(c.id + " " + c.label, c.inputMin, c.inputMax) { showInputField = false, name = "servo-" + c.id };
                slider.AddToClassList("servo-slider");
                slider.RegisterValueChangedCallback(e => {
                    if (SuppressNativeInputForVerification && !DispatchingVerificationInput) return;
                    var values = avatar.servoBridge.Targets(servoSide); values[channel] = e.newValue;
                    avatar.servoBridge.SetPose(servoSide, values);
                });
                servoSliders.Add(slider); fold.Add(slider);
            }
            fold.RegisterValueChangedCallback(_ => RefreshServoSliders());
            AddButton(fold, "Release servo control", avatar.ReturnToIdle);
            RefreshServoSliders();
        }
        private void RefreshServoSliders()
        {
            var values = avatar.servoBridge.Targets(servoSide); var channels = avatar.servoBridge.Profile(servoSide).channels;
            for (int i = 0; i < servoSliders.Count; i++) {
                servoSliders[i].lowValue = channels[i].inputMin; servoSliders[i].highValue = channels[i].inputMax;
                servoSliders[i].SetValueWithoutNotify(values[i]);
                servoSliders[i].label = channels[i].id + " " + channels[i].label + "  " + values[i].ToString("F1") + "°";
            }
        }

        void Update()
        {
            if (state == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .1f;
            float period = Time.unscaledTime - lastSample;
            if (period >= .5f) { hz = (avatar.PhysicsSteps - lastSteps) / period; lastSteps = avatar.PhysicsSteps; lastSample = Time.unscaledTime; }
            state.text = avatar.IsPaused ? "PAUSED" : $"{avatar.gestures.CurrentGesture.ToString().ToUpperInvariant()}\n{avatar.gestures.State}";
            float error = avatar.motors.Where(m => m.bone.role != BodyRole.Finger).Average(m => m.bone.TrackingError);
            int contacts = avatar.GetComponentsInChildren<ContactReporter>().Sum(c => c.ActiveContacts);
            physics.text = $"Physics steps   {hz:F0} Hz\nFixed timestep   {avatar.profile.fixedTimeStep * 1000:F2} ms\nJoint error   {error:F1}° mean\nExternal contacts   {contacts}";
            gateState.text = "RESISTANCE GATE   " + (lab.GateEnabled ? "ON" : "OFF");
            if (servoState != null) {
                servoState.text = avatar.servoBridge.Mode + "\n13 / SIDE · " + (avatar.servoBridge.Profile(servoSide).hardwareCalibrated ? "CALIBRATED" : "UNCALIBRATED");
                RefreshServoSliders();
            }
            if (!showDebug) return;
            var text = new StringBuilder("MEASURED: total joint reactions\nPD EST: approximation, not motor readback\n");
            foreach (var motor in avatar.motors.Where(m => m.bone.role != BodyRole.Finger)) {
                var bone = motor.bone; var a = bone.Angles;
                text.Append($"\n{bone.id}\nXYZ {a.x:F0} {a.y:F0} {a.z:F0}° | err {bone.TrackingError:F1}°\n");
                text.Append($"F {motor.MeasuredConstraintForce.magnitude:F1} N | T {motor.MeasuredConstraintTorque.magnitude:F2} Nm\nPD est {motor.EstimatedTorque.magnitude:F2} Nm\n");
            }
            diagnostics.text = text.ToString();
        }
    }
}
