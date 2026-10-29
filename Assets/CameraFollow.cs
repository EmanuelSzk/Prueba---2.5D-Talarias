
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Posición de cámara")]
    public Vector3 offset = new Vector3(2f, 2f, -10f);

    [Header("Seguimiento")]
    public float smooth = 4f;
    public float lookAheadDistance = 2f;
    public float lookAheadSmooth = 3f;

    private float lookAhead;

    void LateUpdate()
    {
        if (target == null) return;

        // Detectar hacia dónde mira o se mueve el personaje
        float inputX = Input.GetAxisRaw("Horizontal");

        float targetLookAhead = inputX * lookAheadDistance;

        lookAhead = Mathf.Lerp(
            lookAhead,
            targetLookAhead,
            lookAheadSmooth * Time.deltaTime
        );

        // Posición objetivo con anticipación
        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x += lookAhead;

        // Seguimiento suave
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smooth * Time.deltaTime
        );
    }
}
