using SmartXR.Kinematics.Drives;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Macht das Werkzeugziel eines Roboters greifbar: zupacken, durch den
    /// Raum ziehen, der Roboter folgt über die IK.
    ///
    /// Das ist der Hauptweg der Bedienung in VR, weil er ohne Tastenkunde
    /// auskommt. Gearbeitet wird mit dem Ziel, das der
    /// <see cref="RobotController"/> ohnehin führt — es bekommt nur einen
    /// Körper und einen Griff. Die Rückwärtstransformation läuft dadurch
    /// weiter dort, wo sie hingehört.
    ///
    /// Ist das Ziel nicht erreichbar, wird die Kugel rot: Der Roboter streckt
    /// sich so weit wie möglich und bleibt am Rand stehen, statt das Ziel
    /// wegzuziehen. So sieht man den Arbeitsraum.
    ///
    /// Die Komponente gehört auf dasselbe Objekt wie der
    /// <see cref="RobotController"/>; der Rest entsteht zur Laufzeit.
    /// </summary>
    [RequireComponent(typeof(RobotController))]
    [DisallowMultipleComponent]
    public class TcpTargetGrab : MonoBehaviour
    {
        [Tooltip("Beim Zupacken in den Werkzeugpunkt-Modus wechseln.")]
        public bool switchToTcpModeOnGrab = true;

        [Tooltip("Farbe, solange das Ziel erreichbar ist.")]
        public Color reachableColor = new Color(1f, 0.8f, 0.1f);

        [Tooltip("Farbe, wenn der Roboter das Ziel nicht erreicht.")]
        public Color unreachableColor = new Color(0.9f, 0.2f, 0.15f);

        private RobotController robot;
        private XRGrabInteractable grab;
        private Renderer ballRenderer;
        private RobotController.Mode modeBeforeGrab;

        private void Awake() => robot = GetComponent<RobotController>();

        // ------------------------------------------------------------
        // Jeder Roboter, der sich anmeldet, wird greifbar — ohne dass jemand
        // die Komponente von Hand anhaengt.

        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (installed) return;
            installed = true;
            RobotController.Activated += MakeGrabbable;
        }

        private static void MakeGrabbable(RobotController controller)
        {
            if (controller != null && controller.GetComponent<TcpTargetGrab>() == null)
                controller.gameObject.AddComponent<TcpTargetGrab>();
        }

        private void OnEnable()
        {
            robot.Rebuilt += Attach;
            Attach();
        }

        private void OnDisable()
        {
            robot.Rebuilt -= Attach;
            if (grab != null)
            {
                grab.selectEntered.RemoveListener(OnGrabbed);
                grab.selectExited.RemoveListener(OnReleased);
            }
        }

        /// <summary>
        /// Hängt Körper und Griff an das Ziel. Mehrfach aufrufbar: Nach einem
        /// Neuaufbau der Kette kann es ein neues Ziel geben.
        /// </summary>
        private void Attach()
        {
            // Das Ziel entsteht erst beim Wechsel in den TCP-Modus, deshalb
            // hier anstoßen.
            robot.ResetTargetToTcp();

            Transform target = robot.TcpTarget;
            if (target == null) return;

            if (grab != null && grab.transform == target) return;

            var body = target.GetComponent<Rigidbody>();
            if (body == null) body = target.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            grab = target.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = target.gameObject.AddComponent<XRGrabInteractable>();

            // Sofort mitziehen statt werfen: Das Ziel ist ein Bedienelement,
            // kein Gegenstand.
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);

            ballRenderer = FindBall(target);
        }

        private static Renderer FindBall(Transform target)
        {
            foreach (var r in target.GetComponentsInChildren<Renderer>())
                if (r.gameObject.name == "Kugel") return r;
            return null;
        }

        private void OnGrabbed(SelectEnterEventArgs _)
        {
            modeBeforeGrab = robot.ControlMode;
            if (switchToTcpModeOnGrab) robot.ControlMode = RobotController.Mode.Tcp;
        }

        private void OnReleased(SelectExitEventArgs _)
        {
            // Im Werkzeugpunkt-Modus bleiben, wenn wir ihn selbst eingeschaltet
            // haben — sonst springt der Roboter beim Loslassen zurück auf die
            // Gelenkwerte des Panels.
            if (!switchToTcpModeOnGrab) robot.ControlMode = modeBeforeGrab;
        }

        private void Update()
        {
            if (ballRenderer == null) return;

            bool reachable = robot.ControlMode != RobotController.Mode.Tcp || robot.LastIk.Converged;
            ballRenderer.material.color = reachable ? reachableColor : unreachableColor;
        }
    }
}
