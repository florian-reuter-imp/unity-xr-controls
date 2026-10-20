using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace SmartXR.Input
{
    /// <summary>
    /// Tafel mit zwei Seiten: der Tastenübersicht und der Feature-Liste.
    ///
    /// F10 am Desktop und der Menü-Knopf links in VR schalten weiter:
    /// aus, Tasten, Features, aus. Die Tastenseite entsteht aus den gerade
    /// aktiven Aktionskarten, die Feature-Seite aus
    /// <see cref="FeatureRegistry"/>.
    ///
    /// Die Feature-Seite gibt es, weil F1 bis F7 in der Brille nicht
    /// erreichbar sind. Solange sie offen ist, wählt der rechte Stick (oder
    /// Num 8/2) den Eintrag und A (oder Num 5) schaltet ihn um — dafür
    /// übernimmt sie den Eingabekontext, die Sticks fahren also solange
    /// nichts anderes.
    ///
    /// Weil der Text aus der Aktionsdatei kommt, stimmt er nach jeder
    /// Umbelegung automatisch. Legt sich beim Start selbst an; keine Szene
    /// muss dafür geändert werden.
    /// </summary>
    public class InputOverlay : MonoBehaviour
    {
        private const string DesktopGroup = "Desktop";
        private const string XrGroup = "XR";

        /// <summary>
        /// Lesbare Namen für Karten und Aktionen.
        ///
        /// Schlüssel ist der Aktionsname, oder „Karte/Aktion“, wenn dieselbe
        /// Aktion in mehreren Karten vorkommt und dort etwas anderes bedeutet.
        /// <see cref="Label(string, string)"/> probiert erst die qualifizierte
        /// Form, dann die einfache.
        ///
        /// Gefüllt über den Indexer und nicht über ein Literal: Ein doppelter
        /// Schlüssel im Literal wirft, und zwar im statischen Konstruktor —
        /// danach ist der ganze Typ unbrauchbar und die Tastenübersicht tot.
        /// Genau das ist am 19.10. passiert, mit „Bezugssystem“. Ein
        /// Überschreiben ist das kleinere Übel: eine falsche Beschriftung
        /// statt keiner Übersicht.
        /// </summary>
        private static readonly Dictionary<string, string> Labels = Beschriftungen();

        private static Dictionary<string, string> Beschriftungen()
        {
            var d = new Dictionary<string, string>();
            void Setze(string schluessel, string text) => d[schluessel] = text;

            Setze("Global", "Immer");
            Setze("Feature", "Feature");
            Setze("Feature1", "Koordinatensysteme ein/aus");
            Setze("Feature2", "URDF vs. DH ein/aus");
            Setze("Feature3", "Jacobi-Matrix ein/aus");
            Setze("Feature4", "IK-Zweige ein/aus");
            Setze("Feature5", "Singularitäten ein/aus");
            Setze("Feature6", "Bahnplanung ein/aus");
            Setze("Feature7", "Zielpose ein/aus");
            Setze("Tastenuebersicht", "Diese Übersicht");
            Setze("Bedienmodus", "Bedienmodus (Sticks statt Gehen)");
            Setze("RoboterLaden", "Roboter aus URDF laden");
            Setze("Sprache", "Kursinhalte Deutsch/Englisch");
            Setze("Bahnbau", "Bahnbau");
            Setze("Ziehen", "Griff ziehen");
            Setze("Bahnbau/Bezugssystem", "Bezugssystem lokal/global");
            Setze("Abwaehlen", "Auswahl aufheben");
            Setze("Roboter", "Roboter");
            Setze("Verschieben", "Werkzeugpunkt verschieben");
            Setze("DrehenHalten", "halten: drehen statt verschieben");
            Setze("Roboter/Bezugssystem", "Bezugssystem Basis/Werkzeug");
            Setze("ZielZuruecksetzen", "Ziel auf den Werkzeugpunkt");
            Setze("ZielSenden", "Stellung an ROS schicken");
            Setze("Richtung", "Richtung");
            Setze("Hoehe", "Hoch/runter");
            Setze("Hauptaktion", "Hauptaktion");
            Setze("Nebenaktion", "Nebenaktion");
            Setze("Weiter", "Nächste Auswahl");
            Setze("Zurueck", "Vorige Auswahl");
            Setze("FokusWeiter", "Nächstes Feature");
            Setze("FokusZurueck", "Voriges Feature");
            return d;
        }

        /// <summary>aus, Tastenübersicht, Feature-Liste.</summary>
        private enum Page { Off = 0, Keys = 1, Features = 2 }

        private Page page = Page.Off;
        private int highlighted;
        private string text = "";
        private bool dirty = true;
        private bool vrMode;

        private GUIStyle boxStyle, hintStyle;
        private TextMeshPro wristText;
        private Transform wristAnchor;

        // ------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<InputOverlay>() != null) return;
            var go = new GameObject("SMART-XR Tastenübersicht");
            DontDestroyOnLoad(go);
            go.AddComponent<InputOverlay>();
        }

        private void OnEnable()
        {
            InputContext.Changed += MarkDirty;
            OperatingMode.Changed += MarkDirty;
            FeatureRegistry.Changed += MarkDirty;
            var toggle = InputContext.Action(InputContext.GlobalMap, "Tastenuebersicht");
            if (toggle != null) toggle.performed += OnToggle;
        }

        private void OnDisable()
        {
            InputContext.Changed -= MarkDirty;
            OperatingMode.Changed -= MarkDirty;
            FeatureRegistry.Changed -= MarkDirty;
            var toggle = InputContext.Action(InputContext.GlobalMap, "Tastenuebersicht");
            if (toggle != null) toggle.performed -= OnToggle;
            InputContext.Exit(this);
        }

        private void MarkDirty() => dirty = true;

        private void OnToggle(InputAction.CallbackContext _)
        {
            // Die Feature-Seite übersprungen, solange nichts angemeldet ist.
            page = page switch
            {
                Page.Off => Page.Keys,
                Page.Keys => FeatureRegistry.Entries.Count > 0 ? Page.Features : Page.Off,
                _ => Page.Off
            };

            if (page == Page.Features) InputContext.Enter(this, "FeatureListe", "Feature-Liste");
            else InputContext.Exit(this);

            dirty = true;
        }

        private void Update()
        {
            bool vr = XrPresence.HeadsetActive;
            if (vr != vrMode) { vrMode = vr; dirty = true; }

            if (page == Page.Features) HandleFeatureList();

            if (dirty)
            {
                text = page == Page.Features
                    ? BuildFeatureList()
                    : Build(vrMode ? XrGroup : DesktopGroup);
                dirty = false;
            }

            UpdateWrist();
        }

        // ------------------------------------------------------------
        // Feature-Seite

        private float lastStep;

        private void HandleFeatureList()
        {
            var entries = FeatureRegistry.Entries;
            if (entries.Count == 0) { page = Page.Off; InputContext.Exit(this); dirty = true; return; }

            highlighted = Mathf.Clamp(highlighted, 0, entries.Count - 1);

            // Stick: ein Schritt je Auslenkung, nicht je Bild.
            float axis = InputContext.Axis("FeatureListe", "Auswahl");
            if (Mathf.Abs(axis) < 0.4f) lastStep = 0f;
            else if (lastStep == 0f)
            {
                highlighted = (highlighted + (axis > 0f ? -1 : 1) + entries.Count) % entries.Count;
                lastStep = axis;
                dirty = true;
            }

            if (InputContext.Pressed("FeatureListe", "Umschalten"))
            {
                entries[highlighted].Toggle?.Invoke();
                dirty = true;
            }

            // Zustände können sich auch von außen ändern (F1–F7).
            dirty = true;
        }

        private string BuildFeatureList()
        {
            var sb = new StringBuilder("<b>Features</b>\n");
            var entries = FeatureRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                bool on = entries[i].IsActive != null && entries[i].IsActive();
                sb.Append(i == highlighted ? "> " : "  ")
                  .Append(on ? "[x] " : "[ ] ")
                  .Append(entries[i].Label)
                  .Append('\n');
            }

            sb.Append("\n<i>Auswahl: rechter Stick oder Num 8/2, umschalten: A oder Num 5</i>");
            return sb.ToString();
        }

        // ------------------------------------------------------------
        // Inhalt

        private static string Build(string group)
        {
            var asset = InputContext.Asset;
            if (asset == null) return "";

            var sb = new StringBuilder();
            string label = InputContext.ActiveLabel;
            if (!string.IsNullOrEmpty(label))
                sb.Append("<b>Aktiv: ").Append(label).Append("</b>\n");
            if (OperatingMode.Active)
                sb.Append("<b>Bedienmodus: ein</b> — Sticks steuern, Gehen ruht\n");

            foreach (var map in asset.actionMaps)
            {
                if (!map.enabled) continue;

                var lines = new StringBuilder();
                foreach (var action in map.actions)
                {
                    string keys = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
                    if (string.IsNullOrEmpty(keys)) continue;
                    lines.Append("  ").Append(keys).Append("  —  ").Append(Label(map.name, action.name)).Append('\n');
                }

                if (lines.Length == 0) continue;
                sb.Append("\n<b>").Append(Label(map.name)).Append("</b>\n").Append(lines);
            }

            if (group == XrGroup && !OperatingMode.Active)
                sb.Append("\n<i>Die Sticks gehören dem Gehen — Stick-Klick links schaltet den Bedienmodus ein.</i>");

            return sb.ToString().TrimStart('\n');
        }

        /// <summary>Lesbarer Name einer Karte.</summary>
        private static string Label(string name)
            => Labels.TryGetValue(name, out var l) ? l : name;

        /// <summary>
        /// Lesbarer Name einer Aktion. Erst „Karte/Aktion“ versuchen, dann den
        /// blossen Aktionsnamen — so bekommt dieselbe Aktion in zwei Karten
        /// zwei Texte, ohne dass die übrigen Einträge qualifiziert werden
        /// müssen.
        /// </summary>
        private static string Label(string map, string action)
            => Labels.TryGetValue(map + "/" + action, out var l) ? l : Label(action);

        // ------------------------------------------------------------
        // Desktop: Tafel oben links

        private void OnGUI()
        {
            if (vrMode) return;

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    richText = true,
                    fontSize = 14,
                    padding = new RectOffset(12, 12, 10, 10)
                };
                boxStyle.normal.textColor = Color.white;
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            }

            if (page == Page.Off)
            {
                GUI.Label(new Rect(10, 6, 300, 20), "F10: Tastenübersicht", hintStyle);
                return;
            }

            var content = new GUIContent(text);
            float width = 420f;
            float height = boxStyle.CalcHeight(content, width);
            GUI.Box(new Rect(10, 10, width, height), content, boxStyle);
        }

        // ------------------------------------------------------------
        // VR: Tafel über der linken Hand

        private void UpdateWrist()
        {
            bool show = vrMode && page != Page.Off;
            if (!show)
            {
                if (wristText != null) wristText.gameObject.SetActive(false);
                return;
            }

            if (wristAnchor == null || !wristAnchor.gameObject.activeInHierarchy)
                wristAnchor = FindLeftHand();
            if (wristAnchor == null) return;

            if (wristText == null)
            {
                var go = new GameObject("Tastenübersicht (Hand)");
                wristText = go.AddComponent<TextMeshPro>();
                wristText.fontSize = 0.12f;
                wristText.richText = true;
                wristText.alignment = TextAlignmentOptions.BottomLeft;
                wristText.rectTransform.sizeDelta = new Vector2(0.28f, 0.3f);
                wristText.rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            wristText.gameObject.SetActive(true);
            wristText.text = text;

            // Etwas über der Hand, immer dem Blick zugewandt.
            var cam = Camera.main;
            Vector3 pos = wristAnchor.position + Vector3.up * 0.08f;
            wristText.transform.position = pos;
            if (cam != null)
                wristText.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
        }

        private static Transform FindLeftHand()
        {
            var modality = FindAnyObjectByType<XRInputModalityManager>();
            if (modality == null) return null;
            if (modality.leftController != null && modality.leftController.activeInHierarchy)
                return modality.leftController.transform;
            if (modality.leftHand != null && modality.leftHand.activeInHierarchy)
                return modality.leftHand.transform;
            return null;
        }
    }
}
