using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartXR.Input.Tests
{
    /// <summary>
    /// Der Code sucht Aktionen über ihren Namen. Ein Tippfehler fiele sonst
    /// erst zur Laufzeit auf.
    /// </summary>
    public class InputActionsTests
    {
        private static readonly string[] GlobalActions =
        {
            "Feature1", "Feature2", "Feature3", "Feature4", "Feature5", "Feature6", "Feature7",
            "Tastenuebersicht", "Bedienmodus", "RoboterLaden", "Sprache"
        };

        private static readonly string[] FeatureActions =
        {
            "Richtung", "Hoehe", "Hauptaktion", "Nebenaktion", "Weiter", "Zurueck", "FokusWeiter", "FokusZurueck"
        };

        private static readonly string[] BahnbauActions = { "Ziehen", "Bezugssystem", "Abwaehlen" };

        private static readonly string[] RoboterActions =
        {
            "Verschieben", "Hoehe", "DrehenHalten", "Bezugssystem", "ZielZuruecksetzen", "ZielSenden"
        };

        private static readonly string[] FeatureListeActions = { "Auswahl", "Umschalten" };

        /// <summary>Aktionen, die auch am Controller liegen muessen.</summary>
        private static readonly string[] NeedXr =
        {
            "Global/Tastenuebersicht", "Global/Bedienmodus",
            "Feature/Richtung", "Feature/Hoehe", "Feature/Hauptaktion", "Feature/Nebenaktion",
            "Feature/Weiter", "Feature/Zurueck", "Feature/FokusWeiter", "Feature/FokusZurueck",
            "Roboter/Verschieben", "Roboter/Hoehe", "Roboter/DrehenHalten",
            "Roboter/Bezugssystem", "Roboter/ZielZuruecksetzen", "Roboter/ZielSenden",
            "FeatureListe/Auswahl", "FeatureListe/Umschalten"
        };

        private static InputActionAsset Load()
        {
            var asset = Resources.Load<InputActionAsset>("SmartXR");
            Assert.IsNotNull(asset, "Resources/SmartXR.inputactions fehlt");
            return asset;
        }

        [Test]
        public void AllActionsUsedInCodeExist()
        {
            var asset = Load();
            foreach (string a in GlobalActions)
                Assert.IsNotNull(asset.FindActionMap("Global", true).FindAction(a), "Global/" + a);
            foreach (string a in FeatureActions)
                Assert.IsNotNull(asset.FindActionMap("Feature", true).FindAction(a), "Feature/" + a);
            foreach (string a in BahnbauActions)
                Assert.IsNotNull(asset.FindActionMap("Bahnbau", true).FindAction(a), "Bahnbau/" + a);
            foreach (string a in RoboterActions)
                Assert.IsNotNull(asset.FindActionMap("Roboter", true).FindAction(a), "Roboter/" + a);
            foreach (string a in FeatureListeActions)
                Assert.IsNotNull(asset.FindActionMap("FeatureListe", true).FindAction(a), "FeatureListe/" + a);
        }

        [Test]
        public void EveryActionHasADesktopBinding()
        {
            foreach (var action in Load())
                Assert.IsNotEmpty(action.GetBindingDisplayString(InputBinding.MaskByGroup("Desktop")),
                    action.actionMap.name + "/" + action.name);
        }

        /// <summary>
        /// Controller zuerst: Was in VR erreichbar sein muss, braucht eine
        /// XR-Bindung (docs/Tastenplan.md).
        /// </summary>
        [Test]
        public void ControllerBindingsExistWhereNeeded()
        {
            var asset = Load();
            foreach (string path in NeedXr)
            {
                string[] parts = path.Split('/');
                var action = asset.FindActionMap(parts[0], true).FindAction(parts[1], true);
                Assert.IsNotEmpty(action.GetBindingDisplayString(InputBinding.MaskByGroup("XR")), path);
            }
        }

        /// <summary>
        /// Tasten, die der XR Interaction Simulator belegt. Unsere Tastatur
        /// weicht ihnen aus, damit beides zugleich läuft (docs/Tastenplan.md).
        /// </summary>
        [Test]
        public void NoKeyboardBindingCollidesWithSimulator()
        {
            string[] simulatorKeys =
            {
                "w", "a", "s", "d", "q", "e", "r", "h", "x", "y", "v", "c", "z", "t", "g", "m",
                "i", "j", "k", "l", "o", "p", "n", "space", "tab", "shift", "leftShift", "rightShift",
                "upArrow", "downArrow", "leftArrow", "rightArrow", "leftBracket", "rightBracket",
                "backquote", "minus", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"
            };

            foreach (var action in Load())
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || !binding.path.StartsWith("<Keyboard>/")) continue;
                string key = binding.path.Substring("<Keyboard>/".Length);
                CollectionAssert.DoesNotContain(simulatorKeys, key,
                    $"{action.actionMap.name}/{action.name} liegt auf einer Simulator-Taste");
            }
        }
    }

    /// <summary>
    /// Die Tastenuebersicht hat ein Woerterbuch mit lesbaren Namen. Lag darin
    /// ein Schluessel zweimal, warf der **statische** Konstruktor — und damit
    /// war der ganze Typ fuer die Sitzung unbrauchbar, die Uebersicht also
    /// tot. Aufgefallen ist das am 19.10. nur an einer einzigen Zeile im
    /// Editor-Log; in der Brille haette niemand den Zusammenhang gesehen.
    ///
    /// Diese zwei Tests haetten es gefunden.
    /// </summary>
    public class InputOverlayTests
    {
        [Test]
        public void DasWoerterbuchDerUebersichtLaedtUeberhaupt()
        {
            // Erzwingt den statischen Konstruktor. Schlaegt er fehl, kommt
            // eine TypeInitializationException — genau der Fehler, um den es
            // geht.
            Assert.DoesNotThrow(() =>
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                    typeof(InputOverlay).TypeHandle));
        }

        /// <summary>
        /// „Bezugssystem“ gibt es in zwei Karten und bedeutet dort
        /// Verschiedenes: in Bahnbau lokal gegen global, bei Roboter Basis
        /// gegen Werkzeug. Beide Texte muessen durchkommen, und zwar
        /// verschieden — sonst steht in der Uebersicht der falsche.
        /// </summary>
        [Test]
        public void GleichnamigeAktionenInZweiKartenBekommenVerschiedeneTexte()
        {
            var label = typeof(InputOverlay).GetMethod(
                "Label",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string), typeof(string) }, null);
            Assert.IsNotNull(label, "Label(string, string) fehlt — wurde die Signatur geaendert?");

            string bahnbau = (string)label.Invoke(null, new object[] { "Bahnbau", "Bezugssystem" });
            string roboter = (string)label.Invoke(null, new object[] { "Roboter", "Bezugssystem" });

            Assert.AreNotEqual(bahnbau, roboter,
                "Dieselbe Aktion in zwei Karten braucht zwei Texte");
            StringAssert.Contains("lokal", bahnbau);
            StringAssert.Contains("Basis", roboter);
        }

        /// <summary>
        /// Eine Aktion ohne eigenen Eintrag soll ihren Rohnamen behalten und
        /// nicht leer bleiben — lieber unbeschriftet als unsichtbar.
        /// </summary>
        [Test]
        public void UnbekannteAktionBehaeltIhrenNamen()
        {
            var label = typeof(InputOverlay).GetMethod(
                "Label",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string), typeof(string) }, null);

            string text = (string)label.Invoke(null, new object[] { "Irgendwas", "GibtEsNicht" });
            Assert.AreEqual("GibtEsNicht", text);
        }
    }
}
