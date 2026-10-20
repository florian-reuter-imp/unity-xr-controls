using System.Collections;
using UnityEngine;
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
    /// Jetzt wird gewartet, bis die Lage **entschieden** ist, und zwar in
    /// beide Richtungen:
    ///
    ///   - Läuft ein Display-Subsystem, gibt es eine Brille: kein Simulator.
    ///   - Ist die Initialisierung fertig und kein Loader aktiv, gibt es
    ///     keine: Simulator, und zwar sofort, ohne die Wartezeit abzusitzen.
    ///   - Sonst weiter warten, höchstens <see cref="Wartezeit"/> Sekunden.
    ///
    /// Ohne XR-Konfiguration fällt die Entscheidung im ersten Bild, es
    /// verzögert also niemanden, der ohne Brille arbeitet.
    /// </summary>
    public static class SimulatorBootstrap
    {
#if UNITY_EDITOR
        private const string PrefKey = "SmartXR.SimulatorWithoutHeadset";
        private const string MenuPath = "Tools/SMART-XR/Simulator ohne Brille";

        /// <summary>
        /// Wie lange höchstens auf eine Brille gewartet wird.
        ///
        /// Fünf Sekunden sind großzügig — eine über Link verbundene Quest
        /// braucht meist unter einer. Großzügig, weil der Preis unterschiedlich
        /// ausfällt: Wartet man zu kurz, bekommt man die starre Perspektive und
        /// sucht den Fehler woanders. Wartet man zu lang, fängt der
        /// Desktop-Betrieb ein paar Sekunden später an.
        /// </summary>
        private const float Wartezeit = 5f;

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
            private IEnumerator Start()
            {
                float bis = Time.realtimeSinceStartup + Wartezeit;
                bool entschieden = false;

                while (Time.realtimeSinceStartup < bis)
                {
                    if (XrPresence.HeadsetActive)
                    {
                        Debug.Log("[SmartXR.Input] Brille aktiv — kein Simulator.");
                        Destroy(gameObject);
                        yield break;
                    }

                    if (OhneXr())
                    {
                        entschieden = true;   // Kein Loader, kein Warten noetig.
                        break;
                    }

                    yield return null;
                }

                if (!entschieden)
                {
                    Debug.Log($"[SmartXR.Input] Nach {Wartezeit:0.#} s keine Brille "
                              + "erkannt — XR Interaction Simulator wird gestartet. Hängt "
                              + "die Brille doch? Dann war sie zu langsam: Wartezeit in "
                              + "SimulatorBootstrap erhöhen, oder den Simulator über "
                              + "Tools > SMART-XR > Simulator ohne Brille abschalten.");
                }

                Starten();
                Destroy(gameObject);
            }

            /// <summary>
            /// True, wenn feststeht, dass es keine Brille geben wird: Die
            /// Initialisierung ist durch und kein Loader hat übernommen.
            /// Solange sie läuft, ist noch nichts entschieden.
            /// </summary>
            private static bool OhneXr()
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
