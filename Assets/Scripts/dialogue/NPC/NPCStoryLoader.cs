using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using System.Collections;

public class NPCStoryLoader : MonoBehaviour
{
    [SerializeField] private Transform persistentRootTransform;

    [Header("Refs")]
    [SerializeField] private dialog story;
    [SerializeField] private GameObject npcPrefab;

    [Header("Scene Names")]
    [SerializeField] private string outdoorSceneName = "OutdoorScene";
    [SerializeField] private string ossuarySceneName = "OssuaryIndoorScene";

    [Header("Marker Names")]
    [SerializeField] private string outdoorLocation1Name = "NPC_Location1";
    [SerializeField] private string outdoorLocation2Name = "NPC_Location2";
    [SerializeField] private string ossuaryLocation3Name = "NPC_Location3";
    [SerializeField] private string ossuaryLocation4Name = "NPC_Location4";
    [SerializeField] private string location5Name = "NPC_Location5";
    [SerializeField] private string location6Name = "NPC_Location6";

    [Header("Story Flags")]
    [SerializeField] private string outdoorMoveFlag = "npc_move_outdoor";
    [SerializeField] private string ossuaryMoveFlag = "npc_move_ossuary";

    [Header("12_F1_Main Chase Settings")]
    [SerializeField] private string chaseSceneName = "12_F1_Main";
    [SerializeField] private string chaseStartFlag = "npc_chase_started";

    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private float gameOverDuration = 5f;

    [SerializeField] private float detectRadius = 18f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;

    [SerializeField] private string flashlightItemId = "Flashlight";

    [SerializeField] private AudioClip npcDoorSound;
    [SerializeField] private AudioSource npcAudioSource;

    [SerializeField] private string doorNearLocation6Name = "DoorStair1";

    private enum ChaseState
    {
        Idle,
        MovingToLocation6,
        ChasingPlayer,
        WaitingAtLockedDoor
    }

    private ChaseState chaseState = ChaseState.Idle;

    private bool chaseModeStarted = false;
    private bool reachedLocation6 = false;
    private Transform playerTransform;

    private GameObject npcInstance;
    private NavMeshAgent agent;

    private Animator animator;

    private bool outdoorMoveStarted = false;
    private bool outdoorMoveFinished = false;

    private bool ossuaryMoveStarted = false;
    private bool ossuaryMoveFinished = false;

    private bool outdoorScenePhaseEnded = false; // outdoor 복귀 시 다시 안 나오게
    private bool initialized = false;       //line 138에서 쓰고 있음(Warning 무시)

    private bool gameOverRunning = false;

    private void Awake()
    {
        if (story == null)
            story = FindFirstObjectByType<dialog>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (npcInstance == null || agent == null) return;

        // animation
        if (animator != null)
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }

        if (story == null)
            story = FindFirstObjectByType<dialog>();

        if (story == null || npcInstance == null)
            return;

        string currentScene = SceneManager.GetActiveScene().name;

        // Move NPC in Outdoor after S00_N4 and hide after arrival
        if (!outdoorMoveStarted && !outdoorMoveFinished && story.IsFlagTrue(outdoorMoveFlag))
        {
            if (currentScene == outdoorSceneName)
            {
                Transform loc2 = FindMarker(outdoorLocation2Name);
                if (loc2 != null)
                {
                    outdoorMoveStarted = true;
                    StartCoroutine(MoveNpcAndHide(loc2, () =>
                    {
                        outdoorMoveFinished = true;
                        outdoorScenePhaseEnded = true;
                    }));
                }
            }
        }

        // move NPC in Ossuary after S01_N5 and hide after arrival
        if (!ossuaryMoveStarted && !ossuaryMoveFinished && story.IsFlagTrue(ossuaryMoveFlag))
        {
            if (currentScene == ossuarySceneName)
            {
                Transform loc4 = FindMarker(ossuaryLocation4Name);
                if (loc4 != null)
                {
                    ossuaryMoveStarted = true;
                    StartCoroutine(MoveNpcAndHide(loc4, () =>
                    {
                        ossuaryMoveFinished = true;
                    }));
                }
            }
        }

        if (currentScene == chaseSceneName)
        {
            UpdateChaseLogic();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (story == null)
            story = FindFirstObjectByType<dialog>();

        CreateNpcIfNeeded();

        // Enter Outdoor Scene at first, show NPC at location1
        if (scene.name == outdoorSceneName)
        {
            // Don't show NPC if outdoor phase already ended or moved once (복귀 시 안 보이게)
            if (outdoorScenePhaseEnded || outdoorMoveFinished)
            {
                HideNpcImmediate();
                return;
            }

            Transform loc1 = FindMarker(outdoorLocation1Name);
            if (loc1 != null)
            {
                if (loc1 != null)
                {
                    ShowNpc();
                    PlaceNpcAt(loc1);
                }
            }
            else
            {
                Debug.LogWarning("[NPCstoryloader] Outdoor location1 not found.");
                HideNpcImmediate();
            }
        }
        // Enter Ossuary Scene, show NPC at location3 if not moved yet
        else if (scene.name == ossuarySceneName)
        {
            // Don't show NPC if ossuary phase already ended
            if (ossuaryMoveFinished)
            {
                HideNpcImmediate();
                return;
            }

            Transform loc3 = FindMarker(ossuaryLocation3Name);
            if (loc3 != null)
            {
                if (loc3 != null)
                {
                    ShowNpc();
                    PlaceNpcAt(loc3);
                }
            }
            else
            {
                Debug.LogWarning("[NPCstoryloader] Ossuary location3 not found.");
                HideNpcImmediate();
            }
        }
        else if (scene.name == chaseSceneName)
        {
            Transform loc5 = FindMarker(location5Name);

            if (loc5 != null)
            {
                CreateNpcIfNeeded();
                ShowNpc();
                PlaceNpcAt(loc5);

                if (agent != null)
                {
                    agent.isStopped = false;
                    agent.ResetPath();
                }

                chaseModeStarted = false;
                reachedLocation6 = false;

                BindPlayer();
            }
            else
            {
                Debug.LogWarning("[NPCStoryLoader] NPC_Location5 not found.");
                HideNpcImmediate();
            }
        }
        else
        {
            // Hide NPC in any other scene
            HideNpcImmediate();
        }
    }

    private void CreateNpcIfNeeded()
    {
        if (npcInstance != null) return;

        npcInstance = Instantiate(npcPrefab, persistentRootTransform);
        npcInstance.name = npcPrefab.name + "_PersistentNPC";

        agent = npcInstance.GetComponent<NavMeshAgent>();
        animator = npcInstance.GetComponent<Animator>();

        if (agent == null)
            Debug.LogError("[NPCstoryloader] Need NavMeshAgent");

        if (animator == null)
            Debug.LogWarning("[NPCstoryloader] No Animator (animations will not play)");

        NPCGameOverHitbox hitbox = npcInstance.GetComponent<NPCGameOverHitbox>();
        if (hitbox == null)
            hitbox = npcInstance.AddComponent<NPCGameOverHitbox>();

        hitbox.Init(this);
    }

    private Transform FindMarker(string markerName)
    {
        GameObject go = GameObject.Find(markerName);
        return go != null ? go.transform : null;
    }

    private bool PlaceNpcAt(Transform marker)
    {
        if (npcInstance == null || marker == null || agent == null)
            return false;

        NavMeshHit hit;
        bool found = NavMesh.SamplePosition(marker.position, out hit, 2.0f, NavMesh.AllAreas);

        if (!found)
        {
            Debug.LogWarning("[NPCstoryloader] No NavMesh found near marker: " + marker.name);
            npcInstance.transform.SetPositionAndRotation(marker.position, marker.rotation);
            return false;
        }

        if (!agent.enabled)
            agent.enabled = true;

        bool warped = agent.Warp(hit.position);
        npcInstance.transform.rotation = marker.rotation;

        if (!warped)
        {
            Debug.LogWarning("[NPCstoryloader] Warp failed at marker: " + marker.name);
            return false;
        }

        Debug.Log("[NPCstoryloader] NPC placed on NavMesh at: " + hit.position);
        return true;
    }

    private void ShowNpc()
    {
        if (npcInstance != null && !npcInstance.activeSelf)
            npcInstance.SetActive(true);
    }

    private void HideNpcImmediate()
    {
        if (npcInstance != null && npcInstance.activeSelf)
            npcInstance.SetActive(false);
    }

    private IEnumerator MoveNpcAndHide(Transform target, System.Action onArrived)
    {
        if (npcInstance == null || target == null || agent == null)
            yield break;

        ShowNpc();

        if (!agent.enabled)
            agent.enabled = true;

        // target도 NavMesh 위 점으로 보정
        NavMeshHit targetHit;
        bool found = NavMesh.SamplePosition(target.position, out targetHit, 2.0f, NavMesh.AllAreas);

        if (!found)
        {
            Debug.LogWarning("[NPCstoryloader] Target is not near NavMesh: " + target.name);
            yield break;
        }

        // agent가 현재 NavMesh 위에 없으면 현재 위치도 보정
        if (!agent.isOnNavMesh)
        {
            NavMeshHit currentHit;
            bool currentFound = NavMesh.SamplePosition(npcInstance.transform.position, out currentHit, 2.0f, NavMesh.AllAreas);

            if (currentFound)
            {
                agent.Warp(currentHit.position);
            }
            else
            {
                Debug.LogError("[NPCstoryloader] NPC is not on NavMesh and could not be corrected.");
                yield break;
            }
        }

        agent.isStopped = false;
        agent.SetDestination(targetHit.position);

        while (true)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
                yield break;

            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
                        break;
                }
            }

            yield return null;
        }

        agent.ResetPath();
        HideNpcImmediate();
        onArrived?.Invoke();
    }

    // 12_F1_Main에서 플레이어 추격 로직 업데이트
    private void UpdateChaseLogic()
    {
        if (story == null || npcInstance == null || agent == null)
            return;

        if (!story.IsFlagTrue(chaseStartFlag))
            return;

        if (!chaseModeStarted)
        {
            chaseModeStarted = true;
            BindPlayer();
        }

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        UpdateNpcSpeed();

        if (playerTransform == null)
            BindPlayer();

        if (playerTransform == null)
            return;

        float distanceToPlayer =
            Vector3.Distance(
                npcInstance.transform.position,
                playerTransform.position);

        // 플레이어 발견 → 추격
        if (distanceToPlayer <= detectRadius)
        {
            ChasePlayer();
            return;
        }

        // 아직 Location6 안 갔으면 이동
        if (!reachedLocation6)
        {
            MoveToLocation6();
        }
        else
        {
            // Location6 도착 후 문 상태 확인
            CheckDoorThenChase();
        }
    }

    // 플레이어와의 거리에 따라 속도 조절
    private void BindPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            playerTransform = player.transform;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
    }

    private void UpdateNpcSpeed()
    {
        bool hasFlashlight = false;

        if (InventoryManager.Instance != null)
            hasFlashlight = InventoryManager.Instance.HasItem(flashlightItemId);

        agent.speed = hasFlashlight ? runSpeed : walkSpeed;

        if (animator != null)
            animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void ChasePlayer()
    {
        if (playerTransform == null)
            return;

        if (agent.isStopped)
            agent.isStopped = false;

        agent.SetDestination(playerTransform.position);
    }

    private void MoveToLocation6()
    {
        if (reachedLocation6)
        {
            CheckDoorThenChase();
            return;
        }

        Transform loc6 = FindMarker(location6Name);
        if (loc6 == null)
            return;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(loc6.position, out hit, 3f, NavMesh.AllAreas))
            return;

        agent.isStopped = false;
        agent.SetDestination(hit.position);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            reachedLocation6 = true;
            CheckDoorThenChase();
        }
    }

    private void CheckDoorThenChase()
    {
        GameObject doorObj = GameObject.Find(doorNearLocation6Name);

        if (doorObj == null)
        {
            ChasePlayer();
            return;
        }

        DoorInteractable door =
            doorObj.GetComponent<DoorInteractable>();

        // 문 잠김 상태
        if (door != null && door.IsDoorLocked())
        {
            agent.isStopped = true;

            // 문 사운드 1회만 재생
            if (npcDoorSound != null &&
                npcAudioSource != null &&
                !npcAudioSource.isPlaying)
            {
                npcAudioSource.PlayOneShot(npcDoorSound);
            }

            return;
        }

        // 문 열렸으면 다시 추격
        agent.isStopped = false;

        ChasePlayer();
    }

    public void TriggerGameOver()
    {
        if (gameOverRunning)
            return;

        gameOverRunning = true;

        Debug.Log("[NPCStoryLoader] TriggerGameOver called.");

        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        Debug.Log("[NPCStoryLoader] GameOver triggered.");

        gameOverRunning = true;

        if (agent != null)
            agent.isStopped = true;

        // GameOver UI 표시
        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        yield return new WaitForSeconds(gameOverDuration);

        if (story != null)
        {
            story.data.state.flags["npc_chase_started"] = false;
            story.data.state.flags["chase_started"] = false;
            story.data.state.flags["alarm_started"] = false;
        }

        // UI 숨김
        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        SceneLoader.nextSpawnID = "1F_CCTV";

        SceneManager.sceneLoaded += OnGameOverReloaded;

        SceneManager.LoadScene(chaseSceneName);
    }

    private void OnGameOverReloaded(
    Scene scene,
    LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnGameOverReloaded;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (story == null)
            story = FindFirstObjectByType<dialog>();

        if (story != null)
        {
            story.StartScene(
                "S07_CCTV_ROOM",
                "S07_N8"
            );
        }

        gameOverRunning = false;
    }
    /*
    private void RollbackToS07N2()
    {
        if (story != null)
        {
            story.ResetRuntimeStateToCheckpoint_S07N2_Temporary();
            story.StartScene("S07_CCTV_ROOM", "S07_N2");
        }
    }
    */

    private void UpdateMoveToLocation6()
    {
        Transform loc6 = FindMarker(location6Name);

        if (loc6 == null)
            return;

        float playerDist =
            Vector3.Distance(
                npcInstance.transform.position,
                playerTransform.position);

        // 플레이어 발견
        if (playerDist <= detectRadius)
        {
            chaseState = ChaseState.ChasingPlayer;
            return;
        }

        agent.SetDestination(loc6.position);

        // 도착
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            reachedLocation6 = true;

            agent.ResetPath();

            Debug.Log("[NPCStoryLoader] Reached Location6");
        }
    }

    private void UpdateChasePlayer()
    {
        if (playerTransform == null)
            return;

        agent.SetDestination(playerTransform.position);

        // 플레이어 놓치면 다시 Location6 이동
        float dist =
            Vector3.Distance(
                npcInstance.transform.position,
                playerTransform.position);

        if (dist > detectRadius * 1.5f)
        {
            chaseState = ChaseState.MovingToLocation6;
        }
    }
}