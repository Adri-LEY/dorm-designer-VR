using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Choose your room" screen shown on the menu canvas when the app starts.
/// Also adds a "Room" tab to the furniture menu to come back later.
/// </summary>
public sealed class RoomSelectionMenu : MonoBehaviour
{
    [Header("Rooms")]
    public List<RoomPreset> rooms = new List<RoomPreset>();

    [Header("References")]
    public RoomBuilder roomBuilder;
    public VRMenuController menuController;
    [Tooltip("The menu canvas (UI Menu (Canvas)). The room page is built inside it.")]
    public RectTransform canvasRoot;
    [Tooltip("Optional: the furniture menu's tab bar, to add a \"Room\" tab.")]
    public RectTransform tabBar;
    [Tooltip("Used to remove placed furniture when switching room.")]
    public ObjectSpawner spawner;
    [Tooltip("Optional: shows a Smooth / Snap turning toggle on the room page.")]
    public TurnSettings turnSettings;

    [Header("Behaviour")]
    [Tooltip("Show the room selection when the app starts.")]
    public bool showOnStart = true;
    [Tooltip("While the first choice is pending, the menu follows the head if the player looks away by more than this angle.")]
    public float followAngle = 35f;
    public float followSpeed = 4f;

    private RectTransform _page;
    private RectTransform _list;
    private TextMeshProUGUI _subtitle;
    private Button _backButton;
    private Button _turnButton;
    private RoomPreset _pendingSwitch;
    private bool _firstChoicePending;
    private bool _following = true;

    private void Awake()
    {
        if (showOnStart && menuController != null)
        {
            // Must be set before VRMenuController.Start closes the menu.
            menuController.LockedOpen = true;
        }
    }

    private void Start()
    {
        if (canvasRoot == null && menuController != null && menuController.uiMenu != null)
        {
            canvasRoot = menuController.uiMenu.transform as RectTransform;
        }

        if (canvasRoot == null)
        {
            Debug.LogError("RoomSelectionMenu needs the menu canvas.", this);
            enabled = false;
            return;
        }

        rooms.RemoveAll(r => r == null);
        BuildPage();
        AddRoomTab();

        if (showOnStart && rooms.Count > 0)
        {
            Show(true);
        }
        else
        {
            if (menuController != null) menuController.LockedOpen = false;
            if (rooms.Count > 0 && roomBuilder != null) roomBuilder.Build(rooms[0]);
        }
    }

    private void LateUpdate()
    {
        // Before the first choice the menu is the only thing to look at: keep it in view.
        // (Also fixes the head pose being unknown during the first frames.)
        if (!_firstChoicePending || menuController == null || menuController.uiMenu == null ||
            menuController.headCamera == null) return;

        Transform menu = menuController.uiMenu.transform;
        Transform head = menuController.headCamera;
        Vector3 target = menuController.GetInFrontOfHeadPosition();

        Vector3 toMenu = Vector3.ProjectOnPlane(menu.position - head.position, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        float distance = toMenu.magnitude;
        bool outOfView = forward.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toMenu) > followAngle;
        bool wrongDistance = distance < menuController.headDistance * 0.5f || distance > menuController.headDistance * 2f;

        if (outOfView || wrongDistance || _following)
        {
            _following = Vector3.Distance(menu.position, target) > 0.02f;
            menuController.PlaceFacingHead(Vector3.Lerp(menu.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime)));
        }
    }

    // ---------------------------------------------------------------- UI

    private void BuildPage()
    {
        Image background = VRUIFactory.CreatePanel("RoomSelectionPage", canvasRoot, VRUIFactory.PanelColor);
        _page = background.rectTransform;
        VRUIFactory.Stretch(_page);
        float width = canvasRoot.rect.width > 1f ? canvasRoot.rect.width : 500f;
        float contentWidth = width - 24f;

        TextMeshProUGUI title = VRUIFactory.CreateText("Title", _page, "Choose your room", 28f, VRUIFactory.TextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        VRUIFactory.PlaceTopLeft(title.rectTransform, 12f, 10f, contentWidth, 40f);

        _subtitle = VRUIFactory.CreateText("Subtitle", _page, string.Empty, 15f, VRUIFactory.MutedTextColor,
            TextAlignmentOptions.MidlineLeft);
        VRUIFactory.PlaceTopLeft(_subtitle.rectTransform, 12f, 50f, contentWidth, 36f);

        _list = VRUIFactory.CreateRect("RoomList", _page);
        VRUIFactory.PlaceTopLeft(_list, 12f, 92f, contentWidth, 350f);
        VRUIFactory.AddVerticalLayout(_list.gameObject, 8f);

        float footerY = 452f;
        _turnButton = VRUIFactory.CreateButton("TurnToggle", _page, "Turning", ToggleTurnMode, VRUIFactory.CardColor, 15f);
        VRUIFactory.PlaceTopLeft((RectTransform)_turnButton.transform, 12f, footerY, 220f, 36f);
        _turnButton.gameObject.SetActive(turnSettings != null);

        _backButton = VRUIFactory.CreateButton("BackButton", _page, "Back to furniture", Hide, VRUIFactory.AccentColor, 15f);
        VRUIFactory.PlaceTopLeft((RectTransform)_backButton.transform, width - 12f - 200f, footerY, 200f, 36f);

        _page.gameObject.SetActive(false);
    }

    private void RebuildRoomCards()
    {
        VRUIFactory.ClearChildren(_list);

        float cardHeight = Mathf.Clamp((350f - 8f * (rooms.Count - 1)) / Mathf.Max(1, rooms.Count), 48f, 78f);
        foreach (RoomPreset room in rooms)
        {
            RoomPreset captured = room;
            bool isCurrent = roomBuilder != null && roomBuilder.CurrentRoom == room;
            bool isPending = _pendingSwitch == room;

            Color background = isPending ? VRUIFactory.WarningColor
                : isCurrent ? new Color(0.16f, 0.32f, 0.52f, 1f)
                : VRUIFactory.CardColor;

            Button card = VRUIFactory.CreateButton("Room_" + room.name, _list, null, () => OnRoomClicked(captured), background);
            VRUIFactory.SetLayoutSize(card, -1f, cardHeight);

            string roomName = room.displayName + (isCurrent ? "  (current)" : string.Empty);
            TextMeshProUGUI nameText = VRUIFactory.CreateText("Name", card.transform, roomName, 18f, VRUIFactory.TextColor,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            VRUIFactory.Stretch(nameText.rectTransform, 12f, 150f, 6f, cardHeight * 0.45f);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;

            string size = string.Format(CultureInfo.InvariantCulture, "{0:0.0} x {1:0.0} m  |  {2:0} m²",
                room.width, room.depth, room.Area);
            TextMeshProUGUI sizeText = VRUIFactory.CreateText("Size", card.transform, size, 15f, VRUIFactory.TextColor,
                TextAlignmentOptions.TopRight);
            VRUIFactory.Stretch(sizeText.rectTransform, 200f, 12f, 7f, cardHeight * 0.45f);
            sizeText.textWrappingMode = TextWrappingModes.NoWrap;

            string details = isPending
                ? "Tap again to switch. The furniture you placed will be removed."
                : room.description;
            TextMeshProUGUI detailText = VRUIFactory.CreateText("Description", card.transform, details, 13f,
                isPending ? VRUIFactory.TextColor : VRUIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
            VRUIFactory.Stretch(detailText.rectTransform, 12f, 12f, cardHeight * 0.5f, 4f);
        }
    }

    private void RefreshFooter()
    {
        bool hasRoom = roomBuilder != null && roomBuilder.CurrentRoom != null;
        _backButton.gameObject.SetActive(hasRoom);
        _subtitle.text = hasRoom
            ? "Pick another empty room. You can come back to furniture with the button below."
            : "Pick the empty room you want to furnish. You can change it later from the Room tab.";

        if (turnSettings != null)
        {
            TextMeshProUGUI label = VRUIFactory.GetLabel(_turnButton);
            if (label != null) label.text = "Turning: " + (turnSettings.Mode == TurnSettings.TurnMode.Smooth ? "Smooth" : "Snap");
        }
    }

    private void AddRoomTab()
    {
        if (tabBar == null) return;

        Button tab = VRUIFactory.CreateButton("Tab_Room", tabBar, "Room", () => Show(false), Color.white, 18f,
            new Color(0.1f, 0.1f, 0.12f, 1f));
        tab.transform.SetAsFirstSibling();
    }

    // ---------------------------------------------------------------- Flow

    /// <summary>Shows the room page. <paramref name="atLaunch"/> locks the menu open until a room is picked.</summary>
    public void Show(bool atLaunch)
    {
        _pendingSwitch = null;
        _firstChoicePending = atLaunch || roomBuilder == null || roomBuilder.CurrentRoom == null;
        _following = true;

        if (menuController != null)
        {
            menuController.LockedOpen = _firstChoicePending;
            if (!menuController.IsOpen || atLaunch) menuController.OpenInFrontOfHead();
        }

        RebuildRoomCards();
        RefreshFooter();
        _page.gameObject.SetActive(true);
        _page.SetAsLastSibling();
    }

    public void Hide()
    {
        _pendingSwitch = null;
        _firstChoicePending = false;
        if (menuController != null) menuController.LockedOpen = false;
        _page.gameObject.SetActive(false);
    }

    private void OnRoomClicked(RoomPreset room)
    {
        if (roomBuilder == null) return;

        if (roomBuilder.CurrentRoom == room)
        {
            Hide();
            return;
        }

        bool hasFurniture = spawner != null && spawner.SpawnedCount > 0;
        if (hasFurniture && _pendingSwitch != room)
        {
            // First tap: ask for confirmation directly on the card.
            _pendingSwitch = room;
            RebuildRoomCards();
            return;
        }

        if (spawner != null) spawner.ClearSpawned();

        bool wasFirstChoice = _firstChoicePending;
        Hide();
        roomBuilder.Build(room);

        // After moving the player, bring the furniture menu in front of them so they can start right away.
        if (wasFirstChoice && menuController != null) StartCoroutine(ReopenMenuAfterTeleport());
    }

    private IEnumerator ReopenMenuAfterTeleport()
    {
        // The locomotor applies the teleport on its next update, and the head pose follows a frame later.
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);
        if (menuController != null) menuController.OpenInFrontOfHead();
    }

    private void ToggleTurnMode()
    {
        if (turnSettings == null) return;
        turnSettings.Mode = turnSettings.Mode == TurnSettings.TurnMode.Smooth
            ? TurnSettings.TurnMode.Snap
            : TurnSettings.TurnMode.Smooth;
        RefreshFooter();
    }
}
