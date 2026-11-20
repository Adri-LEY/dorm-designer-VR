# dorm-designer-VR

VR app for students to furnish an empty dorm room before buying anything: pick a room, browse a furniture catalogue (prices, real dimensions, sizes, colours), place and arrange furniture with the controllers or by voice.

## Getting started

> **⚠️ Voice commands: use the app in a very quiet room, with internet.**
> - Speech recognition is easily disturbed: background conversations, music or noise lead to wrong or ignored commands. Speak clearly, close to the microphone.
> - Voice commands need an **internet connection**: speech is turned into text online by Wit.ai. When running from Unity, the **PC** must be online; when the app is installed on the Quest, the **headset** must be connected to Wi-Fi. Without internet, everything else works, but not voice commands.

### Requirements

- **Unity 6000.3.6f1** (Unity 6.3 LTS), installed through Unity Hub with **Android Build Support** (for Quest builds).
- A **Meta Quest 3** (or 2 / 3S / Pro) with its controllers.
- A Windows PC with the **Meta Horizon Link** app, and a USB-C data cable (Link) or Air Link (Wi-Fi), to play from Unity.
- For voice commands: a free **Wit.ai** app and its token (see [Voice commands > Setup](#setup-once-per-machine--scene)).

### Open the project

1. Clone the repository.
2. In **Unity Hub**: *Add > Add project from disk*, select the `dorm-designer-VR` folder, and open it with **6000.3.6f1**. The first import takes several minutes (it rebuilds the `Library/` folder).
3. Open the scene **`Assets/DormVRScenes/Main scene.unity`**. This is the app's scene, not `Assets/Scenes/SampleScene`.
4. If the scene has not been set up on this machine yet, run **Tools > Dorm Designer > Set Up Open Scene**, then **Tools > Dorm Designer > Set Up Voice Commands**, and save (Ctrl+S). See [Setting up a scene](#setting-up-a-scene-run-once-after-pulling).

### Run it in the headset from Unity

1. Start **Meta Horizon Link** on the PC and connect the headset with **Link** (cable) or **Air Link**. In the headset, accept the Link connection.
2. In Meta Horizon Link > *Settings > General*, make sure it is the active **OpenXR runtime**.
3. In Unity, press **Play**. The app shows in the headset.
4. With the headset on, choose a room in the menu that appears in front of you.

Logs are in Unity's **Console** (keep the *info* messages filter on). Voice command logs start with `[Voice]`.

### How to use it

| Action | How |
|---|---|
| Open / close the menu (furniture catalogue, **Room** tab) | **Menu** button on the left controller |
| Click in the menu, select a furniture | Point with a controller and press the **trigger** |
| Product sheet of a furniture → choose size / colour → **Place in room** | Click a furniture in the catalogue |
| Move a selected furniture | Hold **right trigger** (select) + **right grip**, then the **right stick** |
| Rotate a selected furniture | While selected and gripped: **left stick** (smooth turn) |
| Cancel the current move | **B** |
| Delete the selected furniture | **A** |
| Turn / move yourself | Thumbsticks (when no furniture is selected); smooth or snap turning is set on the Room page |
| Voice command | Hold **X**, point, speak, wait for the text to appear, release **X** |

Voice commands (all start by holding **X**):

- *"Place a chair here"*: right controller points at the floor. You can name colours / sizes: *"place a red chair"*, *"put a double bed there"*.
- *"Move this furniture here"*: right controller points at the furniture, **left** controller at the new spot (orange marker). Or by name: *"move the red chair here"*.
- *"Delete this furniture"*: right controller points at it. Or *"delete the red chair"*.
- *"Undo"*: cancels the last place / move / delete (voice, menu, joystick or A button).

Labels next to the controllers recall the buttons.

### Build the app

- **Windows (PC VR)**: *File > Build Profiles > Windows*, **Switch Platform**, check that the Scene List contains only `DormVRScenes/Main scene`, then **Build** into a `Builds/` folder (ignored by git). Run the `.exe` while the headset is connected with Link / Air Link. No Meta developer account needed.
- **Quest standalone (Android `.apk`)**: *Build Profiles > Android*, same Scene List, **Build And Run** with the headset plugged in by USB. This requires **developer mode** on the headset, which needs a Meta developer organization (free) and USB debugging enabled from the Meta Horizon phone app. The app then appears in the headset's *Library > Unknown Sources*; accept the microphone permission at first launch.

## Repository contents

The repository holds the full Unity project sources. Generated folders (`Library/`, `Temp/`, `Logs/`, `.utmp/`, builds) are not versioned; Unity recreates them when the project is opened.

| Folder | Contents |
|---|---|
| `Assets/Scripts/` | All the project's C# code: furniture manipulation, menus, rooms (`Rooms/`), catalogue data (`Furniture/`), UI built at runtime (`UI/`), voice commands (`Voice/`), turning / locomotion (`Locomotion/`), Editor setup tools (`Editor/`) |
| `Assets/DormVRScenes/` | The app scene (`Main scene.unity`), furniture models and prefabs (`Prefabs/`), icons / materials (`Resources/`), room presets (`Data/Rooms/`), Wit.ai configuration |
| `Assets/Audio/SFX/` | Sound effects (with their license) |
| `Assets/Resources/Audio/` | `AudioManager` prefab, loaded automatically |
| `Packages/manifest.json` | Unity / Meta packages used (installed automatically by Unity) |
| `ProjectSettings/` | Unity project settings (XR, Android, input, etc.) |

## Team

- Adrien LEY
- Enzo Miech
- Loïc GIUNTINI

## Resources & citations

### Engine, SDKs and packages

- **Unity 6.3 LTS (6000.3.6f1)**, Unity Technologies: <https://unity.com>
- **Meta XR SDK** (`com.meta.xr.sdk.all` 205.0.0: Core, Interaction, Voice, Audio, Haptics, Platform, MR Utility Kit), Meta: camera rig, controller input, ray interaction, locomotion. <https://developers.meta.com/horizon/documentation/unity/>
- **Meta Voice SDK** + **Wit.ai** (Meta): speech-to-text for the voice commands (online service). <https://wit.ai> · <https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/>
- **Unity OpenXR**, **XR Interaction Toolkit** 3.5.1, **XR Hands**, **XR Management**, **AR Foundation**, **Input System**, Unity Technologies. The `VRTemplateAssets/`, `XRI/` and `Samples/` folders come from Unity's **VR project template** and the XR Interaction Toolkit samples.
- **Universal Render Pipeline (URP)** 17.3 and **TextMesh Pro**, Unity Technologies (TextMesh Pro's default font is Liberation Sans, SIL Open Font License).

### Assets

| Asset | Author / source | License | Used for |
|---|---|---|---|
| *Interface Sounds* 1.0 (`Assets/Audio/SFX/*.ogg`) | Kenney, <https://kenney.nl/assets/interface-sounds> | CC0 (public domain), see `Assets/Audio/SFX/Kenney-License.txt` | Selection, movement, deletion and wall-impact sounds |
| *Free Button Set* (`Assets/TWD/`) | TWD, https://assetstore.unity.com/packages/2d/gui/icons/simple-button-set-02-184903?srsltid=AYCX9vIkmL1vCZqk01jGMzSWQpOcYOF2UcRCVgpWAd4KnsLoVCHkWY6u | Standard Unity Asset Store EULA | Menu button graphics |
| Poppins font (in `Assets/TWD/FreeButtonSet/Fonts/`) | Indian Type Foundry, <https://fonts.google.com/specimen/Poppins> | SIL Open Font License 1.1 | Comes with the button set |
| Furniture 3D models `SM_Bed_8`, `SM_Chair_13`, `SM_Chair_28`, `SM_Computer_desk_7`, `SM_Wardrobe_9`, `SM_Wardrobe_15` | *Low Poly Furniture Pack Free* by IGS, https://assetstore.unity.com/packages/3d/props/furniture/low-poly-furniture-pack-free-by-igs-389770 | Standard Unity Asset Store EULA | Catalogue furniture |
| 3D model `Bed2` ("Blue Bed") and its textures `Var_01_*_4k-min.png` (`New Material.mat`) | Unknown | Unknown | Catalogue furniture |
| Furniture icons (`bed.png`, `chair13.png`, ...) | those are screenshot of the 3D models | same as the 3D models | Catalogue thumbnails |

### Data

- **Room presets** (CROUS traditional room ~9 m², renovated room, T1 studio, T1 bis): approximate typical sizes of French student housing, chosen for the prototype. They are not official CROUS data.
- **Product data** (prices, sizes, colours, descriptions): **placeholder values** written for the prototype; they do not come from real products or shops.

### Development tools

- Parts of the code and of this documentation were written with the help of **Claude** (Anthropic), an AI assistant, and reviewed and tested by the team.

---

# Technical documentation

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


## Voice commands: place, move, delete, undo

Hold the **left X button**, point the right controller at the floor, and say *"Place a chair here"*, then release X. While X is held, a blue disc marks the pointed spot and a label shows what is heard. When X is released, that spot is kept ("here" = where you pointed when you finished speaking). The furniture is put at the **closest valid spot** around it.

### All voice commands

Hold **X**, point, say the command, wait for the text to appear, then release X. The pointed spots are taken at release.

| Command | Right controller points at | Left controller points at |
|---|---|---|
| *"Place a chair here"* (also *"add a red chair"*, *"put a double bed there"*) | the floor spot | (nothing; used only if the right one points nowhere) |
| *"Move this furniture here"* (also *"move this there"*, *"put this here"*) | the furniture to move | its new spot (orange marker) |
| *"Move the red chair here"* / *"move the chair here"* | (nothing needed; the item is found by name or type, preferring the one closest to where the right ray points) | its new spot |
| *"Delete this furniture"* (also *"remove this"*, *"get rid of that"*, *"delete the red chair"*) | the furniture | |
| *"Undo"* (also *"go back"*, *"cancel that"*) | | |

- **Undo** covers the last place, move and delete, whether they were made by voice, menu, joystick or the A button (`FurnitureHistory`, last 20 actions). Deleting is a soft delete (the object is hidden, then really destroyed when it leaves the history or when the room changes), so undo brings back the same object with its size and colour. If the old spot has been taken since, undo uses the closest free spot.
- **Move** uses the same free-spot search as placing, but the item ignores itself. Near a wall, it is put with its back to the wall; otherwise it keeps its rotation. It glides there in 0.3 s (`ConstrainedFurnitureManipulator.MoveTo`).
- While X is held, the pointed furniture shows its usual hover outline, the blue disc shows the right controller's floor spot, and the orange disc shows the left one's. The left pointer (`PointingTarget` on the left controller) is created automatically.
- "put / bring / set" mean **move** with "this/that/it" or "the …" (*"put the chair there"*), and **place** with "a/an" (*"put a chair here"*).

### Setup (once per machine / scene)

1. Create a free app on [wit.ai](https://wit.ai) (language **English**). No intents or training are needed: Wit.ai only turns speech into text. Copy the app's *Server Access Token* (Management > Settings).
2. In Unity: **Meta > Voice SDK > Get Started**, paste the token and save the generated Wit configuration asset.
3. **Tools > Dorm Designer > Set Up Voice Commands**, then save the scene. This creates a `VoiceCommands` object with `AppVoiceExperience` (using the Wit configuration), `PointingTarget`, `VoiceFeedbackDisplay` and `VoiceCommandController`. Run it again after step 2 if you set up voice before creating the configuration.
4. The headset needs internet. The microphone permission is requested at launch on Quest.

**Testing without speaking:** in Play mode (headset or emulator), use the `VoiceCommandController` component's context menu (three dots) > **Run Test Command**. It runs the `Test Command` text (default "place a chair here") against the spot currently pointed at. Code can call `RunTextCommand(string)`.

### How it works

- `PointingTarget` follows Meta's visible right-controller ray (falls back to `RightControllerAnchor`). Floor hit = that point. Wall hit = a floor point 30 cm in front of the wall. Furniture hit = the floor under the hit point. Outside the room = no target.
- `VoiceCommandParser` interprets the transcript locally, so the team can extend it without retraining anything on Wit.ai. Verbs: *place, put, add, spawn, drop, bring, install, create*, or "I want / give me ...", or just "a chair here". The object is matched against the catalogue's **category** (with synonyms, e.g. *closet / cupboard → wardrobe*, *table → desk*, *seat → chair*) and **name words** (*"red chair"* picks `Red_Chair`). Size and colour labels are also recognised (*"a double bed"*, *"a walnut desk"*). Add synonyms in `CategorySynonyms`.
- `ObjectSpawner.SpawnNear` searches rings around the target (up to `Max Distance From Target`, 1.5 m by default), closest first. If a wall is within `Wall Align Distance` of the target, the furniture is put with its back to that wall; otherwise it faces the player. Quarter-turned orientations are also tried. The validity rules are the same as for the menu (`IsBoxFree`, on the floor, no wall in between, personal space), so voice-placed furniture can always be moved.
- Errors are spoken back in the label: unknown object ("I don't know 'sofa'. Try: bed, chair, desk, wardrobe."), no target, no free space, nothing heard, Wit.ai errors.

### Microphone and recognition quality

- **Which mic:** `VoiceCommandController > Microphone`. *Auto* uses the PC's own mic in the Editor/on PC, and the headset mic on a Quest build (the only one there). The Quest mic seen by Windows ("Headset Microphone (Oculus Virtual Audio Device)") only carries audio during an active Link session with Link audio enabled (Meta Horizon Link > Devices > your headset > audio settings); otherwise it records silence. *Headset*, *Computer* or *Custom* (a name fragment such as "Realtek") force one. The mic is never switched automatically: an earlier version did, and it bounced between the PC and headset mics. Without this, the Voice SDK always records from the first Windows device, which is the Quest Link mic. On Quest itself there is only one mic.
- **Listen until release:** the SDK normally stops recording after ~1 s without new words, which cut sentences when you paused. While X is held, it now keeps recording (`Listen Until Release`).
- **Too-quiet recordings are ignored** (`Min Speech Level`, 0.02 by default): Wit.ai invents words from noise. Each `[Voice] Push-to-talk released` log line shows the max mic level, which you can use to tune the threshold.
- **Mishearings:** `VoiceCommandParser` maps common sound-alikes ("share" → chair, "disk" → desk, "war drop" → wardrobe, "please" → place) and tolerates one wrong letter in 5+ letter words (two in 8+). Add entries to `SoundAlikes` / `SoundAlikePairs` from what you see in the `[Voice] Full transcription` logs.
- Make sure the Console's info filter (the speech-bubble button) is on, otherwise no `[Voice]` logs are visible.

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

## Controller tooltips

`ControllerHelpTooltips` (on `ControllerHelp`) shows labels next to the Touch controllers. **Tips** appear while a furniture item is selected (manipulation controls). **General Tips** appear the rest of the time: *Hold X + point: voice command* (only when the scene has a `VoiceCommandController`), *Menu: furniture & rooms*, and *Trigger: select furniture / UI*. Edit the texts and positions (controller-local metres) in the Inspector, untick `Show General Tips` to hide them, or tick `Always Show` to place them in Play mode.

## Furniture audio feedback

The persistent `AudioManager` is automatically loaded from `Assets/Resources/Audio/AudioManager.prefab` before the scene starts, so it does not need to be placed in each scene. Its Inspector fields configure the event clips, master/event volumes, optional movement-stop confirmation, and repeat/collision cooldowns. It uses one shared 2D source for UI/manipulation feedback and one shared spatial source for wall impacts. No per-furniture AudioSources are created.

`InteractionFeedback` keeps the existing Meta selection and deselection callbacks and forwards them to the manager. `ConstrainedFurnitureManipulator` plays movement feedback only on grip start/end and deletion immediately before destruction. Wall impacts are detected from the existing blocked-pose overlap check, only for colliders named with "Wall", and are debounced until furniture moves away; the manager adds a cooldown as a second guard.

The selected sounds are in `Assets/Audio/SFX/`; their source and CC0 license are documented in that folder. To verify in Unity/Quest, exercise selection/deselection, grip start/release, deletion, and repeated pushes into then away from a wall. Movement-stop confirmation is disabled by default and can be enabled on the AudioManager prefab.
