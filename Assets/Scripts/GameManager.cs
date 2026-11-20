using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// Gère une partie : démarrage (coupe du StartFruit), chrono, score, fin de partie et meilleur score.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    public FruitSpawnerManager spawner;
    public GameObject startFruitPrefab;
    public Transform startFruitSpawnPoint;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;

    [Header("Game settings")]
    public float gameDuration = 60f;
    [Tooltip("Points retirés quand une bombe est touchée.")]
    public int bombPenalty = 3;

    [Header("High Score UI")]
    public TextMeshProUGUI highScoreText;

    private const string HighScoreKey = "HighScore";

    private bool gameStarted = false;
    private int score = 0;
    private float timeRemaining;
    private Coroutine timerCoroutine;
    private GameObject currentStartFruit;

    // Bouton A (manette droite) : détection d'appui sur front montant
    private UnityEngine.XR.InputDevice rightHand;
    private bool previousPrimaryButton;

    private readonly WaitForSeconds oneSecond = new WaitForSeconds(1f);

    public bool IsGameRunning => gameStarted;

    private void Start()
    {
        if (startFruitPrefab == null)
        {
            startFruitPrefab = Resources.Load<GameObject>("StartFruit");
            if (startFruitPrefab == null)
                Debug.LogWarning("Impossible de trouver StartFruit dans Resources.");
        }

        if (startFruitSpawnPoint == null)
        {
            GameObject found = GameObject.Find("StartFruitSpawnPoint");
            if (found == null) found = GameObject.Find("StartFruitSpawn");
            if (found != null)
                startFruitSpawnPoint = found.transform;
        }

        if (spawner == null)
            spawner = FindAnyObjectByType<FruitSpawnerManager>();

        UpdateScoreUI();
        UpdateTimerUI();
        UpdateHighScoreUI();
    }

    private void Update()
    {
        // Respawn du StartFruit uniquement hors partie, sur un appui (et non à chaque frame).
        bool keyboardRespawn = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool controllerRespawn = PrimaryButtonPressedThisFrame();

        if (!gameStarted && (keyboardRespawn || controllerRespawn))
            RespawnStartFruit();
    }

    private bool PrimaryButtonPressedThisFrame()
    {
        if (!rightHand.isValid)
            rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool pressed = rightHand.isValid
                       && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool value)
                       && value;

        bool pressedThisFrame = pressed && !previousPrimaryButton;
        previousPrimaryButton = pressed;
        return pressedThisFrame;
    }

    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        score = 0;
        UpdateScoreUI();

        timeRemaining = gameDuration;
        UpdateTimerUI();

        if (spawner != null)
            spawner.StartSpawning();

        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        timerCoroutine = StartCoroutine(GameTimer());
    }

    private IEnumerator GameTimer()
    {
        while (gameStarted && timeRemaining > 0f)
        {
            yield return oneSecond;
            timeRemaining -= 1f;
            UpdateTimerUI();
        }

        timerCoroutine = null;

        if (gameStarted)
            EndGame();
    }

    public void EndGame()
    {
        if (!gameStarted) return;
        gameStarted = false;
        Debug.Log("Fin de partie : chrono terminé");

        if (spawner != null)
        {
            spawner.StopSpawning();
            spawner.ClearActiveObjects(includeBombs: true);
        }

        RespawnStartFruit();

        // Mise à jour du meilleur score
        int previousHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (score > previousHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
            Debug.Log("Nouveau meilleur score enregistré : " + score);
        }
        UpdateHighScoreUI();
    }

    public void RespawnStartFruit()
    {
        // Supprime l'ancien StartFruit (celui posé dans la scène au départ, ou le dernier instancié)
        if (currentStartFruit == null)
            currentStartFruit = GameObject.FindWithTag("StartButton");
        if (currentStartFruit != null)
            Destroy(currentStartFruit);

        if (startFruitPrefab == null)
        {
            startFruitPrefab = Resources.Load<GameObject>("StartFruit");
            if (startFruitPrefab == null)
            {
                Debug.LogError("Aucun StartFruit trouvé !");
                return;
            }
        }

        Vector3 spawnPos = startFruitSpawnPoint != null ? startFruitSpawnPoint.position : new Vector3(0f, 1.2f, 0f);
        Quaternion spawnRot = startFruitSpawnPoint != null ? startFruitSpawnPoint.rotation : Quaternion.identity;

        currentStartFruit = Instantiate(startFruitPrefab, spawnPos, spawnRot);
    }

    /// <summary>Appelé par un sabre quand un fruit (normal ou doré) est coupé.</summary>
    public void OnFruitSliced(GameObject fruit)
    {
        AddScore(spawner != null ? spawner.GetPointsFor(fruit) : 1);
    }

    /// <summary>Appelé par un sabre quand une bombe est touchée : pénalité + fruits en vol supprimés.</summary>
    public void OnBombHit()
    {
        AddScore(-bombPenalty);
        ResetFruitsOnBomb();
    }

    public void AddScore(int amount)
    {
        score = Mathf.Max(0, score + amount);
        UpdateScoreUI();
    }

    public void ResetFruitsOnBomb()
    {
        if (spawner != null)
            spawner.ClearActiveObjects(includeBombs: false);
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "Score : " + score;
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = "Temps : " + Mathf.CeilToInt(timeRemaining);
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = "Meilleur score : " + PlayerPrefs.GetInt(HighScoreKey, 0);
    }
}
