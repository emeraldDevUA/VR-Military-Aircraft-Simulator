using UnityEngine;

/// <summary>Desktop test adapter: wheel rotates the hovered knob; click its local left/right half.</summary>
[DisallowMultipleComponent]
public sealed class CockpitMouseRotaryInput : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;
    [SerializeField, Min(0.1f)] private float maxDistance = 2.5f;
    [SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;

    private CockpitRotaryControl hovered;
    private bool inputUnavailable;

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
            RaycastHit hit;
            CockpitRotaryControl next = null;
            Vector3 localHit = Vector3.zero;
            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                next = hit.collider.GetComponentInParent<CockpitRotaryControl>();
                if (next != null)
                    localHit = next.transform.InverseTransformPoint(hit.point);
            }
            if (next != hovered)
            {
                if (hovered != null) hovered.SetHovered(false);
                hovered = next;
                if (hovered != null) hovered.SetHovered(true);
            }
            if (hovered == null)
                return;
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0.01f) hovered.Rotate(1);
            else if (wheel < -0.01f) hovered.Rotate(-1);
            if (Input.GetMouseButtonDown(0))
                hovered.Rotate(localHit.x < 0f ? -1 : 1);
        }
        catch (System.InvalidOperationException exception)
        {
            inputUnavailable = true;
            Debug.LogWarning("Cockpit mouse rotary input requires the legacy Input Manager. " + exception.Message, this);
        }
    }

    private void OnDisable()
    {
        if (hovered != null) hovered.SetHovered(false);
        hovered = null;
    }
}
