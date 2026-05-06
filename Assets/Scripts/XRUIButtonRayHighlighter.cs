using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class XRUIButtonRayHighlighter : MonoBehaviour
{
    [SerializeField] XRRayInteractor rayInteractor;
    [SerializeField] NearFarInteractor nearFarInteractor;
    [SerializeField] XRInteractorLineVisual interactorLineVisual;
    [SerializeField] LineRenderer targetLineRenderer;
    [SerializeField] Color defaultColor = Color.white;
    [SerializeField] Color buttonHoverColor = new Color(0.2f, 0.6f, 1f, 1f);

    Color currentDefaultStartColor;
    Color currentDefaultEndColor;
    Gradient currentDefaultGradient;

    void Reset()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
        nearFarInteractor = GetComponent<NearFarInteractor>();
        interactorLineVisual = GetComponent<XRInteractorLineVisual>();
        targetLineRenderer = ResolveRayLineRenderer();
    }

    void Awake()
    {
        if (rayInteractor == null)
        {
            rayInteractor = GetComponent<XRRayInteractor>();
        }

        if (nearFarInteractor == null)
        {
            nearFarInteractor = GetComponent<NearFarInteractor>();
        }

        if (interactorLineVisual == null)
        {
            interactorLineVisual = ResolveInteractorLineVisual();
        }

        if (targetLineRenderer == null)
        {
            targetLineRenderer = ResolveRayLineRenderer();
        }

        CacheDefaultColors();
        ApplyColor(defaultColor);
    }

    void LateUpdate()
    {
        if ((rayInteractor == null && nearFarInteractor == null) || targetLineRenderer == null)
        {
            return;
        }

        if (IsHoveringUIButton())
        {
            ApplyColor(buttonHoverColor);
        }
        else
        {
            RestoreDefaultColor();
        }
    }

    bool IsHoveringUIButton()
    {
        if (!TryGetCurrentRaycastResult(out var currentRaycast))
        {
            return false;
        }

        var hoveredObject = currentRaycast.gameObject;
        if (hoveredObject == null)
        {
            return false;
        }

        return hoveredObject.GetComponentInParent<Button>() != null;
    }

    bool TryGetCurrentRaycastResult(out RaycastResult currentRaycast)
    {
        if (rayInteractor != null)
        {
            if (rayInteractor.IsOverUIGameObject() &&
                rayInteractor.TryGetUIModel(out var rayUiModel) &&
                rayUiModel.currentRaycast.isValid)
            {
                currentRaycast = rayUiModel.currentRaycast;
                return true;
            }
        }

        if (nearFarInteractor != null)
        {
            if (nearFarInteractor.TryGetUIModel(out var nearFarUiModel) && nearFarUiModel.currentRaycast.isValid)
            {
                currentRaycast = nearFarUiModel.currentRaycast;
                return true;
            }
        }

        currentRaycast = default;
        return false;
    }

    void CacheDefaultColors()
    {
        if (targetLineRenderer == null)
        {
            return;
        }

        currentDefaultStartColor = targetLineRenderer.startColor;
        currentDefaultEndColor = targetLineRenderer.endColor;
        currentDefaultGradient = targetLineRenderer.colorGradient;
        if (currentDefaultStartColor.a <= 0f && currentDefaultEndColor.a <= 0f)
        {
            currentDefaultStartColor = defaultColor;
            currentDefaultEndColor = defaultColor;
        }
    }

    void ApplyColor(Color color)
    {
        if (interactorLineVisual != null)
        {
            interactorLineVisual.setLineColorGradient = false;
        }

        targetLineRenderer.colorGradient = CreateSolidGradient(color);
        if (targetLineRenderer.material != null && targetLineRenderer.material.HasProperty("_Color"))
        {
            targetLineRenderer.material.color = color;
        }
    }

    void RestoreDefaultColor()
    {
        if (interactorLineVisual != null)
        {
            interactorLineVisual.setLineColorGradient = true;
        }

        if (currentDefaultGradient != null)
        {
            targetLineRenderer.colorGradient = currentDefaultGradient;
        }
        else
        {
            targetLineRenderer.colorGradient = CreateDualColorGradient(currentDefaultStartColor, currentDefaultEndColor);
        }

        if (targetLineRenderer.material != null && targetLineRenderer.material.HasProperty("_Color"))
        {
            targetLineRenderer.material.color = currentDefaultStartColor;
        }
    }

    LineRenderer ResolveRayLineRenderer()
    {
        var localLine = GetComponent<LineRenderer>();
        if (localLine != null)
        {
            return localLine;
        }

        var childLineRenderers = GetComponentsInChildren<LineRenderer>(true);
        if (childLineRenderers != null && childLineRenderers.Length > 0)
        {
            foreach (var childLine in childLineRenderers)
            {
                if (childLine != null && childLine.gameObject.name.Contains("Line"))
                {
                    return childLine;
                }
            }

            return childLineRenderers[0];
        }

        return null;
    }

    XRInteractorLineVisual ResolveInteractorLineVisual()
    {
        var localVisual = GetComponent<XRInteractorLineVisual>();
        if (localVisual != null)
        {
            return localVisual;
        }

        var parentVisual = GetComponentInParent<XRInteractorLineVisual>(true);
        if (parentVisual != null)
        {
            return parentVisual;
        }

        return GetComponentInChildren<XRInteractorLineVisual>(true);
    }

    static Gradient CreateSolidGradient(Color color)
    {
        return CreateDualColorGradient(color, color);
    }

    static Gradient CreateDualColorGradient(Color startColor, Color endColor)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        return gradient;
    }
}
