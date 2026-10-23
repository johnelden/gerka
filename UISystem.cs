using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UISystem : MonoBehaviour
{
    [System.Serializable]
    class CardData
    {
        public string title;
        public BattleSystem.UnitType type;
        public int cost;
        public int count;
        public int health;
        public int damage;
        public Color color;
        public bool isFireball;

        public CardData(string title, BattleSystem.UnitType type, int cost, int health, int damage, Color color, int count = 1, bool isFireball = false)
        {
            this.title = title;
            this.type = type;
            this.cost = cost;
            this.count = count;
            this.health = health;
            this.damage = damage;
            this.color = color;
            this.isFireball = isFireball;
        }
    }

    readonly List<CardData> pool = new List<CardData>();
    readonly List<CardData> deck = new List<CardData>();
    readonly List<CardData> hand = new List<CardData>();
    readonly List<CardData> recentlyPlayedCards = new List<CardData>();
    readonly List<GameObject> cardObjects = new List<GameObject>();

    BattleSystem battle;
    GameManager manager;
    Canvas canvas;
    RectTransform canvasRect;
    Text elixirText;
    Text timerText;
    Text statusText;
    Text cameraViewButtonText;
    Image elixirFill;
    Image nextCardSwatch;
    Text nextCardText;
    GameObject dragGhost;
    RectTransform dragGhostRect;
    Text dragGhostText;
    GameObject deployGuide;
    GameObject deployMarker;
    Material deployGuideMaterial;
    Material deployMarkerMaterial;
    GameObject elixirCostPopup;
    TextMesh elixirCostText;
    TextMesh elixirCostShadow;
    Vector3 elixirCostStartPosition;
    float elixirCostPopupRemaining;
    int draggingIndex = -1;
    int nextDeckCard = 4;
    bool matchEnded;
    float elixir = 5f;
    float regenTimer;
    const float MaxElixir = 10f;
    const float ElixirRegenSeconds = 2.5f;

    GameObject gameOverPanel;
    GameObject playerEmoteBubble;
    GameObject enemyEmoteBubble;
    RectTransform playerEmoteRect;
    RectTransform enemyEmoteRect;
    Text playerEmoteText;
    Text enemyEmoteText;
    float playerEmoteLife;
    float enemyEmoteLife;
    float botEmoteTimer = 7f;
    bool initialized;

    // Pause/settings/main-menu overlays are built at runtime so no scene prefab is required.
    GameObject pauseOverlay;
    GameObject pausePanel;
    GameObject settingsPanel;
    GameObject mainMenuPanel;
    Slider musicSlider;
    Slider effectsSlider;
    AudioSystem audioSystem;
    bool isPaused;
    bool settingsFromMainMenu;

    // Common entry points used by GameManager variants.
    public void Initialize(BattleSystem battleSystem, GameManager gameManager)
    {
        battle = battleSystem;
        manager = gameManager;
        BuildUI();
    }

    public void Initialize(ArenaSystem arenaSystem, BattleSystem battleSystem, GameManager gameManager)
    {
        Initialize(battleSystem, gameManager);
    }

    public void Initialize(ArenaSystem arenaSystem, GameManager gameManager)
    {
        Initialize(FindObjectOfType<BattleSystem>(), gameManager);
    }

    public void Initialize(GameManager gameManager, BattleSystem battleSystem)
    {
        Initialize(battleSystem, gameManager);
    }

    public void Initialize(ArenaSystem arenaSystem, BattleSystem battleSystem)
    {
        Initialize(battleSystem, FindObjectOfType<GameManager>());
    }

    void BuildUI()
    {
        if (initialized) return;
        initialized = true;
        EnsureEventSystem();
        BuildCardPool();
        ShuffleDeck();

        GameObject canvasObject = new GameObject("Game UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasRect = canvasObject.GetComponent<RectTransform>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;

        BuildTopBar();
        BuildCameraViewButton();
        BuildCardPanel();
        BuildNextCardPreview();
        BuildEmotes();
        BuildStatus();
        BuildPauseAndSettingsUI();
        for (int i = 0; i < Mathf.Min(4, deck.Count); i++) hand.Add(deck[i]);
        nextDeckCard = 4;
        RenderHand();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void BuildCardPool()
    {
        pool.Clear();
        pool.Add(new CardData("Rycerz", BattleSystem.UnitType.Knight, 3, 250, 48, new Color(0.20f, 0.43f, 0.80f)));
        pool.Add(new CardData("Łucznicy x2", BattleSystem.UnitType.Archer, 3, 85, 14, new Color(0.29f, 0.58f, 0.38f), 2));
        pool.Add(new CardData("Olbrzym", BattleSystem.UnitType.Giant, 5, 680, 75, new Color(0.62f, 0.37f, 0.22f)));
        pool.Add(new CardData("Kusznik", BattleSystem.UnitType.Crossbowman, 4, 145, 48, new Color(0.26f, 0.53f, 0.43f)));
        pool.Add(new CardData("Mag", BattleSystem.UnitType.Mage, 4, 155, 44, new Color(0.48f, 0.30f, 0.72f)));
        pool.Add(new CardData("Szkielety x3", BattleSystem.UnitType.Skeleton, 2, 38, 11, new Color(0.68f, 0.68f, 0.58f), 3));
        pool.Add(new CardData("Fireball", BattleSystem.UnitType.Knight, 4, 0, 220, new Color(0.90f, 0.30f, 0.10f), 1, true));
        pool.Add(new CardData("Cannon", BattleSystem.UnitType.Cannon, 3, 180, 24, new Color(0.42f, 0.45f, 0.48f)));
        pool.Add(new CardData("Dragon", BattleSystem.UnitType.Dragon, 4, 400, 30, new Color(0.25f, 0.62f, 0.34f)));
        pool.Add(new CardData("Hog Rider", BattleSystem.UnitType.HogRider, 4, 260, 65, new Color(0.62f, 0.32f, 0.19f)));
        pool.Add(new CardData("Nietoperze x5", BattleSystem.UnitType.Bat, 2, 32, 8, new Color(0.37f, 0.30f, 0.52f), 5));
        pool.Add(new CardData("Mini P.E.K.K.A", BattleSystem.UnitType.MiniPekka, 4, 320, 135, new Color(0.22f, 0.36f, 0.54f)));
    }

    void ShuffleDeck()
    {
        deck.Clear();
        List<CardData> shuffled = new List<CardData>(pool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardData temp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = temp;
        }
        for (int i = 0; i < shuffled.Count; i++) deck.Add(shuffled[i]);
    }

    void BuildTopBar()
    {
        GameObject top = Panel("Top Status Bar", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(330, 42), new Color(0.08f, 0.10f, 0.11f, 0.78f));
        timerText = MakeText("Timer", top.transform, "03:00", 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform tr = timerText.rectTransform;
        tr.anchorMin = new Vector2(0.5f, 0.5f); tr.anchorMax = new Vector2(0.5f, 0.5f); tr.sizeDelta = new Vector2(112, 38); tr.anchoredPosition = Vector2.zero;

        GameObject elixirPanel = Panel("Elixir Meter", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 132), new Vector2(300, 38), new Color(0.07f, 0.09f, 0.10f, 0.78f));
        Outline elixirOutline = elixirPanel.AddComponent<Outline>();
        elixirOutline.effectColor = new Color(0.42f, 0.65f, 0.62f, 0.55f);
        elixirOutline.effectDistance = new Vector2(1, -1);
        GameObject track = Panel("Elixir Track", elixirPanel.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(17, -9), new Vector2(266, 7), new Color(0.025f, 0.035f, 0.04f, 1f));
        track.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
        track.GetComponent<Image>().raycastTarget = false;
        GameObject fill = Panel("Elixir Fill", elixirPanel.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(17, -9), new Vector2(266, 7), new Color(0.30f, 0.82f, 0.72f, 1f));
        elixirFill = fill.GetComponent<Image>();
        elixirFill.rectTransform.anchorMin = new Vector2(0, 0.5f); elixirFill.rectTransform.anchorMax = new Vector2(0, 0.5f); elixirFill.rectTransform.pivot = new Vector2(0, 0.5f);
        elixirFill.rectTransform.anchoredPosition = new Vector2(17, -9);
        elixirFill.rectTransform.sizeDelta = new Vector2(266, 7);
        elixirFill.raycastTarget = false;
        for (int segment = 1; segment < 10; segment++)
        {
            float x = 17f + 266f * segment / 10f;
            GameObject divider = Panel("Elixir Segment", elixirPanel.transform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, -9),
                new Vector2(1.5f, 7), new Color(0.035f, 0.055f, 0.06f, 0.9f));
            divider.GetComponent<Image>().raycastTarget = false;
        }

        elixirText = MakeText("Elixir Text", elixirPanel.transform, "ELIKSIR 5 / 10", 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        elixirText.rectTransform.anchorMin = new Vector2(0, 0.4f); elixirText.rectTransform.anchorMax = Vector2.one; elixirText.rectTransform.offsetMin = new Vector2(4, 0); elixirText.rectTransform.offsetMax = new Vector2(-4, 0);
    }

    void BuildCameraViewButton()
    {
        GameObject button = MakeButton("Camera View Button", canvas.transform,
            "WIDOK: 3D", 13, new Color(0.08f, 0.13f, 0.16f, 0.92f), ToggleCameraView);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(148f, 38f);
        rect.anchoredPosition = new Vector2(12f, -12f);
        Outline outline = button.AddComponent<Outline>();
        outline.effectColor = new Color(0.54f, 0.73f, 0.72f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);
        cameraViewButtonText = button.GetComponentInChildren<Text>();
    }

    void ToggleCameraView()
    {
        if (manager == null) return;
        manager.ToggleCameraView();
        if (cameraViewButtonText != null)
            cameraViewButtonText.text = manager.IsBirdEyeCamera ? "WIDOK: Z LOTU PTAKA" : "WIDOK: 3D";
    }

    void BuildCardPanel()
    {
        GameObject panel = Panel("Cards Panel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(560, 104), new Color(0.07f, 0.08f, 0.075f, 0.92f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        // Bottom pivot means the whole hand stays above the screen edge on every resolution.
        panelRect.pivot = new Vector2(0.5f, 0f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.52f, 0.26f, 0.95f);
        outline.effectDistance = new Vector2(2, -2);
        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
        panelRect.sizeDelta = new Vector2(560, 104);
    }

    void BuildNextCardPreview()
    {
        GameObject panel = Panel("Next Card Preview", canvas.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-326, 8), new Vector2(72, 92),
            new Color(0.07f, 0.08f, 0.075f, 0.94f));
        panel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.54f, 0.34f, 0.72f, 0.9f);
        outline.effectDistance = new Vector2(1, -1);

        Text label = MakeText("Next Label", panel.transform, "NEXT", 9, FontStyle.Bold,
            TextAnchor.MiddleCenter, new Color(0.78f, 0.7f, 0.86f));
        label.rectTransform.anchorMin = new Vector2(0, 0.76f);
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        GameObject swatch = Panel("Next Card Color", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 8), new Vector2(24, 24), Color.white);
        nextCardSwatch = swatch.GetComponent<Image>();
        nextCardSwatch.raycastTarget = false;

        nextCardText = MakeText("Next Card Name", panel.transform, "--", 9, FontStyle.Bold,
            TextAnchor.MiddleCenter, Color.white);
        nextCardText.rectTransform.anchorMin = new Vector2(0.04f, 0.04f);
        nextCardText.rectTransform.anchorMax = new Vector2(0.96f, 0.35f);
        nextCardText.rectTransform.offsetMin = Vector2.zero;
        nextCardText.rectTransform.offsetMax = Vector2.zero;
        nextCardText.resizeTextForBestFit = true;
        nextCardText.resizeTextMinSize = 7;
        nextCardText.resizeTextMaxSize = 9;
    }

    Transform GetCardPanel()
    {
        Transform existing = canvas.transform.Find("Cards Panel");
        if (existing != null) return existing;
        return null;
    }

    void RenderHand()
    {
        Transform panel = GetCardPanel();
        if (panel == null) return;
        for (int i = panel.childCount - 1; i >= 0; i--) Destroy(panel.GetChild(i).gameObject);
        cardObjects.Clear();
        for (int i = 0; i < hand.Count; i++)
        {
            CardData data = hand[i];
            GameObject card = new GameObject("Card - " + data.title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardDragHandler));
            card.transform.SetParent(panel, false);
            Image image = card.GetComponent<Image>(); image.color = Color.Lerp(data.color, new Color(0.045f, 0.06f, 0.065f), 0.62f);
            Button button = card.GetComponent<Button>();
            ColorBlock cb = button.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(0.78f, 0.78f, 0.78f); button.colors = cb;
            LayoutElement le = card.AddComponent<LayoutElement>(); le.minWidth = 120; le.preferredWidth = 130; le.minHeight = 88;
            Outline outline = card.AddComponent<Outline>(); outline.effectColor = new Color(data.color.r, data.color.g, data.color.b, 0.9f); outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject accent = Panel("Card Accent", card.transform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 5), data.color);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.pivot = new Vector2(0.5f, 1f);
            accent.GetComponent<Image>().raycastTarget = false;
            Text title = MakeText("Card Title", card.transform, data.title, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.67f); title.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
            title.rectTransform.offsetMin = Vector2.zero; title.rectTransform.offsetMax = Vector2.zero;

            string stats = data.isFireball
                ? "HP --   DMG " + data.damage
                : "HP " + data.health + "   DMG " + data.damage;
            Text statsText = MakeText("Card Stats", card.transform, stats, 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 0.95f, 0.91f));
            statsText.rectTransform.anchorMin = new Vector2(0.03f, 0.28f); statsText.rectTransform.anchorMax = new Vector2(0.97f, 0.60f);
            statsText.rectTransform.offsetMin = Vector2.zero; statsText.rectTransform.offsetMax = new Vector2(-42, 0);
            GameObject cost = Panel("Cost Badge", card.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 15), new Vector2(54, 23), new Color(0.44f, 0.20f, 0.67f, 1f));
            Outline costOutline = cost.AddComponent<Outline>(); costOutline.effectColor = new Color(0.91f, 0.70f, 1f); costOutline.effectDistance = new Vector2(1, -1);
            Text costText = MakeText("Cost", cost.transform, data.cost + " ELIXIR", 10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            costText.rectTransform.anchorMin = Vector2.zero; costText.rectTransform.anchorMax = Vector2.one; costText.rectTransform.offsetMin = Vector2.zero; costText.rectTransform.offsetMax = Vector2.zero;

            CardDragHandler drag = card.GetComponent<CardDragHandler>(); drag.ui = this; drag.cardIndex = i;
            cardObjects.Add(card);
        }
        UpdateCardCosts();
        UpdateNextCardPreview();
    }

    void UpdateNextCardPreview()
    {
        if (nextCardText == null || nextCardSwatch == null) return;
        int nextIndex = FindNextCardNotInHand(-1);
        if (nextIndex < 0)
        {
            nextCardText.text = "--";
            nextCardSwatch.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            return;
        }

        CardData nextCard = deck[nextIndex];
        nextCardText.text = nextCard.title;
        nextCardSwatch.color = nextCard.color;
    }

    string UnitName(BattleSystem.UnitType type)
    {
        switch (type)
        {
            case BattleSystem.UnitType.Knight: return "RYCERZ";
            case BattleSystem.UnitType.Archer: return "ŁUCZNIK";
            case BattleSystem.UnitType.Giant: return "OLBRZYM";
            case BattleSystem.UnitType.Goblin: return "GOBLIN";
            case BattleSystem.UnitType.Guard: return "STRAŻNIK";
            case BattleSystem.UnitType.Crossbowman: return "KUSZNIK";
            case BattleSystem.UnitType.Troll: return "TROLL";
            case BattleSystem.UnitType.Scout: return "ZWIADOWCA";
            case BattleSystem.UnitType.Veteran: return "WETERAN";
            case BattleSystem.UnitType.Hunter: return "ŁOWCA";
            case BattleSystem.UnitType.Mage: return "MAG";
            case BattleSystem.UnitType.Skeleton: return "SZKIELETY";
            case BattleSystem.UnitType.Cannon: return "CANNON";
            case BattleSystem.UnitType.Dragon: return "DRAGON";
            case BattleSystem.UnitType.HogRider: return "HOG RIDER";
            case BattleSystem.UnitType.Bat: return "NIETOPERZE";
            case BattleSystem.UnitType.MiniPekka: return "MINI P.E.K.K.A";
            default: return "PROCARZ";
        }
    }

    void BuildEmotes()
    {
        string[] labels = { "BRAWO!", "O NIE!", "HAHA!", "WALCZ!" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            GameObject b = MakeButton("Emote " + i, canvas.transform, labels[i], 12, new Color(0.12f, 0.15f, 0.17f, 0.88f), () => ShowEmote(index));
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(1, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.pivot = new Vector2(1, 0.5f);
            r.sizeDelta = new Vector2(86, 29); r.anchoredPosition = new Vector2(-8, 49 - i * 33);
        }
        CreateEmoteBubble(true);
        CreateEmoteBubble(false);
    }

    void CreateEmoteBubble(bool playerSide)
    {
        GameObject bubble = Panel(playerSide ? "Player King Emote Bubble" : "Enemy King Emote Bubble", canvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 46),
            new Color(0.98f, 0.96f, 0.86f, 0.97f));
        Outline outline = bubble.AddComponent<Outline>();
        outline.effectColor = playerSide ? new Color(0.28f, 0.62f, 1f) : new Color(1f, 0.36f, 0.28f);
        outline.effectDistance = new Vector2(2, -2);
        Text label = MakeText("Emote Text", bubble.transform, "", 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.13f, 0.13f, 0.13f));
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(4, 2); label.rectTransform.offsetMax = new Vector2(-4, -2);
        bubble.GetComponent<Image>().raycastTarget = false;
        bubble.SetActive(false);
        if (playerSide)
        {
            playerEmoteBubble = bubble; playerEmoteRect = bubble.GetComponent<RectTransform>(); playerEmoteText = label;
        }
        else
        {
            enemyEmoteBubble = bubble; enemyEmoteRect = bubble.GetComponent<RectTransform>(); enemyEmoteText = label;
        }
    }

    void BuildPauseAndSettingsUI()
    {
        audioSystem = FindObjectOfType<AudioSystem>();
        pauseOverlay = Panel("Pause Overlay", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.035f, 0.82f));
        RectTransform overlayRect = pauseOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero; overlayRect.offsetMax = Vector2.zero;
        pauseOverlay.transform.SetAsLastSibling();
        pauseOverlay.SetActive(false);

        pausePanel = Panel("Pause Panel", pauseOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 390), new Color(0.10f, 0.13f, 0.15f, 0.98f));
        Outline pauseOutline = pausePanel.AddComponent<Outline>(); pauseOutline.effectColor = new Color(0.77f, 0.60f, 0.30f); pauseOutline.effectDistance = new Vector2(3, -3);
        AddOverlayTitle(pausePanel.transform, "PAUZA", 0.78f, 0.98f);
        AddOverlayButton(pausePanel.transform, "WZNÓW GRĘ", 0.59f, 0.73f, new Color(0.20f, 0.48f, 0.28f), ResumeGame);
        AddOverlayButton(pausePanel.transform, "USTAWIENIA", 0.42f, 0.56f, new Color(0.20f, 0.32f, 0.48f), ShowSettings);
        AddOverlayButton(pausePanel.transform, "MENU GŁÓWNE", 0.25f, 0.39f, new Color(0.48f, 0.29f, 0.17f), ShowMainMenu);

        settingsPanel = Panel("Settings Panel", pauseOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470, 390), new Color(0.10f, 0.13f, 0.15f, 0.99f));
        Outline settingsOutline = settingsPanel.AddComponent<Outline>(); settingsOutline.effectColor = new Color(0.77f, 0.60f, 0.30f); settingsOutline.effectDistance = new Vector2(3, -3);
        AddOverlayTitle(settingsPanel.transform, "USTAWIENIA DŹWIĘKU", 0.79f, 0.97f);
        AddOverlayLabel(settingsPanel.transform, "Muzyka", 0.64f, 0.73f);
        musicSlider = CreateVolumeSlider(settingsPanel.transform, "Music Volume", 0.57f, 0.63f, 0.8f, SetMusicVolume);
        AddOverlayLabel(settingsPanel.transform, "Efekty dźwiękowe", 0.44f, 0.53f);
        effectsSlider = CreateVolumeSlider(settingsPanel.transform, "Effects Volume", 0.36f, 0.42f, 0.8f, SetEffectsVolume);
        AddOverlayButton(settingsPanel.transform, "WRÓĆ", 0.12f, 0.27f, new Color(0.20f, 0.42f, 0.29f), ShowPreviousMenu);
        settingsPanel.SetActive(false);

        mainMenuPanel = Panel("Main Menu Overlay Panel", pauseOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430, 360), new Color(0.10f, 0.13f, 0.15f, 0.99f));
        Outline menuOutline = mainMenuPanel.AddComponent<Outline>(); menuOutline.effectColor = new Color(0.77f, 0.60f, 0.30f); menuOutline.effectDistance = new Vector2(3, -3);
        AddOverlayTitle(mainMenuPanel.transform, "MENU GŁÓWNE", 0.77f, 0.97f);
        AddOverlayButton(mainMenuPanel.transform, "NOWY MECZ", 0.55f, 0.69f, new Color(0.20f, 0.48f, 0.28f), RestartMatch);
        AddOverlayButton(mainMenuPanel.transform, "WRÓĆ DO GRY", 0.36f, 0.50f, new Color(0.20f, 0.32f, 0.48f), ResumeGame);
        AddOverlayButton(mainMenuPanel.transform, "USTAWIENIA", 0.17f, 0.31f, new Color(0.35f, 0.30f, 0.48f), ShowSettingsFromMainMenu);
        AddOverlayButton(mainMenuPanel.transform, "WYJDŹ Z GRY", 0.02f, 0.13f, new Color(0.45f, 0.18f, 0.16f), QuitGame);
        mainMenuPanel.SetActive(false);
    }

    void AddOverlayTitle(Transform parent, string value, float minY, float maxY)
    {
        Text title = MakeText("Overlay Title", parent, value, 27, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.56f));
        RectTransform r = title.rectTransform; r.anchorMin = new Vector2(0.06f, minY); r.anchorMax = new Vector2(0.94f, maxY); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    void AddOverlayLabel(Transform parent, string value, float minY, float maxY)
    {
        Text label = MakeText("Settings Label", parent, value, 17, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        RectTransform r = label.rectTransform; r.anchorMin = new Vector2(0.12f, minY); r.anchorMax = new Vector2(0.88f, maxY); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    void AddOverlayButton(Transform parent, string value, float minY, float maxY, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject button = MakeButton(value, parent, value, 17, color, action);
        RectTransform r = button.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0.14f, minY); r.anchorMax = new Vector2(0.86f, maxY); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    Slider CreateVolumeSlider(Transform parent, string name, float minY, float maxY, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        RectTransform rr = root.GetComponent<RectTransform>(); rr.anchorMin = new Vector2(0.13f, minY); rr.anchorMax = new Vector2(0.87f, maxY); rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(root.transform, false);
        RectTransform br = background.GetComponent<RectTransform>(); br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(0, 7); br.offsetMax = new Vector2(0, -7);
        background.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 1f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(root.transform, false);
        RectTransform fr = fill.GetComponent<RectTransform>(); fr.anchorMin = new Vector2(0, 0.15f); fr.anchorMax = new Vector2(initialValue, 0.85f); fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.70f, 0.48f, 0.22f, 1f);
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(root.transform, false);
        RectTransform hr = handle.GetComponent<RectTransform>(); hr.sizeDelta = new Vector2(18, 28); hr.anchorMin = new Vector2(initialValue, 0.5f); hr.anchorMax = new Vector2(initialValue, 0.5f); hr.anchoredPosition = Vector2.zero;
        Image handleImage = handle.GetComponent<Image>(); handleImage.color = new Color(1f, 0.86f, 0.55f, 1f);
        Slider slider = root.GetComponent<Slider>(); slider.minValue = 0f; slider.maxValue = 1f; slider.value = initialValue; slider.fillRect = fr; slider.handleRect = hr; slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    void SetMusicVolume(float value)
    {
        if (audioSystem == null) audioSystem = FindObjectOfType<AudioSystem>();
        if (audioSystem != null) audioSystem.SetMusicVolume(value);
    }

    void SetEffectsVolume(float value)
    {
        if (audioSystem == null) audioSystem = FindObjectOfType<AudioSystem>();
        if (audioSystem != null) audioSystem.SetEffectsVolume(value);
    }

    void TogglePause()
    {
        if (matchEnded) return;
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
            pauseOverlay.SetActive(true);
            ShowPausePanel();
        }
        else if (settingsPanel.activeSelf || mainMenuPanel.activeSelf)
        {
            ShowPausePanel();
        }
        else ResumeGame();
    }

    void ShowPausePanel()
    {
        settingsFromMainMenu = false;
        pausePanel.SetActive(true); settingsPanel.SetActive(false); mainMenuPanel.SetActive(false);
    }

    void ShowSettings()
    {
        settingsFromMainMenu = false;
        pausePanel.SetActive(false); settingsPanel.SetActive(true); mainMenuPanel.SetActive(false);
    }

    void ShowSettingsFromMainMenu()
    {
        settingsFromMainMenu = true;
        pausePanel.SetActive(false); settingsPanel.SetActive(true); mainMenuPanel.SetActive(false);
    }

    void ShowPreviousMenu()
    {
        if (settingsFromMainMenu) ShowMainMenu();
        else ShowPausePanel();
    }

    void ShowMainMenu()
    {
        pausePanel.SetActive(false); settingsPanel.SetActive(false); mainMenuPanel.SetActive(true);
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
    }

    void RestartMatch()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnDestroy()
    {
        if (isPaused) Time.timeScale = 1f;
    }

    void BuildStatus()
    {
        statusText = MakeText("Status Message", canvas.transform, "Przeciągnij kartę na swoją połowę areny", 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform r = statusText.rectTransform;
        r.anchorMin = new Vector2(0.5f, 1f); r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(470, 22); r.anchoredPosition = new Vector2(0, -54);
    }

    GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = position; rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return go;
    }

    Text MakeText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        return text;
    }

    GameObject MakeButton(string name, Transform parent, string label, int fontSize, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        Button button = go.GetComponent<Button>(); button.onClick.AddListener(action);
        Text text = MakeText("Label", go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = Vector2.zero; text.rectTransform.offsetMax = Vector2.zero;
        return go;
    }

    public void BeginCardDrag(int index, Vector2 screenPosition)
    {
        if (index < 0 || index >= hand.Count || battle == null) return;
        draggingIndex = index;
        CardData data = hand[index];
        dragGhost = Panel("Dragging Card Ghost", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 82), new Color(0.08f, 0.10f, 0.11f, 0.96f));
        dragGhost.transform.SetAsLastSibling();
        dragGhostRect = dragGhost.GetComponent<RectTransform>();
        Outline outline = dragGhost.AddComponent<Outline>(); outline.effectColor = data.color; outline.effectDistance = new Vector2(2, -2);
        string unitLabel = data.isFireball ? "DOWOLNE MIEJSCE NA MAPIE" : UnitName(data.type) + (data.count > 1 ? " x" + data.count : "");
        dragGhostText = MakeText("Drag Card Label", dragGhost.transform, data.title + "\n" + unitLabel + "\n" + data.cost + " eliksiru", 11, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        dragGhostText.rectTransform.anchorMin = Vector2.zero; dragGhostText.rectTransform.anchorMax = Vector2.one; dragGhostText.rectTransform.offsetMin = new Vector2(4, 3); dragGhostText.rectTransform.offsetMax = new Vector2(-4, -3);
        dragGhost.GetComponent<Image>().raycastTarget = false;
        dragGhostText.raycastTarget = false;
        CreateDeploymentPreview();
        SetStatus("Upuść na swojej połowie. Zielony znacznik oznacza poprawne miejsce.");
        UpdateCardDrag(screenPosition);
    }

    public void UpdateCardDrag(Vector2 screenPosition)
    {
        if (dragGhostRect == null || canvasRect == null) return;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out local))
            dragGhostRect.anchoredPosition = local;
        UpdateDeploymentPreview(screenPosition);
    }

    public void EndCardDrag(int index, Vector2 screenPosition, int pointerId)
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null; dragGhostRect = null; dragGhostText = null;
        ClearDeploymentPreview();
        int cardIndex = draggingIndex; draggingIndex = -1;
        if (matchEnded || cardIndex < 0 || cardIndex >= hand.Count || cardIndex != index) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
        {
            SetStatus("Upuść kartę na trawie po swojej stronie.");
            return;
        }
        Camera cam = Camera.main;
        if (cam == null) { SetStatus("Brak kamery MainCamera."); return; }
        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        float enter;
        if (!ground.Raycast(ray, out enter)) { SetStatus("Nie udało się wyznaczyć miejsca wystawienia."); return; }
        Vector3 position = ray.GetPoint(enter);
        position.y = 0f;
        CardData card = hand[cardIndex];
        if (elixir < card.cost) { SetStatus("Za mało eliksiru!"); return; }
        if (card.isFireball)
        {
            if (!battle.CastFireball(position, true)) { SetStatus("Rzuć Fireball na arenę."); return; }
        }
        else if (card.type == BattleSystem.UnitType.Cannon && battle.CountCannons(true) >= 2)
        {
            SetStatus("Na swojej stronie możesz mieć maksymalnie 2 Cannony.");
            return;
        }
        else if (!battle.TryDeploy(card.type, position, card.count))
        {
            SetStatus("Wystawiaj jednostki na swojej połowie areny.");
            return;
        }
        elixir -= card.cost;
        ShowElixirSpent(position, card.cost);
        recentlyPlayedCards.Add(card);
        if (recentlyPlayedCards.Count > 2)
            recentlyPlayedCards.RemoveAt(0);
        int replacementIndex = FindNextCardNotInHand(cardIndex);
        if (replacementIndex >= 0)
        {
            hand[cardIndex] = deck[replacementIndex];
            nextDeckCard = (replacementIndex + 1) % deck.Count;
        }
        RenderHand();
        SetStatus(card.title + " wystawiony!");
    }

    void CreateDeploymentPreview()
    {
        ClearDeploymentPreview();
        deployGuide = new GameObject("Deployment Area Guide");
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        deployGuideMaterial = new Material(shader);
        deployGuideMaterial.color = new Color(0.20f, 0.95f, 0.55f, 1f);
        bool fireball = hand[draggingIndex].isFireball;
        float minX = fireball ? -9.5f : battle.DeployMinX;
        float maxX = fireball ? 9.5f : battle.DeployMaxX;
        float minZ = fireball ? -17.5f : battle.PlayerDeployMinZ;
        float maxZ = fireball ? 17.5f : battle.PlayerDeployMaxZ;
        float midX = (minX + maxX) * 0.5f;
        float midZ = (minZ + maxZ) * 0.5f;
        CreateDeploymentGuideLine(new Vector3(midX, 0.055f, minZ), new Vector3(maxX - minX, 0.035f, 0.07f));
        CreateDeploymentGuideLine(new Vector3(midX, 0.055f, maxZ), new Vector3(maxX - minX, 0.035f, 0.07f));
        CreateDeploymentGuideLine(new Vector3(minX, 0.055f, midZ), new Vector3(0.07f, 0.035f, maxZ - minZ));
        CreateDeploymentGuideLine(new Vector3(maxX, 0.055f, midZ), new Vector3(0.07f, 0.035f, maxZ - minZ));

        deployMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        deployMarker.name = "Deployment Drop Marker";
        deployMarker.transform.localScale = new Vector3(0.8f, 0.025f, 0.8f);
        Renderer markerRenderer = deployMarker.GetComponent<Renderer>();
        deployMarkerMaterial = new Material(shader);
        deployMarkerMaterial.color = new Color(0.20f, 0.95f, 0.55f, 1f);
        markerRenderer.sharedMaterial = deployMarkerMaterial;
        Collider markerCollider = deployMarker.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);
        deployMarker.SetActive(false);
    }

    void CreateDeploymentGuideLine(Vector3 position, Vector3 scale)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.transform.SetParent(deployGuide.transform, false);
        line.transform.localPosition = position;
        line.transform.localScale = scale;
        line.GetComponent<Renderer>().sharedMaterial = deployGuideMaterial;
        Collider lineCollider = line.GetComponent<Collider>();
        if (lineCollider != null) Destroy(lineCollider);
    }

    void UpdateDeploymentPreview(Vector2 screenPosition)
    {
        if (deployMarker == null || battle == null || draggingIndex < 0 || draggingIndex >= hand.Count)
            return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        float enter;
        if (!ground.Raycast(ray, out enter))
        {
            deployMarker.SetActive(false);
            return;
        }

        Vector3 position = ray.GetPoint(enter);
        position.y = 0.08f;
        deployMarker.transform.position = position;
        deployMarker.SetActive(true);
        CardData card = hand[draggingIndex];
        bool allowed = card.isFireball
            ? battle.CanCastFireballAt(position)
            : battle.CanDeployAt(position, card.count);
        deployMarkerMaterial.color = allowed
            ? new Color(0.20f, 0.95f, 0.55f, 1f)
            : new Color(1f, 0.22f, 0.18f, 1f);
    }

    void ClearDeploymentPreview()
    {
        if (deployGuide != null) Destroy(deployGuide);
        if (deployMarker != null) Destroy(deployMarker);
        if (deployGuideMaterial != null) Destroy(deployGuideMaterial);
        if (deployMarkerMaterial != null) Destroy(deployMarkerMaterial);
        deployGuide = null;
        deployMarker = null;
        deployGuideMaterial = null;
        deployMarkerMaterial = null;
    }


    int FindNextCardNotInHand(int replacingHandIndex)
    {
        if (deck.Count == 0) return -1;
        for (int offset = 0; offset < deck.Count; offset++)
        {
            int candidateIndex = (nextDeckCard + offset) % deck.Count;
            CardData candidate = deck[candidateIndex];
            if (candidate.type == BattleSystem.UnitType.Cannon && battle != null && battle.CountCannons(true) >= 2)
                continue;
            if (recentlyPlayedCards.Contains(candidate))
                continue;
            bool alreadyVisible = false;
            for (int i = 0; i < hand.Count; i++)
            {
                if (i == replacingHandIndex) continue;
                if (ReferenceEquals(hand[i], candidate))
                {
                    alreadyVisible = true;
                    break;
                }
            }
            if (!alreadyVisible) return candidateIndex;
        }
        return -1;
    }

    // Kept for compatibility with GameManager scripts that call this every frame.
    public void HandleArenaClick() { }

    public void ShowGameOver(bool won, string reason)
    {
        matchEnded = true;
        if (gameOverPanel != null) Destroy(gameOverPanel);
        gameOverPanel = Panel("Game Over Panel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470, 260), new Color(0.07f, 0.08f, 0.09f, 0.96f));
        Outline outline = gameOverPanel.AddComponent<Outline>(); outline.effectColor = won ? new Color(0.3f, 0.9f, 0.45f) : new Color(0.9f, 0.3f, 0.25f); outline.effectDistance = new Vector2(3, -3);
        Text title = MakeText("Result", gameOverPanel.transform, won ? "ZWYCIĘSTWO!" : "KONIEC GRY", 32, FontStyle.Bold, TextAnchor.MiddleCenter, won ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.48f, 0.42f));
        title.rectTransform.anchorMin = new Vector2(0, 0.58f); title.rectTransform.anchorMax = new Vector2(1, 0.95f); title.rectTransform.offsetMin = Vector2.zero; title.rectTransform.offsetMax = Vector2.zero;
        Text details = MakeText("Result Details", gameOverPanel.transform, reason ?? "", 16, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
        details.rectTransform.anchorMin = new Vector2(0, 0.38f); details.rectTransform.anchorMax = new Vector2(1, 0.62f); details.rectTransform.offsetMin = Vector2.zero; details.rectTransform.offsetMax = Vector2.zero;
        GameObject replay = MakeButton("Replay Button", gameOverPanel.transform, "ZAGRAJ PONOWNIE", 18, new Color(0.18f, 0.43f, 0.25f), () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        RectTransform rr = replay.GetComponent<RectTransform>(); rr.anchorMin = new Vector2(0.5f, 0.12f); rr.anchorMax = new Vector2(0.5f, 0.12f); rr.sizeDelta = new Vector2(220, 48); rr.anchoredPosition = Vector2.zero;
    }

    public void ResetMatch()
    {
        matchEnded = false;
        elixir = 5f; regenTimer = 0f; hand.Clear(); ShuffleDeck();
        recentlyPlayedCards.Clear();
        for (int i = 0; i < Mathf.Min(4, deck.Count); i++) hand.Add(deck[i]);
        nextDeckCard = 4;
        RenderHand();
        if (gameOverPanel != null) Destroy(gameOverPanel);
        gameOverPanel = null;
        SetStatus("Przeciągnij kartę na swoją połowę areny.");
    }

    void ShowEmote(int index)
    {
        string[] messages = { "BRAWO!", "O NIE!", "HAHA!", "WALCZ!" };
        if (playerEmoteText != null)
        {
            playerEmoteText.text = messages[Mathf.Clamp(index, 0, messages.Length - 1)];
            playerEmoteBubble.SetActive(true);
            playerEmoteLife = 2.5f;
            if (battle != null && Camera.main != null) PositionBubble(playerEmoteBubble, playerEmoteRect, battle.GetKingTowerPosition(true), Camera.main);
        }
        SetStatus("Ty: " + messages[Mathf.Clamp(index, 0, messages.Length - 1)]);
    }

    void ShowEnemyEmote()
    {
        string[] messages = { "Powodzenia!", "O nie!", "Haha!", "Walczmy!" };
        if (enemyEmoteText == null) return;
        enemyEmoteText.text = messages[Random.Range(0, messages.Length)];
        enemyEmoteBubble.SetActive(true);
        enemyEmoteLife = 2.5f;
        if (battle != null && Camera.main != null) PositionBubble(enemyEmoteBubble, enemyEmoteRect, battle.GetKingTowerPosition(false), Camera.main);
    }

    void UpdateEmoteBubbles(float dt)
    {
        Camera cam = Camera.main;
        if (cam == null || battle == null) return;
        if (playerEmoteLife > 0f)
        {
            playerEmoteLife -= dt;
            PositionBubble(playerEmoteBubble, playerEmoteRect, battle.GetKingTowerPosition(true), cam);
            if (playerEmoteLife <= 0f && playerEmoteBubble != null) playerEmoteBubble.SetActive(false);
        }
        if (enemyEmoteLife > 0f)
        {
            enemyEmoteLife -= dt;
            PositionBubble(enemyEmoteBubble, enemyEmoteRect, battle.GetKingTowerPosition(false), cam);
            if (enemyEmoteLife <= 0f && enemyEmoteBubble != null) enemyEmoteBubble.SetActive(false);
        }
    }

    void PositionBubble(GameObject bubble, RectTransform rect, Vector3 towerPosition, Camera cam)
    {
        if (bubble == null || rect == null) return;
        Vector3 world = towerPosition + Vector3.up * 5.2f;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) { bubble.SetActive(false); return; }
        bubble.SetActive(true);
        rect.position = screen + new Vector3(0f, 8f, 0f);
    }

    void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }

    void ShowElixirSpent(Vector3 worldPosition, int amount)
    {
        if (elixirCostPopup != null) Destroy(elixirCostPopup);
        elixirCostPopup = new GameObject("Elixir Spent Popup");
        elixirCostPopup.transform.position = worldPosition + Vector3.up * 1.8f;
        elixirCostStartPosition = elixirCostPopup.transform.position;
        elixirCostText = elixirCostPopup.AddComponent<TextMesh>();
        elixirCostText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        elixirCostText.fontSize = 36;
        elixirCostText.characterSize = 0.07f;
        elixirCostText.anchor = TextAnchor.MiddleCenter;
        elixirCostText.alignment = TextAlignment.Center;
        elixirCostText.fontStyle = FontStyle.Bold;
        elixirCostText.color = new Color(0.78f, 0.42f, 1f);
        elixirCostText.text = "-" + amount;

        elixirCostShadow = new GameObject("Elixir Spent Shadow").AddComponent<TextMesh>();
        elixirCostShadow.transform.SetParent(elixirCostPopup.transform, false);
        elixirCostShadow.transform.localPosition = new Vector3(0.018f, -0.018f, -0.006f);
        elixirCostShadow.font = elixirCostText.font;
        elixirCostShadow.fontSize = elixirCostText.fontSize;
        elixirCostShadow.characterSize = elixirCostText.characterSize * 1.08f;
        elixirCostShadow.anchor = elixirCostText.anchor;
        elixirCostShadow.alignment = elixirCostText.alignment;
        elixirCostShadow.fontStyle = FontStyle.Bold;
        elixirCostShadow.color = new Color(0.12f, 0.06f, 0.18f, 0.85f);
        elixirCostShadow.text = elixirCostText.text;
        elixirCostPopupRemaining = 0.75f;
    }

    void UpdateElixirSpentPopup(float dt)
    {
        if (elixirCostPopup == null) return;
        elixirCostPopupRemaining -= dt;
        float progress = 1f - Mathf.Clamp01(elixirCostPopupRemaining / 0.75f);
        elixirCostPopup.transform.position = elixirCostStartPosition + Vector3.up * (0.3f * progress);
        Camera cam = Camera.main;
        if (cam != null) elixirCostPopup.transform.rotation = cam.transform.rotation;
        Color textColor = elixirCostText.color;
        textColor.a = 1f - progress;
        elixirCostText.color = textColor;
        Color shadowColor = elixirCostShadow.color;
        shadowColor.a = 0.85f * (1f - progress);
        elixirCostShadow.color = shadowColor;
        if (elixirCostPopupRemaining <= 0f)
        {
            Destroy(elixirCostPopup);
            elixirCostPopup = null;
            elixirCostText = null;
            elixirCostShadow = null;
        }
    }

    void UpdateCardCosts()
    {
        for (int i = 0; i < cardObjects.Count && i < hand.Count; i++)
        {
            Button button = cardObjects[i].GetComponent<Button>();
            bool affordable = elixir >= hand[i].cost;
            if (button != null) button.interactable = true; // Keep drag available; cost is checked on drop.
            Image image = cardObjects[i].GetComponent<Image>();
            if (image != null) image.color = affordable ? hand[i].color : Color.Lerp(hand[i].color, Color.black, 0.42f);
        }
    }

    bool IsDoubleElixirActive()
    {
        if (manager == null) return false;
        string value = manager.GetTimeText();
        if (string.IsNullOrEmpty(value)) return false;
        string[] parts = value.Split(':');
        if (parts.Length != 2) return false;
        int minutes, seconds;
        if (!int.TryParse(parts[0], out minutes) || !int.TryParse(parts[1], out seconds)) return false;
        return minutes * 60 + seconds <= 60;
    }

    void Update()
    {
        if (!initialized) return;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (isPaused) return;
        UpdateElixirSpentPopup(Time.deltaTime);
        float regenSeconds = IsDoubleElixirActive() ? ElixirRegenSeconds * 0.5f : ElixirRegenSeconds;
        regenTimer += Time.deltaTime;
        while (regenTimer >= regenSeconds)
        {
            regenTimer -= regenSeconds;
            elixir = Mathf.Min(MaxElixir, elixir + 1f);
        }
        if (elixirText != null)
            elixirText.text = "ELIKSIR " + Mathf.FloorToInt(elixir) + " / 10" + (IsDoubleElixirActive() ? "   x2" : "");
        if (elixirFill != null)
        {
            bool doubleElixir = IsDoubleElixirActive();
            elixirFill.rectTransform.sizeDelta = new Vector2(266f * elixir / MaxElixir, 7f);
            elixirFill.color = doubleElixir ? new Color(1f, 0.69f, 0.25f) : new Color(0.30f, 0.82f, 0.72f);
        }
        if (timerText != null && manager != null) timerText.text = manager.GetTimeText();
        botEmoteTimer -= Time.deltaTime;
        if (botEmoteTimer <= 0f && !matchEnded)
        {
            ShowEnemyEmote();
            botEmoteTimer = Random.Range(10f, 18f);
        }
        UpdateEmoteBubbles(Time.deltaTime);
        UpdateCardCosts();
    }
}
