using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;                                  // el personaje (objeto Player)
    public Vector3 offset = new Vector3(0f, 2f, -10f);        // distancia respecto del personaje
    public float smooth = 5f;                                 // suavidad del seguimiento

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
    }
}
