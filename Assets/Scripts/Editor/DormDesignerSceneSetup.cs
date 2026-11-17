using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Oculus.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for the room selection, product details and smooth turning.
/// Menu: Tools > Dorm Designer > Set Up Open Scene. Safe to run several times:
/// it only adds what is missing and never overwrites product data you already entered.
/// </summary>
public static class DormDesignerSceneSetup
{
    private const string RoomsFolder = "Assets/DormVRScenes/Data/Rooms";
    private const string VoiceFolder = "Assets/DormVRScenes/Data/Voice";

    [MenuItem("Tools/Dorm Designer/Set Up Open Scene")]
    public static void SetUpOpenScene()
    {
        var report = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();

        RoomBuilder builder = SetUpRoomBuilder(report);
        List<RoomPreset> presets = LoadOrCreateRoomPresets(builder, report);
        TurnSettings turnSettings = SetUpTurnSettings(report);
        SetUpRoomSelection(builder, presets, turnSettings, report);
        FillPlaceholderProductData(report);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Dorm Designer] Scene setup done:\n" + report);
        EditorUtility.DisplayDialog("Dorm Designer setup",
            report + "\nSave the scene (Ctrl+S) to keep the changes.", "OK");
    }

    [MenuItem("Tools/Dorm Designer/Fill Missing Product Data")]
    public static void FillMissingProductDataMenu()
    {
        var report = new StringBuilder();
        FillPlaceholderProductData(report);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Dorm Designer] " + report);
    }

    // ------------------------------------------------------------------ Voice

    [MenuItem("Tools/Dorm Designer/Set Up Voice Commands")]
    public static void SetUpVoiceCommands()
    {
        var report = new StringBuilder();

        VoiceCommandController controller = Object.FindFirstObjectByType<VoiceCommandController>(FindObjectsInactive.Include);
        GameObject host = controller != null ? controller.gameObject : GameObject.Find("VoiceCommands");
        if (host == null)
        {
            host = new GameObject("VoiceCommands");
            Undo.RegisterCreatedObjectUndo(host, "Create VoiceCommands");
            report.AppendLine("- Created the 'VoiceCommands' object.");
        }

        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        Transform rightController = FindChild(rig, "RightControllerAnchor");
        Transform head = FindChild(rig, "CenterEyeAnchor");

        // Speech: Meta Voice SDK + Wit.ai configuration.
        AppVoiceExperience voice = Object.FindFirstObjectByType<AppVoiceExperience>(FindObjectsInactive.Include);
        if (voice == null)
        {
            voice = Undo.AddComponent<AppVoiceExperience>(host);
            report.AppendLine("- Added AppVoiceExperience (Meta Voice SDK).");
        }

        Undo.RecordObject(voice, "Set up AppVoiceExperience");
        if (voice.RuntimeConfiguration == null) voice.RuntimeConfiguration = new WitRuntimeConfiguration();
        if (voice.RuntimeConfiguration.witConfiguration == null)
        {
            string[] configs = AssetDatabase.FindAssets("t:WitConfiguration");
            if (configs.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(configs[0]);
                voice.RuntimeConfiguration.witConfiguration = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
                report.AppendLine($"- Using Wit.ai configuration '{path}'.");
            }
            else
            {
                report.AppendLine("- WARNING: no Wit.ai configuration found. Create one with Meta > Voice SDK > Get Started " +
                                  "(paste your Wit.ai app's Server Access Token), then run this command again.");
            }
        }

        EditorUtility.SetDirty(voice);

        PointingTarget pointing = GetOrAdd<PointingTarget>(host);
        Undo.RecordObject(pointing, "Set up PointingTarget");
        if (pointing.pointer == null) pointing.pointer = rightController;
        EditorUtility.SetDirty(pointing);

        VoiceFeedbackDisplay feedback = GetOrAdd<VoiceFeedbackDisplay>(host);
        Undo.RecordObject(feedback, "Set up VoiceFeedbackDisplay");
        if (feedback.headCamera == null) feedback.headCamera = head;
        if (feedback.markerMaterial == null) feedback.markerMaterial = GetOrCreateMarkerMaterial(feedback.markerColor);
        EditorUtility.SetDirty(feedback);

        if (controller == null)
        {
            controller = Undo.AddComponent<VoiceCommandController>(host);
            report.AppendLine("- Added VoiceCommandController (hold left X, point, speak).");
        }

        Undo.RecordObject(controller, "Set up VoiceCommandController");
        if (controller.voice == null) controller.voice = voice;
        if (controller.catalog == null) controller.catalog = Object.FindFirstObjectByType<FurnitureMenuManager>(FindObjectsInactive.Include);
        if (controller.spawner == null) controller.spawner = Object.FindFirstObjectByType<ObjectSpawner>(FindObjectsInactive.Include);
        if (controller.pointing == null) controller.pointing = pointing;
        if (controller.feedback == null) controller.feedback = feedback;
        EditorUtility.SetDirty(controller);

        if (rightController == null) report.AppendLine("- WARNING: RightControllerAnchor not found; assign PointingTarget > Pointer.");
        if (head == null) report.AppendLine("- WARNING: CenterEyeAnchor not found; assign VoiceFeedbackDisplay > Head Camera.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string text = report.Length > 0 ? report.ToString() : "- Everything was already set up.\n";
        Debug.Log("[Dorm Designer] Voice setup:\n" + text);
        EditorUtility.DisplayDialog("Dorm Designer voice setup", text + "\nSave the scene (Ctrl+S) to keep the changes.", "OK");
    }

    private static T GetOrAdd<T>(GameObject host) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        return existing != null ? existing : Undo.AddComponent<T>(host);
    }

    private static Transform FindChild(Component root, string childName)
    {
        return root != null
            ? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName)
            : null;
    }

    private static Material GetOrCreateMarkerMaterial(Color color)
    {
        string path = VoiceFolder + "/VoiceTargetMarker.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        EnsureFolder(VoiceFolder);
        material = new Material(shader) { name = "VoiceTargetMarker" };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }

    // ------------------------------------------------------------------ Room

    private static RoomBuilder SetUpRoomBuilder(StringBuilder report)
    {
        RoomBuilder builder = Object.FindFirstObjectByType<RoomBuilder>(FindObjectsInactive.Include);
        if (builder == null)
        {
            GameObject room = GameObject.Find("Room");
            if (room == null)
            {
                report.AppendLine("- ERROR: no 'Room' object found (expected Floor + Wall_Left/Right/Front/Back under it).");
                return null;
            }

            builder = Undo.AddComponent<RoomBuilder>(room);
            report.AppendLine("- Added RoomBuilder to 'Room'.");
        }

        Undo.RecordObject(builder, "Set up RoomBuilder");
        Transform root = builder.transform;
        if (builder.floor == null) builder.floor = root.Find("Floor");
        if (builder.wallLeft == null) builder.wallLeft = root.Find("Wall_Left");
        if (builder.wallRight == null) builder.wallRight = root.Find("Wall_Right");
        if (builder.wallFront == null) builder.wallFront = root.Find("Wall_Front");
        if (builder.wallBack == null) builder.wallBack = root.Find("Wall_Back");

        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        if (rig != null)
        {
            if (builder.playerRig == null) builder.playerRig = rig.transform;
            if (builder.playerHead == null)
            {
                // centerEyeAnchor is only assigned at runtime, so look the anchor up by name in edit mode.
                builder.playerHead = rig.centerEyeAnchor != null
                    ? rig.centerEyeAnchor
                    : rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CenterEyeAnchor");
            }
        }

        EditorUtility.SetDirty(builder);
        if (builder.floor == null || builder.wallLeft == null || builder.wallRight == null ||
            builder.wallFront == null || builder.wallBack == null)
        {
            report.AppendLine("- WARNING: some RoomBuilder walls/floor are not assigned; check the Inspector.");
        }

        return builder;
    }

    private static List<RoomPreset> LoadOrCreateRoomPresets(RoomBuilder builder, StringBuilder report)
    {
        List<RoomPreset> presets = FindRoomPresets();
        if (presets.Count > 0)
        {
            report.AppendLine($"- Using {presets.Count} existing room preset(s) in {RoomsFolder}.");
            return presets;
        }

        EnsureFolder(RoomsFolder);

        CreatePreset("01_CrousTraditional", "CROUS traditional room", 2.6f, 3.6f, 2.5f,
            "Classic single room (~9 m²). Bathroom and kitchen are shared on the floor.");
        CreatePreset("02_RenovatedRoom", "Renovated room with shower", 3.0f, 4.2f, 2.5f,
            "Refurbished single room (~13 m²). Keep space near the entrance for the shower.");
        CreatePreset("03_T1Studio", "T1 studio", 3.5f, 5.2f, 2.5f,
            "Studio (~18 m²) with kitchenette: bed, desk and a place to eat.");
        CreatePreset("04_T1Bis", "T1 bis / shared studio", 4.2f, 6.0f, 2.6f,
            "Larger studio (~25 m²), often shared by two students.");

        // Keep the original prototype room as an option, measured from the current floor.
        if (builder != null && builder.floor != null)
        {
            Vector3 floorSize = builder.floor.lossyScale;
            float height = builder.wallLeft != null ? builder.wallLeft.lossyScale.y : 2.6f;
            CreatePreset("05_Prototype", "Prototype room",
                Mathf.Round(floorSize.x * 10f) / 10f, Mathf.Round(floorSize.z * 10f) / 10f, height,
                "The original test room of the project.");
        }

        AssetDatabase.SaveAssets();
        presets = FindRoomPresets();
        report.AppendLine($"- Created {presets.Count} room presets in {RoomsFolder}.");
        return presets;
    }

    private static List<RoomPreset> FindRoomPresets()
    {
        if (!AssetDatabase.IsValidFolder(RoomsFolder)) return new List<RoomPreset>();

        return AssetDatabase.FindAssets("t:RoomPreset", new[] { RoomsFolder })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .OrderBy(path => path)
            .Select(path => AssetDatabase.LoadAssetAtPath<RoomPreset>(path))
            .Where(preset => preset != null)
            .ToList();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void CreatePreset(string fileName, string displayName, float width, float depth, float height, string description)
    {
        var preset = ScriptableObject.CreateInstance<RoomPreset>();
        preset.displayName = displayName;
        preset.width = width;
        preset.depth = depth;
        preset.height = height;
        preset.description = description;
        AssetDatabase.CreateAsset(preset, $"{RoomsFolder}/{fileName}.asset");
    }

    // ------------------------------------------------------------------ Turning

    private static TurnSettings SetUpTurnSettings(StringBuilder report)
    {
        TurnSettings settings = Object.FindFirstObjectByType<TurnSettings>(FindObjectsInactive.Include);
        if (settings != null) return settings;

        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        GameObject host = rig != null ? rig.gameObject : new GameObject("TurnSettings");
        if (rig == null) Undo.RegisterCreatedObjectUndo(host, "Create TurnSettings");

        settings = Undo.AddComponent<TurnSettings>(host);
        report.AppendLine($"- Added TurnSettings (smooth turning) to '{host.name}'.");
        return settings;
    }

    // ------------------------------------------------------------------ Menu

    private static void SetUpRoomSelection(RoomBuilder builder, List<RoomPreset> presets, TurnSettings turnSettings, StringBuilder report)
    {
        VRMenuController menuController = Object.FindFirstObjectByType<VRMenuController>(FindObjectsInactive.Include);
        if (menuController == null || menuController.uiMenu == null)
        {
            report.AppendLine("- ERROR: no VRMenuController with a UI Menu canvas found; room selection not set up.");
            return;
        }

        RoomSelectionMenu menu = Object.FindFirstObjectByType<RoomSelectionMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            menu = Undo.AddComponent<RoomSelectionMenu>(menuController.gameObject);
            report.AppendLine($"- Added RoomSelectionMenu to '{menuController.gameObject.name}'.");
        }

        Undo.RecordObject(menu, "Set up RoomSelectionMenu");
        if (menu.rooms.Count == 0) menu.rooms.AddRange(presets);
        if (menu.roomBuilder == null) menu.roomBuilder = builder;
        if (menu.menuController == null) menu.menuController = menuController;
        if (menu.canvasRoot == null) menu.canvasRoot = menuController.uiMenu.transform as RectTransform;
        if (menu.tabBar == null && menu.canvasRoot != null) menu.tabBar = menu.canvasRoot.Find("TabBar") as RectTransform;
        if (menu.spawner == null) menu.spawner = Object.FindFirstObjectByType<ObjectSpawner>(FindObjectsInactive.Include);
        if (menu.turnSettings == null) menu.turnSettings = turnSettings;
        EditorUtility.SetDirty(menu);
    }

    // ------------------------------------------------------------------ Product data

    private static void FillPlaceholderProductData(StringBuilder report)
    {
        FurnitureMenuManager manager = Object.FindFirstObjectByType<FurnitureMenuManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            report.AppendLine("- No FurnitureMenuManager found; product data not filled.");
            return;
        }

        Undo.RecordObject(manager, "Fill placeholder product data");
        int filled = 0;
        foreach (FurnitureItem item in manager.allFurniture)
        {
            if (item == null) continue;
            bool changed = false;

            if (string.IsNullOrEmpty(item.description))
            {
                item.description = Placeholder.Description(item);
                changed = true;
            }

            if (item.sizes == null || item.sizes.Count == 0)
            {
                item.sizes = Placeholder.Sizes(item);
                changed = true;
            }

            if (item.colors == null || item.colors.Count == 0)
            {
                item.colors = Placeholder.Colors(item);
                changed = true;
            }

            if (changed) filled++;
        }

        EditorUtility.SetDirty(manager);
        report.AppendLine(filled > 0
            ? $"- Filled placeholder price / size / colour data for {filled} catalogue item(s) (edit them on '{manager.gameObject.name}')."
            : "- All catalogue items already had product data; nothing overwritten.");
    }

    /// <summary>Plausible student-budget placeholder data. Replace with real products.</summary>
    private static class Placeholder
    {
        private static string Kind(FurnitureItem item)
        {
            string text = ((item.category ?? string.Empty) + " " + (item.itemName ?? string.Empty)).ToLowerInvariant();
            // Category wins over name (e.g. an item named "Modern_Bed" in the Desk category is a desk).
            string category = (item.category ?? string.Empty).ToLowerInvariant();
            foreach (string kind in new[] { "bed", "chair", "desk", "wardrobe" })
            {
                if (category.Contains(kind)) return kind;
            }

            foreach (string kind in new[] { "wardrobe", "desk", "chair", "bed" })
            {
                if (text.Contains(kind)) return kind;
            }

            return "other";
        }

        private static bool Has(FurnitureItem item, string word) =>
            (item.itemName ?? string.Empty).ToLowerInvariant().Contains(word);

        public static string Description(FurnitureItem item)
        {
            switch (Kind(item))
            {
                case "bed":
                    return Has(item, "blue")
                        ? "Upholstered bed with padded headboard and slatted base. Mattress sold separately."
                        : "Solid pine bed frame with slatted base. Mattress sold separately.";
                case "chair":
                    return Has(item, "red")
                        ? "Light stackable chair, easy to move. Plastic seat on steel legs."
                        : "Desk chair with adjustable seat height and swivel base.";
                case "desk":
                    return "Study desk with room for a laptop and a monitor. Cable hole at the back.";
                case "wardrobe":
                    return Has(item, "double")
                        ? "Two-door wardrobe with hanging rail and shelves."
                        : "Narrow one-door wardrobe for small rooms, with hanging rail and shelf.";
                default:
                    return "Placeholder description. Replace it with the real product details.";
            }
        }

        public static List<FurnitureSizeOption> Sizes(FurnitureItem item)
        {
            // The default size is the model's own size, so the first option never distorts the model.
            FurnitureCatalogUtility.TryGetModelSize(item.prefab, out Vector3 model);
            float a = Mathf.Max(model.x, model.z) * 100f; // longer horizontal side, cm
            float b = Mathf.Min(model.x, model.z) * 100f; // shorter horizontal side, cm
            float h = model.y * 100f;
            if (a < 1f) { a = 100f; b = 50f; h = 75f; }

            switch (Kind(item))
            {
                case "bed":
                {
                    bool blue = Has(item, "blue");
                    return new List<FurnitureSizeOption>
                    {
                        Size("Single", b, a, h, blue ? 189f : 139f),
                        Size("Double", b + 50f, a, h, blue ? 269f : 219f),
                    };
                }
                case "chair":
                    return new List<FurnitureSizeOption> { Size("Standard", a, b, h, Has(item, "red") ? 29f : 59f) };
                case "desk":
                    return new List<FurnitureSizeOption>
                    {
                        Size("Standard", a, b, h, 89f),
                        Size("Large", a + 40f, b, h, 119f),
                    };
                case "wardrobe":
                {
                    bool twoDoors = Has(item, "double");
                    return new List<FurnitureSizeOption>
                    {
                        Size("Standard", a, b, h, twoDoors ? 179f : 99f),
                        Size("Tall", a, b, h + 35f, twoDoors ? 219f : 129f),
                    };
                }
                default:
                    return new List<FurnitureSizeOption> { Size("Standard", a, b, h, 49f) };
            }
        }

        public static List<FurnitureColorOption> Colors(FurnitureItem item)
        {
            var colors = new List<FurnitureColorOption>
            {
                new FurnitureColorOption { label = "Original", color = Color.white, mode = FurnitureColorMode.Original },
            };

            switch (Kind(item))
            {
                case "bed":
                    colors.Add(Tint("Light grey", 0.82f, 0.83f, 0.85f));
                    colors.Add(Tint("Sage green", 0.66f, 0.78f, 0.64f));
                    colors.Add(Tint("Dusty blue", 0.62f, 0.72f, 0.86f));
                    break;
                case "chair":
                    colors.Add(Tint("Graphite", 0.40f, 0.41f, 0.44f));
                    colors.Add(Tint("Ocean blue", 0.45f, 0.62f, 0.90f));
                    colors.Add(Tint("Mustard", 0.92f, 0.76f, 0.35f));
                    break;
                case "desk":
                case "wardrobe":
                    colors.Add(Tint("Light oak", 0.93f, 0.84f, 0.68f));
                    colors.Add(Tint("Walnut", 0.62f, 0.46f, 0.34f));
                    colors.Add(Tint("Black-brown", 0.38f, 0.34f, 0.32f));
                    break;
                default:
                    colors.Add(Tint("Grey", 0.7f, 0.7f, 0.7f));
                    break;
            }

            return colors;
        }

        private static FurnitureSizeOption Size(string label, float width, float depth, float height, float price) =>
            new FurnitureSizeOption
            {
                label = label,
                widthCm = Mathf.Round(width),
                depthCm = Mathf.Round(depth),
                heightCm = Mathf.Round(height),
                price = price,
            };

        private static FurnitureColorOption Tint(string label, float r, float g, float b) =>
            new FurnitureColorOption { label = label, color = new Color(r, g, b, 1f), mode = FurnitureColorMode.Tint };
    }
}
