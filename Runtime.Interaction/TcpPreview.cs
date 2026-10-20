using SmartXR.Kinematics.Drives;
using TMPro;
using UnityEngine;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Zeigt beim Ziehen am Griff, was der Roboter daraus wirklich macht.
    ///
    /// Der Griff ist der Wunsch, der Geist ist die Wirklichkeit. Liegen
    /// beide übereinander, geht die Pose. Klafft etwas, sieht man sofort
    /// wieviel — und woran es liegt:
    ///
    /// <list type="bullet">
    ///   <item><b>Grün:</b> erreicht, Punkt und Ausrichtung stimmen.</item>
    ///   <item><b>Gelb:</b> Punkt erreicht, Ausrichtung nicht. Bei einem
    ///   Fünfachser ist das der Normalfall, sobald man quer zur Arbeitsebene
    ///   dreht — ihm fehlt diese Drehung (siehe FiveAxisProjection).</item>
    ///   <item><b>Rot:</b> der Punkt selbst liegt außerhalb der Reichweite.</item>
    /// </list>
    ///
    /// Das ist der Zweck der Vorschau: keine falsche Erwartung aufkommen
    /// lassen. Angezeigt wird nie, was gewünscht war, sondern immer, was
    /// herauskommt.
    ///
    /// Hängt sich an jeden Roboter, der sich anmeldet; keine Szene muss
    /// dafür geändert werden.
    /// </summary>
    [RequireComponent(typeof(RobotController))]
    [DisallowMultipleComponent]
    public class TcpPreview : MonoBehaviour
    {
        [Tooltip("Größe des Geistes in Metern.")]
        public float size = 0.05f;

        [Tooltip("Ab welcher Abweichung die Anzeige erscheint: Meter.")]
        public float positionThreshold = 0.003f;

        [Tooltip("Ab welcher Abweichung die Anzeige erscheint: Grad.")]
        public float angleThreshold = 2f;

        public Color reachedColor = new Color(0.3f, 0.9f, 0.4f);
        public Color orientationLostColor = new Color(1f, 0.75f, 0.1f);
        public Color unreachableColor = new Color(0.9f, 0.25f, 0.2f);

        private RobotController robot;
        private Transform ghost;
        private Renderer ghostRenderer;
        private TextMeshPro label;

        private void Awake() => robot = GetComponent<RobotController>();

        private void LateUpdate()
        {
            // Nach dem Loesen des Reglers, damit die gezeigte Pose die ist,
            // die gerade gefahren wurde.
            bool active = robot.IsReady && robot.HasChain &&
                          robot.ControlMode == RobotController.Mode.Tcp &&
                          robot.TcpTarget != null;

            if (!active)
            {
                if (ghost != null) ghost.gameObject.SetActive(false);
                return;
            }

            Ensure();

            Pose reached = robot.ReachedPose;
            float missPosition = robot.LastPositionError;
            float missAngle = Quaternion.Angle(reached.rotation, robot.TcpTarget.rotation);

            bool somethingToSay = missPosition > positionThreshold || missAngle > angleThreshold;
            ghost.gameObject.SetActive(somethingToSay);
            if (!somethingToSay) return;

            ghost.SetPositionAndRotation(reached.position, reached.rotation);

            Color color = missPosition > positionThreshold
                ? unreachableColor
                : (robot.OrientationSacrificed || missAngle > angleThreshold
                    ? orientationLostColor
                    : reachedColor);

            if (ghostRenderer != null) ghostRenderer.material.color = color;

            if (label != null)
            {
                label.color = color;
                label.text = Describe(missPosition, missAngle);
                label.transform.position = reached.position + Vector3.up * (size * 2.2f);

                var cam = Camera.main;
                if (cam != null)
                    label.transform.rotation =
                        Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
            }
        }

        /// <summary>Ein Satz, der sagt, was der Roboter aus dem Wunsch macht.</summary>
        private string Describe(float missPosition, float missAngle)
        {
            if (missPosition > positionThreshold)
                return $"außer Reichweite\n{missPosition * 1000f:F0} mm daneben";

            if (robot.IsFiveAxis && robot.DroppedYawDegrees > angleThreshold)
                return $"Drehung nicht möglich\n{robot.DroppedYawDegrees:F0}° fehlen dem Arm";

            return $"Ausrichtung angepasst\n{missAngle:F0}° Abweichung";
        }

        private void Ensure()
        {
            if (ghost != null) return;

            var go = new GameObject($"{name} – Vorschau");
            ghost = go.transform;
            ghost.SetParent(transform.parent, worldPositionStays: true);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Geist";
            ball.transform.SetParent(ghost, false);
            ball.transform.localScale = Vector3.one * size;
            Destroy(ball.GetComponent<Collider>());
            ghostRenderer = ball.GetComponent<Renderer>();

            // Die Anfahrrichtung sichtbar machen: Dort zeigt der Unterschied
            // zwischen Wunsch und Wirklichkeit am deutlichsten.
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "Anfahrrichtung";
            arrow.transform.SetParent(ghost, false);
            arrow.transform.localPosition = Vector3.forward * size;
            arrow.transform.localScale = new Vector3(size * 0.15f, size * 0.15f, size * 2f);
            Destroy(arrow.GetComponent<Collider>());

            var text = new GameObject("Hinweis");
            text.transform.SetParent(ghost, worldPositionStays: false);
            label = text.AddComponent<TextMeshPro>();
            label.fontSize = 0.1f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(0.4f, 0.12f);
        }

        // ------------------------------------------------------------

        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (installed) return;
            installed = true;
            RobotController.Activated += Attach;
        }

        private static void Attach(RobotController controller)
        {
            if (controller != null && controller.GetComponent<TcpPreview>() == null)
                controller.gameObject.AddComponent<TcpPreview>();
        }
    }
}
