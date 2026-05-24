using System.Collections.Generic;
using UnityEngine;

public class PictureGlitchManager : MonoBehaviour
{
    [Header("무서운 그림 재질")]
    [SerializeField] private Material scaryMaterial;

    [Header("글리치 지속 시간 (현실 시간 기준)")]
    [SerializeField] private float scaryDuration = 0.5f;

    [Header("이 방의 액자들")]
    [SerializeField] private List<MeshRenderer> picturesInRoom;

    // 각 액자의 원래 런타임 머티리얼 저장
    private readonly Dictionary<MeshRenderer, Material> originalMaterials
        = new Dictionary<MeshRenderer, Material>();

    // 생성된 런타임 Material Instance 추적
    private readonly List<Material> runtimeMaterialInstances
        = new List<Material>();

    // 현재 글리치 중인 액자들
    private readonly List<MeshRenderer> activeGlitchPictures
        = new List<MeshRenderer>();

    // 플레이어 방 안 여부
    private bool isPlayerInRoom = false;
    public bool IsPlayerInRoom => isPlayerInRoom;

    // 글리치 실행 여부
    private bool isGlitchRunning = false;

    // 글리치 종료 시각 (현실 시간 기준)
    private float glitchEndTime;

    private void Start()
    {
        foreach (var renderer in picturesInRoom)
        {
            if (renderer == null)
                continue;

            // 런타임 전용 Material Instance 생성
            Material runtimeInstance = renderer.material;

            // 원래 머티리얼 저장
            originalMaterials[renderer] = runtimeInstance;

            // 나중에 Destroy하기 위해 추적
            runtimeMaterialInstances.Add(runtimeInstance);
        }
    }

    private void Update()
    {
        if (!isGlitchRunning)
            return;

        // Time.timeScale 영향 안 받는 현실 시간 기준
        if (Time.unscaledTime >= glitchEndTime)
        {
            RestoreGlitch();
        }
    }

    public void TriggerGlitch()
    {
        // 플레이어 없으면 실행 안 함
        if (!isPlayerInRoom)
            return;

        // 이미 실행 중이면 중복 실행 방지
        if (isGlitchRunning)
            return;

        activeGlitchPictures.Clear();

        List<MeshRenderer> candidates =
            new List<MeshRenderer>(picturesInRoom);

        if (candidates.Count == 0)
            return;

        // 랜덤 개수 선택
        int count = Random.Range(1, candidates.Count + 1);

        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0)
                break;

            int idx = Random.Range(0, candidates.Count);

            MeshRenderer target = candidates[idx];

            candidates.RemoveAt(idx);

            if (target == null)
                continue;

            activeGlitchPictures.Add(target);
        }

        // 무서운 그림 적용
        foreach (var renderer in activeGlitchPictures)
        {
            if (renderer == null)
                continue;

            renderer.material = scaryMaterial;
        }

        isGlitchRunning = true;

        // 현실 시간 기준 종료 시각 설정
        glitchEndTime = Time.unscaledTime + scaryDuration;
    }

    private void RestoreGlitch()
    {
        foreach (var renderer in activeGlitchPictures)
        {
            if (renderer == null)
                continue;

            if (!originalMaterials.ContainsKey(renderer))
                continue;

            renderer.material = originalMaterials[renderer];
        }

        activeGlitchPictures.Clear();

        isGlitchRunning = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRoom = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRoom = false;

            // 방 나가면 즉시 원상복구
            RestoreGlitch();
        }
    }

    private void OnDisable()
    {
        RestoreGlitch();
    }

    private void OnDestroy()
    {
        // 생성한 런타임 Material Instance 정리
        foreach (var mat in runtimeMaterialInstances)
        {
            if (mat != null)
            {
                Destroy(mat);
            }
        }

        runtimeMaterialInstances.Clear();
        originalMaterials.Clear();
        activeGlitchPictures.Clear();
    }
}