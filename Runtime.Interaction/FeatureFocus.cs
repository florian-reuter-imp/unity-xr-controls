using System;
using System.Collections.Generic;
using SmartXR.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Verteilt Eingaben an genau ein Feature: das fokussierte.
    ///
    /// WARUM STATISCH UND SELBSTSTARTEND
    /// ---------------------------------
    /// Ein Modul wird als Prefab in eine fremde Szene gezogen. Braeuchte es
    /// dort ein zweites, von Hand platziertes Bauteil, waere die
    /// Modulregel verletzt -- der Empfaenger muesste etwas verkabeln, und
    /// genau das soll er nicht.
    ///
    /// Deshalb meldet sich ein Feature hier an, und der Verteiler entsteht
    /// beim ersten Anmelden von selbst. Er haengt an einem eigenen
    /// GameObject, weil Eingaben je Bild gelesen werden muessen und das in
    /// Unity ein <see cref="MonoBehaviour"/> verlangt.
    ///
    /// Der Vorgaenger im Anwendungsprojekt suchte seine Features mit
    /// <c>FindObjectsOfType&lt;MonoBehaviour&gt;()</c> -- also einmal durch
    /// die ganze Szene, ueber jedes Bauteil, und nur beim Start. Features,
    /// die spaeter entstehen, fand er nie.
    ///
    /// WAS HIER NICHT STEHT
    /// --------------------
    /// Nichts ueber Robotik. Der Verteiler kennt Beschriftungen, einen
    /// Zustand „eingeschaltet" und acht Ereignisse. Welches Feature was
    /// damit tut, bleibt beim Feature.
    /// </summary>
    public static class FeatureFocus
    {
        /// <summary>Name der Eingabekarte, die beim Fokus gehalten wird.</summary>
        public const string Map = "Feature";

        private static readonly List<IControllableFeature> features = new List<IControllableFeature>();
        private static int focused = -1;
        private static Driver driver;

        /// <summary>
        /// Meter, die eine gehaltene Richtungseingabe je Bild ergibt.
        /// Absichtlich global: Zwei Features mit verschiedener
        /// Empfindlichkeit fuehlen sich wie zwei Programme an.
        /// </summary>
        public static float MoveStep { get; set; } = 0.01f;

        /// <summary>Alle angemeldeten Features, in Anmeldereihenfolge.</summary>
        public static IReadOnlyList<IControllableFeature> Features => features;

        /// <summary>Das Feature mit Fokus, oder null.</summary>
        public static IControllableFeature Focused =>
            focused >= 0 && focused < features.Count ? features[focused] : null;

        /// <summary>Wird ausgeloest, wenn sich der Fokus oder die Liste aendert.</summary>
        public static event Action Changed;

        // ── Anmeldung ───────────────────────────────────────────────────────

        public static void Register(IControllableFeature feature)
        {
            if (feature == null || features.Contains(feature)) return;

            features.Add(feature);
            EnsureDriver();

            // Hat noch nichts den Fokus, bekommt das erste eingeschaltete
            // Feature ihn sofort -- sonst muesste der Benutzer erst
            // durchschalten, um ueberhaupt etwas bedienen zu koennen.
            if (Focused == null) FocusFirstActive();
            else Changed?.Invoke();
        }

        public static void Unregister(IControllableFeature feature)
        {
            int index = features.IndexOf(feature);
            if (index < 0) return;

            // Wer den Fokus hatte, erfaehrt auch, dass er ihn verliert --
            // sonst bleibt ein Feature in seinem Fokuszustand haengen und
            // zeichnet etwa weiter seine Hervorhebung.
            if (index == focused)
            {
                Blur();
                features.RemoveAt(index);
                FocusFirstActive();
                return;
            }

            features.RemoveAt(index);

            // Index nachziehen: Ein Feature vor dem fokussierten
            // verschiebt dessen Position, und ohne diese Zeile bedient der
            // Verteiler danach das falsche.
            if (focused > index) focused--;

            Changed?.Invoke();
        }

        // ── Fokus ───────────────────────────────────────────────────────────

        /// <summary>
        /// Schaltet den Fokus auf das naechste eingeschaltete Feature.
        /// Ausgeschaltete werden uebersprungen; gibt es kein
        /// eingeschaltetes, bleibt es beim bisherigen.
        /// </summary>
        public static void Cycle(bool forward)
        {
            int count = features.Count;
            if (count == 0) return;

            int step = forward ? 1 : -1;
            int start = focused < 0 ? (forward ? -1 : 0) : focused;

            for (int i = 1; i <= count; i++)
            {
                // Positiver Rest auch bei negativem step.
                int next = ((start + step * i) % count + count) % count;
                if (features[next].IsActive)
                {
                    Apply(next);
                    return;
                }
            }
        }

        /// <summary>
        /// Setzt den Fokus auf ein bestimmtes Feature, wenn es angemeldet
        /// und eingeschaltet ist.
        /// </summary>
        public static bool Focus(IControllableFeature feature)
        {
            int index = features.IndexOf(feature);
            if (index < 0 || !features[index].IsActive) return false;

            Apply(index);
            return true;
        }

        /// <summary>
        /// Prueft, ob das fokussierte Feature noch eingeschaltet ist, und
        /// wandert sonst weiter. Ein Feature ruft das, nachdem es sich
        /// selbst ausgeschaltet hat.
        /// </summary>
        public static void Revalidate()
        {
            if (Focused != null && Focused.IsActive)
            {
                Changed?.Invoke();
                return;
            }

            Blur();
            FocusFirstActive();
        }

        /// <summary>
        /// Nimmt dem fokussierten Feature den Fokus und sagt es ihm. Eine
        /// eigene Methode, weil es zwei Wege hierher gibt -- abmelden und
        /// sich selbst ausschalten -- und auf beiden dieselbe Zusage gilt:
        /// Wer <see cref="IControllableFeature.OnFocusGained"/> bekommen hat,
        /// bekommt auch <see cref="IControllableFeature.OnFocusLost"/>.
        /// </summary>
        private static void Blur()
        {
            Focused?.OnFocusLost();
            focused = -1;
        }

        private static void FocusFirstActive()
        {
            for (int i = 0; i < features.Count; i++)
            {
                if (!features[i].IsActive) continue;
                Apply(i);
                return;
            }

            focused = -1;
            Announce();
        }

        private static void Apply(int index)
        {
            if (focused == index) return;

            Focused?.OnFocusLost();
            focused = index;
            Focused?.OnFocusGained();
            Announce();
        }

        private static void Announce()
        {
            // Die Eingabekarte genau so lange halten, wie etwas den Fokus
            // hat. Ohne Fokus feuert keine Aktion, und die Tasten bleiben
            // frei fuer alles andere.
            if (Focused != null)
                InputContext.Enter(Owner, Map, "Feature: " + Focused.DisplayName);
            else
                InputContext.Exit(Owner);

            Changed?.Invoke();
        }

        /// <summary>
        /// Besitzermarke fuer <see cref="InputContext"/>. Eine eigene
        /// Marke, weil der Verteiler statisch ist und kein Objekt hat, das
        /// man uebergeben koennte.
        /// </summary>
        private static readonly object Owner = new object();

        // ── Der Treiber ─────────────────────────────────────────────────────

        private static void EnsureDriver()
        {
            if (driver != null) return;
            if (!Application.isPlaying) return;   // im Editor kein Objekt anlegen

            var go = new GameObject("SmartXR Feature-Fokus")
            {
                // Nicht mit der Szene speichern: Das Objekt entsteht bei
                // jedem Start neu, und gespeichert wuerde es sich bei jedem
                // Speichern der Szene vermehren.
                hideFlags = HideFlags.DontSave
            };
            driver = go.AddComponent<Driver>();
        }

        /// <summary>
        /// Liest je Bild die Eingaben und gibt sie an das fokussierte
        /// Feature weiter. Oeffentlich nur, weil Unity eine Komponente
        /// nicht aus einem privaten Typ erzeugen kann.
        /// </summary>
        [AddComponentMenu("")]   // nicht im Menue: entsteht von selbst
        public sealed class Driver : MonoBehaviour
        {
            private static InputAction A(string name) => InputContext.Action(Map, name);

            private void Update()
            {
                if (Focused == null || InputContext.Asset == null) return;

                if (A("FokusWeiter").WasPressedThisFrame()) Cycle(true);
                if (A("FokusZurueck").WasPressedThisFrame()) Cycle(false);

                // Nach dem Durchschalten kann ein anderes Feature im Fokus
                // stehen. Erneut pruefen, damit die folgenden Aktionen nicht
                // noch an das alte gehen.
                IControllableFeature f = Focused;
                if (f == null) return;

                if (A("Hauptaktion").WasPressedThisFrame()) f.OnPrimary();
                if (A("Nebenaktion").WasPressedThisFrame()) f.OnSecondary();
                if (Pressed("Weiter")) f.OnNext();
                if (Pressed("Zurueck")) f.OnPrevious();

                InputAction richtung = A("Richtung");
                Vector2 dir = richtung.ReadValue<Vector2>();
                if (dir.sqrMagnitude > 1e-4f && !OperatingMode.SticksSuppressed(richtung))
                    f.OnDirection(Vector2.ClampMagnitude(dir, 1f) * MoveStep);

                InputAction hoehe = A("Hoehe");
                float y = hoehe.ReadValue<float>();
                if (Mathf.Abs(y) > 1e-2f && !OperatingMode.SticksSuppressed(hoehe))
                    f.OnVertical(y * MoveStep);
            }

            /// <summary>
            /// Gedrueckt, aber nicht, solange die Sticks zum Gehen gehoeren
            /// (siehe <see cref="OperatingMode"/>).
            /// </summary>
            private static bool Pressed(string name)
            {
                InputAction action = A(name);
                return action.WasPressedThisFrame() && !OperatingMode.SticksSuppressed(action);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Ohne das traegt der Verteiler bei abgeschaltetem Domain Reload
            // die Features des vorigen Starts weiter -- und bedient Objekte,
            // die es nicht mehr gibt.
            features.Clear();
            focused = -1;
            driver = null;
            Changed = null;
        }
    }
}
