using SmartXR.Input;
using UnityEngine;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Grundgeruest fuer ein bedienbares Feature: meldet sich selbst an,
    /// erscheint in der Liste an der Hand, laesst sich ein- und ausschalten
    /// und bekommt Eingaben, wenn es den Fokus hat.
    ///
    /// WOFUER
    /// ------
    /// Damit die Facette „bedienen" ein Modul fast nichts kostet. Vorher
    /// musste jedes Feature drei Dinge selbst erledigen -- sich beim
    /// Verteiler anmelden, sich bei
    /// <see cref="FeatureRegistry"/> anmelden und seinen Ein-Aus-Zustand
    /// fuehren. Drei der vier ersten Module haben die Facette deshalb
    /// einfach leer gelassen.
    ///
    /// Ein Feature erbt hier, gibt einen Namen und ueberschreibt die zwei
    /// oder drei Ereignisse, die es braucht. Alles andere steht schon.
    ///
    /// SO WENIG WIE MOEGLICH
    /// ---------------------
    /// Alle Ereignisse sind leere virtuelle Methoden, nicht abstrakte. Ein
    /// Feature, das nur <see cref="OnNext"/> und
    /// <see cref="OnPrevious"/> braucht, soll nicht sieben leere Methoden
    /// hinschreiben muessen -- sonst wird das Geruest zur Last und niemand
    /// benutzt es.
    ///
    /// EIN- UND AUSSCHALTEN GEGEN AKTIVIEREN
    /// ------------------------------------
    /// <see cref="IsOn"/> ist nicht dasselbe wie <c>enabled</c>. Ein
    /// ausgeschaltetes Feature bleibt angemeldet und in der Liste sichtbar,
    /// es kommt nur beim Durchschalten des Fokus nicht vor. Ein
    /// deaktiviertes Bauteil verschwindet ganz.
    ///
    /// Das ist Absicht: Wer F3 drueckt, will die Jacobi-Matrix aus- und
    /// wieder einblenden koennen, nicht sie aus der Liste verlieren.
    /// </summary>
    public abstract class ControllableFeature : MonoBehaviour, IControllableFeature
    {
        [Tooltip("Eingeschaltet, sobald das Bauteil aktiv wird.")]
        [SerializeField] private bool startsOn;

        private bool on;

        /// <summary>Beschriftung im Fokusanzeiger und in der Liste an der Hand.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Ob das Feature eingeschaltet ist.</summary>
        public bool IsOn => on;

        /// <summary>
        /// Fuer den Verteiler: eingeschaltet <b>und</b> das Bauteil aktiv.
        /// Beides, weil ein deaktiviertes Bauteil keine Ereignisse
        /// verarbeiten kann -- es bekaeme sie und tuete nichts.
        /// </summary>
        public bool IsActive => on && isActiveAndEnabled;

        /// <summary>Wird nach jedem Ein- oder Ausschalten gerufen.</summary>
        protected virtual void OnSwitched(bool isOn) { }

        public void Toggle() => SetOn(!on);

        public void SetOn(bool value)
        {
            if (on == value) return;

            on = value;
            OnSwitched(on);

            // Dem Verteiler sagen, dass sich etwas geaendert hat. Ohne das
            // behaelt ein gerade ausgeschaltetes Feature den Fokus und
            // schluckt weiter die Eingaben.
            FeatureFocus.Revalidate();
        }

        protected virtual void OnEnable()
        {
            on = startsOn;

            FeatureFocus.Register(this);
            FeatureRegistry.Register(this, DisplayName, () => IsOn, Toggle);
        }

        protected virtual void OnDisable()
        {
            FeatureFocus.Unregister(this);
            FeatureRegistry.UnregisterAll(this);
        }

        // ── Ereignisse, alle zum Ueberschreiben und alle freiwillig ─────────

        public virtual void OnFocusGained() { }
        public virtual void OnFocusLost() { }
        public virtual void OnDirection(Vector2 delta) { }
        public virtual void OnVertical(float delta) { }
        public virtual void OnPrimary() { }
        public virtual void OnSecondary() { }
        public virtual void OnNext() { }
        public virtual void OnPrevious() { }
    }
}
