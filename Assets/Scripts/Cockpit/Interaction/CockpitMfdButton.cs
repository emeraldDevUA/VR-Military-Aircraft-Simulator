using UnityEngine;

/// <summary>
/// Reusable physical MFD key. Input sources call Press/Release; the optional action is
/// invoked on release so mouse, XR, and other input devices can share the same mapping.
/// </summary>
[DisallowMultipleComponent]
public sealed class CockpitMfdButton : MonoBehaviour
{
    [SerializeField] private CockpitMfdButtonAction action;
    [SerializeField] private Color hoverTint = new Color(0.25f, 0.9f, 0.82f, 1f);
    [SerializeField] private Color pressedTint = new Color(1f, 0.72f, 0.28f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.8f;

    private Renderer targetRenderer;
    private Material[] sharedMaterials;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock workingBlock;
    private bool isHovered;
    private bool isPressed;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsHovered => isHovered;
    public bool IsPressed => isPressed;
    public CockpitMfdButtonAction Action => action;

    private void Awake()
    {
        if (action == null)
            action = GetComponent<CockpitMfdButtonAction>();
        InitializeRendererState();
    }

    private void InitializeRendererState()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
            return;

        if (sharedMaterials == null)
            sharedMaterials = targetRenderer.sharedMaterials;
        if (workingBlock == null)
            workingBlock = new MaterialPropertyBlock();
        if (originalBlocks == null || originalBlocks.Length != sharedMaterials.Length)
            originalBlocks = new MaterialPropertyBlock[sharedMaterials.Length];
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            if (originalBlocks[i] == null)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(originalBlocks[i], i);
            }
        }
    }

    /// <summary>Called by an input source when this key becomes the pointer target.</summary>
    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
            return;
        isHovered = hovered;
        ApplyVisualState();
    }

    /// <summary>Begin a press. Safe to call from mouse, XR, or another input adapter.</summary>
    public void Press()
    {
        if (isPressed)
            return;
        isPressed = true;
        ApplyVisualState();
    }

    /// <summary>End a press and invoke the configured action once if the press was active.</summary>
    public void Release()
    {
        Release(true);
    }

    /// <summary>End a press, optionally invoking its action when the pointer was released elsewhere.</summary>
    public void Release(bool invokeAction)
    {
        if (!isPressed)
            return;
        isPressed = false;
        ApplyVisualState();
        if (invokeAction && action != null)
            action.InvokeAction();
    }

    /// <summary>Cancel a press without executing the mapped action.</summary>
    public void CancelPress()
    {
        if (!isPressed)
            return;
        isPressed = false;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        isPressed = false;
        isHovered = false;
        RestoreOriginalBlocks();
    }

    private void ApplyVisualState()
    {
        if (targetRenderer == null || sharedMaterials == null || workingBlock == null || originalBlocks == null)
            InitializeRendererState();
        if (targetRenderer == null || sharedMaterials == null)
            return;

        Color tint = isPressed ? pressedTint : hoverTint;
        bool highlight = isHovered || isPressed;
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material material = sharedMaterials[i];
            if (material == null)
                continue;

            targetRenderer.GetPropertyBlock(workingBlock, i);
            if (highlight)
            {
                if (material.HasProperty(BaseColorId))
                    workingBlock.SetColor(BaseColorId, Color.Lerp(material.GetColor(BaseColorId), tint, tintStrength));
                else if (material.HasProperty(ColorId))
                    workingBlock.SetColor(ColorId, Color.Lerp(material.GetColor(ColorId), tint, tintStrength));
            }
            else if (originalBlocks != null && i < originalBlocks.Length)
            {
                targetRenderer.SetPropertyBlock(originalBlocks[i], i);
                continue;
            }
            targetRenderer.SetPropertyBlock(workingBlock, i);
        }
    }

    private void RestoreOriginalBlocks()
    {
        if (targetRenderer == null || originalBlocks == null)
            return;
        for (int i = 0; i < originalBlocks.Length; i++)
            if (originalBlocks[i] != null)
                targetRenderer.SetPropertyBlock(originalBlocks[i], i);
    }
}
