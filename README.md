# XR Controls

Bedienung von Roboterszenen in XR **und** am Schreibtisch, mit derselben
Belegung.

## Zwei Assemblies

| Assembly | Ordner | Braucht | Inhalt |
|---|---|---|---|
| `SmartXR.Input` | `Runtime.Input/` | Input System, XRI, XR Management, TMP | Eingabekontexte, Feature-Liste, Bedienmodus, XR-Erkennung |
| `SmartXR.Interaction` | `Runtime.Interaction/` | dazu das Kinematikpaket | Fokusverteiler, Greifen, Joggen am Werkzeugpunkt |

`SmartXR.Input` weiß nichts über Robotik. `SmartXR.Interaction` ist die
Schicht, die Roboter mit XR-Interaktion verbindet.

## Die vier Gedanken, um die es geht

**1. Eingabekontexte stapeln sich.**
`InputContext.Enter(owner, map, label)` legt eine Karte oben auf einen
Stapel; `Exit` nimmt sie weg. Nur die oberste ist aktiv. Damit streiten sich
zwei Features nie um dieselbe Taste, und der Benutzer kann jederzeit sehen,
welche Belegung gerade gilt: Die Beschriftung aus dem Kontext steht in der
Übersicht.

`Action(map, action)` gibt **null** zurück, wenn eine Aktion fehlt, statt zu
werfen. In einem fremden Projekt ohne unsere Belegung soll sich ein Bauteil
beschweren, nicht abstürzen, und zwar nicht im Konstruktor, wo niemand nach
der Ursache sucht.

**2. Features melden sich selbst an.**
`FeatureRegistry.Register(owner, label, isActive, toggle)`: Beschriftung,
Zustand, Umschaltfunktion, nichts weiter. Die Eingabeschicht muss dadurch
nichts über Robotik wissen, und in der Brille erscheint der Eintrag auf der
Tafel an der linken Hand. Am Schreibtisch liegt dasselbe auf F1 bis F7.

**3. Nur ein Feature wird bedient.**
`FeatureFocus` verteilt Richtung, Höhe und vier diskrete Aktionen an genau
das fokussierte Feature. `ControllableFeature` ist das Grundgerüst dazu:
erben, einen Namen geben, zwei Ereignisse überschreiben.

**Der Verteiler entsteht beim ersten Anmelden von selbst.** Ein Prefab, das
in eine fremde Szene gezogen wird, braucht also kein zweites, von Hand
platziertes Bauteil, und das ist die Bedingung dafür, dass ein Feature wirklich
umziehbar ist.

**4. Was hier nicht steht: eine Aufzählung der Features.**
Es gab eine. `RobotFeature` hatte sieben feste Werte, und jedes Feature nahm
sich eine Nummer daraus. Damit konnte niemand ein Feature mitbringen, ohne
diesen Kern zu erweitern, und wer ein Paket einbindet, kann es nicht
ändern. Jetzt sagt ein Feature selbst, ob es eingeschaltet ist.

## Der Nebeneffekt, der die Mühe wert war

Weil ein Feature keine Taste liest, sondern Ereignisse zugeteilt bekommt,
**kann ein Modul bedienbar sein, ohne XR zu kennen**. Es verweist auf dieses
Paket, nicht auf XRI. Die Trennung von „zeigt etwas" und „braucht eine
Brille" bleibt damit erhalten.

## XR erkennen, statt es zu raten

`XrPresence` sagt, ob eine Brille da ist, und `SimulatorBootstrap` **wartet
darauf**, höchstens fünf Sekunden. Vorher entschied der automatische
Simulator in einem Augenblick, in dem das Display-Subsystem noch nicht lief:
Mit angeschlossener Brille stand dann die starre Schreibtischperspektive im
Headset, und man suchte den Fehler in der XR-Konfiguration.

`DesktopNavigationGuard` schaltet fremde Schreibtischnavigation ab, sobald
ein XR-Rig läuft: über Typnamen und **ohne** das fremde Paket zu
referenzieren, damit es ohne dieses Paket trotzdem übersetzt.

## Einbinden

**Die Reihenfolge ist wichtig.** Dieses Paket braucht das Kinematikpaket,
weil `SmartXR.Interaction` am Werkzeugpunkt arbeitet. Es steht deshalb in den
`dependencies`, aber Unity löst solche Angaben gegen eine **Registry** auf,
und dort steht es nicht : transitive Git-Abhängigkeiten verfolgt der Package
Manager nicht. Wer dieses Paket zuerst einbindet, bekommt

```
Package [com.github.hka-imp.robotkinematics@0.6.0] cannot be found
```

Also **zuerst**:

```
https://github.com/florian-reuter-imp/unity-robot-kinematics.git#v0.6.0
```

**dann**:

```
https://github.com/florian-reuter-imp/unity-xr-controls.git#v0.2.0
```

Unity: *Window > Package Manager > + > Add package from git URL*, beide Male.
Als eingebettete Pakete in `Packages/` geht es ebenso, dann spielt die
Reihenfolge keine Rolle.

Die Belegung liegt in `Runtime.Input/Resources/SmartXR.inputactions` und wird
über `Resources.Load` gefunden, nichts zuzuweisen.

Geprüft gegen Unity **6000.0** und **6000.3**.

## Lizenz

[Apache-2.0](LICENSE.md). Copyright 2026 Hochschule Karlsruhe (HKA),
Institute of Materials and Processes (IMP).

## Herkunft

Entstanden im Projekt SMART-XR und herausgelöst, weil diese Schicht
unabhängig von Robotik nützlich ist. SMART-XR wurde gefördert durch
**Open Call 2** des MASTER-Projekts ([master-xr.eu](https://master-xr.eu/)),
das seine Mittel aus dem Programm Horizont Europa der Europäischen Union
unter der Fördervereinbarung Nr. 101093079 erhielt. Das Hauptprojekt liegt
unter [florian-reuter-imp/SMART-XR](https://github.com/florian-reuter-imp/SMART-XR).
