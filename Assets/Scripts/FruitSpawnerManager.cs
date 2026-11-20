using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Lance des fruits, des fruits dorés (bonus) et des bombes depuis les points de spawn.
/// Garde la liste des objets lancés pour pouvoir les nettoyer sans parcourir toute la scène.
/// </summary>
public class FruitSpawnerManager : MonoBehaviour
{
    [Header("Spawners")]
    public Transform[] spawners;

    [Header("Objets à lancer")]
    public GameObject[] fruitPrefabs;
    public GameObject bombPrefab;
    public GameObject goldenFruitPrefab;

    [Header("Paramètres de spawn")]
    [Range(0f, 1f)]
    public float bombSpawnChance = 0.15f;
    [Range(0f, 1f)]
    public float goldenFruitChance = 0.10f;
    public float spawnInterval = 1.5f;
    public float launchForce = 4f;
    public float minRandomDelay = 0.3f;
    [Tooltip("Durée de vie (s) d'un objet lancé avant sa destruction automatique.")]
    public float objectLifetime = 1.5f;

    [Header("Score")]
    public int fruitPoints = 1;
    public int goldenFruitPoints = 5;

    // Objets actuellement en vol (fruits, fruits dorés et bombes)
    private readonly List<GameObject> activeObjects = new List<GameObject>();
    private readonly HashSet<GameObject> goldenObjects = new HashSet<GameObject>();
    private readonly HashSet<GameObject> bombObjects = new HashSet<GameObject>();

    private static readonly Predicate<GameObject> IsDestroyed = o => o == null;

    private Coroutine spawnRoutine;
    private readonly WaitForSeconds groupDelay = new WaitForSeconds(0.2f);

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval + Random.Range(-minRandomDelay, minRandomDelay));

            // 1 à 3 objets par vague
            int objectsToSpawn = Random.Range(1, 4);
            for (int i = 0; i < objectsToSpawn; i++)
            {
                SpawnRandomObject();
                yield return groupDelay;
            }
        }
    }

    private void SpawnRandomObject()
    {
        if (spawners == null || spawners.Length == 0) return;

        Transform spawner = spawners[Random.Range(0, spawners.Length)];
        if (spawner == null) return;

        // Un seul tirage : [0, bomb[ = bombe, [bomb, bomb+golden[ = fruit doré, sinon fruit normal.
        // (Avant, le choix de la bombe était toujours écrasé : aucune bombe n'apparaissait.)
        GameObject prefabToSpawn = null;
        bool isBomb = false;
        bool isGolden = false;
        float roll = Random.value;

        if (bombPrefab != null && roll < bombSpawnChance)
        {
            prefabToSpawn = bombPrefab;
            isBomb = true;
        }
        else if (goldenFruitPrefab != null && roll < bombSpawnChance + goldenFruitChance)
        {
            prefabToSpawn = goldenFruitPrefab;
            isGolden = true;
        }
        else if (fruitPrefabs != null && fruitPrefabs.Length > 0)
        {
            prefabToSpawn = fruitPrefabs[Random.Range(0, fruitPrefabs.Length)];
        }

        if (prefabToSpawn == null) return;

        Vector3 spawnPos = spawner.position;
        spawnPos.y += 0.5f;

        GameObject obj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        Destroy(obj, objectLifetime);

        PruneDestroyed();
        activeObjects.Add(obj);
        if (isBomb) bombObjects.Add(obj);
        if (isGolden) goldenObjects.Add(obj);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Direction verticale avec très légère déviation
            Vector3 launchDir = Vector3.up + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
            rb.AddForce(launchDir.normalized * launchForce, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
        }
    }

    /// <summary>Points rapportés par la coupe de cet objet (fruit doré = bonus).</summary>
    public int GetPointsFor(GameObject fruit)
    {
        return fruit != null && goldenObjects.Contains(fruit) ? goldenFruitPoints : fruitPoints;
    }

    /// <summary>Détruit les objets en vol. Les bombes ne sont détruites que si includeBombs est vrai.</summary>
    public void ClearActiveObjects(bool includeBombs)
    {
        for (int i = activeObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = activeObjects[i];
            if (obj == null)
            {
                activeObjects.RemoveAt(i);
                continue;
            }

            if (!includeBombs && bombObjects.Contains(obj))
                continue;

            goldenObjects.Remove(obj);
            bombObjects.Remove(obj);
            activeObjects.RemoveAt(i);
            Destroy(obj);
        }
    }

    private void PruneDestroyed()
    {
        activeObjects.RemoveAll(IsDestroyed);
        goldenObjects.RemoveWhere(IsDestroyed);
        bombObjects.RemoveWhere(IsDestroyed);
    }

    public void StartSpawning()
    {
        if (spawnRoutine == null)
            spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
}
