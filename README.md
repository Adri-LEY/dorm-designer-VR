# dorm-designer-VR

## Setting up a scene (run once after pulling)

Open `Assets/DormVRScenes/Main scene.unity`, then **Tools > Dorm Designer > Set Up Open Scene**, then save the scene. The command is safe to run again: it only adds what is missing and never overwrites product data you have already entered. It:

- adds `RoomBuilder` to the `Room` object and links the floor and the 4 walls;
- creates room presets in `Assets/DormVRScenes/Data/Rooms/` (only if that folder is empty);
- adds `RoomSelectionMenu` next to `VRMenuController` (`UIMenuManager`) and `TurnSettings` to the camera rig;
- fills placeholder description / sizes / prices / colours for catalogue items that have none (`MenuManager > FurnitureMenuManager > All Furniture`).

## Room selection

At launch the menu opens in front of the player on a "Choose your room" page and stays open until a room is picked. `RoomBuilder` then resizes the floor and walls to the preset's inner dimensions (the floor top stays at the `Room` object's height) and moves the player near the -Z wall, facing +Z, through Meta's `FirstPersonLocomotor`. The furniture menu then opens in front of the player.

A **Room** tab in the furniture menu brings the page back. Switching to another room removes the placed furniture, so the first tap on a room card asks for confirmation when furniture is present. The page also has the **Turning: Smooth / Snap** toggle.

Add a room: *Assets > Create > Dorm Designer > Room Preset*, fill in name, description and inner width (X) / depth (Z) / height in metres, and add it to `RoomSelectionMenu > Rooms`. Optional wall/floor colours apply when their alpha is above 0.

## Furniture product sheet

Clicking an item in the catalogue opens a product sheet (built at runtime inside the same canvas) with the picture, price, real dimensions, description, size buttons and colour swatches. **Place in room** spawns the item with the chosen options:

- **Sizes** (`FurnitureSizeOption`: label, width/depth/height in cm, price). The model is rescaled per axis to the real dimensions. The longer horizontal dimension is matched to the model's longer side, so it doesn't matter whether a model was authored along X or Z. With no sizes, the model's own size and `Base Price` are shown.
- **Colours** (`FurnitureColorOption`): *Original* keeps the materials, *Tint* multiplies the texture by the colour, *Solid* removes the texture, *CustomMaterial* uses `Override Material`. `Material Index` limits the change to one material slot (-1 = all).

### Placing: free-space check and menu closing

Before creating anything, `ObjectSpawner` searches for a free spot for the furniture's box at its final size. It starts straight ahead at `Spawn Distance`, then nearer or further, then to the sides (up to `Max Search Angle`, 90° by default), and finally turned by 90° for long items. A spot is accepted only if it is on the floor, not behind a wall, at least `Personal Space` from the head, and passes `FurnitureCollisionHandler.IsBoxFree`, the same rule used while moving furniture. That way a spawned piece can always be moved. If no spot is free, nothing is spawned and the product sheet shows a warning ("No free space for ... here"). Picking another size or colour hides it.

After a successful placement the sheet closes and the menu closes too (`FurnitureMenuManager > Close Menu After Placing`). Press the menu button to reopen it on the grid.

Each spawned furniture gets a `FurnitureInstanceInfo` (product, size, colour, price), which a future shopping list / budget view can read through `FurnitureInstanceInfo.All`. The placeholder data from the setup command uses the model's own size as the default option, so nothing is distorted until real dimensions are entered.

## Camera turning and moving

`TurnSettings` switches every Meta `TurnerEventBroadcaster` in the rig to **smooth** turning (75°/s at full deflection by default, editable on the component). The Smooth / Snap choice is saved in `PlayerPrefs`. Camera turning is paused while a furniture item is selected, so the sticks only move and rotate the furniture.

**Pointing no longer blocks locomotion.** Meta's `BaseInteractors` prefab has a `ScrollLocomotionGate` whose `ActiveStateTracker` deactivates a controller's whole `LocomotionControllerInteractorGroup` (turn, teleport, slide) as soon as that controller's ray hovers anything. `LocomotionGate`, added automatically next to `TurnSettings`, replaces that tracker's condition at runtime (the prefab is in the package cache, so it can't be edited). Locomotion is now available while pointing at the menu or furniture. It is turned off on **both** controllers only while a furniture item is selected, so you no longer need to point both controllers at the item. Meta's other tracker in the same gate still hides the ray while you are turning or moving. Hand-tracking locomotion (`HoverInteractorsGate`) is unchanged. Other features can block locomotion temporarily with `LocomotionGate.AddBlocker(key)` / `RemoveBlocker(key)`.


## Joystick furniture manipulation

Furniture keeps its Meta Interaction SDK ray hover and select interaction. While the ray is selecting a piece (right trigger), holding the right grip enables manipulation:

- The right stick moves the object in room/world X and Z; the tracked hand/controller pose is not used.
- The left stick turns the object smoothly around Y (90°/s at full deflection; small deflections turn slower for precise alignment). The old one-step-per-deflection behaviour is still available: set **Rotation Mode** to *Snap* on `ConstrainedFurnitureManipulator`.
- Releasing the right grip confirms the current position. Releasing the trigger ends Meta ray selection.
- B cancels the current selection/manipulation and restores the position and yaw from the start of the grip. Release and reselect to start again.
- Press the right controller's A button while the furniture is selected to delete it. The left menu/catalogue action is reserved for later.

The root `ConstrainedFurnitureManipulator` is the only component that writes the furniture Transform. `FurnitureManipulationState` reads Meta's `RayInteractable` selection state, and `FurnitureControllerInput` samples Quest Touch controls through `OVRInput`. The manipulator fixes world Y and sets world rotation to `(0, yaw, 0)`. Its Inspector fields configure movement speed, rotation mode/speed/snap angle, and deadzone. The Rigidbody is made kinematic with gravity disabled; the Meta ray's `MoveFromTargetProvider` is removed/cleared, and Meta's default pose-follow `Grabbable` transformer is replaced at runtime with a no-op transformer. The legacy `FreezeRotationOnGrab` and `FurnitureCollisionHandler` callbacks remain compatible with serialized scenes but no longer write the furniture Transform.

### Quest 3 verification

1. Aim the right ray at furniture and verify it gets a subtle hover tint. Hold the right trigger and verify it turns white; release the trigger and verify it returns to the hover tint while still pointed at, then to its original color when the ray moves away.
2. Keep the trigger held, hold the right grip, and move the right stick left/right. The furniture should move on world X.
3. Keep the right grip held and move the right stick up/down. The furniture should move on world Z.
4. Hold the left stick left or right while the furniture is selected and gripped. Each deflection should rotate by 15 degrees only once; return to neutral between steps.
5. Release the right grip to confirm placement, then release the trigger to end selection and clear the white selection highlight. While the furniture is selected, press the right A button to delete it.
6. During a grip, verify Y stays fixed and X/Z rotation remains zero. Tilt or rotate either physical controller while using the sticks; furniture movement and yaw should still follow only the stick axes.
7. Press B while manipulating to restore the position and yaw from the start of that grip. Verify the existing furniture prefab still highlights/selects and its mesh/collider remain intact.

## Furniture audio feedback

The persistent `AudioManager` is automatically loaded from `Assets/Resources/Audio/AudioManager.prefab` before the scene starts, so it does not need to be placed in each scene. Its Inspector fields configure the event clips, master/event volumes, optional movement-stop confirmation, and repeat/collision cooldowns. It uses one shared 2D source for UI/manipulation feedback and one shared spatial source for wall impacts. No per-furniture AudioSources are created.

`InteractionFeedback` keeps the existing Meta selection and deselection callbacks and forwards them to the manager. `ConstrainedFurnitureManipulator` plays movement feedback only on grip start/end and deletion immediately before destruction. Wall impacts are detected from the existing blocked-pose overlap check, only for colliders named with "Wall", and are debounced until furniture moves away; the manager adds a cooldown as a second guard.

The selected sounds are in `Assets/Audio/SFX/`; their source and CC0 license are documented in that folder. To verify in Unity/Quest, exercise selection/deselection, grip start/release, deletion, and repeated pushes into then away from a wall. Movement-stop confirmation is disabled by default and can be enabled on the AudioManager prefab.
