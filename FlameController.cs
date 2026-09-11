using UnityEngine;                 // Basis-Funktionen von Unity (Mathf, Color, ...)
using UnityEngine.InputSystem;     // neues Input System (Tastatur-Abfrage)
using TMPro;                       // TextMeshPro (Textanzeige)

// Dieses Skript gehört auf das Feuer-Objekt.
// Es erzeugt einen simulierten Puls und steuert damit Flamme, Licht, Ton und Text.
public class FlameController : MonoBehaviour
{
    // --- Diese Felder erscheinen im Inspector und werden dort per Drag & Drop verbunden ---
    public ParticleSystem[] flameSystems;   // nur die Kern-Flammen: flames + flames secondary
    public float minBpm = 50f;               // unteres Ende des Puls-Bereichs
    public float maxBpm = 120f;              // oberes Ende des Puls-Bereichs

    [Header("Automatische Simulation")]
    public float calmMin = 55f;              // Zufalls-Untergrenze im Ruhemodus
    public float calmMax = 75f;              // Zufalls-Obergrenze im Ruhemodus
    public float newTargetEvery = 3f;        // alle X Sekunden ein neues Puls-Ziel
    public float smoothing = 0.5f;           // wie weich der Puls dem Ziel folgt

    [Header("Tastatur (Pfeil hoch/runter)")]
    public float keySpeed = 40f;             // wie stark die Pfeiltasten den Puls ziehen

    [Header("Anzeige")]
    public TMP_Text bpmText;                 // Textfeld für die bpm-Zahl

    [Header("Farbe (ruhig -> Stress)")]
    public Color calmColor = new Color(1f, 0.75f, 0.2f);   // warmes Gelb (ruhig)
    public Color stressColor = new Color(1f, 0.2f, 0.1f);  // Rot (Stress)

    [Header("Licht")]
    public Light flameLight;                 // Point Light, das die Umgebung beleuchtet
    public float lightMin = 3f;              // Helligkeit bei Ruhe
    public float lightMax = 8f;              // Helligkeit bei Stress

    [Header("Herzschlag-Sound")]
    public AudioSource heartbeat;            // AudioSource mit dem Herzschlag-Clip

    [Header("Atem-Anleitung")]
    public TMP_Text breathText;              // Textfeld für "Einatmen/Ausatmen"
    public float breathOn = 95f;             // ab diesem Puls erscheint der Hinweis
    public float breathOff = 85f;            // erst darunter verschwindet er wieder
    public float breathCycle = 8f;           // Dauer eines Atemzugs in Sekunden
    public float fadeSpeed = 2f;             // Tempo des Ein-/Ausblendens

    // --- Interne Variablen (nur im Skript, nicht im Inspector) ---
    private float bpm = 70f;                 // aktueller (angezeigter) Puls
    private float targetBpm = 70f;           // Ziel, auf das sich der Puls zubewegt
    private float timer = 0f;                // zählt bis zum nächsten Puls-Ziel
    private float beatTimer = 0f;            // zählt bis zum nächsten Herzschlag-Ton

    private bool breathingActive = false;    // ist der Atem-Hinweis gerade an?
    private float breathAlpha = 0f;          // aktuelle Sichtbarkeit (0 = unsichtbar, 1 = sichtbar)

    // Ausgangswerte des Assets, damit wir sie nicht zerstören, sondern nur multiplizieren
    private float[] baseRate;
    private float[] baseSize;

    // Start() läuft einmal am Anfang.
    void Start()
    {
        // Für jedes Flammen-System die Original-Werte merken.
        baseRate = new float[flameSystems.Length];
        baseSize = new float[flameSystems.Length];
        for (int i = 0; i < flameSystems.Length; i++)
        {
            baseRate[i] = flameSystems[i].emission.rateOverTimeMultiplier;
            baseSize[i] = flameSystems[i].main.startSizeMultiplier;
        }
    }

    // Update() läuft jedes Bild (viele Male pro Sekunde).
    void Update()
    {
        HandleKeyboard();     // Tastatur auswerten
        HandleAutomatic();    // automatische Puls-Schwankung

        // Puls weich Richtung Ziel bewegen und im gültigen Bereich halten.
        bpm = Mathf.Lerp(bpm, targetBpm, Time.deltaTime * smoothing);
        bpm = Mathf.Clamp(bpm, minBpm, maxBpm);

        // bpm-Zahl anzeigen (auf ganze Zahl gerundet).
        if (bpmText != null)
            bpmText.text = Mathf.RoundToInt(bpm) + " bpm";

        ApplyToFlame(bpm);       // Flamme, Farbe, Licht setzen
        HandleHeartbeat(bpm);    // Herzschlag-Ton im Takt
        HandleBreathing(bpm);    // Atem-Hinweis
    }

    // Pfeiltasten: Puls-Ziel hoch/runter ziehen.
    void HandleKeyboard()
    {
        if (Keyboard.current == null) return;   // Sicherheitscheck

        bool used = false;
        if (Keyboard.current.upArrowKey.isPressed)     // Pfeil hoch = Stress
        {
            targetBpm += keySpeed * Time.deltaTime;
            used = true;
        }
        if (Keyboard.current.downArrowKey.isPressed)   // Pfeil runter = beruhigen
        {
            targetBpm -= keySpeed * Time.deltaTime;
            used = true;
        }

        if (used)
        {
            targetBpm = Mathf.Clamp(targetBpm, minBpm, maxBpm);
            timer = 0f;   // Automatik-Timer zurücksetzen, damit sie nicht dagegenarbeitet
        }
    }

    // Automatik: sucht sich alle paar Sekunden ein neues, zufälliges Ruhe-Ziel.
    void HandleAutomatic()
    {
        timer += Time.deltaTime;
        if (timer >= newTargetEvery)
        {
            targetBpm = Random.Range(calmMin, calmMax);
            timer = 0f;
        }
    }

    // Wandelt den Puls in Optik um: Größe, Menge, Flackern, Farbe, Licht.
    void ApplyToFlame(float currentBpm)
    {
        // t = Anteil zwischen 0 (ruhig) und 1 (Stress).
        float t = Mathf.InverseLerp(minBpm, maxBpm, currentBpm);

        // Flacker-Wert: eine schwingende Zahl um 1 herum, schneller bei Stress.
        float flickerSpeed = Mathf.Lerp(3f, 15f, t);
        float flicker = 1f + Mathf.Sin(Time.time * flickerSpeed) * Mathf.Lerp(0.05f, 0.2f, t);

        // Faktoren für Menge und Größe (bei Ruhe ~ Original, bei Stress deutlich mehr).
        float rateFactor = Mathf.Lerp(1.0f, 4.5f, t) * flicker;
        float sizeFactor = Mathf.Lerp(0.7f, 2.4f, t) * flicker;

        // Farbe zwischen Gelb und Rot mischen.
        Color currentColor = Color.Lerp(calmColor, stressColor, t);

        // Auf jedes Flammen-System anwenden: Original-Wert MAL unser Faktor.
        for (int i = 0; i < flameSystems.Length; i++)
        {
            var main = flameSystems[i].main;
            var emission = flameSystems[i].emission;
            emission.rateOverTimeMultiplier = baseRate[i] * rateFactor;
            main.startSizeMultiplier = baseSize[i] * sizeFactor;
            main.startColor = currentColor;
        }

        // Licht mitatmen lassen.
        if (flameLight != null)
            flameLight.intensity = Mathf.Lerp(lightMin, lightMax, t) * flicker;
    }

    // Spielt den Herzschlag im Takt des aktuellen Pulses.
    void HandleHeartbeat(float currentBpm)
    {
        if (heartbeat == null) return;

        // 60 / bpm = Sekunden zwischen zwei Schlägen (z. B. 60 bpm -> 1 Sekunde).
        float interval = 60f / currentBpm;

        beatTimer += Time.deltaTime;
        if (beatTimer >= interval)
        {
            heartbeat.Play();
            beatTimer = 0f;
        }
    }

    // Blendet den Atem-Hinweis bei hohem Puls ein und im Ruhezustand aus.
    void HandleBreathing(float currentBpm)
    {
        if (breathText == null) return;

        // Pufferzone: an ab breathOn, aus erst unter breathOff (kein Flackern an der Grenze).
        if (currentBpm >= breathOn) breathingActive = true;
        else if (currentBpm <= breathOff) breathingActive = false;

        // Sichtbarkeit weich Richtung Ziel bewegen (1 = sichtbar, 0 = unsichtbar).
        float targetAlpha = breathingActive ? 1f : 0f;
        breathAlpha = Mathf.MoveTowards(breathAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // Text im Atemrhythmus umschalten: erste Hälfte einatmen, zweite ausatmen.
        float phase = (Time.time % breathCycle) / breathCycle;
        breathText.text = (phase < 0.5f) ? "Einatmen…" : "Ausatmen…";

        // Transparenz setzen -> sanftes Ein-/Ausblenden.
        Color col = breathText.color;
        col.a = breathAlpha;
        breathText.color = col;
    }
}
