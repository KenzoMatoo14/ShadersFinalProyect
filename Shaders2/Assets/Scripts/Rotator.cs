using UnityEngine;

// Gira el objeto constantemente sobre un eje local.
public class Rotator : MonoBehaviour
{
    [Tooltip("Eje local de giro.")]
    public Vector3 axis = Vector3.forward;
    [Tooltip("Grados por segundo.")]
    public float speed = 15f;

    void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}
