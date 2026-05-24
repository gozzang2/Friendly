using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ScareManager : MonoBehaviour
{
    public static ScareManager Instance;

    // ──────────────────────────────────────
    // 1. 마네킹
    // ──────────────────────────────────────
    [Header("1. 마네킹")]
    public List<MannequinTrigger> mannequins;

    // ──────────────────────────────────────
    // 2. 조명 깜빡임
    // ──────────────────────────────────────
    [Header("2. 조명 깜빡임")]
    [Tooltip("ScareLight 태그 달린 조명들 자동 수집됨")]
    private List<(GameObject glow, Transform parent)> scareLights
    = new List<(GameObject, Transform)>();
    public float lightFlickerDuration = 1.5f;

    // ──────────────────────────────────────
    // 3. 점프스케어
    // ──────────────────────────────────────
    [Header("3. 점프스케어 - UI 이미지")]
    public Canvas jumpScareCanvas;
    public GameObject jumpScareImage; 

    // ──────────────────────────────────────
    // 4. 소리
    // ──────────────────────────────────────
    [Header("4. 소리")]
    public AudioSource scareAudio;
    public AudioClip footstepSound;
    public AudioClip eerySound;
    public AudioClip laughSound;
    public AudioClip hospitalBeepSound;
    public AudioClip cryingSound;

    // ──────────────────────────────────────
    // 5. 문
    // ──────────────────────────────────────
    [Header("5. 문")]
    public List<DoorInteractable> doors;

    // ──────────────────────────────────────
    // 6. 그림 글리치
    // ──────────────────────────────────────
    [Header("6. 그림 글리치")]
    public List<PictureGlitchManager> pictureGlitchManagers;

    // ──────────────────────────────────────
    // 상태
    // ──────────────────────────────────────
    [Header("상태")]
    public bool isActing = false;

    // 플레이어 참조 (위치 기반 체크용)
    private Transform playerTransform;
    public float nearbyCheckRadius = 8f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (jumpScareImage != null) jumpScareImage.SetActive(false);

        AssignCameraToCanvas();
    }

    void Start()
    {
        // ScareLight 태그 달린 조명 자동 수집
        GameObject[] lightObjects = GameObject.FindGameObjectsWithTag("ScareLight");
        foreach (var obj in lightObjects)
        {
            Transform glow = obj.transform.Find("Glow");
            if (glow != null)
                scareLights.Add((glow.gameObject, obj.transform));
        }
        Debug.Log($"[ScareManager] ScareLight {scareLights.Count}개 수집");

        // 플레이어 자동 바인딩
        StartCoroutine(BindPlayerNextFrame());
    }

    IEnumerator BindPlayerNextFrame()
    {
        yield return null;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;
    }

    public void AssignCameraToCanvas()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null && jumpScareCanvas != null)
        {
            jumpScareCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            jumpScareCanvas.worldCamera = mainCam;
            jumpScareCanvas.planeDistance = 0.3f;
            jumpScareCanvas.sortingOrder = 999;
        }
    }

    // ──────────────────────────────────────
    // 위치 기반 체크 함수들
    // ──────────────────────────────────────

    // 주변에 조명 있는지
    public bool HasNearbyLight()
    {
        if (playerTransform == null) return false;
        foreach (var (glow, parent) in scareLights)
        {
            if (parent == null) continue;
            if (Vector3.Distance(playerTransform.position, parent.position)
                <= nearbyCheckRadius)
                return true;
        }
        return false;
    }

    // 주변에 마네킹 있는지 (isPlayerNearby 활용)
    public bool HasNearbyMannequin()
    {
        foreach (var m in mannequins)
            if (m != null && m.IsPlayerNearby) return true;
        return false;
    }

    // 잠금 해제된 문 있는지
    public bool HasUnlockedDoor()
    {
        foreach (var d in doors)
            if (d != null && !d.GetIsLocked() && !d.IsMoving)
                return true;
        return false;
    }

    // 그림 글리치 가능한지 (플레이어가 방 안)
    public bool CanTriggerGlitch()
    {
        foreach (var manager in pictureGlitchManagers)
            if (manager != null && manager.IsPlayerInRoom)
                return true;
        return false;
    }

    // ──────────────────────────────────────
    // AI가 호출할 연출 함수들
    // ──────────────────────────────────────

    // Action 1: 마네킹 움찔
    public void CallMannequin()
    {
        if (isActing) return;
        if (!HasNearbyMannequin()) return;

        // 주변 마네킹 중 랜덤 선택
        List<MannequinTrigger> nearby = new List<MannequinTrigger>();
        foreach (var m in mannequins)
            if (m != null && m.IsPlayerNearby) nearby.Add(m);

        if (nearby.Count == 0) return;
        nearby[Random.Range(0, nearby.Count)].ActivateScare();
    }

    // Action 2: 조명 깜빡임
    public void CallRedLights()
    {
        Debug.Log($"[ScareManager] CallRedLights isActing: {isActing}");
        if (isActing) return;
        if (!HasNearbyLight()) return;
        StartCoroutine(LightFlickerRoutine());
    }

    // Action 3: 점프스케어 (UI 이미지)
    public void CallJumpScare()
    {
        Debug.Log($"[ScareManager] isActing: {isActing}");
        if (isActing) return;
        if (jumpScareCanvas.worldCamera == null) AssignCameraToCanvas();
        if (jumpScareImage == null) return;
        StartCoroutine(JumpScareUIRoutine());
    }

    // Action 4: 소리 재생 (종류 랜덤)
    public void CallScareSound()
    {
        Debug.Log($"[ScareManager] CallScareSound isActing: {isActing}");
        if (isActing) return;
        if (scareAudio == null) return;

        AudioClip clip = GetRandomSoundClip();
        if (clip == null) return;

        StartCoroutine(ScareSoundRoutine(clip));
    }

    // Action 5: 문 열림/닫힘
    public void CallDoorScare()
    {
        if (isActing) return;
        if (!HasUnlockedDoor()) return;
        StartCoroutine(DoorScareRoutine());
    }

    // Action 6: 그림 글리치
    public void CallPictureGlitch()
    {
        if (isActing) return;
        // 플레이어가 있는 방의 글리치 매니저만 실행
        foreach (var manager in pictureGlitchManagers)
        {
            if (manager != null && manager.IsPlayerInRoom)
            {
                manager.TriggerGlitch();
                return; // 하나만 실행
            }
        }
    }

    // ──────────────────────────────────────
    // 코루틴들
    // ──────────────────────────────────────

    IEnumerator LightFlickerRoutine()
    {
        isActing = true;

        // 주변 조명만 깜빡임
        List<GameObject> nearbyLights = new List<GameObject>();
        foreach (var (glow, parent) in scareLights)
        {
            if (parent == null) continue;
            if (playerTransform == null ||
                Vector3.Distance(playerTransform.position, parent.position)
                <= nearbyCheckRadius)
                nearbyLights.Add(glow);
        }

        float elapsed = 0f;
        while (elapsed < lightFlickerDuration)
        {
            foreach (var l in nearbyLights) l.SetActive(!l.activeSelf);
            float interval = Random.Range(0.05f, 0.2f);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
        foreach (var l in nearbyLights) l.SetActive(true);
        isActing = false;
    }
    IEnumerator JumpScareUIRoutine()
    {
        isActing = true;
        jumpScareImage.SetActive(true);
        yield return new WaitForSeconds(0.6f);
        jumpScareImage.SetActive(false);
        isActing = false;
    }

    IEnumerator ScareSoundRoutine(AudioClip clip)
    {
        isActing = true;
        scareAudio.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length);
        isActing = false;
    }

    IEnumerator DoorScareRoutine()
    {
        isActing = true;

        // 잠금 해제된 문 중 랜덤 선택
        List<DoorInteractable> available = new List<DoorInteractable>();
        foreach (var d in doors)
            if (d != null && !d.GetIsLocked() && !d.IsMoving) available.Add(d);

        if (available.Count > 0)
        {
            DoorInteractable door = available[Random.Range(0, available.Count)];
            if (door.IsOpen)
                door.CloseDoor();         // 열린 문 → 갑자기 닫힘
            else
                door.Interact();          // 닫힌 문 → 갑자기 열림
        }

        yield return new WaitForSeconds(1.0f);
        isActing = false;
    }

    // ──────────────────────────────────────
    // 소리 헬퍼
    // ──────────────────────────────────────

    private AudioClip GetRandomSoundClip()
    {
        // 있는 클립들만 모아서 랜덤 선택
        List<AudioClip> available = new List<AudioClip>();
        if (footstepSound != null) available.Add(footstepSound);
        if (eerySound != null) available.Add(eerySound);
        if (laughSound != null) available.Add(laughSound);
        if (hospitalBeepSound != null) available.Add(hospitalBeepSound);
        if (cryingSound != null) available.Add(cryingSound);

        if (available.Count == 0) return null;
        return available[Random.Range(0, available.Count)];
    }

}