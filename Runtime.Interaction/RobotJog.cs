using SmartXR.Input;
using SmartXR.Kinematics.Drives;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Verfährt den Werkzeugpunkt schrittweise — die Feinkorrektur neben dem
    /// Greifen (<see cref="TcpTargetGrab"/>).
    ///
    /// Aktiv wird das nur im Bedienmodus und nur, wenn ein Roboter ausgewählt
    /// ist. Dann übernimmt die Karte „Roboter" die Sticks; ohne Roboter
    /// behält das fokussierte Feature sie. So gehören die Sticks immer genau
    /// einer Sache.
    ///
    /// Gefahren wird in einem wählbaren Bezugssystem — Basis oder Werkzeug,
    /// wie am Handbediengerät. Das ist nicht Bequemlichkeit, sondern der
    /// Lehrinhalt: Dass „vorwärts" von der Wahl des Bezugssystems abhängt,
    /// versteht man am schnellsten, wenn man es umschalten kann.
    ///
    /// Legt sich beim Start selbst an; keine Szene muss dafür geändert werden.
    /// </summary>
    public class RobotJog : MonoBehaviour
    {
        public enum Frame
        {
            /// <summary>Achsen des Roboterfußes.</summary>
            Basis = 0,
            /// <summary>Achsen des Werkzeugpunkts — mitgedreht.</summary>
            Werkzeug = 1
        }

        [Tooltip("Vorschub bei voll ausgelenktem Stick, Meter je Sekunde.")]
        public float metersPerSecond = 0.25f;

        [Tooltip("Drehgeschwindigkeit bei voll ausgelenktem Stick, Grad je Sekunde.")]
        public float degreesPerSecond = 45f;

        public Frame frame = Frame.Basis;

        private RobotController robot;
        private bool holdsContext;

        // ------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<RobotJog>() != null) return;
            var go = new GameObject("SMART-XR Robotersteuerung");
            DontDestroyOnLoad(go);
            go.AddComponent<RobotJog>();
        }

        private void OnDisable()
        {
            InputContext.Exit(this);
            holdsContext = false;
        }

        private void Update()
        {
            robot = RobotController.Latest;

            bool wanted = OperatingMode.Active && robot != null && robot.HasChain;
            if (wanted != holdsContext)
            {
                if (wanted) InputContext.Enter(this, "Roboter", Label());
                else InputContext.Exit(this);
                holdsContext = wanted;
            }

            if (!wanted) return;

            if (Pressed("Bezugssystem"))
            {
                frame = frame == Frame.Basis ? Frame.Werkzeug : Frame.Basis;
                InputContext.SetLabel(this, Label());
            }

            if (Pressed("ZielZuruecksetzen"))
                robot.ResetTargetToTcp();

            // Vor dem Abbruch lesen, damit die Anzeige auch stimmt, wenn man
            // nur die Drehtaste haelt und noch nicht fahrt.
            bool rotating = Action("DrehenHalten").IsPressed();
            if (rotating != rotatedLastFrame)
            {
                rotatedLastFrame = rotating;
                InputContext.SetLabel(this, Label());
            }

            Vector2 move = Action("Verschieben").ReadValue<Vector2>();
            float lift = Action("Hoehe").ReadValue<float>();
            if (move.sqrMagnitude < 1e-4f && Mathf.Abs(lift) < 1e-2f) return;

            // Der Roboter folgt dem Ziel nur im Werkzeugpunkt-Modus.
            if (robot.ControlMode != RobotController.Mode.Tcp)
                robot.SetMode(RobotController.Mode.Tcp);

            Vector3 local = new Vector3(move.x, lift, move.y);
            Quaternion basis = frame == Frame.Werkzeug
                ? robot.TcpPose.rotation
                : robot.BasisPose.rotation;

            if (rotating)
                robot.MoveTarget(Vector3.zero, basis * local * (degreesPerSecond * Time.deltaTime));
            else
                robot.MoveTarget(basis * local * (metersPerSecond * Time.deltaTime), Vector3.zero);
        }

        private bool rotatedLastFrame;

        private string Label()
            => $"Roboter: {(rotatedLastFrame ? "drehen" : "verschieben")} ({frame})";

        private static InputAction Action(string name) => InputContext.Action("Roboter", name);
        private static bool Pressed(string name) => Action(name).WasPressedThisFrame();
    }
}
