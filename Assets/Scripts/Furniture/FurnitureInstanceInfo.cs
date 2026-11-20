using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Added to every spawned furniture. Remembers which product, size and colour it is
/// (for a future shopping list / budget) and cleans up the materials created for it.
/// </summary>
public sealed class FurnitureInstanceInfo : MonoBehaviour
{
    private static readonly List<FurnitureInstanceInfo> _all = new List<FurnitureInstanceInfo>();

    /// <summary>Every furniture currently in the room.</summary>
    public static IReadOnlyList<FurnitureInstanceInfo> All => _all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _all.Clear();

    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField] public string Category { get; private set; }
    [field: SerializeField] public string SizeLabel { get; private set; }
    [field: SerializeField] public string ColorLabel { get; private set; }
    [field: SerializeField] public float Price { get; private set; }

    private readonly List<Material> _ownedMaterials = new List<Material>();

    public void Initialize(FurnitureItem item, FurnitureSizeOption size, FurnitureColorOption color, IEnumerable<Material> ownedMaterials)
    {
        ItemName = item != null ? item.itemName : string.Empty;
        Category = item != null ? item.category : string.Empty;
        SizeLabel = size != null ? size.label : string.Empty;
        ColorLabel = color != null ? color.label : string.Empty;
        Price = size != null ? size.price : (item != null ? item.basePrice : 0f);

        if (ownedMaterials != null) _ownedMaterials.AddRange(ownedMaterials);
    }

    private void OnEnable() => _all.Add(this);

    private void OnDisable() => _all.Remove(this);

    private void OnDestroy()
    {
        foreach (Material material in _ownedMaterials)
        {
            if (material != null) Destroy(material);
        }

        _ownedMaterials.Clear();
    }
}
