using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// One purchasable size of a furniture item. Dimensions are the real product's
/// outer dimensions; the spawned model is rescaled to match them.
/// </summary>
[Serializable]
public class FurnitureSizeOption
{
    [Tooltip("Label shown on the size button, e.g. \"Single\", \"Double\", \"Tall\".")]
    public string label = "Standard";

    [Tooltip("Width in centimetres (side to side when facing the item).")]
    [Min(1f)] public float widthCm = 100f;

    [Tooltip("Depth in centimetres (front to back).")]
    [Min(1f)] public float depthCm = 50f;

    [Tooltip("Height in centimetres.")]
    [Min(1f)] public float heightCm = 75f;

    [Tooltip("Price of this size, in euros.")]
    [Min(0f)] public float price;

    /// <summary>Real size in metres as (width, height, depth).</summary>
    public Vector3 SizeMeters => new Vector3(widthCm, heightCm, depthCm) / 100f;
}

/// <summary>How a colour option changes the model's materials.</summary>
public enum FurnitureColorMode
{
    [Tooltip("Keep the model's own materials (the default finish).")]
    Original,
    [Tooltip("Multiply the existing texture by the colour (keeps wood grain, fabric, etc.).")]
    Tint,
    [Tooltip("Remove the texture and use the flat colour.")]
    Solid,
    [Tooltip("Replace the material with Override Material.")]
    CustomMaterial
}

/// <summary>One purchasable colour / finish of a furniture item.</summary>
[Serializable]
public class FurnitureColorOption
{
    public string label = "Original";

    [Tooltip("Swatch colour in the menu, and the colour applied in Tint / Solid mode.")]
    public Color color = Color.white;

    public FurnitureColorMode mode = FurnitureColorMode.Original;

    [Tooltip("Used only in CustomMaterial mode.")]
    public Material overrideMaterial;

    [Tooltip("Material slot to change. -1 = every slot.")]
    public int materialIndex = -1;
}

/// <summary>Shared helpers for sizing, colouring and formatting catalogue items.</summary>
public static class FurnitureCatalogUtility
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    /// <summary>
    /// Size of the model as it currently spawns (mesh bounds x scale), in metres,
    /// as (x, y, z) in the mesh's local axes. Returns false if the prefab has no mesh.
    /// </summary>
    public static bool TryGetModelSize(GameObject prefab, out Vector3 sizeMeters)
    {
        sizeMeters = Vector3.zero;
        if (prefab == null) return false;

        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return false;

        Vector3 bounds = meshFilter.sharedMesh.bounds.size;
        Vector3 scale = meshFilter.transform.lossyScale;
        sizeMeters = new Vector3(
            bounds.x * Mathf.Abs(scale.x),
            bounds.y * Mathf.Abs(scale.y),
            bounds.z * Mathf.Abs(scale.z));
        return true;
    }

    /// <summary>
    /// Local scale to give the spawned furniture root so its mesh matches the
    /// requested real size. The longer requested horizontal dimension is matched to
    /// the mesh's longer horizontal axis, so it works whichever way the model was
    /// authored (a bed's length can be on X or Z).
    /// </summary>
    public static Vector3 ComputeScaleForSize(Mesh mesh, Vector3 currentScale, FurnitureSizeOption size)
    {
        if (mesh == null || size == null) return currentScale;

        Vector3 bounds = mesh.bounds.size;
        if (bounds.x < 1e-4f || bounds.y < 1e-4f || bounds.z < 1e-4f) return currentScale;

        float width = size.widthCm / 100f;
        float depth = size.depthCm / 100f;
        float height = size.heightCm / 100f;

        float nativeX = bounds.x * Mathf.Abs(currentScale.x);
        float nativeZ = bounds.z * Mathf.Abs(currentScale.z);
        bool modelXIsLonger = nativeX >= nativeZ;
        bool widthIsLonger = width >= depth;

        float targetX = modelXIsLonger == widthIsLonger ? width : depth;
        float targetZ = modelXIsLonger == widthIsLonger ? depth : width;

        return new Vector3(
            Mathf.Sign(currentScale.x == 0f ? 1f : currentScale.x) * targetX / bounds.x,
            Mathf.Sign(currentScale.y == 0f ? 1f : currentScale.y) * height / bounds.y,
            Mathf.Sign(currentScale.z == 0f ? 1f : currentScale.z) * targetZ / bounds.z);
    }

    /// <summary>
    /// Builds the material array for a colour option. New material instances are
    /// added to <paramref name="createdMaterials"/> so the caller can destroy them.
    /// </summary>
    public static Material[] BuildMaterials(
        Material[] source,
        FurnitureColorOption option,
        System.Collections.Generic.List<Material> createdMaterials)
    {
        if (source == null || option == null || option.mode == FurnitureColorMode.Original)
        {
            return source;
        }

        Material[] result = (Material[])source.Clone();
        for (int i = 0; i < result.Length; i++)
        {
            if (option.materialIndex >= 0 && i != option.materialIndex) continue;
            if (result[i] == null) continue;

            if (option.mode == FurnitureColorMode.CustomMaterial)
            {
                if (option.overrideMaterial != null) result[i] = option.overrideMaterial;
                continue;
            }

            Material copy = new Material(result[i]) { name = result[i].name + " (" + option.label + ")" };
            if (option.mode == FurnitureColorMode.Solid)
            {
                if (copy.HasProperty(BaseMapId)) copy.SetTexture(BaseMapId, null);
                if (copy.HasProperty(MainTexId)) copy.SetTexture(MainTexId, null);
            }

            if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, option.color);
            if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, option.color);

            createdMaterials?.Add(copy);
            result[i] = copy;
        }

        return result;
    }

    public static string FormatPrice(float price)
    {
        if (price <= 0f) return "Price not set";
        bool whole = Mathf.Approximately(price, Mathf.Round(price));
        return price.ToString(whole ? "0" : "0.00", CultureInfo.InvariantCulture) + " €";
    }

    public static string FormatDimensions(float widthCm, float depthCm, float heightCm)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "W {0:0} x D {1:0} x H {2:0} cm",
            widthCm, depthCm, heightCm);
    }

    public static string PrettyName(string rawName)
    {
        return string.IsNullOrEmpty(rawName) ? "Unnamed item" : rawName.Replace('_', ' ');
    }
}
