using System.Collections;
using UnityEngine;

public class DollLookAtPlayer : MonoBehaviour
{
    [Header("Target Bones")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform hairBone;

    [Header("Distance")]
    [SerializeField] private float activeDistance = 12f;

    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 5f;

    [Header("Vision Check")]
    [SerializeField] private Renderer targetRenderer;

    private Transform player;
    private Camera playerCam;

    private Quaternion headInitialRot;
    private Quaternion hairInitialRot;

    private void Start()
    {
        StartCoroutine(FindPlayerRoutine());

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        // 초기 회전 저장
        if (headBone != null)
            headInitialRot = headBone.localRotation;

        if (hairBone != null)
            hairInitialRot = hairBone.localRotation;
    }

    private IEnumerator FindPlayerRoutine()
    {
        while (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");

            if (obj != null)
            {
                player = obj.transform;
                playerCam = Camera.main;
                yield break;
            }

            yield return null;
        }
    }

    private void Update()
    {
        if (player == null || playerCam == null)
            return;

        float dist = Vector3.Distance(transform.position, player.position);

        // 거리 밖이면 원래 방향 유지
        if (dist > activeDistance)
        {
            ResetHeadRotation();
            return;
        }

        // 플레이어 시야 안이면 움직이지 않음
        if (IsVisibleToCamera())
        {
            ResetHeadRotation();
            return;
        }

        RotateHeadTowardPlayer();
    }

    private bool IsVisibleToCamera()
    {
        if (targetRenderer == null)
            return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCam);

        return GeometryUtility.TestPlanesAABB(
            planes,
            targetRenderer.bounds
        );
    }

    private void RotateHeadTowardPlayer()
    {
        if (headBone == null)
            return;

        Vector3 targetPos = player.position;
        targetPos.y = headBone.position.y;

        Vector3 dir = targetPos - headBone.position;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir);

        Quaternion smoothRot = Quaternion.Slerp(
            headBone.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );

        headBone.rotation = smoothRot;

        // hair도 같이 회전
        if (hairBone != null)
        {
            hairBone.rotation = smoothRot;
        }
    }

    private void ResetHeadRotation()
    {
        if (headBone != null)
        {
            headBone.localRotation = Quaternion.Slerp(
                headBone.localRotation,
                headInitialRot,
                rotateSpeed * Time.deltaTime
            );
        }

        if (hairBone != null)
        {
            hairBone.localRotation = Quaternion.Slerp(
                hairBone.localRotation,
                hairInitialRot,
                rotateSpeed * Time.deltaTime
            );
        }
    }
}