using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartXR.Input
{
    /// <summary>
    /// Zugang zu den SMART-XR-Aktionen und Verwaltung der Eingabekontexte.
    ///
    /// Die Karte <c>Global</c> ist immer aktiv. Alle anderen Karten sind
    /// Kontexte: Es läuft immer nur der zuletzt betretene. Verlässt ihn sein
    /// Besitzer, übernimmt wieder der vorige. So gehören Richtungstasten und
    /// Stick nie zwei Kontexten gleichzeitig.
    ///
    /// Die Belegung selbst steht in <c>Resources/SmartXR.inputactions</c>;
    /// Plan und Begründung in <c>docs/Tastenplan.md</c>.
    /// </summary>
    public static class InputContext
    {
        public const string GlobalMap = "Global";

        private struct Entry
        {
            public object Owner;
            public string Map;
            public string Label;
        }

        private static InputActionAsset asset;
        private static readonly List<Entry> stack = new List<Entry>();
        private static readonly List<Entry> parallel = new List<Entry>();

        /// <summary>Wird nach jedem Kontextwechsel ausgelöst.</summary>
        public static event Action Changed;

        public static InputActionAsset Asset
        {
            get
            {
                if (asset == null)
                {
                    asset = Resources.Load<InputActionAsset>("SmartXR");
                    if (asset == null)
                        Debug.LogError("[SmartXR.Input] Resources/SmartXR.inputactions fehlt.");
                    else
                        asset.FindActionMap(GlobalMap, true).Enable();
                }
                return asset;
            }
        }

        /// <summary>
        /// Aktion aus einer Karte, z. B. <c>Action("Feature", "Richtung")</c>.
        /// Gibt null zurueck, wenn Karte oder Aktion fehlen.
        ///
        /// WARUM NICHT WERFEN
        /// ------------------
        /// Vorher stand hier <c>FindActionMap(map, true).FindAction(action, true)</c>,
        /// also beides mit <c>throwIfNotFound</c>. In diesem Projekt ist das
        /// folgenlos, weil die Belegung mitgeliefert wird -- aber ein Modul,
        /// das in ein fremdes Projekt wandert, stuerzt damit ab, statt sich zu
        /// beschweren. Und zwar im Konstruktor einer Komponente, wo niemand
        /// nach der Ursache sucht.
        ///
        /// Jetzt kommt null zurueck und einmal pro fehlender Aktion eine
        /// Meldung, die sagt, was fehlte und was es stattdessen gibt. Der
        /// Aufrufer muss auf null pruefen; das ist die Stelle, an der er
        /// entscheiden kann, ob er ohne die Aktion sinnvoll weiterarbeitet.
        /// </summary>
        public static InputAction Action(string map, string action)
        {
            if (Asset == null) return null;

            var karte = Asset.FindActionMap(map);
            if (karte == null)
            {
                Beschweren($"Karte \"{map}\" fehlt",
                           Namen(Asset.actionMaps, m => m.name));
                return null;
            }

            var gefunden = karte.FindAction(action);
            if (gefunden == null)
                Beschweren($"Aktion \"{action}\" fehlt in Karte \"{map}\"",
                           Namen(karte.actions, a => a.name));
            return gefunden;
        }

        /// <summary>
        /// Wurde die Aktion in diesem Bild gedrueckt? Fehlt sie, false.
        ///
        /// Diese drei Abfragen gibt es, weil der haeufigste Fehler beim
        /// Aufrufen war, das Ergebnis von <see cref="Action"/> sofort zu
        /// benutzen. Wer sie verwendet, kann das nicht falsch machen.
        /// </summary>
        public static bool Pressed(string map, string action)
            => Action(map, action)?.WasPressedThisFrame() ?? false;

        /// <summary>Wird die Aktion gehalten? Fehlt sie, false.</summary>
        public static bool Held(string map, string action)
            => Action(map, action)?.IsPressed() ?? false;

        /// <summary>Wert einer eindimensionalen Achse. Fehlt sie, 0.</summary>
        public static float Axis(string map, string action)
            => Action(map, action)?.ReadValue<float>() ?? 0f;

        /// <summary>Wert einer zweidimensionalen Achse. Fehlt sie, Vector2.zero.</summary>
        public static Vector2 Axis2(string map, string action)
            => Action(map, action)?.ReadValue<Vector2>() ?? Vector2.zero;

        private static readonly HashSet<string> gemeldet = new HashSet<string>();

        /// <summary>
        /// Meldet eine fehlende Aktion genau einmal. Ohne diese Sperre fuellt
        /// eine Komponente, die je Bild nachfragt, das Protokoll.
        /// </summary>
        private static void Beschweren(string was, string vorhanden)
        {
            if (!gemeldet.Add(was)) return;
            Debug.LogWarning($"[SmartXR.Input] {was}. Vorhanden: {vorhanden}. "
                             + "Entweder die Belegung in Resources/SmartXR.inputactions "
                             + "ergaenzen oder den Aufrufer ohne diese Aktion auskommen lassen.");
        }

        private static string Namen<T>(IEnumerable<T> quelle, Func<T, string> name)
        {
            var liste = new List<string>();
            foreach (var x in quelle) liste.Add(name(x));
            liste.Sort();
            return liste.Count > 0 ? string.Join(", ", liste) : "nichts";
        }

        /// <summary>Kurzbeschreibung des aktiven Kontexts, z. B. „Feature: IK-Zweige".</summary>
        public static string ActiveLabel => stack.Count > 0 ? stack[stack.Count - 1].Label : null;

        /// <summary>Name der aktiven Kontextkarte, oder null.</summary>
        public static string ActiveMap => stack.Count > 0 ? stack[stack.Count - 1].Map : null;

        /// <summary>
        /// Betritt einen Kontext. Hat derselbe Besitzer schon einen, wird er
        /// ersetzt und nach oben geholt.
        /// </summary>
        public static void Enter(object owner, string map, string label = null)
        {
            stack.RemoveAll(e => e.Owner == owner);
            stack.Add(new Entry { Owner = owner, Map = map, Label = label ?? map });
            Apply();
        }

        /// <summary>
        /// Hält eine Karte dauerhaft aktiv, ohne einen Kontext zu verdrängen.
        ///
        /// Für Karten, die sich mit keiner anderen um Tasten streiten — etwa
        /// Kevins Gizmo, das nur Maus, Esc und eine Ziffernblock-Taste nutzt.
        /// </summary>
        public static void EnterParallel(object owner, string map)
        {
            parallel.RemoveAll(e => e.Owner == owner);
            parallel.Add(new Entry { Owner = owner, Map = map, Label = map });
            Apply();
        }

        /// <summary>Ändert nur die Beschreibung, ohne den Kontext nach oben zu holen.</summary>
        public static void SetLabel(object owner, string label)
        {
            for (int k = 0; k < stack.Count; k++)
            {
                if (stack[k].Owner != owner) continue;
                var e = stack[k];
                e.Label = label;
                stack[k] = e;
                Changed?.Invoke();
                return;
            }
        }

        public static void Exit(object owner)
        {
            int removed = stack.RemoveAll(e => e.Owner == owner) + parallel.RemoveAll(e => e.Owner == owner);
            if (removed > 0)
                Apply();
        }

        private static void Apply()
        {
            if (Asset == null) return;

            string active = ActiveMap;
            foreach (var map in asset.actionMaps)
            {
                if (map.name == GlobalMap) continue;
                if (map.name == active || parallel.Exists(e => e.Map == map.name)) map.Enable();
                else map.Disable();
            }
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Für den Fall, dass „Enter Play Mode Options" die Domäne nicht neu lädt.
            stack.Clear();
            parallel.Clear();
            gemeldet.Clear();
            Changed = null;
            if (asset != null) asset.Disable();
            asset = null;
        }
    }
}
