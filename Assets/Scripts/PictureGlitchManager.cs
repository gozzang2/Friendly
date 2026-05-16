using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PictureGlitchManager : MonoBehaviour
{
    [Header("무서운 그림 재질")]
    public Material scaryMaterial;

    [Header("글리치 지속 시간")]
    public float scaryDuration = 0.5f;

    [Header("이 방의 액자들 (Inspector에서 직접 연결)")]
    public List<MeshRenderer> picturesInRoom;

    // material 대신 sharedMaterial 사용 → 메모리 누수 방지
    private Dictionary<MeshRenderer, Material> originalMaterials
        = new Dictionary<MeshRenderer, Material>();

    private Coroutine glitchRoutine;
    public bool isPlayerInRoom = false;

    void Start()
    {
        foreach (var renderer in picturesInRoom)
        {
            if (renderer != null)
                // sharedMaterial 사용 → 인스턴스 생성 안 함
                originalMaterials[renderer] = renderer.sharedMaterial;
        }
    }

    void OnDisable()
    {
        // 오브젝트 비활성화 시 코루틴 정리
        StopAllCoroutines();
        glitchRoutine = null;
        RestoreAll();
    }

    void OnDestroy()
    {
        // 오브젝트 삭제 시 코루틴 정리
        StopAllCoroutines();
        glitchRoutine = null;
    }

    public void TriggerGlitch()
    {
        if (!isPlayerInRoom) return;
        if (glitchRoutine != null) return;
        if (!gameObject.activeInHierarchy) return; // 비활성화 상태면 무시
        glitchRoutine = StartCoroutine(SingleGlitchRoutine());
    }

    IEnumerator SingleGlitchRoutine()
    {
        List<MeshRenderer> allRenderers = new List<MeshRenderer>(picturesInRoom);
        int count = Random.Range(1, allRenderers.Count + 1);

        List<MeshRenderer> selected = new List<MeshRenderer>();
        for (int i = 0; i < count; i++)
        {
            if (allRenderers.Count == 0) break;
            int idx = Random.Range(0, allRenderers.Count);
            selected.Add(allRenderers[idx]);
            allRenderers.RemoveAt(idx);
        }

        // sharedMaterial 사용 → 메모리 누수 방지
        foreach (var r in selected)
            if (r != null) r.sharedMaterial = scaryMaterial;

        yield return new WaitForSeconds(scaryDuration);

        // 복구도 sharedMaterial로
        foreach (var r in selected)
            if (r != null && originalMaterials.ContainsKey(r))
                r.sharedMaterial = originalMaterials[r];

        glitchRoutine = null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerInRoom = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRoom = false;
            StopAllCoroutines();
            glitchRoutine = null;
            RestoreAll();
        }
    }

    private void RestoreAll()
    {
        foreach (var kvp in originalMaterials)
            if (kvp.Key != null)
                kvp.Key.sharedMaterial = kvp.Value;
    }
}