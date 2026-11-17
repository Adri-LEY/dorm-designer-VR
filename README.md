# dorm-designer-VR

A room-scale VR furniture planner for Meta Quest 3. Browse the in-world furniture catalogue, place furniture in front of you, and arrange it with the Touch controllers.

## Requirements

- Meta Quest 3 and two Touch controllers.
- A Windows PC that can run Meta Horizon Link.
- A USB-C data cable that supports Link (not a charge-only cable).
- Unity `6000.3.6f1` (the project editor version), installed through Unity Hub.
- The project packages, including Meta XR SDK `205.0.0`, which Unity resolves from `Packages/manifest.json`.

## Run a test with Quest 3 and Unity

This workflow runs the project from the Unity Editor while the headset is connected to the PC through Meta Horizon Link.

1. Install and set up the Meta Horizon Link PC app from [Meta's Quest setup page](https://www.meta.com/quest/setup/). Sign in and complete its initial PC/headset setup.
2. Connect Quest 3 to the PC with the USB-C data cable. Accept any connection prompts in the headset, then open Quick Settings and enter **Link** to connect to the PC. Confirm the headset is shown as connected in the PC app.
3. If Unity does not start VR in the headset, check the PC app's OpenXR runtime setting and make **Meta Quest Link** the active runtime.
4. In Unity Hub, install Unity `6000.3.6f1` and open this repository as a project. Wait for package import and compilation to finish.
5. Open the main scene, `Assets/DormVRScenes/Main scene.unity`.
6. Leave the headset in Link mode, then press **Play** in the Unity Editor. The scene should run through the PC Link connection in the headset. Use the controllers to try the catalogue and furniture controls below.

If the headset is not detected, first verify that the Link PC app reports it connected, that the cable supports data, and that Meta Quest Link is selected as the active OpenXR runtime.

## What you can do

### Open the furniture catalogue and place an item

- Press the **left controller Menu/Start button** to open or close the in-world catalogue. It starts closed and opens in front of the left controller.
- Use the catalogue tabs and item buttons to browse and choose furniture. Choosing an item spawns it in front of the player, facing the player; the spawn point moves closer if an obstacle is in the way.

### Select and arrange furniture

| Controller input | Action |
| --- | --- |
| Right ray + **right trigger** | Point at and select furniture. Keep the trigger held to keep it selected. Hover and selection are shown with outlines. |
| **Right grip** (while selected) | Hold to enable furniture movement and rotation. Release to confirm the current placement. |
| **Right thumbstick** (while gripping) | Move the furniture across the horizontal floor plane, relative to the direction you are facing. Up/down moves forward/back; left/right moves left/right. |
| **Left thumbstick left/right** (while gripping) | Rotate the furniture by 15 degrees for each stick deflection. Return the stick to its deadzone between steps. |
| **B** on the right controller (while selected) | Cancel the current manipulation and restore the position and rotation from the start of that grip. Deselect and select the furniture again before manipulating it again. |
| **A** on the right controller (while selected) | Delete the selected furniture. |
| Release the **right trigger** | End ray selection. |

Movement keeps the furniture at its original height and constrains rotation to upright yaw. Controller pose/tilt does not directly move the furniture; the thumbsticks do.

`ConstrainedFurnitureManipulator` is the only component that writes the furniture Transform. Its Inspector settings control movement speed, rotation snap angle, and stick deadzone. The Rigidbody is kinematic with gravity disabled; Meta's ray hover/select behavior is retained while its pose-follow movement is disabled. The older `FreezeRotationOnGrab` and `FurnitureCollisionHandler` callbacks remain compatible with serialized scenes but do not move the furniture.

### Audio feedback

Selection, deselection, movement start, deletion, and wall impacts have sound feedback. A movement-stop confirmation sound is available but disabled by default. Audio files and their source/license information are documented in [`Assets/Audio/SFX/README.md`](Assets/Audio/SFX/README.md).

The persistent `AudioManager` is loaded from `Assets/Resources/Audio/AudioManager.prefab` before the scene starts; it does not need to be added to individual scenes. Its Inspector settings configure the event clips and volumes, optional movement-stop confirmation, and repeat/collision cooldowns. Wall-impact feedback is triggered by the furniture's blocked-pose check for colliders named with `Wall`; it is debounced until the furniture moves away, with an additional manager cooldown.

## Test checklist

1. Open the catalogue with the left Menu/Start button, choose an item, and verify that it appears in front of you.
2. Point the right ray at the furniture. Verify the hover outline appears; hold the right trigger and verify that it is selected. Move the ray away and release the trigger to clear the outline.
3. Select furniture again, hold the right grip, and use the right thumbstick in each direction. Verify movement follows your view on the floor plane and the furniture height stays fixed.
4. While still gripping, deflect the left thumbstick horizontally. Verify one 15-degree turn per deflection; return to neutral between turns.
5. Release the right grip to confirm placement, then release the trigger to deselect. Select and grip it again, press B, and verify it returns to the position and rotation from the start of that grip. Deselect and reselect before testing another move.
6. Select a piece and press A to delete it.
7. Listen for selection/deselection, movement-start, and deletion sounds. Push furniture into a wall and away again to test wall-impact feedback.

## Resources and credits

### Sound effects

The six furniture feedback sounds are from Kenney's [Interface Sounds](https://kenney.nl/assets/interface-sounds) pack, version 1.0. The pack's `Kenney-License.txt` identifies the sounds as CC0; attribution is not required. See [`Assets/Audio/SFX/README.md`](Assets/Audio/SFX/README.md) for the clip-to-feature mapping and license references.

### 3D assets

The 3D assets were sourced from free 3D assets available on the internet. Individual asset source URLs, authors, and license details are not catalogued in this repository.
