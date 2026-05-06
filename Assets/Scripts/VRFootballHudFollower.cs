using UnityEngine;

public class VRFootballHudFollower : MonoBehaviour
{
    [SerializeField] Transform targetHead;
    [SerializeField] float forwardDistance = 1.3f;
    [SerializeField] float verticalOffset = -0.1f;
    [SerializeField] float positionLerpSpeed = 10f;
    [SerializeField] float rotationLerpSpeed = 12f;

    void LateUpdate()
    {
        if (targetHead == null)
        {
            return;
        }

        var flattenedForward = targetHead.forward;
        flattenedForward.y = 0f;
        if (flattenedForward.sqrMagnitude < 0.0001f)
        {
            flattenedForward = Vector3.forward;
        }

        flattenedForward.Normalize();

        var desiredPosition = targetHead.position + flattenedForward * forwardDistance + Vector3.up * verticalOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionLerpSpeed * Time.unscaledDeltaTime));

        var desiredRotation = Quaternion.LookRotation(transform.position - targetHead.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationLerpSpeed * Time.unscaledDeltaTime));
    }

    public void SetTargetHead(Transform head)
    {
        targetHead = head;
    }
}
