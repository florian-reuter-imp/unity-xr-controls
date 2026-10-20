using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SmartXR.Interaction.Tests
{
    /// <summary>
    /// Prueft die Fokusverteilung ohne Eingaben und ohne Szene.
    ///
    /// Das geht, weil der Verteiler zwei Teile hat: die Fokuslogik, die
    /// reine Listenarbeit ist, und den Treiber, der je Bild Tasten liest.
    /// Nur der zweite braucht Unity, und er wird im Editor absichtlich nicht
    /// angelegt. Genau deshalb ist der Schnitt dort.
    /// </summary>
    public class FeatureFocusTests
    {
        /// <summary>
        /// Ein Feature zum Mitzaehlen. Absichtlich kein MonoBehaviour: Der
        /// Verteiler verlangt keines, und ohne GameObject laufen die Tests
        /// schneller und ohne Aufraeumen in der Szene.
        /// </summary>
        private sealed class Probe : IControllableFeature
        {
            public string DisplayName { get; set; } = "Probe";
            public bool Switched = true;

            public bool IsActive => Switched;

            public int Gained, Lost, Primary, Secondary, Next, Previous;
            public Vector2 Direction;
            public float Vertical;

            public void OnFocusGained() => Gained++;
            public void OnFocusLost() => Lost++;
            public void OnDirection(Vector2 delta) => Direction += delta;
            public void OnVertical(float delta) => Vertical += delta;
            public void OnPrimary() => Primary++;
            public void OnSecondary() => Secondary++;
            public void OnNext() => Next++;
            public void OnPrevious() => Previous++;

            /// <summary>
            /// Wie <c>ControllableFeature.SetOn</c>: umschalten und den
            /// Verteiler nachziehen lassen.
            /// </summary>
            public void Set(bool on)
            {
                Switched = on;
                FeatureFocus.Revalidate();
            }

            public override string ToString() => DisplayName;
        }

        private readonly List<Probe> angemeldet = new List<Probe>();

        private Probe Anmelden(string name, bool on = true)
        {
            var p = new Probe { DisplayName = name, Switched = on };
            angemeldet.Add(p);
            FeatureFocus.Register(p);
            return p;
        }

        [TearDown]
        public void Aufraeumen()
        {
            // Der Verteiler ist statisch. Seine Rueckstellung haengt am
            // Start des Spielmodus, den es hier nicht gibt -- also von Hand,
            // sonst sieht der naechste Test die Features des vorigen.
            foreach (Probe p in angemeldet) FeatureFocus.Unregister(p);
            angemeldet.Clear();

            Assert.IsNull(FeatureFocus.Focused,
                "Nach dem Aufraeumen darf nichts mehr den Fokus haben.");
        }

        // ── Anmelden ────────────────────────────────────────────────────────

        [Test]
        public void DasErsteEingeschalteteFeatureBekommtDenFokusSofort()
        {
            // Ohne das muesste der Benutzer erst durchschalten, um
            // ueberhaupt etwas bedienen zu koennen -- und waere ein einziges
            // Feature in der Szene, waere das Durchschalten eine Taste ohne
            // sichtbare Wirkung.
            Probe a = Anmelden("A");

            Assert.AreSame(a, FeatureFocus.Focused);
            Assert.AreEqual(1, a.Gained);
        }

        [Test]
        public void EinAusgeschaltetesFeatureBekommtKeinenFokus()
        {
            Probe a = Anmelden("A", on: false);

            Assert.IsNull(FeatureFocus.Focused);
            Assert.AreEqual(0, a.Gained);
        }

        [Test]
        public void DasZweiteFeatureNimmtDemErstenNichtDenFokusWeg()
        {
            // Sonst wandert der Fokus bei jedem Prefab, das in die Szene
            // kommt -- und waere von der Ladereihenfolge abhaengig.
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");

            Assert.AreSame(a, FeatureFocus.Focused);
            Assert.AreEqual(0, b.Gained);
        }

        [Test]
        public void ZweimalAnmeldenZaehltEinmal()
        {
            Probe a = Anmelden("A");
            FeatureFocus.Register(a);

            Assert.AreEqual(1, FeatureFocus.Features.Count);
        }

        // ── Durchschalten ───────────────────────────────────────────────────

        [Test]
        public void DurchschaltenUeberspringtAusgeschaltete()
        {
            Probe a = Anmelden("A");
            Anmelden("B", on: false);
            Probe c = Anmelden("C");

            FeatureFocus.Cycle(forward: true);

            Assert.AreSame(c, FeatureFocus.Focused, "B ist aus und muss uebersprungen werden.");
            Assert.AreEqual(1, a.Lost);
        }

        [Test]
        public void DurchschaltenLaeuftUm()
        {
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");

            FeatureFocus.Cycle(true);
            Assert.AreSame(b, FeatureFocus.Focused);

            FeatureFocus.Cycle(true);
            Assert.AreSame(a, FeatureFocus.Focused, "Nach dem letzten kommt wieder das erste.");
        }

        [Test]
        public void DurchschaltenGehtAuchRueckwaerts()
        {
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");
            Probe c = Anmelden("C");

            Assert.AreSame(a, FeatureFocus.Focused);

            FeatureFocus.Cycle(forward: false);

            // Der Rest in C# ist bei negativen Zahlen negativ. Ohne
            // Korrektur greift die Rueckwaertssuche hier ins Leere statt auf
            // das letzte Feature.
            Assert.AreSame(c, FeatureFocus.Focused);
            Assert.AreEqual(0, b.Gained, "B wurde uebersprungen, nicht besucht.");
        }

        [Test]
        public void IstNurEinesEingeschaltetBleibtDerFokusDort()
        {
            Probe a = Anmelden("A");
            Anmelden("B", on: false);

            FeatureFocus.Cycle(true);

            Assert.AreSame(a, FeatureFocus.Focused);
            Assert.AreEqual(1, a.Gained, "Kein erneutes OnFocusGained fuer dasselbe Feature.");
            Assert.AreEqual(0, a.Lost);
        }

        [Test]
        public void OhneEingeschaltetesFeatureHatNiemandDenFokus()
        {
            Anmelden("A", on: false);
            Anmelden("B", on: false);

            FeatureFocus.Cycle(true);

            Assert.IsNull(FeatureFocus.Focused);
        }

        // ── Gezielt setzen ──────────────────────────────────────────────────

        [Test]
        public void FokusLaesstSichGezieltSetzen()
        {
            Anmelden("A");
            Probe b = Anmelden("B");

            Assert.IsTrue(FeatureFocus.Focus(b));
            Assert.AreSame(b, FeatureFocus.Focused);
        }

        [Test]
        public void FokusAufEinAusgeschaltetesFeatureWirdAbgelehnt()
        {
            Probe a = Anmelden("A");
            Probe b = Anmelden("B", on: false);

            Assert.IsFalse(FeatureFocus.Focus(b));
            Assert.AreSame(a, FeatureFocus.Focused, "Der alte Fokus muss stehen bleiben.");
        }

        [Test]
        public void FokusAufEinFremdesFeatureWirdAbgelehnt()
        {
            Probe a = Anmelden("A");
            var fremd = new Probe { DisplayName = "nicht angemeldet" };

            Assert.IsFalse(FeatureFocus.Focus(fremd));
            Assert.AreSame(a, FeatureFocus.Focused);
        }

        // ── Abmelden ────────────────────────────────────────────────────────

        [Test]
        public void WerDenFokusDurchAbmeldenVerliertErfaehrtEs()
        {
            // Die Zusage, auf die ein Feature sich verlassen darf: Auf jedes
            // OnFocusGained folgt genau ein OnFocusLost. Ohne das bleibt ein
            // Feature in seinem Fokuszustand haengen und zeichnet etwa
            // weiter seine Hervorhebung.
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");

            FeatureFocus.Unregister(a);
            angemeldet.Remove(a);

            Assert.AreEqual(1, a.Gained);
            Assert.AreEqual(1, a.Lost);
            Assert.AreSame(b, FeatureFocus.Focused, "Der Fokus muss weiterwandern.");
        }

        [Test]
        public void AbmeldenEinesFrueherenFeaturesLaesstDenFokusWoErIst()
        {
            // Der Fehler, den dieser Test faengt: Der Fokus wird als Index
            // gefuehrt. Faellt ein Feature vor dem fokussierten weg,
            // verschiebt sich dessen Platz -- und ohne Nachziehen bedient
            // der Verteiler danach den Nachbarn.
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");
            Probe c = Anmelden("C");

            FeatureFocus.Focus(c);
            Assert.AreSame(c, FeatureFocus.Focused);

            FeatureFocus.Unregister(a);
            angemeldet.Remove(a);

            Assert.AreSame(c, FeatureFocus.Focused,
                "C hatte den Fokus und muss ihn behalten, obwohl A wegfiel.");
            Assert.AreEqual(0, b.Gained, "B darf ihn nicht erben.");
        }

        [Test]
        public void EinFremdesFeatureAbzumeldenAendertNichts()
        {
            Probe a = Anmelden("A");

            FeatureFocus.Unregister(new Probe { DisplayName = "fremd" });

            Assert.AreSame(a, FeatureFocus.Focused);
            Assert.AreEqual(1, FeatureFocus.Features.Count);
        }

        // ── Selbst ausschalten ──────────────────────────────────────────────

        [Test]
        public void WerSichSelbstAusschaltetGibtDenFokusAbUndErfaehrtEs()
        {
            // Sonst schluckt ein ausgeschaltetes Feature weiter alle
            // Eingaben, und der Benutzer haelt die Belegung fuer kaputt.
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");

            a.Set(false);

            Assert.AreEqual(1, a.Lost);
            Assert.AreSame(b, FeatureFocus.Focused);
        }

        [Test]
        public void SchaltetSichDasLetzteAusHatNiemandDenFokus()
        {
            Probe a = Anmelden("A");

            a.Set(false);

            Assert.IsNull(FeatureFocus.Focused);
            Assert.AreEqual(1, a.Lost);
        }

        [Test]
        public void WiederEinschaltenHoltDenFokusZurueckWennSonstNichtsDaIst()
        {
            Probe a = Anmelden("A");
            a.Set(false);
            Assert.IsNull(FeatureFocus.Focused);

            a.Set(true);

            Assert.AreSame(a, FeatureFocus.Focused);
            Assert.AreEqual(2, a.Gained);
        }

        [Test]
        public void EinschaltenNimmtEinemAnderenDenFokusNichtWeg()
        {
            Probe a = Anmelden("A");
            Probe b = Anmelden("B", on: false);

            b.Set(true);

            Assert.AreSame(a, FeatureFocus.Focused,
                "Wer schon bedient wird, soll nicht unterbrochen werden.");
            Assert.AreEqual(0, b.Gained);
        }

        // ── Die Zusage insgesamt ────────────────────────────────────────────

        [Test]
        public void AufJedesOnFocusGainedFolgtGenauEinOnFocusLost()
        {
            Probe a = Anmelden("A");
            Probe b = Anmelden("B");
            Probe c = Anmelden("C");

            FeatureFocus.Cycle(true);
            FeatureFocus.Cycle(true);
            FeatureFocus.Cycle(true);
            b.Set(false);
            b.Set(true);
            FeatureFocus.Cycle(false);

            foreach (Probe p in new[] { a, b, c })
            {
                int offen = p.Gained - p.Lost;
                Assert.IsTrue(offen == 0 || offen == 1,
                    $"{p.DisplayName}: {p.Gained}x bekommen, {p.Lost}x verloren -- "
                    + "erlaubt ist nur gleich viel oder einer mehr (der aktuelle Fokus).");
            }

            int mitFokus = 0;
            foreach (Probe p in new[] { a, b, c })
                if (p.Gained - p.Lost == 1) mitFokus++;

            Assert.AreEqual(1, mitFokus, "Genau eines darf den Fokus offen haben.");
        }
    }
}
