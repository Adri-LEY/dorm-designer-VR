using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers the last furniture actions (placed, moved, deleted) so they can be undone ("undo" voice command).
/// Deleting is a soft delete: the object is deactivated and kept, so undo restores exactly the same object
/// (size, colour, rotation). Objects that fall out of the history are destroyed for real.
/// Created automatically the first time it is used.
/// </summary>
public sealed class FurnitureHistory : MonoBehaviour
{
    private enum Kind { Placed, Moved, Deleted }

    private sealed class Entry
    {
        public Kind Kind;
        public GameObject Furniture;
        public Vector3 Position;
        public Quaternion Rotation;
        public string Name;
    }

    [Tooltip("How many actions can be undone.")]
    [Min(1)] public int maxSteps = 20;

    private readonly List<Entry> _entries = new List<Entry>();
    private static FurnitureHistory _instance;

    public static FurnitureHistory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<FurnitureHistory>();
                if (_instance == null && Application.isPlaying)
                {
                    _instance = new GameObject(nameof(FurnitureHistory)).AddComponent<FurnitureHistory>();
                }
            }

            return _instance;
        }
    }

    public bool CanUndo => _entries.Count > 0;

    // ------------------------------------------------------------------ Recording

    public void RecordPlaced(GameObject furniture) => Push(Kind.Placed, furniture);

    /// <summary>Call after a move, with the pose it had before.</summary>
    public void RecordMoved(GameObject furniture, Vector3 previousPosition, Quaternion previousRotation)
    {
        Entry entry = Push(Kind.Moved, furniture);
        if (entry == null) return;
        entry.Position = previousPosition;
        entry.Rotation = previousRotation;
    }

    /// <summary>Soft-deletes the furniture (hidden, can be restored with undo).</summary>
    public void Delete(GameObject furniture)
    {
        if (furniture == null) return;
        Push(Kind.Deleted, furniture);
        furniture.SetActive(false);
    }

    /// <summary>Forgets everything and destroys soft-deleted furniture (e.g. when switching room).</summary>
    public void Clear()
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Kind == Kind.Deleted && entry.Furniture != null) Destroy(entry.Furniture);
        }

        _entries.Clear();
    }

    private Entry Push(Kind kind, GameObject furniture)
    {
        if (furniture == null) return null;

        var entry = new Entry
        {
            Kind = kind,
            Furniture = furniture,
            Position = furniture.transform.position,
            Rotation = furniture.transform.rotation,
            Name = DisplayName(furniture),
        };
        _entries.Add(entry);

        while (_entries.Count > maxSteps)
        {
            Entry dropped = _entries[0];
            _entries.RemoveAt(0);
            if (dropped.Kind == Kind.Deleted && dropped.Furniture != null) Destroy(dropped.Furniture);
        }

        return entry;
    }

    // ------------------------------------------------------------------ Undo

    /// <summary>Undoes the last action. <paramref name="message"/> describes what happened, for the player.</summary>
    public bool TryUndo(ObjectSpawner spawner, out string message)
    {
        while (_entries.Count > 0)
        {
            Entry entry = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);

            // The object may have been destroyed since (room change...): skip to the previous action.
            if (entry.Furniture == null) continue;

            switch (entry.Kind)
            {
                case Kind.Placed:
                    Destroy(entry.Furniture);
                    message = $"Undone: placing {entry.Name}";
                    return true;

                case Kind.Moved:
                    if (!RestorePose(entry, spawner))
                    {
                        message = $"Can't move {entry.Name} back: its old spot is taken.";
                        return false;
                    }

                    message = $"Undone: moving {entry.Name}";
                    return true;

                case Kind.Deleted:
                    entry.Furniture.SetActive(true);
                    if (!RestorePose(entry, spawner))
                    {
                        entry.Furniture.SetActive(false);
                        _entries.Add(entry); // still deleted: keep it undoable
                        message = $"Can't bring {entry.Name} back: its spot is taken.";
                        return false;
                    }

                    message = $"Undone: deleting {entry.Name}";
                    return true;
            }
        }

        message = "Nothing to undo.";
        return false;
    }

    /// <summary>Puts the furniture back at the recorded pose, or at the closest free spot if something is there now.</summary>
    private static bool RestorePose(Entry entry, ObjectSpawner spawner)
    {
        GameObject furniture = entry.Furniture;
        var manipulator = furniture.GetComponentInChildren<ConstrainedFurnitureManipulator>(true);
        var collision = furniture.GetComponent<FurnitureCollisionHandler>();

        Vector3 position = entry.Position;
        Quaternion rotation = entry.Rotation;

        Physics.SyncTransforms();
        bool free = collision == null || IsFreeIgnoringSelf(collision, position, rotation);
        if (!free)
        {
            if (spawner == null || !spawner.TryFindFreePoseNear(furniture, entry.Position, entry.Rotation, out position, out rotation))
            {
                return false;
            }
        }

        if (manipulator != null)
        {
            manipulator.MoveTo(position, rotation.eulerAngles.y);
        }
        else
        {
            furniture.transform.SetPositionAndRotation(position, rotation);
        }

        return true;
    }

    private static bool IsFreeIgnoringSelf(FurnitureCollisionHandler collision, Vector3 position, Quaternion rotation)
    {
        // The object is still at its current pose: it would block itself, so check with it ignored.
        return collision.IsPoseValid(position, rotation);
    }

    private static string DisplayName(GameObject furniture)
    {
        var info = furniture.GetComponent<FurnitureInstanceInfo>();
        return info != null && !string.IsNullOrEmpty(info.ItemName)
            ? FurnitureCatalogUtility.PrettyName(info.ItemName)
            : "the furniture";
    }
}
