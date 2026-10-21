using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SmartXR.Input
{
    /// <summary>
    /// Holt den XR Interaction Simulator herein, wenn im Editor keine Brille
    /// läuft.
    ///
    /// Die eingebaute Einstellung des XR Interaction Toolkit kennt nur „immer"
    /// oder „nie" — mit angeschlossener Brille würde der Simulator dann
    /// dazwischenfunken. Deshalb bleibt sie aus, und diese Klasse entscheidet
    /// beim Start selbst. Nur im Editor; Builds bekommen nie einen Simulator.
    ///
    /// Abschaltbar über das Menü Tools &gt; SMART-XR &gt; Simulator ohne Brille.
    ///
    /// WARUM GEWARTET WIRD
    /// -------------------
    /// Die erste Fassung entschied in einem einzigen Augenblick, nämlich bei
    /// <c>AfterSceneLoad</c>. Das ist zu früh: XR Management startet seine
    /// Subsysteme **asynchron**, und bis das Display-Subsystem läuft, gehen
    /// einige Bilder ins Land. Mit angeschlossener Brille sah die Prüfung
    /// deshalb „keine Brille", warf den Simulator an — und der lieferte dann
    /// die starre Perspektive, obwohl die Brille längst da war. Genau dieser
    /// Fehler ist am 19.10. auf dem Gerät aufgefallen.
    ///
    /// DREI SIGNALE, DREI VERSCHIEDENE FRAGEN
    /// --------------------------------------
    /// Die zweite Fassung wartete pauschal fünf Sekunden auf ein laufendes
    /// Display-Subsystem. Auch das war zu knapp: Über Air Link braucht eine
    /// Quest regelmäßig länger, bis wirklich ein Bild fließt, und dann sprang
    /// der Simulator an, obwohl die Brille unterwegs war. Am 21.10. gemeldet.
    ///
    /// Der Fehler lag nicht in der Zahl, sondern darin, **ein** Signal für
    /// drei Fragen zu benutzen:
    ///
    ///   - <c>Manager.isInitializationComplete</c> sagt: Der Versuch ist
    ///     vorbei. Nicht, ob er geglückt ist.
    ///   - <c>Manager.activeLoader != null</c> sagt: Eine XR-Laufzeit hat uns
    ///     angenommen. **Es wird eine Brille geben.** Ab hier ist ein
    ///     Simulator falsch, egal wie lange das Bild noch braucht.
    ///   - <c>XRDisplaySubsystem.running</c> sagt: Es fließt ein Bild. Das
    ///     ist das späteste der drei und das einzige, auf das es ankam.
    ///
    /// Dazu kommt ein echtes Ereignis statt Abfragen:
    /// <c>InputDevices.deviceConnected</c> meldet die Brille in dem Moment,
    /// in dem sie sich anmeldet.
    ///
    /// SO WIRD JETZT ENTSCHIEDEN
    /// -------------------------
    ///   - Display läuft oder eine Brille meldet sich: kein Simulator, sofort.
    ///   - Initialisierung durch und **kein** Loader: keine Brille in Sicht,
    ///     Simulator sofort. Wer ohne Brille arbeitet, wartet keine Sekunde.
    ///   - Loader aktiv, Bild noch nicht da: weiter warten, und zwar
    ///     großzügig (<see cref="WartezeitMitLoader"/>). Hier kostet Warten
    ///     nichts, Aufgeben aber die richtige Ansicht.
    /// </summary>
    public static class SimulatorBootstrap
    {
#if UNITY_EDITOR
        private const string PrefKey = "SmartXR.SimulatorWithoutHeadset";
        private const string MenuPath = "Tools/SMART-XR/Simulator ohne Brille";

        /// <summary>
        /// Wie lange auf die Entscheidung gewartet wird, **solange noch kein
        /// Loader übernommen hat**. Hier geht es nur um die wenigen Bilder,
        /// die XR Management zum Hochfahren braucht.
        /// </summary>
        private const float WartezeitBisEntscheidung = 5f;

        /// <summary>
        /// Wie lange auf das Bild gewartet wird, **wenn ein Loader aktiv
        /// ist**, die Brille also kommt.
        ///
        /// Eine Minute, und das ist Absicht. Der Preis ist hier sehr
        /// ungleich verteilt: Wartet man zu lang, passiert nichts weiter —
        /// es gibt ohnehin keine Ansicht, auf die man warten müsste, denn
        /// die Brille ist im Anmarsch. Gibt man zu früh auf, startet der
        /// Simulator und übernimmt die Kamera, und dann sieht man die starre
        /// Perspektive, obwohl die Brille zwei Sekunden später bereit wäre.
        /// Genau das ist über Air Link passiert.
        /// </summary>
        public static float WartezeitMitLoader = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (!EditorPrefs.GetBool(PrefKey, true)) return;
            if (Object.FindAnyObjectByType<XRInteractionSimulator>() != null) return;

            // Ein Träger, weil eine Koroutine ein MonoBehaviour braucht und
            // diese Klasse statisch ist. Er räumt sich selbst weg.
            var traeger = new GameObject("SmartXR Simulator Bootstrap")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(traeger);
            traeger.AddComponent<Warten>();
        }

        /// <summary>Wartet die Entscheidung ab und legt dann los — oder nicht.</summary>
        private sealed class Warten : MonoBehaviour
        {
            /// <summary>
            /// Wird vom Ereignis gesetzt, sobald sich eine Brille anmeldet.
            /// Ein Ereignis statt einer Abfrage: Es meldet sich im selben
            /// Augenblick und nicht erst im nächsten Bild.
            /// </summary>
            private bool brilleGemeldet;

            private void OnEnable() => InputDevices.deviceConnected += Angemeldet;
            private void OnDisable() => InputDevices.deviceConnected -= Angemeldet;

            private void Angemeldet(InputDevice geraet)
            {
                if ((geraet.characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
                    brilleGemeldet = true;
            }

            private IEnumerator Start()
            {
                // --- Stufe 1: Warten, bis klar ist, ob XR überhaupt läuft ---
                float bis = Time.realtimeSinceStartup + WartezeitBisEntscheidung;

                while (Time.realtimeSinceStartup < bis)
                {
                    if (Da()) { Abbrechen("Brille aktiv"); yield break; }
                    if (EntschiedenOhneXr()) break;
                    if (LoaderAktiv()) break;
                    yield return null;
                }

                // --- Stufe 2: Loader aktiv? Dann kommt die Brille ----------
                if (LoaderAktiv())
                {
                    Debug.Log("[SmartXR.Input] XR-Laufzeit aktiv, warte auf das Bild. "
                              + $"Bis zu {WartezeitMitLoader:0.#} s — über Air Link dauert "
                              + "das regelmäßig länger. Kein Simulator, solange ein Loader "
                              + "läuft.");

                    float spaetestens = Time.realtimeSinceStartup + WartezeitMitLoader;
                    while (Time.realtimeSinceStartup < spaetestens)
                    {
                        if (Da()) { Abbrechen("Brille liefert ein Bild"); yield break; }

                        // Der Loader kann auch wieder verschwinden, etwa wenn
                        // OpenXR die Sitzung aufgibt. Dann ist die Lage neu
                        // entschieden, und zwar gegen die Brille.
                        if (!LoaderAktiv()) break;

                        yield return null;
                    }

                    if (LoaderAktiv())
                    {
                        Debug.LogWarning(
                            $"[SmartXR.Input] Nach {WartezeitMitLoader:0.#} s läuft eine "
                            + "XR-Laufzeit, aber es kommt kein Bild. Steht die Brille im "
                            + "Standby, oder ist Link nicht gestartet? Der Simulator wird "
                            + "jetzt doch angeworfen; er übernimmt dann die Kamera.");
                    }
                }

                Starten();
                Destroy(gameObject);
            }

            private void Abbrechen(string grund)
            {
                Debug.Log($"[SmartXR.Input] {grund} — kein Simulator.");
                Destroy(gameObject);
            }

            /// <summary>Brille da: Bild fließt, oder sie hat sich angemeldet.</summary>
            private bool Da() => XrPresence.HeadsetActive || brilleGemeldet;

            /// <summary>Eine XR-Laufzeit hat uns angenommen.</summary>
            private static bool LoaderAktiv()
            {
                var settings = XRGeneralSettings.Instance;
                return settings != null && settings.Manager != null
                       && settings.Manager.activeLoader != null;
            }

            /// <summary>
            /// True, wenn feststeht, dass es keine Brille geben wird: Die
            /// Initialisierung ist durch und kein Loader hat übernommen.
            /// Solange sie läuft, ist noch nichts entschieden.
            /// </summary>
            private static bool EntschiedenOhneXr()
            {
                var settings = XRGeneralSettings.Instance;
                if (settings == null || settings.Manager == null) return true;
                return settings.Manager.isInitializationComplete
                       && settings.Manager.activeLoader == null;
            }
        }

        private static void Starten()
        {
            if (Object.FindAnyObjectByType<XRInteractionSimulator>() != null) return;

            var prefab = FindPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[SmartXR.Input] XR Interaction Simulator nicht gefunden. " +
                                 "Package Manager > XR Interaction Toolkit > Samples > XR Interaction Simulator importieren.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            Object.DontDestroyOnLoad(instance);
            Debug.Log("[SmartXR.Input] Keine Brille aktiv — XR Interaction Simulator gestartet.");
        }

        private static GameObject FindPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("\"XR Interaction Simulator\" t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/XR Interaction Simulator.prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, true));

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }
#endif
    }
}
