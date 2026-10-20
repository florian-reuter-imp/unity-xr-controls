using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartXR.Input
{
    /// <summary>
    /// Die ein- und ausschaltbaren Features, damit sie auch ohne Tastatur
    /// erreichbar sind.
    ///
    /// Am Schreibtisch liegen sie auf F1 bis F7. In der Brille gibt es keine
    /// F-Tasten, deshalb zeigt die Tafel an der linken Hand eine Liste
    /// (<see cref="InputOverlay"/>). Damit die Eingabe-Assembly nichts über
    /// Robotik wissen muss, meldet die Feature-Verwaltung ihre Einträge hier
    /// an — Beschriftung, Zustand und eine Umschaltfunktion, nichts weiter.
    /// </summary>
    public static class FeatureRegistry
    {
        public sealed class Entry
        {
            public string Label;
            public Func<bool> IsActive;
            public Action Toggle;
            public object Owner;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => entries;

        /// <summary>Wird ausgelöst, wenn Einträge dazukommen oder wegfallen.</summary>
        public static event Action Changed;

        public static void Register(object owner, string label, Func<bool> isActive, Action toggle)
        {
            entries.Add(new Entry { Owner = owner, Label = label, IsActive = isActive, Toggle = toggle });
            Changed?.Invoke();
        }

        public static void UnregisterAll(object owner)
        {
            if (entries.RemoveAll(e => e.Owner == owner) > 0)
                Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            Changed = null;
        }
    }
}
