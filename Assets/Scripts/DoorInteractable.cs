using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Door Parts")]
    [SerializeField] private Transform hingePivot;
    [SerializeField] private Transform player; // 자동 바인딩 대상

    [Header("Open Settings")]
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float openCloseDuration = 0.25f;

    [Header("Lock Settings")]
    [SerializeField] private string requiredUnlockId;   // 예: "door1"
    [SerializeField] private bool startsLocked = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip lockedSound;   // optional
    [SerializeField] private AudioClip openSound;     // optional

    private Quaternion closedRotation;
    private Quaternion openedRotation;

    private bool isOpen = false;
    private bool isMoving = false;

    private NavMeshObstacle navObstacle;

    private void Awake()
    {
        if (hingePivot == null)
            hingePivot = transform;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        navObstacle = hingePivot.GetComponent<NavMeshObstacle>();
        closedRotation = hingePivot.localRotation;
    }

    private void Start()
    {
        // 부트스트랩/플레이어 스폰 타이밍 때문에 한 프레임 뒤 자동 바인딩
        StartCoroutine(BindPlayerNextFrame());
    }

    private IEnumerator BindPlayerNextFrame()
    {
        yield return null;
        BindPlayerIfNeeded();
    }

    private void BindPlayerIfNeeded()
    {
        if (player != null) return;

        // 씬에서 PlayerController 타입을 찾아 자동 바인딩 시도
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            Debug.Log($"[Door] Player bound: {player.name}");
        }
        else
        {
            Debug.LogWarning("[Door] Player not found for auto-bind.");
        }
    }

    public void Interact()
    {
        Debug.Log("Door interacted");

        if (isMoving) return;

        // 혹시 시작 타이밍에 못 잡았으면 여기서 한 번 더 보정
        BindPlayerIfNeeded();

        if (IsLocked())
        {
            PlayLockedSound();
            Debug.Log($"[Door] Locked. Required unlock id: {requiredUnlockId}");
            DoorFeedbackUI.Instance?.ShowLockedUI();
            return;
        }

        if (!isOpen)
            OpenDoorAwayFromPlayer();
        else
            StartCoroutine(RotateDoor(closedRotation, false));
    }

    private bool IsLocked()
    {
        if (!startsLocked) return false;

        return !unlockingItem.IsUnlocked(requiredUnlockId);
    }

    public bool IsDoorLocked()
    {
        return IsLocked();
    }

    #region door opening&closing logic
    // 플레이어 반대 방향으로 문 여는 함수 (방향 계산)
    private void OpenDoorAwayFromPlayer()
    {
        if (player == null)
        {
            Debug.LogWarning("[Door] Cannot open: player is null.");
            return;
        }

        // 문 힌지 기준 오른쪽/왼쪽 어디에 있는지 계산
        Vector3 toPlayer = (player.position - hingePivot.position).normalized;
        float side = Vector3.Dot(hingePivot.right, toPlayer);

        // 플레이어 반대 방향으로 열기
        PlayOpenSound();

        float targetAngle = (side >= 0f) ? -openAngle : openAngle;

        openedRotation = closedRotation * Quaternion.Euler(0f, targetAngle, 0f);
        StartCoroutine(RotateDoor(openedRotation, true));
    }


    // 문 닫기
    public void CloseDoor()
    {
        if (isMoving) return;
        if (!isOpen) return;

        StartCoroutine(RotateDoor(closedRotation, false));
    }

    private IEnumerator RotateDoor(Quaternion targetRotation, bool openState)
    {
        isMoving = true;

        // 문 열기 시작 시 장애물 제거
        if (openState && navObstacle != null)
        {
            navObstacle.enabled = false;
        }

        Quaternion startRotation = hingePivot.localRotation;
        float elapsed = 0f;

        while (elapsed < openCloseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openCloseDuration);
            hingePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        hingePivot.localRotation = targetRotation;

        // 문 닫힌 후 장애물 복구
        if (!openState && navObstacle != null)
        {
            navObstacle.enabled = true;
        }

        isOpen = openState;
        isMoving = false;
    }
    #endregion

    #region sound
    private void PlayLockedSound()
    {
        if (audioSource != null && lockedSound != null)
            audioSource.PlayOneShot(lockedSound);
    }

    private void PlayOpenSound()
    {
        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);
    }
    #endregion

    // 바깥 스크립트가 문 상태를 확인할 수 있도록 프로퍼티 제공
    public bool IsOpen => isOpen;
    public bool IsMoving => isMoving;
    public bool GetIsLocked() => IsLocked(); 
}