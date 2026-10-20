using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace SmartXR.Input
{
    /// <summary>
    /// Der Bedienmodus: Die Sticks steuern vorübergehend die Anwendung statt
    /// der Fortbewegung.
    ///
    /// Das XR-Rig belegt beide Sticks mit Gehen, Drehen und Teleport. Damit
    /// derselbe Stick auch ein IK-Ziel verschieben kann, schaltet dieser Modus
    /// alle Fortbewegungs-Komponenten vorübergehend ab und gibt sie beim
    /// Verlassen genau so zurück, wie sie waren.
    ///
    /// Umschalten: Klick auf den linken Stick, am Schreibtisch F8.
    ///
    /// Tastatur ist davon nicht betroffen. Der Ziffernblock steuert immer; nur
    /// Stick-Eingaben werden außerhalb des Bedienmodus ignoriert, siehe
    /// <see cref="SticksSuppressed"/>.
    /// </summary>
    public static class OperatingMode
    {
        private static readonly List<LocomotionProvider> suspended = new List<LocomotionProvider>();

        public static bool Active { get; private set; }

        /// <summary>Wird bei jedem Umschalten ausgelöst.</summary>
        public static event Action Changed;

        public static void Toggle() => Set(!Active);

        public static void Set(bool active)
        {
            if (active == Active) return;
            Active = active;

            if (active) SuspendLocomotion();
            else RestoreLocomotion();

            Changed?.Invoke();
        }

        /// <summary>
        /// Kommt der aktuelle Wert dieser Aktion von einem Controller, ohne
        /// dass der Bedienmodus läuft? Dann gehört der Stick der Fortbewegung.
        /// </summary>
        public static bool SticksSuppressed(InputAction action)
        {
            if (Active || action == null) return false;
            return action.activeControl?.device is UnityEngine.InputSystem.XR.XRController;
        }

        // ------------------------------------------------------------

        private static void SuspendLocomotion()
        {
            suspended.Clear();
            foreach (var provider in UnityEngine.Object.FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
            {
                if (!provider.enabled) continue;
                provider.enabled = false;
                suspended.Add(provider);
            }
        }

        private static void RestoreLocomotion()
        {
            foreach (var provider in suspended)
                if (provider != null) provider.enabled = true;
            suspended.Clear();
        }

        // ------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
            suspended.Clear();
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var toggle = InputContext.Action(InputContext.GlobalMap, "Bedienmodus");
            if (toggle != null) toggle.performed += _ => Toggle();
        }
    }
}
