# Änderungen

Dieses Projekt folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.2.0] — 21.10.2026

### Die Brille bekommt Zeit, und ein Ereignis
`SimulatorBootstrap` wartete pauschal fünf Sekunden darauf, dass ein
Display-Subsystem läuft. Über Air Link reicht das regelmäßig nicht: Dann
sprang der Simulator an und übernahm die Kamera, obwohl die Brille unterwegs
war.

Der Fehler lag nicht in der Zahl, sondern darin, **ein** Signal für **drei**
Fragen zu benutzen:

| Signal | sagt |
|---|---|
| `Manager.isInitializationComplete` | Der Versuch ist vorbei. Nicht, ob er geglückt ist. |
| `Manager.activeLoader != null` | Eine XR-Laufzeit hat uns angenommen. Es **wird** eine Brille geben. |
| `XRDisplaySubsystem.running` | Es fließt ein Bild. Das späteste der drei. |
| `InputDevices.deviceConnected` | Ein echtes Ereignis: die Brille meldet sich an. |

Entscheidend ist das zweite: **Sobald ein Loader aktiv ist, ist ein Simulator
falsch**, egal wie lange das Bild noch braucht.

Jetzt zweistufig: Bild fließt oder Brille meldet sich → kein Simulator,
sofort. Initialisierung durch und kein Loader → Simulator sofort, wer am
Schreibtisch arbeitet wartet keine Sekunde. Loader aktiv, Bild fehlt noch →
warten bis zu `WartezeitMitLoader` (eine Minute, öffentlich und zur Laufzeit
setzbar).

Die Minute ist Absicht: Zu lang warten kostet nichts, denn es gibt ohnehin
keine Ansicht, auf die man warten müsste. Zu früh aufgeben kostet die
richtige Perspektive.

Verschwindet der Loader wieder, ist die Lage neu entschieden und der
Simulator startet. Läuft nach einer Minute eine Laufzeit ohne Bild, gibt es
eine Warnung mit dem wahrscheinlichen Grund statt stillschweigend die Kamera
zu übernehmen.

### Kinematik 0.6.0
Die deklarierte Abhängigkeit verweist jetzt auf Robot Kinematics 0.6.0,
den gemeinsam veröffentlichten und geprüften Stand.

## [0.1.0] — 20.10.2026

### Vor der Veroeffentlichung durchgesehen
Das Paket wurde daraufhin gelesen, was oeffentlich stehen darf. Zwei
Kommentare verwiesen auf `00-work-here`, einen Ordner im Entwicklungsprojekt,
den ein Fremder nie zu sehen bekommt; sie nennen jetzt neutral „das
Anwendungsprojekt". Sonst stand nichts darin, was nicht nach draussen soll:
keine Zugangsdaten, keine internen Adressen, keine Namen von Projektpartnern.

Die realvirtual-Nennungen in `DesktopNavigationGuard` bleiben. Sie sind
Kommentare und Reflexion ueber Typnamen, kein Verweis auf eine fremde
Assembly, und genau das ist dort auch dokumentiert.

### Drei zu niedrig angegebene Abhaengigkeiten
Ein Probelauf in einem leeren Projekt brach mit CS0234 und CS0246 ab:
`XRInteractionSimulator` und der Namensraum
`UnityEngine.XR.Interaction.Toolkit.Jump` fehlten. Die `package.json` nannte
XRI **3.0.0**, benutzt wird aber **3.5.1**. Dasselbe bei Input System
(1.7.0 angegeben, 1.19.0 benutzt) und XR Management (4.4.0 angegeben, 4.5.4
benutzt). Alle drei Zahlen waren geraten und nie geprueft.

Im Entwicklungsprojekt faellt das nie auf, weil dort ohnehin die neueren
Fassungen liegen. Ein Fremder mit den angegebenen Fassungen haette das Paket
nicht uebersetzen koennen.

Die Angaben stehen jetzt auf dem, was gemessen ist. Aeltere Fassungen mogen
reichen; das ist nicht geprueft, und darum wird es nicht behauptet.

### Einbaureihenfolge
Dieses Paket fuehrt `com.github.hka-imp.robotkinematics` in seinen
`dependencies`. Unity loest Versionsangaben gegen eine Registry auf, und
dieses Paket steht in keiner; transitive Git-Abhaengigkeiten verfolgt der
Package Manager nicht. Deshalb zuerst robotkinematics einbauen, dann dieses.
Die README sagt es an der Stelle, an der es gebraucht wird.

### Unity
`"unity"` steht auf `6000.0` statt `2022.3`. Die alte Angabe war nie
geprueft.


Erste Fassung. Herausgelöst aus der SMART-XR-Anwendung, wo diese Schicht
unter `Assets/SmartXR/Input` und `Assets/SmartXR/Interaction` lag.

### Warum als Paket
Ein Modul, das bedienbar sein will, musste bisher auf Projektcode verweisen
statt auf ein Paket. Damit ließ sich der bedienbare Teil eines Moduls nicht
über `versionDefines` abschalten — er brach beim Kopieren in ein fremdes
Projekt, statt sauber zu verschwinden. Ein Paketname behebt das, und weil
eingebettete Pakete für `versionDefines` genauso zählen wie geladene, kostet
es kein zweites Repository.

Assembly-Namen und Namensräume sind unverändert (`SmartXR.Input`,
`SmartXR.Interaction`). Der Umzug ändert für Aufrufer nichts.

### Inhalt
**`SmartXR.Input`** — kennt keine Robotik:
- `InputContext`: Eingabekarten auf einem Stapel. Nur die oberste ist aktiv,
  die Beschriftung der obersten steht in der Übersicht. `Action` gibt null
  zurück statt zu werfen, wenn eine Aktion fehlt.
- `FeatureRegistry`: ein- und ausschaltbare Features melden Beschriftung,
  Zustand und Umschaltfunktion an — in der Brille erscheinen sie auf der
  Tafel an der linken Hand, am Schreibtisch auf F1 bis F7.
- `OperatingMode`: trennt Gehen von Bedienen, damit die Sticks nicht beides
  gleichzeitig tun.
- `XrPresence` und `SimulatorBootstrap`: erkennen, ob eine Brille da ist, und
  **warten darauf**, höchstens fünf Sekunden.
- `DesktopNavigationGuard`: schaltet fremde Schreibtischnavigation ab, über
  Typnamen und ohne das fremde Paket zu referenzieren.
- `InputOverlay`: zeigt die geltende Belegung.

**`SmartXR.Interaction`** — verbindet Roboter mit XR:
- `FeatureFocus`: verteilt Eingaben an genau ein Feature. Entsteht beim
  ersten Anmelden von selbst, damit ein Modulprefab kein zweites Bauteil in
  der Szene braucht.
- `IControllableFeature` und `ControllableFeature`: das Grundgerüst für ein
  bedienbares Feature.
- `RobotJog`, `TcpPreview`, `TcpTargetGrab`: Greifen und Joggen am
  Werkzeugpunkt.

### Was beim Herauslösen entfallen ist
`RobotFeature`, eine Aufzählung mit sieben festen Werten, aus der sich jedes
Feature eine Nummer nahm. Damit konnte niemand ein Feature mitbringen, ohne
diesen Kern zu erweitern — und wer ein Paket einbindet, kann es nicht ändern.
Ein Feature sagt jetzt selbst, ob es eingeschaltet ist.

Außerdem fiel die Suche nach Features über
`FindObjectsOfType<MonoBehaviour>()` weg: einmal durch die ganze Szene, über
jedes Bauteil, und nur beim Start — Features, die später entstehen, fand sie
nie. Jetzt melden sie sich an.
