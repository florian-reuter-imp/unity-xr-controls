using System;
using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartXR.Input
{
    /// <summary>
    /// Schaltet die Desktop-Navigation von realvirtual ab, sobald ein XR-Rig in
    /// der Szene ist.
    ///
    /// realvirtual liest für seinen <c>FirstPersonController</c> und die
    /// <c>SceneMouseNavigation</c> Pfeile, WASD und Maus über das alte
    /// Eingabesystem. Im VR-Betrieb bewegt das XR-Rig die Kamera, die
    /// Desktop-Navigation stört dann nur. Das Paket selbst bleibt unverändert:
    /// Diese Komponente hängt sich beim Laden jeder Szene automatisch ans
    /// XR-Rig und deaktiviert die beiden Komponenten — auch wenn realvirtual
    /// sie später wieder einschaltet.
    ///
    /// Für einen künftigen reinen Desktop-Betrieb genügt es, das Häkchen am
    /// XR-Rig zu entfernen.
    ///
    /// Dasselbe gilt für den Sprung des XR-Rigs (Scene3 hat einen): Er liegt
    /// auf A, und dort liegt unsere Hauptaktion.
    /// </summary>
    [DisallowMultipleComponent]
    public class DesktopNavigationGuard : MonoBehaviour
    {
        [Tooltip("realvirtuals FirstPersonController und SceneMouseNavigation abschalten.")]
        public bool disableDesktopNavigation = true;

        [Tooltip("Den Sprung des XR-Rigs abschalten. Er liegt auf A, und dort " +
                 "liegt unsere Hauptaktion; gesprungen wird in einer Roboterzelle nicht.")]
        public bool disableJump = true;

        private const float CheckInterval = 1f;
        private float nextCheck;

        private void Start() => Sweep(log: true);

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;
            Sweep(log: false);
        }

        private void Sweep(bool log)
        {
            if (!disableDesktopNavigation && !disableJump) return;

            var sb = log ? new StringBuilder() : null;
            if (disableDesktopNavigation)
            {
                AbschaltenNachTypnamen("realvirtual.FirstPersonController", sb);
                AbschaltenNachTypnamen("realvirtual.SceneMouseNavigation", sb);
            }

            if (disableJump)
                foreach (var c in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>(
                             FindObjectsSortMode.None))
                    Disable(c, sb);

            if (sb != null && sb.Length > 0)
                Debug.Log("[SmartXR.Input] Abgeschaltet, weil es mit der XR-Bedienung kollidiert:" + sb);
        }

        private static void Disable(Behaviour c, StringBuilder sb)
        {
            if (!c.enabled) return;
            c.enabled = false;
            sb?.Append($"\n  {c.GetType().Name} an '{c.gameObject.name}'");
        }

        // realvirtual wird absichtlich nicht als Assembly referenziert: Diese
        // Baugruppe soll auch in Projekten uebersetzen, in denen das Paket
        // nicht liegt. Die beiden Komponenten werden deshalb ueber ihren
        // Typnamen gesucht — fehlt das Paket, passiert einfach nichts.
        private static readonly Dictionary<string, Type> Typen = new Dictionary<string, Type>();

        private static void AbschaltenNachTypnamen(string typname, StringBuilder sb)
        {
            if (!Typen.TryGetValue(typname, out Type typ))
            {
                typ = Type.GetType(typname + ", realvirtual.base");
                if (typ == null)
                {
                    // Falls die Assembly einmal anders heisst, alle durchsehen.
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        typ = assembly.GetType(typname);
                        if (typ != null) break;
                    }
                }
                Typen[typname] = typ;
            }

            if (typ == null) return;

            foreach (var gefunden in FindObjectsByType(typ, FindObjectsSortMode.None))
                if (gefunden is Behaviour behaviour)
                    Disable(behaviour, sb);
        }

        // ------------------------------------------------------------
        // Automatisch an jedes XR-Rig hängen

        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!installed)
            {
                SceneManager.sceneLoaded += (_, _) => AttachToRigs();
                installed = true;
            }
            AttachToRigs();
        }

        private static void AttachToRigs()
        {
            foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
                if (origin.GetComponent<DesktopNavigationGuard>() == null)
                    origin.gameObject.AddComponent<DesktopNavigationGuard>();
        }
    }
}
