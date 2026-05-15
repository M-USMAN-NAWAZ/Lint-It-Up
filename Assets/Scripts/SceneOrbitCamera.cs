using UnityEngine;

public class SceneOrbitCamera : MonoBehaviour
{
    [SerializeField] Transform orbitTarget;
    [SerializeField] Vector3 orbitOffset = new Vector3(-2.5f, 5.15f, 10.2f);
    [SerializeField] Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] float orbitDegreesPerSecond = 8f;
    [SerializeField] bool captureCurrentOffsetOnStart;
    [SerializeField] bool useChildRendererBoundsCenter = true;
    [SerializeField] Vector3 orbitCenterOffset;

    Vector3 startingOffset;
    float orbitAngle;
    Vector3 orbitCenter;

    void Start()
    {
        if (orbitTarget == null)
        {
            return;
        }

        orbitCenter = ResolveOrbitCenter();
        startingOffset = captureCurrentOffsetOnStart
            ? transform.position - orbitCenter
            : orbitOffset;

        if (startingOffset.sqrMagnitude <= 0.0001f)
        {
            startingOffset = new Vector3(0f, 5f, -10f);
        }

        UpdateCameraPose();
    }

    void LateUpdate()
    {
        if (orbitTarget == null)
        {
            return;
        }

        orbitAngle += orbitDegreesPerSecond * Time.deltaTime;
        UpdateCameraPose();
    }

    void UpdateCameraPose()
    {
        var rotatedOffset = Quaternion.AngleAxis(orbitAngle, Vector3.up) * startingOffset;
        orbitCenter = ResolveOrbitCenter();
        transform.position = orbitCenter + rotatedOffset;
        transform.LookAt(orbitCenter + lookAtOffset, Vector3.up);
    }

    Vector3 ResolveOrbitCenter()
    {
        if (orbitTarget == null)
        {
            return transform.position;
        }

        var center = orbitTarget.position + orbitCenterOffset;
        if (!useChildRendererBoundsCenter)
        {
            return center;
        }

        var renderers = orbitTarget.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return center;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center + orbitCenterOffset;
    }
}
