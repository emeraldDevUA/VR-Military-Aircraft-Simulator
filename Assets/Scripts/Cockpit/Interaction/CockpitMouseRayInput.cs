using UnityEngine;

/// <summary>Optional desktop test adapter. Raycasts from a chosen camera and forwards to MFD keys.</summary>
[DisallowMultipleComponent]
public sealed class CockpitMouseRayInput : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;
    [SerializeField, Min(0.1f)] private float maxDistance = 2.5f;
    [SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;

    private CockpitMfdButton hoveredButton;
    private CockpitMfdButton pressedButton;
    private bool inputUnavailable;

    public Camera RayCamera
    {
        get => rayCamera;
        set => rayCamera = value;
    }

    private void Awake()
    {
        if (rayCamera == null)
            rayCamera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (rayCamera == null || !rayCamera.isActiveAndEnabled || inputUnavailable)
            return;

        try
        {
            ProcessScreenPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
                BeginPointerPress();
            if (Input.GetMouseButtonUp(0))
                EndPointerPress();
        }
        catch (System.InvalidOperationException exception)
        {
            inputUnavailable = true;
            Debug.LogWarning("Cockpit mouse input requires the legacy Input Manager (Project Settings > Player > Active Input Handling). " + exception.Message, this);
        }
    }

    /// <summary>Update hover from a screen-space pointer position; also useful to test ray paths.</summary>
    public void ProcessScreenPoint(Vector2 screenPoint)
    {
        CockpitMfdButton next = null;
        if (rayCamera == null || !rayCamera.isActiveAndEnabled)
            return;
        Ray ray = rayCamera.ScreenPointToRay(screenPoint);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            next = hit.collider.GetComponentInParent<CockpitMfdButton>();

        if (next == hoveredButton)
            return;
        if (hoveredButton != null)
            hoveredButton.SetHovered(false);
        hoveredButton = next;
        if (hoveredButton != null)
            hoveredButton.SetHovered(true);
    }

    /// <summary>Forward a pointer press to the current button. XR adapters can call the same API.</summary>
    public void BeginPointerPress()
    {
        pressedButton = hoveredButton;
        if (pressedButton != null)
            pressedButton.Press();
    }

    /// <summary>Forward a pointer release; mapped actions fire only when a button was pressed.</summary>
    public void EndPointerPress()
    {
        CockpitMfdButton releasedButton = pressedButton;
        pressedButton = null;
        if (releasedButton == null)
            return;
        releasedButton.Release(releasedButton == hoveredButton);
    }

    private void OnDisable()
    {
        if (hoveredButton != null)
            hoveredButton.SetHovered(false);
        if (pressedButton != null)
            pressedButton.CancelPress();
        hoveredButton = null;
        pressedButton = null;
    }
}
