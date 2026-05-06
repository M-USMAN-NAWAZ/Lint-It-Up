using UnityEngine;
using UnityEngine.EventSystems;

public class LoopedFishEyeScroller : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Images")]
    [SerializeField] RectTransform[] scrollableImages;
    [SerializeField] bool autoCollectDirectChildren;

    [Header("Layout")]
    [SerializeField] float imageSpacing = 260f;
    [SerializeField] Vector2 centerOffset;
    [SerializeField] bool horizontal = true;
    [SerializeField] bool arrangeOnStart = true;
    [SerializeField] bool keepOriginalCrossAxis = true;

    [Header("Fish Eye")]
    [SerializeField, Range(0.1f, 1f)] float minimumScale = 0.55f;
    [SerializeField] float fullSizeDistance = 80f;
    [SerializeField] float shrinkDistance = 520f;
    [SerializeField] AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] bool bringFrontImageToTop = true;

    [Header("Movement")]
    [SerializeField] bool autoScroll;
    [SerializeField] float autoScrollSpeed = 80f;
    [SerializeField] bool allowDrag = true;
    [SerializeField] float dragSensitivity = 1f;
    [SerializeField] bool useInertia = true;
    [SerializeField] float inertiaDamping = 8f;

    RectTransform rectTransform;
    Vector2[] originalAnchoredPositions;
    Vector3[] originalScales;
    float scrollOffset;
    float dragVelocity;
    bool dragging;

    void Awake()
    {
        rectTransform = transform as RectTransform;
        RefreshImages();
    }

    void OnValidate()
    {
        minimumScale = Mathf.Clamp(minimumScale, 0.1f, 1f);
        imageSpacing = Mathf.Max(1f, imageSpacing);
        shrinkDistance = Mathf.Max(fullSizeDistance + 1f, shrinkDistance);
    }

    void Start()
    {
        RefreshImages();
        if (arrangeOnStart)
        {
            ApplyLayout();
        }
    }

    void Update()
    {
        if (!Application.isPlaying || scrollableImages == null || scrollableImages.Length == 0)
        {
            return;
        }

        if (autoScroll && !dragging)
        {
            scrollOffset += autoScrollSpeed * Time.unscaledDeltaTime;
        }

        if (useInertia && !dragging && Mathf.Abs(dragVelocity) > 0.01f)
        {
            scrollOffset += dragVelocity * Time.unscaledDeltaTime;
            dragVelocity = Mathf.Lerp(dragVelocity, 0f, 1f - Mathf.Exp(-inertiaDamping * Time.unscaledDeltaTime));
        }

        ApplyLayout();
    }

    public void RefreshImages()
    {
        if (autoCollectDirectChildren)
        {
            var children = new RectTransform[transform.childCount];
            for (var i = 0; i < transform.childCount; i++)
            {
                children[i] = transform.GetChild(i) as RectTransform;
            }

            scrollableImages = children;
        }

        if (scrollableImages == null)
        {
            originalAnchoredPositions = null;
            originalScales = null;
            return;
        }

        originalAnchoredPositions = new Vector2[scrollableImages.Length];
        originalScales = new Vector3[scrollableImages.Length];

        for (var i = 0; i < scrollableImages.Length; i++)
        {
            var image = scrollableImages[i];
            if (image == null)
            {
                continue;
            }

            originalAnchoredPositions[i] = image.anchoredPosition;
            originalScales[i] = image.localScale;
        }
    }

    public void SnapToIndex(int index)
    {
        if (scrollableImages == null || scrollableImages.Length == 0)
        {
            return;
        }

        scrollOffset = Mathf.Clamp(index, 0, scrollableImages.Length - 1) * imageSpacing;
        dragVelocity = 0f;
        ApplyLayout();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!allowDrag)
        {
            return;
        }

        dragging = true;
        dragVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!allowDrag)
        {
            return;
        }

        var dragDelta = horizontal ? eventData.delta.x : eventData.delta.y;
        var delta = -dragDelta * dragSensitivity;
        scrollOffset += delta;
        dragVelocity = delta / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        ApplyLayout();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    void ApplyLayout()
    {
        if (scrollableImages == null || scrollableImages.Length == 0)
        {
            return;
        }

        var totalSpan = Mathf.Max(imageSpacing, imageSpacing * scrollableImages.Length);
        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < scrollableImages.Length; i++)
        {
            var image = scrollableImages[i];
            if (image == null)
            {
                continue;
            }

            var loopedPosition = GetLoopedPosition(i * imageSpacing - scrollOffset, totalSpan);
            var originalPosition = GetOriginalPosition(i);
            var targetPosition = originalPosition;

            if (horizontal)
            {
                targetPosition.x = centerOffset.x + loopedPosition;
                targetPosition.y = keepOriginalCrossAxis ? originalPosition.y : centerOffset.y;
            }
            else
            {
                targetPosition.x = keepOriginalCrossAxis ? originalPosition.x : centerOffset.x;
                targetPosition.y = centerOffset.y + loopedPosition;
            }

            image.anchoredPosition = targetPosition;

            var distance = Mathf.Abs(loopedPosition);
            var scaleMultiplier = GetScaleMultiplier(distance);
            image.localScale = Vector3.Scale(GetOriginalScale(i), Vector3.one * scaleMultiplier);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (bringFrontImageToTop && closestIndex >= 0 && scrollableImages[closestIndex] != null)
        {
            scrollableImages[closestIndex].SetAsLastSibling();
        }
    }

    float GetScaleMultiplier(float distance)
    {
        if (distance <= fullSizeDistance)
        {
            return 1f;
        }

        var t = Mathf.InverseLerp(fullSizeDistance, shrinkDistance, distance);
        var curved = shrinkCurve != null ? shrinkCurve.Evaluate(t) : t;
        return Mathf.Lerp(1f, minimumScale, Mathf.Clamp01(curved));
    }

    float GetLoopedPosition(float position, float totalSpan)
    {
        position = Mathf.Repeat(position + totalSpan * 0.5f, totalSpan) - totalSpan * 0.5f;
        return position;
    }

    Vector2 GetOriginalPosition(int index)
    {
        if (originalAnchoredPositions == null || index < 0 || index >= originalAnchoredPositions.Length)
        {
            return Vector2.zero;
        }

        return originalAnchoredPositions[index];
    }

    Vector3 GetOriginalScale(int index)
    {
        if (originalScales == null || index < 0 || index >= originalScales.Length)
        {
            return Vector3.one;
        }

        return originalScales[index];
    }
}
