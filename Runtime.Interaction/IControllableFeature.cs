using UnityEngine;

namespace SmartXR.Interaction
{
    /// <summary>
    /// Ein Feature, das Eingaben bekommen kann, wenn es den Fokus hat.
    ///
    /// Nur das fokussierte Feature wird bedient. Das ist der ganze Zweck:
    /// Sind mehrere Features gleichzeitig eingeschaltet und greift jedes
    /// selbst nach denselben Tasten, streiten sie darum, und der Benutzer
    /// kann nicht sehen, wer gewinnt.
    ///
    /// WAS SICH GEGENUEBER DER VORIGEN FASSUNG GEAENDERT HAT
    /// -----------------------------------------------------
    /// Die Vorgaengerschnittstelle lag im Anwendungsprojekt und hatte
    /// statt <see cref="IsActive"/> eine Eigenschaft <c>Feature</c> vom Typ
    /// <c>RobotFeature</c> -- ein Aufzaehlungstyp mit sieben festen Werten,
    /// aus dem sich jedes Feature eine Nummer nehmen musste.
    ///
    /// Damit konnte kein Modul ein Feature mitbringen, ohne den
    /// Aufzaehlungstyp im Kern zu erweitern. Genau das ist das Gegenteil von
    /// „ein Feature steckt in einem Prefab und meldet sich selbst an": Der
    /// Empfaenger eines Moduls kann unseren Kern nicht aendern, und er soll
    /// es auch nicht muessen.
    ///
    /// Jetzt sagt das Feature selbst, ob es eingeschaltet ist. Der Kern
    /// zaehlt nicht mehr mit und muss nichts ueber Robotik wissen.
    ///
    /// EINHEITEN
    /// ---------
    /// <see cref="OnDirection"/> und <see cref="OnVertical"/> kommen bereits
    /// mit der Schrittweite verrechnet, also in Metern je Bild. Wer sie noch
    /// einmal skaliert, verdoppelt die Empfindlichkeit.
    /// </summary>
    public interface IControllableFeature
    {
        /// <summary>Beschriftung im Fokusanzeiger und in der Liste an der Hand.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Ob das Feature gerade eingeschaltet ist. Nur eingeschaltete
        /// Features kommen beim Durchschalten des Fokus vor.
        /// </summary>
        bool IsActive { get; }

        /// <summary>Dieses Feature hat den Fokus bekommen.</summary>
        void OnFocusGained();

        /// <summary>Ein anderes Feature hat den Fokus uebernommen.</summary>
        void OnFocusLost();

        /// <summary>Richtung in der Waagerechten. X = rechts, Y = vorwaerts, in Metern.</summary>
        void OnDirection(Vector2 delta);

        /// <summary>Richtung in der Senkrechten, positiv ist aufwaerts, in Metern.</summary>
        void OnVertical(float delta);

        /// <summary>Hauptaktion: A-Knopf, Ziffernblock 5, Eingabetaste.</summary>
        void OnPrimary();

        /// <summary>Nebenaktion, meist ein Umschalter: B-Knopf, Ziffernblock 0.</summary>
        void OnSecondary();

        /// <summary>Auswahl vorwaerts durchschalten.</summary>
        void OnNext();

        /// <summary>Auswahl rueckwaerts durchschalten.</summary>
        void OnPrevious();
    }
}
