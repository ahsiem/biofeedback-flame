using UnityEngine;                 // Basis-Funktionen von Unity
using UnityEngine.InputSystem;     // neues Input System (Maus-Abfrage)

// Dieses Skript gehört auf die Main Camera.
// Es dreht die Kamera mit der Maus über das neue Input System.
public class CameraLook : MonoBehaviour
{
    public float sensitivity = 0.1f;   // Empfindlichkeit (kleiner als beim alten System!)

    private float rotX = 0f;           // Drehung links/rechts
    private float rotY = 0f;           // Drehung oben/unten

    void Update()
    {
        if (Mouse.current == null) return;   // Sicherheitscheck: gibt es eine Maus?

        // Mausbewegung dieses Bildes auslesen (delta = Veränderung seit letztem Bild).
        Vector2 delta = Mouse.current.delta.ReadValue();

        rotX += delta.x * sensitivity;       // horizontale Bewegung
        rotY -= delta.y * sensitivity;       // vertikale Bewegung (minus = natürliche Richtung)

        // Nach oben/unten begrenzen, damit sich die Kamera nicht überschlägt.
        rotY = Mathf.Clamp(rotY, -80f, 80f);

        // Drehung auf die Kamera anwenden.
        transform.localRotation = Quaternion.Euler(rotY, rotX, 0f);
    }
}
