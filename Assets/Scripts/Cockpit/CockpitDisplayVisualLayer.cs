using System;
using System.IO;
using System.Text;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional image layer for cockpit display canvases. Accepts imported Sprites or
/// PNG/JPG/SVG files loaded from a path; it never changes a shared material.
/// </summary>
[DisallowMultipleComponent]
public sealed class CockpitDisplayVisualLayer : MonoBehaviour
{
    private const int MaxFileBytes = 16 * 1024 * 1024;
    private const int SvgRasterViewport = 2048;
    private Image image;
    private CockpitSvgGraphic svgGraphic;
    private Sprite ownedSprite;
    private Texture2D ownedTexture;

    /// <summary>Create a full-size, transparent-by-default visual layer behind display symbology.</summary>
    public static CockpitDisplayVisualLayer Create(Transform parent, Vector2 size)
    {
        GameObject layerObject = new GameObject("UserVisualLayer", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(CockpitDisplayVisualLayer));
        layerObject.transform.SetParent(parent, false);
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        layerObject.transform.SetAsFirstSibling();

        CockpitDisplayVisualLayer layer = layerObject.GetComponent<CockpitDisplayVisualLayer>();
        layer.image = layerObject.GetComponent<Image>();
        GameObject svgObject = new GameObject("SvgVisual", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(CockpitSvgGraphic));
        svgObject.transform.SetParent(layerObject.transform, false);
        RectTransform svgRect = svgObject.GetComponent<RectTransform>();
        svgRect.anchorMin = Vector2.zero;
        svgRect.anchorMax = Vector2.one;
        svgRect.offsetMin = Vector2.zero;
        svgRect.offsetMax = Vector2.zero;
        layer.svgGraphic = svgObject.GetComponent<CockpitSvgGraphic>();
        layer.image.enabled = false;
        layer.image.raycastTarget = false;
        layer.image.preserveAspect = true;
        layer.svgGraphic.enabled = false;
        layer.svgGraphic.raycastTarget = false;
        return layer;
    }

    /// <summary>Load PNG, JPG/JPEG, or SVG from an absolute or project-resolved file path.</summary>
    public bool LoadFromFile(string path, float opacity = 1f)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Fail("Image path is empty.");

        try
        {
            if (!File.Exists(path))
                return Fail("Image file does not exist: " + path);

            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaxFileBytes)
                return Fail("Image file must be between 1 byte and 16 MB.");

            string extension = Path.GetExtension(path).ToLowerInvariant();
            byte[] bytes = File.ReadAllBytes(path);
            Sprite sprite;
            Texture2D texture = null;

            if (extension == ".svg")
            {
                LoadSvg(bytes, out var geometry, out Rect viewport);
                SetSvgVisual(geometry, viewport, opacity);
                return true;
            }
            else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    DestroyOwnedUnityObject(texture);
                    return Fail("Unity could not decode this PNG/JPG image.");
                }
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                return Fail("Supported file extensions are .png, .jpg, .jpeg, and .svg.");
            }

            if (sprite == null)
            {
                if (texture != null)
                    DestroyOwnedUnityObject(texture);
                return Fail("The image could not be converted to a Unity Sprite.");
            }

            SetOwnedVisual(sprite, texture, opacity);
            return true;
        }
        catch (Exception exception)
        {
            return Fail("Could not load image: " + exception.Message);
        }
    }

    /// <summary>Display an imported Sprite asset, including an SVG imported by Unity's SVG importer.</summary>
    public void SetSprite(Sprite sprite, float opacity = 1f)
    {
        ReleaseOwnedVisual();
        EnsureImage();
        ClearSvgVisual();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
        image.enabled = sprite != null;
    }

    /// <summary>Display a Texture2D asset without taking ownership of the texture.</summary>
    public void SetTexture(Texture2D texture, float opacity = 1f)
    {
        if (texture == null)
        {
            Clear();
            return;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        ReleaseOwnedVisual();
        EnsureImage();
        ClearSvgVisual();
        ownedSprite = sprite;
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
        image.enabled = true;
    }

    /// <summary>Remove the optional visual and release any texture created from a file.</summary>
    public void Clear()
    {
        EnsureImage();
        image.sprite = null;
        image.enabled = false;
        ClearSvgVisual();
        ReleaseOwnedVisual();
    }

    private static void LoadSvg(byte[] bytes, out System.Collections.Generic.List<VectorUtils.Geometry> geometry,
        out Rect viewport)
    {
        string svgText = Encoding.UTF8.GetString(bytes);
        SVGParser.SceneInfo sceneInfo;
        using (StringReader reader = new StringReader(svgText))
            sceneInfo = SVGParser.ImportSVG(reader, 96f, 1f, SvgRasterViewport, SvgRasterViewport, true);

        VectorUtils.TessellationOptions options = new VectorUtils.TessellationOptions
        {
            StepDistance = 8f,
            MaxCordDeviation = 0.5f,
            MaxTanAngleDeviation = 0.1f,
            SamplingStepSize = 4f
        };
        geometry = VectorUtils.TessellateScene(sceneInfo.Scene, options);
        viewport = sceneInfo.SceneViewport;
        if (geometry == null || geometry.Count == 0 || viewport.width <= 0f || viewport.height <= 0f)
            throw new InvalidDataException("SVG has no renderable shapes or a valid viewBox.");
    }

    private void SetSvgVisual(System.Collections.Generic.List<VectorUtils.Geometry> geometry, Rect viewport,
        float opacity)
    {
        ReleaseOwnedVisual();
        EnsureImage();
        image.sprite = null;
        image.enabled = false;
        svgGraphic.SetGeometry(geometry, viewport);
        svgGraphic.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
        svgGraphic.enabled = true;
    }

    private void SetOwnedVisual(Sprite sprite, Texture2D texture, float opacity)
    {
        ReleaseOwnedVisual();
        EnsureImage();
        ClearSvgVisual();
        ownedSprite = sprite;
        ownedTexture = texture;
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
        image.enabled = true;
    }

    private void EnsureImage()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (svgGraphic == null)
            svgGraphic = GetComponent<CockpitSvgGraphic>();
    }

    private void ClearSvgVisual()
    {
        if (svgGraphic == null)
            svgGraphic = GetComponent<CockpitSvgGraphic>();
        if (svgGraphic == null)
            return;
        svgGraphic.ClearGeometry();
        svgGraphic.enabled = false;
    }

    private bool Fail(string message)
    {
        Debug.LogWarning("Cockpit display visual: " + message, this);
        return false;
    }

    private void ReleaseOwnedVisual()
    {
        if (ownedSprite != null)
            DestroyOwnedUnityObject(ownedSprite);
        if (ownedTexture != null)
            DestroyOwnedUnityObject(ownedTexture);
        ownedSprite = null;
        ownedTexture = null;
    }

    private void OnDestroy() => ReleaseOwnedVisual();

    private static void DestroyOwnedUnityObject(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
