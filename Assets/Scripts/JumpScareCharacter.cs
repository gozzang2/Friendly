using UnityEngine;

public class JumpScareCharacter : MonoBehaviour
{
    [SerializeField] private GameObject jumpScareObject; // 캐릭터 오브젝트
    private Animator animator;
    private bool hasTriggered = false;

    void Start()
    {
        jumpScareObject.SetActive(false); // 처음엔 꺼둠
        animator = jumpScareObject.GetComponent<Animator>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            // 플레이어 방향으로 회전
            Vector3 direction = other.transform.position - jumpScareObject.transform.position;
            direction.y = 0f;
            jumpScareObject.transform.rotation = Quaternion.LookRotation(direction);

            jumpScareObject.SetActive(true);
            animator.SetTrigger("Scare");

            float clipLength = GetClipLength();
            Invoke("DestroySelf", clipLength);
        }
    }

    private float GetClipLength()
    {
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            return clip.length;
        return 2f;
    }

    private void DestroySelf()
    {
        Destroy(jumpScareObject);
    }
}