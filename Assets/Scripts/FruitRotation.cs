using UnityEngine;

/// <summary>
/// Fait tourner un fruit : couple aléatoire initial s'il a un Rigidbody, sinon rotation manuelle.
/// </summary>
public class FruitRotation : MonoBehaviour
{
    [Header("Vitesse de rotation")]
    public float rotationSpeed = 180f; // degrés/seconde

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // La physique gère la rotation : un couple aléatoire suffit, Update devient inutile.
            Vector3 randomTorque = Random.insideUnitSphere * (rotationSpeed * 0.1f);
            rb.AddTorque(randomTorque, ForceMode.VelocityChange);
            enabled = false;
        }
    }

    private void Update()
    {
        // Pas de Rigidbody : rotation manuelle
        Vector3 randomAxis = new Vector3(
            Mathf.Sin(Time.time * 1.3f),
            1f,
            Mathf.Cos(Time.time * 0.8f)
        ).normalized;

        transform.Rotate(randomAxis * rotationSpeed * Time.deltaTime, Space.Self);
    }
}
