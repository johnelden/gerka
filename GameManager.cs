using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool IsGameOver { get; private set; }

    public ArenaSystem arenaSystem;
    public BattleSystem battleSystem;
    public UISystem uiSystem;
    public AudioSystem audioSystem;

    public float matchDuration = 180f;
    public float enemySpawnInterval = 8f;

    float matchTime;
    float enemySpawnTimer;
    Camera gameCamera;
    bool birdEyeCamera;
    Vector3 cameraTargetPosition;
    Quaternion cameraTargetRotation;
    float cameraTargetFieldOfView;

    public bool IsBirdEyeCamera { get { return birdEyeCamera; } }

    void Start()
    {
        IsGameOver = false;
        matchTime = matchDuration;
        enemySpawnTimer = 4f;

        SetupLighting();
        SetupCamera();

        arenaSystem = gameObject.AddComponent<ArenaSystem>();
        arenaSystem.BuildArena();

        battleSystem = gameObject.AddComponent<BattleSystem>();
        battleSystem.Initialize(arenaSystem, this);

        audioSystem = gameObject.AddComponent<AudioSystem>();
        audioSystem.Initialize();

        uiSystem = gameObject.AddComponent<UISystem>();
        uiSystem.Initialize(this, battleSystem);
    }

    void Update()
    {
        UpdateCameraView();
        if (IsGameOver) return;

        matchTime -= Time.deltaTime;
        enemySpawnTimer -= Time.deltaTime;

        if (enemySpawnTimer <= 0f)
        {
            battleSystem.SpawnEnemyWave();
            enemySpawnTimer = enemySpawnInterval;
        }

        if (matchTime <= 0f)
        {
            EndGame(false, "Koniec czasu!");
            return;
        }

        if (uiSystem != null)
            uiSystem.HandleArenaClick();
    }

    void SetupCamera()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            GameObject obj = new GameObject("Main Camera");
            obj.tag = "MainCamera";
            cam = obj.AddComponent<Camera>();
            obj.AddComponent<AudioListener>();
        }

        cam.orthographic = false;
        cam.fieldOfView = 45f;
        cam.transform.position = new Vector3(0f, 23f, -18.5f);
        cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.49f, 0.59f);
        cam.allowHDR = true;
        cam.allowMSAA = true;
        gameCamera = cam;
        cameraTargetPosition = cam.transform.position;
        cameraTargetRotation = cam.transform.rotation;
        cameraTargetFieldOfView = cam.fieldOfView;
    }

    public void ToggleCameraView()
    {
        birdEyeCamera = !birdEyeCamera;
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null)
        {
            Debug.LogError("GameManager: nie znaleziono kamery do zmiany widoku.");
            birdEyeCamera = !birdEyeCamera;
            return;
        }

        if (birdEyeCamera)
        {
            cameraTargetPosition = new Vector3(0f, 39f, 0f);
            cameraTargetRotation = Quaternion.Euler(90f, 0f, 0f);
            cameraTargetFieldOfView = 52f;
        }
        else
        {
            cameraTargetPosition = new Vector3(0f, 23f, -18.5f);
            cameraTargetRotation = Quaternion.Euler(50f, 0f, 0f);
            cameraTargetFieldOfView = 45f;
        }
    }

    void UpdateCameraView()
    {
        if (gameCamera == null) return;
        float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, cameraTargetPosition, blend);
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation, cameraTargetRotation, blend);
        gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, cameraTargetFieldOfView, blend);
    }

    void SetupLighting()
    {
        RenderSettings.ambientLight = new Color(0.73f, 0.75f, 0.72f);
        RenderSettings.ambientMode =
            UnityEngine.Rendering.AmbientMode.Flat;

        Light sun = null;

        foreach (Light light in FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                sun = light;
                break;
            }
        }

        if (sun == null)
        {
            GameObject obj = new GameObject("Arena Sun");
            sun = obj.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.name = "Arena Sun";
        sun.color = new Color(1f, 0.92f, 0.78f);
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.82f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
    }

    public string GetTimeText()
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(matchTime));
        return (total / 60).ToString("00") + ":" +
               (total % 60).ToString("00");
    }

    public void OnTowerDestroyed(bool isPlayerTower, bool isKing)
    {
        if (!isKing) return;

        if (isPlayerTower)
            EndGame(false, "Twoja wieza krola zostala zniszczona!");
        else
            EndGame(true, "Zniszczyles wieze krola przeciwnika!");
    }

    void EndGame(bool won, string reason)
    {
        if (IsGameOver) return;

        IsGameOver = true;

        if (uiSystem != null)
            uiSystem.ShowGameOver(won, reason);

        if (won && audioSystem != null)
            audioSystem.PlayVictory();

        Debug.Log((won ? "ZWYCIESTWO: " : "KONIEC GRY: ") + reason);
    }

    public void PlayDeploySound()
    {
        if (audioSystem != null)
            audioSystem.PlayDeploy();
    }

    public void PlayHitSound()
    {
        if (audioSystem != null)
            audioSystem.PlayHit();
    }
}