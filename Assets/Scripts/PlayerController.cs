using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float runSpeed = 6f;
    public float jumpForce = 5f;

    [Header("Animation")]
    public Animator animator;

    [Header("Look")]
    public Transform cameraTransform;
    public float mouseSensitivity = 0.12f;

    [Header("Interact")]
    public float interactDistance = 4f;
    public LayerMask interactLayer;
    public GameObject interactUI;          // InteractText(DDOL) 자동 바인딩 대상
    public InventoryManager inventory;     // 자동 바인딩 대상(없으면 인벤 입력 스킵)

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource walkAudioSource;
    [SerializeField] private AudioSource runAudioSource;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Default Footstep Clips")]
    [SerializeField] private AudioClip defaultWalkClip;
    [SerializeField] private AudioClip defaultRunClip;

    [Header("Outdoor Footstep Clips")]
    [SerializeField] private string outdoorSceneName = "OutdoorScene";
    [SerializeField] private AudioClip outdoorWalkClip;
    [SerializeField] private AudioClip outdoorRunClip;

    Rigidbody rb;
    Vector2 moveInput;
    Vector2 lookInput;
    float cameraPitch;
    bool isGrounded;
    bool isSprinting;

    bool _uiBound;
    bool _inventoryBound;
    bool _loggedMissingInteractUI;
    bool _loggedMissingCamera;

    private Outline _lastOutline; //outline 켜져 있는 오브젝트 저장용 

    #region start & update
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // 카메라 자동 바인딩(없으면 나중에 경고 1회)
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>(true);

            if (cam != null)
            {
                cameraTransform = cam.transform;
                Debug.Log($"[PlayerController] cameraTransform auto-bound: {cameraTransform.name}");
            }
        }

        // UI/인벤 자동 바인딩 시도
        BindDependenciesIfNeeded();

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (defaultWalkClip == null && walkAudioSource != null)
            defaultWalkClip = walkAudioSource.clip;

        if (defaultRunClip == null && runAudioSource != null)
            defaultRunClip = runAudioSource.clip;

        ApplyFootstepClipsForCurrentScene();
    }

    void FixedUpdate()
    {
        Vector3 move =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        float currentSpeed = isSprinting ? runSpeed : moveSpeed;

        rb.linearVelocity = new Vector3(
            move.x * currentSpeed,
            rb.linearVelocity.y,
            move.z * currentSpeed
        );

        //Animator에 Speed 값 전달
        if(animator != null)
        {
            float animSpeed = 0f; // 기본은 Idle(숨쉬기 속도 0)

            if (moveInput.magnitude > 0.1f)
            {
                animSpeed = isSprinting ? 2f : 1f;
            }
            animator.SetFloat("Speed", animSpeed);
        }

        UpdateFootstepAudio();
    }

    void Update()
    {
        if (cameraTransform == null)
        {
            if (!_loggedMissingCamera)
            {
                Debug.LogError("[PlayerController] cameraTransform missing. Assign in Inspector or ensure a Camera exists under Player.");
                _loggedMissingCamera = true;
            }
            return;
        }

        HandleLook();
        CheckInteractable();
    }

    #endregion

    #region input handlers

    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        lookInput = ctx.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext ctx)
    {
        isSprinting = ctx.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !isGrounded) return;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        isGrounded = false;

        StopFootstepAudio();

        if (animator != null)
        {
            animator.SetTrigger("JumpTrigger");
        }
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            var interactable = hit.collider.GetComponent<IInteractable>()
                 ?? hit.collider.GetComponentInParent<IInteractable>(); // 자식 collider를 맞아도 부모의 IInteractable까지 찾도록
            if (interactable != null)
                interactable.Interact();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopFootstepAudio();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFootstepClipsForCurrentScene();
    }

    public void OnInventory(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (InventoryManager.Instance == null) return;

        var input = GetComponent<PlayerInput>();
        if (input == null) return;

        bool open = !InventoryManager.Instance.IsOpen;

        InventoryManager.Instance.SetOpen(open);

        input.SwitchCurrentActionMap(open ? "UI" : "Player");

        Debug.Log($"[PlayerController] OnInventory performed -> open={open}, switched to {(open ? "UI" : "Player")}");
    }

    #endregion

    #region core logic

    void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void CheckInteractable()
    {
        // interactUI가 비어 있으면 자동 바인딩 재시도
        if (!_uiBound || interactUI == null)
            BindInteractUIIfNeeded();

        // 아직도 없으면 스킵
        if (interactUI == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {

            // 자식 collider를 맞아도 부모의 IInteractable까지 찾도록 변경
            var interactable = hit.collider.GetComponent<IInteractable>()
                            ?? hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null) // 상호작용 가능한 오브젝트면
            {   // interactUI 켜기
                if (!interactUI.activeSelf) interactUI.SetActive(true);

                // Outline도 자식/부모 구조 대응 위해 부모까지 탐색하도록 변경
                Outline outline = hit.collider.GetComponent<Outline>()
                            ?? hit.collider.GetComponentInParent<Outline>();

                if (outline != null)
                {
                    // null 체크 && 이전에 켜둔 게 있다면 끄고 지금 거를 킴
                    if (_lastOutline != null && _lastOutline != outline)
                        _lastOutline.enabled = false;

                    outline.enabled = true;
                    _lastOutline = outline;
                }
                return;
            }
        }

        if (interactUI.activeSelf) interactUI.SetActive(false);

        // 아무것도 안 가리키면 _lastOutline 끔
        if (_lastOutline != null)
        {
            _lastOutline.enabled = false;
            _lastOutline = null;
        }
    }

    #endregion

    #region dependency binding

    void BindDependenciesIfNeeded()
    {
        BindInteractUIIfNeeded();
        BindInventoryIfNeeded();
    }

    void BindInteractUIIfNeeded()
    {
        if (_uiBound && interactUI != null) return;

        // 비활성 포함해서 마커 탐색 (DDOL UICanvas 아래 InteractText에 InteractUIMarker 붙어 있어야 함)
        var marker = FindInteractUIMarkerEvenIfInactive();
        if (marker != null)
        {
            interactUI = marker.gameObject;
            _uiBound = true;
            return;
        }

        // 못 찾으면 1회만 로그(폭주 금지)
        if (!_loggedMissingInteractUI)
        {
            Debug.LogError("[PlayerController] InteractUIMarker not found. Add InteractUIMarker to PersistentRoot/UICanvas/InteractText.");
            _loggedMissingInteractUI = true;
        }
    }

    // 핵심: Resources.FindObjectsOfTypeAll => 비활성/HideInHierarchy 포함해서 찾음
    InteractUIMarker FindInteractUIMarkerEvenIfInactive()
    {
        var markers = Resources.FindObjectsOfTypeAll<InteractUIMarker>();

        for (int i = 0; i < markers.Length; i++)
        {
            var m = markers[i];
            if (m == null) continue;

            // 에셋/프리팹(씬에 없는 것) 배제: scene이 로드된 오브젝트만 사용
            if (!m.gameObject.scene.isLoaded) continue;

            return m; // 첫 번째 마커 사용
        }

        return null;
    }

    void BindInventoryIfNeeded()
    {
        if (_inventoryBound && inventory != null) return;

        // 네 프로젝트에 InventoryManager 싱글톤이 있으면 Instance로 바꿔도 됨
        inventory = FindFirstObjectByType<InventoryManager>();
        _inventoryBound = inventory != null;
    }

    #endregion

    #region ground check

    void OnCollisionEnter(Collision col)
    {
        if (col.collider.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionStay(Collision col)
    {
        if (col.collider.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionExit(Collision col)
    {
        if (col.collider.CompareTag("Ground")) isGrounded = false;
    }

    #endregion

    #region Footstep Audio
    private void UpdateFootstepAudio()
    {
        bool hasMoveInput = moveInput.magnitude > movementThreshold;
        bool shouldPlayFootstep = hasMoveInput && isGrounded;

        if (!shouldPlayFootstep)
        {
            StopFootstepAudio();
            return;
        }

        if (isSprinting)
        {
            if (walkAudioSource != null && walkAudioSource.isPlaying)
                walkAudioSource.Stop();

            if (runAudioSource != null && !runAudioSource.isPlaying)
                runAudioSource.Play();
        }
        else
        {
            if (runAudioSource != null && runAudioSource.isPlaying)
                runAudioSource.Stop();

            if (walkAudioSource != null && !walkAudioSource.isPlaying)
                walkAudioSource.Play();
        }
    }

    private void StopFootstepAudio()
    {
        if (walkAudioSource != null && walkAudioSource.isPlaying)
            walkAudioSource.Stop();

        if (runAudioSource != null && runAudioSource.isPlaying)
            runAudioSource.Stop();
    }

    private void ApplyFootstepClipsForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool isOutdoorScene = sceneName == outdoorSceneName;

        AudioClip targetWalk = isOutdoorScene && outdoorWalkClip != null
            ? outdoorWalkClip
            : defaultWalkClip;

        AudioClip targetRun = isOutdoorScene && outdoorRunClip != null
            ? outdoorRunClip
            : defaultRunClip;

        bool walkWasPlaying = walkAudioSource != null && walkAudioSource.isPlaying;
        bool runWasPlaying = runAudioSource != null && runAudioSource.isPlaying;

        StopFootstepAudio();

        if (walkAudioSource != null)
            walkAudioSource.clip = targetWalk;

        if (runAudioSource != null)
            runAudioSource.clip = targetRun;

        // 필요하면 현재 이동 상태에 맞춰 다시 재생
        if (walkWasPlaying || runWasPlaying)
            UpdateFootstepAudio();
    }

    #endregion
}
