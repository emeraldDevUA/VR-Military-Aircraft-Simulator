using UnityEngine;

/// <summary>Input-independent rotary control. Mouse/XR adapters call Rotate(direction).</summary>
[DisallowMultipleComponent]
public sealed class CockpitRotaryControl : MonoBehaviour
{
    [SerializeField] private CockpitCentralRotaryAction action;
    [SerializeField] private Color hoverTint = new Color(0.25f, 0.9f, 0.82f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.65f;

    private Renderer targetRenderer;
    private MaterialPropertyBlock originalBlock;
    private MaterialPropertyBlock workingBlock;
    private bool hovered;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsHovered => hovered;

    private void Awake()
    {
        if (action == null)
            action = GetComponent<CockpitCentralRotaryAction>();
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            originalBlock = new MaterialPropertyBlock();
            workingBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(originalBlock);
        }
    }

    public void SetHovered(bool value)
    {
        if (hovered == value)
            return;
        hovered = value;
        ApplyVisualState();
    }

    /// <summary>Rotate one detent. Negative is left/counter-clockwise, positive is right/clockwise.</summary>
    public void Rotate(int direction)
    {
        if (direction != 0 && action != null)
            action.Invoke(direction < 0 ? -1 : 1);
    }

    private void ApplyVisualState()
    {
        if (targetRenderer == null)
            return;
        if (!hovered)
        {
            targetRenderer.SetPropertyBlock(originalBlock);
            return;
        }
        targetRenderer.GetPropertyBlock(workingBlock);
        Material material = targetRenderer.sharedMaterial;
        if (material != null && material.HasProperty(BaseColorId))
            workingBlock.SetColor(BaseColorId, Color.Lerp(material.GetColor(BaseColorId), hoverTint, tintStrength));
        else if (material != null && material.HasProperty(ColorId))
            workingBlock.SetColor(ColorId, Color.Lerp(material.GetColor(ColorId), hoverTint, tintStrength));
        targetRenderer.SetPropertyBlock(workingBlock);
    }
}
