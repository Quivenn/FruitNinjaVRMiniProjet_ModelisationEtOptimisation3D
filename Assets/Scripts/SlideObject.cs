using System.Collections;
using System.Collections.Generic;
using EzySlice;
using UnityEngine;

/// <summary>
/// Sabre : détecte les objets traversés par la lame (Linecast entre deux points)
/// et les coupe (fruits), les fait exploser (bombes) ou démarre la partie (StartFruit).
/// </summary>
public class SlideObject : MonoBehaviour
{
    public Transform startSlicePoint;
    public Transform endSlicePoint;
    public LayerMask sliceableLayer;
    public Material crossSectionMaterial;
    public float cutForce = 2000f;

    [Tooltip("Estime la vitesse de la lame pour orienter le plan de coupe. Recherché automatiquement si vide.")]
    public VelocityEstimator velocityEstimator;

    public GameObject explosionVFX;
    public AudioClip fruitCutSound;
    public AudioClip bombExplosionSound;
    public GameObject fruitJuiceFXPrefab;

    public string bombTag = "Bomb";
    public string startButtonTag = "StartButton";
    public GameObject gameManager;

    [Header("Durées de vie")]
    [Tooltip("Durée de vie (s) des deux moitiés d'un fruit coupé.")]
    public float slicedHullLifetime = 3f;
    [Tooltip("Durée de vie par défaut (s) d'un effet si elle ne peut pas être déduite de ses Particle Systems.")]
    public float fxFallbackLifetime = 2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private AudioSource audioSource;
    private GameManager gm;

    // Objets déjà coupés récemment (évite de couper deux fois le même objet sur plusieurs pas physiques)
    private readonly HashSet<GameObject> recentlySliced = new HashSet<GameObject>();
    private const float SliceCooldown = 0.15f;
    private readonly WaitForSeconds sliceCooldownWait = new WaitForSeconds(SliceCooldown);

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (gameManager != null)
            gm = gameManager.GetComponent<GameManager>();
        if (gm == null)
            gm = FindAnyObjectByType<GameManager>();

        if (velocityEstimator == null)
            velocityEstimator = FindVelocityEstimator();
    }

    private VelocityEstimator FindVelocityEstimator()
    {
        VelocityEstimator ve = GetComponent<VelocityEstimator>();
        if (ve == null) ve = GetComponentInParent<VelocityEstimator>();
        if (ve == null) ve = GetComponentInChildren<VelocityEstimator>();
        if (ve == null && transform.parent != null) ve = transform.parent.GetComponentInChildren<VelocityEstimator>();
        if (ve == null && endSlicePoint != null) ve = endSlicePoint.GetComponentInParent<VelocityEstimator>();
        return ve;
    }

    private void FixedUpdate()
    {
        if (startSlicePoint == null || endSlicePoint == null) return;

        if (Physics.Linecast(startSlicePoint.position, endSlicePoint.position, out RaycastHit hit, sliceableLayer))
        {
            GameObject target = hit.collider.gameObject;
            if (!recentlySliced.Contains(target))
            {
                StartCoroutine(RegisterSliceCooldown(target));
                SliceObject(target, hit.point);
            }
        }
    }

    private IEnumerator RegisterSliceCooldown(GameObject obj)
    {
        recentlySliced.Add(obj);
        yield return sliceCooldownWait;
        recentlySliced.Remove(obj);
    }

    public void SliceObject(GameObject target)
    {
        if (target == null) return;
        SliceObject(target, target.transform.position);
    }

    public void SliceObject(GameObject target, Vector3 contactPoint)
    {
        if (target == null) return;

        // Fruit de démarrage
        if (target.CompareTag(startButtonTag))
        {
            if (gm != null)
                gm.StartGame();

            SpawnFX(explosionVFX, target.transform.position);
            PlaySound(fruitCutSound);
            Destroy(target);
            return;
        }

        // Bombe
        if (target.CompareTag(bombTag))
        {
            PlaySound(bombExplosionSound);
            SpawnFX(explosionVFX, target.transform.position);

            if (gm != null)
                gm.OnBombHit();

            Destroy(target);
            return;
        }

        // Fruit (normal ou doré) : plan de coupe orienté selon le mouvement de la lame
        Vector3 planeNormal = ComputeSlicePlaneNormal();
        SlicedHull hull = target.Slice(contactPoint, planeNormal, crossSectionMaterial);
        if (hull == null) return;

        GameObject upperHull = hull.CreateUpperHull(target, crossSectionMaterial);
        GameObject lowerHull = hull.CreateLowerHull(target, crossSectionMaterial);

        Rigidbody originalRb = target.GetComponent<Rigidbody>();
        Vector3 originalVelocity = originalRb ? originalRb.linearVelocity : Vector3.zero;
        Vector3 originalAngularVelocity = originalRb ? originalRb.angularVelocity : Vector3.zero;

        AddPhysicsToSlice(upperHull, originalVelocity, originalAngularVelocity);
        AddPhysicsToSlice(lowerHull, originalVelocity, originalAngularVelocity);

        PlaySound(fruitCutSound);

        // Effet de jus au point de contact, teinté avec la couleur du fruit
        GameObject fx = SpawnFX(fruitJuiceFXPrefab, contactPoint);
        if (fx != null)
        {
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                if (TryGetFruitColor(target, out Color fruitColor))
                {
                    var main = ps.main;
                    main.startColor = fruitColor;
                }
                ps.Play();
            }
        }

        if (gm != null)
            gm.OnFruitSliced(target);

        Destroy(target);
    }

    private Vector3 ComputeSlicePlaneNormal()
    {
        Vector3 bladeDirection = endSlicePoint.position - startSlicePoint.position;
        Vector3 bladeVelocity = velocityEstimator != null ? velocityEstimator.GetVelocityEstimate() : Vector3.zero;

        Vector3 normal = Vector3.Cross(bladeDirection, bladeVelocity);
        if (normal.sqrMagnitude < 1e-6f)
            return Vector3.up; // lame immobile ou estimateur absent : coupe horizontale

        return normal.normalized;
    }

    private static bool TryGetFruitColor(GameObject target, out Color color)
    {
        color = Color.white;
        Renderer rend = target.GetComponentInChildren<Renderer>();
        // sharedMaterial : ne crée pas de copie du matériau (rend.material en créait une à chaque coupe)
        Material mat = rend != null ? rend.sharedMaterial : null;
        if (mat == null) return false;

        if (mat.HasProperty(BaseColorId)) { color = mat.GetColor(BaseColorId); return true; }
        if (mat.HasProperty(ColorId)) { color = mat.GetColor(ColorId); return true; }
        return false;
    }

    private GameObject SpawnFX(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;

        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        Destroy(fx, GetFXLifetime(fx));
        return fx;
    }

    private float GetFXLifetime(GameObject fx)
    {
        float lifetime = 0f;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            if (main.loop) return fxFallbackLifetime;
            lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }
        return lifetime > 0f ? lifetime : fxFallbackLifetime;
    }

    private void AddPhysicsToSlice(GameObject slicedObject, Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity)
    {
        if (slicedObject == null) return;

        Rigidbody rb = slicedObject.AddComponent<Rigidbody>();
        slicedObject.AddComponent<SphereCollider>();

        rb.linearVelocity = inheritedVelocity;
        rb.angularVelocity = inheritedAngularVelocity;
        rb.AddExplosionForce(cutForce, slicedObject.transform.position, 5f);
        rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
        Destroy(slicedObject, slicedHullLifetime);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
