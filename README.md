# dorm-designer-VR

## Joystick furniture manipulation

Furniture keeps its Meta Interaction SDK ray hover and select interaction. While the ray is selecting a piece (right trigger), holding the right grip enables manipulation:

- The right stick moves the object in room/world X and Z; the tracked hand/controller pose is not used.
- The left stick applies one Y-axis rotation snap per deflection. Return the stick to its deadzone before another 15-degree step can occur.
- Releasing the right grip confirms the current position. Releasing the trigger ends Meta ray selection.
- B cancels the current selection/manipulation and restores the position and yaw from the start of the grip. Release and reselect to start again.
- Press the right controller's A button while the furniture is selected to delete it. The left menu/catalogue action is reserved for later.

The root `ConstrainedFurnitureManipulator` is the only component that writes the furniture Transform. `FurnitureManipulationState` reads Meta's `RayInteractable` selection state, and `FurnitureControllerInput` samples Quest Touch controls through `OVRInput`. The manipulator fixes world Y and sets world rotation to `(0, yaw, 0)`. Its Inspector fields configure movement speed, rotation snap angle, and deadzone. The Rigidbody is made kinematic with gravity disabled; the Meta ray's `MoveFromTargetProvider` is removed/cleared, and Meta's default pose-follow `Grabbable` transformer is replaced at runtime with a no-op transformer. The legacy `FreezeRotationOnGrab` and `FurnitureCollisionHandler` callbacks remain compatible with serialized scenes but no longer write the furniture Transform.

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
