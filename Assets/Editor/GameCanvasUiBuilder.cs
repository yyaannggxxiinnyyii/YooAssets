using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 在当前场景中生成一套基于 CanvasGroup 的游戏 UI 骨架，并自动绑定基础控制脚本。
/// </summary>
public static class GameCanvasUiBuilder
{
    private const string RootName = "GameCanvasRoot";
    private const string MenuPath = "Tools/Game UI/生成 Canvas UI 骨架";
    private const string SolidSpritePath = "Assets/GameRes/ArtAssets/纯色.png";
    private const string FullHeartSpritePath = "Assets/GameRes/ArtAssets/满心.png";
    private const string HalfHeartSpritePath = "Assets/GameRes/ArtAssets/半心.png";
    private const string EmptyHeartSpritePath = "Assets/GameRes/ArtAssets/空心血槽.png";
    private const string FallbackEmptyHeartSpritePath = "Assets/GameRes/ArtAssets/空心.png";
    private const string HeartPrefabFolder = "Assets/Game/Prefabs/UI";
    private const string RedHeartPrefabPath = HeartPrefabFolder + "/HeartSlot_Red.prefab";
    private const string BlueHeartPrefabPath = HeartPrefabFolder + "/HeartSlot_Blue.prefab";
    private const string PinkHeartPrefabPath = HeartPrefabFolder + "/HeartSlot_Pink.prefab";
    private const string GlassHeartPrefabPath = HeartPrefabFolder + "/HeartSlot_Glass.prefab";
    private const string ExplosiveHeartPrefabPath = HeartPrefabFolder + "/HeartSlot_Explosive.prefab";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    /// <summary>
    /// 强制重建心槽 prefab，修复手写或旧版本 prefab 中的脚本丢失问题。
    /// </summary>
    [MenuItem("Tools/Game UI/重建心槽 Prefab")]
    public static void RebuildHeartSlotPrefabs()
    {
        EnsureHeartSlotPrefabs(true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameUI] 心槽 prefab 已重建。");
    }

    /// <summary>
    /// 创建完整 UI 骨架，供策划或美术在场景中继续细调。
    /// </summary>
    [MenuItem(MenuPath)]
    public static void BuildCanvasUi()
    {
        BuildCanvasUi(false, false);
    }

    /// <summary>
    /// 批处理模式下强制重建当前场景 UI 并保存场景。
    /// </summary>
    public static void BuildCanvasUiForBatch()
    {
        BuildCanvasUi(true, true);
    }

    /// <summary>
    /// 批处理模式下打开主场景，强制重建 UI 并保存场景。
    /// </summary>
    public static void BuildMainSceneCanvasUiForBatch()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/MainScene.unity");
        BuildCanvasUi(true, true);
    }

    /// <summary>
    /// 创建完整 UI 骨架，可选择强制替换并保存场景。
    /// </summary>
    /// <param name="forceReplace">是否跳过替换确认。</param>
    /// <param name="saveScene">是否保存当前打开场景。</param>
    private static void BuildCanvasUi(bool forceReplace, bool saveScene)
    {
        Canvas canvas = GetOrCreateCanvas();
        EnsureEventSystem();
        EnsureHeartSlotPrefabs(false);
        GameStateManager gameStateManager = Object.FindObjectOfType<GameStateManager>();

        if (gameStateManager == null)
        {
            EditorUtility.DisplayDialog("生成失败", "当前场景中没有找到 GameStateManager。", "确定");
            return;
        }

        Transform oldRoot = canvas.transform.Find(RootName);
        if (oldRoot != null && !forceReplace && !EditorUtility.DisplayDialog("替换 UI 骨架", "当前 Canvas 下已经存在 GameCanvasRoot，是否替换？", "替换", "取消"))
            return;

        if (oldRoot != null)
            Undo.DestroyObjectImmediate(oldRoot.gameObject);

        RectTransform root = CreateRect("GameCanvasRoot", canvas.transform);
        Stretch(root);
        root.SetAsLastSibling();

        GameCanvasPage titlePage = CreateTitlePanel(root, out Button titleClickButton, out TMP_Text titleText, out TMP_Text titleHintText);
        GameCanvasPage characterSelectPage = CreateCharacterSelectPanel(root, out CharacterSelectRefs characterSelectRefs);
        GameCanvasPage weaponSelectPage = CreateWeaponSelectPanel(root, out WeaponSelectRefs weaponSelectRefs);
        GameCanvasPage hudPage = CreateHudPanel(root, out HudRefs hudRefs);
        GameCanvasPage levelUpPage = CreateOfferPanel(root, "LevelUpPanel", "升级强化", out OfferPanelRefs levelUpRefs);
        GameCanvasPage weaponUpgradePage = CreateOfferPanel(root, "WeaponUpgradePanel", "武器强化", out OfferPanelRefs weaponRefs);
        GameCanvasPage shopPage = CreateShopPanel(root, out ShopRefs shopRefs);
        GameCanvasPage resultPage = CreateResultPanel(root, out ResultRefs resultRefs);
        GameCanvasPage pausePage = CreatePausePanel(root, out PauseRefs pauseRefs);

        GameCanvasPageRouter router = root.gameObject.AddComponent<GameCanvasPageRouter>();
        GameFlowCanvasController flowController = root.gameObject.AddComponent<GameFlowCanvasController>();
        GameCombatCanvasController combatController = root.gameObject.AddComponent<GameCombatCanvasController>();

        BindRouter(router, gameStateManager, titlePage, characterSelectPage, weaponSelectPage, hudPage, levelUpPage, weaponUpgradePage, shopPage, resultPage, pausePage);
        BindFlowController(flowController, gameStateManager, titleClickButton, titleText, titleHintText, characterSelectRefs, weaponSelectRefs);
        BindCombatController(combatController, gameStateManager, hudRefs, levelUpRefs, weaponRefs, shopRefs, resultRefs, pauseRefs);
        BindCombatControllerToGame(flowController, combatController);

        Selection.activeObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        if (saveScene)
            EditorSceneManager.SaveScene(root.gameObject.scene);

        Debug.Log("[GameUI] Canvas UI 骨架生成完成。");
    }

    /// <summary>
    /// 获取当前场景 Canvas，不存在时创建 Screen Space Overlay Canvas。
    /// </summary>
    /// <returns>可用 Canvas。</returns>
    private static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
            return canvas;

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    /// <summary>
    /// 确保场景里存在 EventSystem，保证按钮可以响应输入。
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
    }

    /// <summary>
    /// 创建标题页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="clickButton">全屏点击按钮。</param>
    /// <param name="titleText">标题文本。</param>
    /// <param name="hintText">提示文本。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateTitlePanel(
        Transform parent,
        out Button clickButton,
        out TMP_Text titleText,
        out TMP_Text hintText)
    {
        RectTransform panel = CreatePanel("TitlePanel", parent, new Color(0.04f, 0.06f, 0.09f, 1f));
        GameCanvasPage page = AddPage(panel);

        titleText = CreateText("TitleText", panel, "戳泡泡", 72, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.75f), Vector2.zero, Vector2.zero);

        hintText = CreateText("HintText", panel, "按任意键继续", 30, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.82f, 0.9f, 1f));
        SetAnchor(hintText.rectTransform, new Vector2(0.2f, 0.36f), new Vector2(0.8f, 0.45f), Vector2.zero, Vector2.zero);

        clickButton = CreateTransparentButton("ClickCatcher", panel);
        Stretch(clickButton.GetComponent<RectTransform>());
        clickButton.transform.SetAsLastSibling();
        GameTitlePanelController titlePanelController = panel.gameObject.AddComponent<GameTitlePanelController>();
        BindTitlePanelController(titlePanelController, titleText, hintText, clickButton);
        return page;
    }

    /// <summary>
    /// 创建角色选择页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">角色选择页引用。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateCharacterSelectPanel(Transform parent, out CharacterSelectRefs refs)
    {
        RectTransform panel = CreatePanel("CharacterSelectPanel", parent, new Color(0.05f, 0.055f, 0.065f, 0.98f));
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("CharacterSelectTitleText", panel, "选择角色", 46, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.93f), Vector2.zero, Vector2.zero);

        RectTransform optionPanel = CreateCard("CharacterOptions", panel, new Vector2(0.1f, 0.18f), new Vector2(0.48f, 0.78f));
        TMP_Text optionTitleText = CreateText("OptionTitleText", optionPanel, "角色列表", 26, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.9f, 0.95f, 1f));
        SetAnchor(optionTitleText.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);

        Button[] optionButtons = new Button[6];
        TMP_Text[] optionTexts = new TMP_Text[6];
        CreateOptionGrid(optionPanel, "CharacterOption", optionButtons, optionTexts);

        RectTransform detailPanel = CreateCard("CharacterDetail", panel, new Vector2(0.52f, 0.18f), new Vector2(0.9f, 0.78f));
        TMP_Text nameText = CreateText("NameText", detailPanel, "见习冒险者", 38, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        TMP_Text descriptionText = CreateText("DescriptionText", detailPanel, "选择一名角色作为本局基础属性。", 24, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.94f));
        TMP_Text statsText = CreateText("StatsText", detailPanel, "血量：5 心\n基础攻击力：12\n攻击力倍率：100%\n移速：4.50\n经验倍率：100%\n闪避无敌：0.20s", 26, FontStyles.Normal, TextAlignmentOptions.Left, new Color(1f, 0.92f, 0.68f));
        SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
        SetAnchor(descriptionText.rectTransform, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
        SetAnchor(statsText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.42f), Vector2.zero, Vector2.zero);

        Button confirmButton = CreateButton("ConfirmCharacterButton", panel, "选择角色", 30);
        SetAnchor(confirmButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.07f), new Vector2(0.59f, 0.15f), Vector2.zero, Vector2.zero);

        refs = new CharacterSelectRefs(titleText, nameText, descriptionText, statsText, confirmButton, optionButtons, optionTexts);
        GameCharacterSelectPanelController panelController = panel.gameObject.AddComponent<GameCharacterSelectPanelController>();
        BindCharacterSelectPanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 创建武器选择页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">武器选择页引用。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateWeaponSelectPanel(Transform parent, out WeaponSelectRefs refs)
    {
        RectTransform panel = CreatePanel("WeaponSelectPanel", parent, new Color(0.05f, 0.055f, 0.065f, 0.98f));
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("WeaponSelectTitleText", panel, "选择武器", 46, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.93f), Vector2.zero, Vector2.zero);

        Button backButton = CreateButton("BackButton", panel, "返回", 22);
        SetAnchor(backButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.84f), new Vector2(0.15f, 0.91f), Vector2.zero, Vector2.zero);

        RectTransform optionPanel = CreateCard("WeaponOptions", panel, new Vector2(0.1f, 0.18f), new Vector2(0.48f, 0.78f));
        TMP_Text optionTitleText = CreateText("OptionTitleText", optionPanel, "武器列表", 26, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.9f, 0.95f, 1f));
        SetAnchor(optionTitleText.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);

        Button[] optionButtons = new Button[6];
        TMP_Text[] optionTexts = new TMP_Text[6];
        CreateOptionGrid(optionPanel, "WeaponOption", optionButtons, optionTexts);

        RectTransform detailPanel = CreateCard("WeaponDetail", panel, new Vector2(0.52f, 0.18f), new Vector2(0.9f, 0.78f));
        TMP_Text nameText = CreateText("NameText", detailPanel, "训练飞弹", 38, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        TMP_Text descriptionText = CreateText("DescriptionText", detailPanel, "自动锁定最近敌人，按固定间隔发射投射物。", 24, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.94f));
        TMP_Text statsText = CreateText("StatsText", detailPanel, "武器攻击力：12\n射速：0.55s\n弹速：9.00\n暴击率：5%\n暴击伤害：180%\n子弹穿透：0\n子弹反弹：0\n弹道数：1\n子弹存在时间：2.20s", 22, FontStyles.Normal, TextAlignmentOptions.Left, new Color(1f, 0.92f, 0.68f));
        SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
        SetAnchor(descriptionText.rectTransform, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
        SetAnchor(statsText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.42f), Vector2.zero, Vector2.zero);

        Button startButton = CreateButton("StartGameButton", panel, "开始游戏", 30);
        SetAnchor(startButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.07f), new Vector2(0.59f, 0.15f), Vector2.zero, Vector2.zero);

        refs = new WeaponSelectRefs(titleText, nameText, descriptionText, statsText, backButton, startButton, optionButtons, optionTexts);
        GameWeaponSelectPanelController panelController = panel.gameObject.AddComponent<GameWeaponSelectPanelController>();
        BindWeaponSelectPanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 创建战斗 HUD 页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">HUD 引用集合。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateHudPanel(Transform parent, out HudRefs refs)
    {
        RectTransform panel = CreateRect("HudPanel", parent);
        Stretch(panel);
        GameCanvasPage page = AddPage(panel);

        TMP_Text levelText = CreateText("LevelText", panel, "Lv.1", 26, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        SetAnchor(levelText.rectTransform, new Vector2(0.02f, 0.91f), new Vector2(0.18f, 0.97f), Vector2.zero, Vector2.zero);

        GameHeartHealthView heartHealthView = CreateHeartHealthView(panel, 5);

        TMP_Text goldText = CreateText("GoldText", panel, "Gold 0", 24, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.36f));
        SetAnchor(goldText.rectTransform, new Vector2(0.02f, 0.79f), new Vector2(0.18f, 0.85f), Vector2.zero, Vector2.zero);

        Image expBack = CreateImage("ExpBarBackground", panel, new Color(0f, 0f, 0f, 0.55f));
        SetAnchor(expBack.rectTransform, new Vector2(0.39f, 0.94f), new Vector2(0.61f, 0.965f), Vector2.zero, Vector2.zero);

        Image expFill = CreateFillImage("ExpBarFill", expBack.rectTransform, new Color(0.32f, 0.72f, 1f, 1f));
        expFill.type = Image.Type.Filled;
        expFill.fillMethod = Image.FillMethod.Horizontal;
        expFill.fillOrigin = 0;
        expFill.fillAmount = 0.35f;
        Stretch(expFill.rectTransform);

        Image ammoBack = CreateImage("AmmoBarBackground", panel, new Color(0f, 0f, 0f, 0.55f));
        SetAnchor(ammoBack.rectTransform, new Vector2(0.93f, 0.13f), new Vector2(0.95f, 0.29f), Vector2.zero, Vector2.zero);

        Image ammoFill = CreateFillImage("AmmoBarFill", ammoBack.rectTransform, Color.white);
        ammoFill.type = Image.Type.Filled;
        ammoFill.fillMethod = Image.FillMethod.Vertical;
        ammoFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        ammoFill.fillAmount = 1f;
        Stretch(ammoFill.rectTransform);

        TMP_Text ammoText = CreateText("AmmoText", ammoBack.rectTransform, "12/12", 14, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(ammoText.rectTransform);

        Image energyBack = CreateImage("EnergyBarBackground", panel, new Color(0f, 0f, 0f, 0.55f));
        SetAnchor(energyBack.rectTransform, new Vector2(0.84f, 0.06f), new Vector2(0.96f, 0.085f), Vector2.zero, Vector2.zero);

        Image energyFill = CreateFillImage("EnergyBarFill", energyBack.rectTransform, new Color(0.3f, 0.75f, 1f));
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        energyFill.fillAmount = 1f;
        Stretch(energyFill.rectTransform);

        TMP_Text energyText = CreateText("EnergyText", energyBack.rectTransform, "100%", 14, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(energyText.rectTransform);

        Image chargeBack = CreateImage("ChargeBarBackground", panel, new Color(0f, 0f, 0f, 0.55f));
        SetAnchor(chargeBack.rectTransform, new Vector2(0.46f, 0.41f), new Vector2(0.54f, 0.425f), Vector2.zero, Vector2.zero);
        chargeBack.gameObject.SetActive(false);

        Image chargeFill = CreateFillImage("ChargeBarFill", chargeBack.rectTransform, new Color(1f, 0.82f, 0.24f));
        chargeFill.type = Image.Type.Filled;
        chargeFill.fillMethod = Image.FillMethod.Horizontal;
        chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        chargeFill.fillAmount = 0f;
        Stretch(chargeFill.rectTransform);

        TMP_Text floorTimerText = CreateText("FloorTimerText", panel, "1/3  倒计时 30s", 24, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(floorTimerText.rectTransform, new Vector2(0.36f, 0.89f), new Vector2(0.64f, 0.94f), Vector2.zero, Vector2.zero);

        TMP_Text killText = CreateText("KillText", panel, "击杀 0", 22, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
        SetAnchor(killText.rectTransform, new Vector2(0.02f, 0.12f), new Vector2(0.18f, 0.17f), Vector2.zero, Vector2.zero);

        TMP_Text scoreText = CreateText("ScoreText", panel, "得分 0", 22, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
        SetAnchor(scoreText.rectTransform, new Vector2(0.02f, 0.07f), new Vector2(0.18f, 0.12f), Vector2.zero, Vector2.zero);

        TMP_Text timeText = CreateText("TimeText", panel, "时间 0s", 22, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
        SetAnchor(timeText.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.18f, 0.07f), Vector2.zero, Vector2.zero);

        refs = new HudRefs(levelText, goldText, heartHealthView, floorTimerText, killText, scoreText, timeText, expFill, ammoFill, energyFill, chargeFill, ammoText, energyText);
        GameHudPanelController panelController = panel.gameObject.AddComponent<GameHudPanelController>();
        BindHudPanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 创建升级或武器强化选择页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="panelName">页面名称。</param>
    /// <param name="title">标题文本。</param>
    /// <param name="refs">选项页引用集合。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateOfferPanel(Transform parent, string panelName, string title, out OfferPanelRefs refs)
    {
        RectTransform panel = CreateOverlayPanel(panelName, parent);
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("TitleText", panel, title, 42, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.28f, 0.76f), new Vector2(0.72f, 0.85f), Vector2.zero, Vector2.zero);

        TMP_Text subtitleText = CreateText("SubtitleText", panel, "剩余选择次数：1", 24, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.86f, 0.9f, 0.94f));
        SetAnchor(subtitleText.rectTransform, new Vector2(0.28f, 0.7f), new Vector2(0.72f, 0.76f), Vector2.zero, Vector2.zero);

        Button refreshButton = null;
        if (panelName == "LevelUpPanel")
        {
            refreshButton = CreateButton("RefreshButton", panel, "刷新（1）", 22);
            SetAnchor(refreshButton.GetComponent<RectTransform>(), new Vector2(0.68f, 0.7f), new Vector2(0.78f, 0.76f), Vector2.zero, Vector2.zero);
        }

        GameOfferView[] offers = new GameOfferView[3];
        for (int i = 0; i < offers.Length; i++)
        {
            float left = 0.18f + i * 0.22f;
            RectTransform card = CreateCard($"OfferCard_{i}", panel, new Vector2(left, 0.34f), new Vector2(left + 0.2f, 0.66f));
            TMP_Text nameText = CreateText("NameText", card, $"选项 {i + 1}", 28, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            TMP_Text descriptionText = CreateText("DescriptionText", card, "这里显示选项说明。", 22, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.86f, 0.9f, 0.94f));
            Button chooseButton = CreateButton("ChooseButton", card, "选择", 22);

            SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
            SetAnchor(descriptionText.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.66f), Vector2.zero, Vector2.zero);
            SetAnchor(chooseButton.GetComponent<RectTransform>(), new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.24f), Vector2.zero, Vector2.zero);

            GameOfferView offerView = card.gameObject.AddComponent<GameOfferView>();
            BindOfferView(offerView, card.GetComponent<Image>(), nameText, descriptionText, chooseButton);
            offers[i] = offerView;
        }

        refs = new OfferPanelRefs(titleText, subtitleText, refreshButton, offers);
        if (panelName == "LevelUpPanel")
        {
            GameLevelUpPanelController panelController = panel.gameObject.AddComponent<GameLevelUpPanelController>();
            BindLevelUpPanelController(panelController, refs);
        }
        else if (panelName == "WeaponUpgradePanel")
        {
            GameWeaponUpgradePanelController panelController = panel.gameObject.AddComponent<GameWeaponUpgradePanelController>();
            BindWeaponUpgradePanelController(panelController, refs);
        }

        return page;
    }

    /// <summary>
    /// 创建商店页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">商店引用集合。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateShopPanel(Transform parent, out ShopRefs refs)
    {
        RectTransform panel = CreateOverlayPanel("ShopPanel", parent);
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("TitleText", panel, "商店  楼层 1/3", 42, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.28f, 0.79f), new Vector2(0.72f, 0.88f), Vector2.zero, Vector2.zero);

        TMP_Text goldText = CreateText("GoldText", panel, "金币：0", 24, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.36f));
        SetAnchor(goldText.rectTransform, new Vector2(0.28f, 0.73f), new Vector2(0.72f, 0.79f), Vector2.zero, Vector2.zero);

        TMP_Text descriptionText = CreateText("DescriptionText", panel, "商店出售道具；角色升级会从不同稀有度词条池中抽取强化。", 20, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.86f, 0.9f, 0.94f));
        SetAnchor(descriptionText.rectTransform, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.73f), Vector2.zero, Vector2.zero);

        TMP_Text emptyText = CreateText("EmptyText", panel, "当前没有可用商品。", 24, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        SetAnchor(emptyText.rectTransform, new Vector2(0.3f, 0.46f), new Vector2(0.7f, 0.54f), Vector2.zero, Vector2.zero);

        GameShopOfferView[] offers = new GameShopOfferView[6];
        for (int i = 0; i < offers.Length; i++)
        {
            int row = i / 3;
            int column = i % 3;
            float left = 0.15f + column * 0.235f;
            float top = 0.66f - row * 0.22f;
            RectTransform card = CreateCard($"ShopCard_{i}", panel, new Vector2(left, top - 0.18f), new Vector2(left + 0.22f, top));
            TMP_Text nameText = CreateText("NameText", card, $"商品 {i + 1}", 26, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            TMP_Text description = CreateText("DescriptionText", card, "这里显示商品说明。", 19, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.86f, 0.9f, 0.94f));
            TMP_Text costText = CreateText("CostText", card, "价格：0", 18, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
            TMP_Text lockText = CreateText("LockStateText", card, "状态：未锁定", 18, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
            Button buyButton = CreateButton("BuyButton", card, "购买", 18);
            Button lockButton = CreateButton("LockButton", card, "锁定", 18);

            SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
            SetAnchor(description.rectTransform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            SetAnchor(costText.rectTransform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.47f), Vector2.zero, Vector2.zero);
            SetAnchor(lockText.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.39f), Vector2.zero, Vector2.zero);
            SetAnchor(buyButton.GetComponent<RectTransform>(), new Vector2(0.14f, 0.17f), new Vector2(0.86f, 0.28f), Vector2.zero, Vector2.zero);
            SetAnchor(lockButton.GetComponent<RectTransform>(), new Vector2(0.14f, 0.05f), new Vector2(0.86f, 0.15f), Vector2.zero, Vector2.zero);

            GameShopOfferView offerView = card.gameObject.AddComponent<GameShopOfferView>();
            BindShopOfferView(offerView, card.GetComponent<Image>(), nameText, description, costText, lockText, buyButton, lockButton);
            offers[i] = offerView;
        }

        Button refreshButton = CreateButton("RefreshButton", panel, "刷新商品", 24);
        SetAnchor(refreshButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.18f), new Vector2(0.42f, 0.26f), Vector2.zero, Vector2.zero);

        Button nextButton = CreateButton("NextFloorButton", panel, "下一层", 24);
        SetAnchor(nextButton.GetComponent<RectTransform>(), new Vector2(0.58f, 0.18f), new Vector2(0.72f, 0.26f), Vector2.zero, Vector2.zero);

        refs = new ShopRefs(titleText, goldText, descriptionText, emptyText, offers, refreshButton, nextButton);
        GameShopPanelController panelController = panel.gameObject.AddComponent<GameShopPanelController>();
        BindShopPanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 创建结算页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">结算引用集合。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreateResultPanel(Transform parent, out ResultRefs refs)
    {
        RectTransform panel = CreateOverlayPanel("ResultPanel", parent);
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("TitleText", panel, "通关结算", 46, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.28f, 0.78f), new Vector2(0.72f, 0.88f), Vector2.zero, Vector2.zero);

        Image avatarImage = CreateImage("AvatarImage", panel, new Color(0.95f, 0.72f, 0.28f, 1f));
        SetAnchor(avatarImage.rectTransform, new Vector2(0.26f, 0.52f), new Vector2(0.36f, 0.7f), Vector2.zero, Vector2.zero);

        TMP_Text characterText = CreateText("CharacterText", panel, "见习冒险者", 22, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(characterText.rectTransform, new Vector2(0.22f, 0.46f), new Vector2(0.4f, 0.52f), Vector2.zero, Vector2.zero);

        TMP_Text weaponText = CreateText("WeaponText", panel, "武器：训练飞弹", 22, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        SetAnchor(weaponText.rectTransform, new Vector2(0.22f, 0.4f), new Vector2(0.4f, 0.46f), Vector2.zero, Vector2.zero);

        TMP_Text statsText = CreateText("StatsText", panel, "击杀怪物：0\n最终得分：0\n游玩时长：0 秒", 24, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.94f));
        SetAnchor(statsText.rectTransform, new Vector2(0.45f, 0.36f), new Vector2(0.74f, 0.7f), Vector2.zero, Vector2.zero);

        Button restartButton = CreateButton("RestartButton", panel, "重新开始", 22);
        Button returnTitleButton = CreateButton("ReturnTitleButton", panel, "返回标题", 22);
        Button quitButton = CreateButton("QuitButton", panel, "退出游戏", 22);
        SetAnchor(restartButton.GetComponent<RectTransform>(), new Vector2(0.25f, 0.2f), new Vector2(0.39f, 0.28f), Vector2.zero, Vector2.zero);
        SetAnchor(returnTitleButton.GetComponent<RectTransform>(), new Vector2(0.43f, 0.2f), new Vector2(0.57f, 0.28f), Vector2.zero, Vector2.zero);
        SetAnchor(quitButton.GetComponent<RectTransform>(), new Vector2(0.61f, 0.2f), new Vector2(0.75f, 0.28f), Vector2.zero, Vector2.zero);

        refs = new ResultRefs(titleText, avatarImage, characterText, weaponText, statsText, restartButton, returnTitleButton, quitButton);
        GameResultPanelController panelController = panel.gameObject.AddComponent<GameResultPanelController>();
        BindResultPanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 创建暂停页面。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="refs">暂停引用集合。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage CreatePausePanel(Transform parent, out PauseRefs refs)
    {
        RectTransform panel = CreateOverlayPanel("PausePanel", parent);
        GameCanvasPage page = AddPage(panel);

        TMP_Text titleText = CreateText("TitleText", panel, "暂停", 46, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetAnchor(titleText.rectTransform, new Vector2(0.28f, 0.76f), new Vector2(0.72f, 0.86f), Vector2.zero, Vector2.zero);

        TMP_Text statsText = CreateText("StatsText", panel, "当前属性\n生命：5/5 心\n等级：Lv.1", 22, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.94f));
        SetAnchor(statsText.rectTransform, new Vector2(0.24f, 0.34f), new Vector2(0.44f, 0.7f), Vector2.zero, Vector2.zero);

        TMP_Text weaponText = CreateText("WeaponText", panel, "当前武器：训练飞弹", 22, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        SetAnchor(weaponText.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.74f, 0.68f), Vector2.zero, Vector2.zero);

        TMP_Text itemsText = CreateText("ItemsText", panel, "道具：暂无", 22, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 0.94f));
        SetAnchor(itemsText.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(0.74f, 0.62f), Vector2.zero, Vector2.zero);

        Button resumeButton = CreateButton("ResumeButton", panel, "继续", 22);
        Button restartButton = CreateButton("RestartButton", panel, "重新开始", 22);
        Button settingsButton = CreateButton("SettingsButton", panel, "设置", 22);
        Button quitButton = CreateButton("QuitButton", panel, "退出", 22);
        SetAnchor(resumeButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.37f), new Vector2(0.7f, 0.45f), Vector2.zero, Vector2.zero);
        SetAnchor(restartButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.27f), new Vector2(0.7f, 0.35f), Vector2.zero, Vector2.zero);
        SetAnchor(settingsButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.17f), new Vector2(0.7f, 0.25f), Vector2.zero, Vector2.zero);
        SetAnchor(quitButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.07f), new Vector2(0.7f, 0.15f), Vector2.zero, Vector2.zero);

        refs = new PauseRefs(titleText, statsText, weaponText, itemsText, resumeButton, restartButton, settingsButton, quitButton);
        GamePausePanelController panelController = panel.gameObject.AddComponent<GamePausePanelController>();
        BindPausePanelController(panelController, refs);
        return page;
    }

    /// <summary>
    /// 绑定页面路由的状态和页面。
    /// </summary>
    private static void BindRouter(
        GameCanvasPageRouter router,
        GameStateManager gameStateManager,
        GameCanvasPage titlePage,
        GameCanvasPage characterSelectPage,
        GameCanvasPage weaponSelectPage,
        GameCanvasPage hudPage,
        GameCanvasPage levelUpPage,
        GameCanvasPage weaponUpgradePage,
        GameCanvasPage shopPage,
        GameCanvasPage resultPage,
        GameCanvasPage pausePage)
    {
        SerializedObject serializedObject = new SerializedObject(router);
        serializedObject.FindProperty("gameStateManager").objectReferenceValue = gameStateManager;
        SerializedProperty pageBindings = serializedObject.FindProperty("pageBindings");
        pageBindings.arraySize = 10;

        SetPageBinding(pageBindings, 0, GameStateManager.GameState.Title, titlePage);
        SetPageBinding(pageBindings, 1, GameStateManager.GameState.CharacterSelect, characterSelectPage);
        SetPageBinding(pageBindings, 2, GameStateManager.GameState.WeaponSelect, weaponSelectPage);
        SetPageBinding(pageBindings, 3, GameStateManager.GameState.Playing, hudPage);
        SetPageBinding(pageBindings, 4, GameStateManager.GameState.Paused, pausePage);
        SetPageBinding(pageBindings, 5, GameStateManager.GameState.LevelUp, levelUpPage);
        SetPageBinding(pageBindings, 6, GameStateManager.GameState.WeaponUpgrade, weaponUpgradePage);
        SetPageBinding(pageBindings, 7, GameStateManager.GameState.Shop, shopPage);
        SetPageBinding(pageBindings, 8, GameStateManager.GameState.GameOver, resultPage);
        SetPageBinding(pageBindings, 9, GameStateManager.GameState.Victory, resultPage);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定主流程 UI 控制器。
    /// </summary>
    private static void BindFlowController(
        GameFlowCanvasController controller,
        GameStateManager gameStateManager,
        Button titleClickButton,
        TMP_Text titleText,
        TMP_Text titleHintText,
        CharacterSelectRefs characterRefs,
        WeaponSelectRefs weaponRefs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("gameStateManager").objectReferenceValue = gameStateManager;
        SetObjectArray(serializedObject.FindProperty("characterOptions"), LoadAssets<CharacterData>("Assets/Game/ScriptableObjects/Characters"));
        SetObjectArray(serializedObject.FindProperty("weaponOptions"), LoadAssets<WeaponData>("Assets/Game/ScriptableObjects/Weapons"));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定标题面板控制器引用。
    /// </summary>
    private static void BindTitlePanelController(GameTitlePanelController controller, TMP_Text titleText, TMP_Text hintText, Button clickButton)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("hintText").objectReferenceValue = hintText;
        serializedObject.FindProperty("clickButton").objectReferenceValue = clickButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定角色选择面板控制器引用。
    /// </summary>
    private static void BindCharacterSelectPanelController(GameCharacterSelectPanelController controller, CharacterSelectRefs characterSelectRefs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = characterSelectRefs.TitleText;
        serializedObject.FindProperty("nameText").objectReferenceValue = characterSelectRefs.NameText;
        serializedObject.FindProperty("descriptionText").objectReferenceValue = characterSelectRefs.DescriptionText;
        serializedObject.FindProperty("statsText").objectReferenceValue = characterSelectRefs.StatsText;
        serializedObject.FindProperty("confirmButton").objectReferenceValue = characterSelectRefs.ConfirmButton;
        SetObjectArray(serializedObject.FindProperty("optionButtons"), characterSelectRefs.OptionButtons);
        SetObjectArray(serializedObject.FindProperty("optionTexts"), characterSelectRefs.OptionTexts);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定武器选择面板控制器引用。
    /// </summary>
    private static void BindWeaponSelectPanelController(GameWeaponSelectPanelController controller, WeaponSelectRefs weaponSelectRefs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = weaponSelectRefs.TitleText;
        serializedObject.FindProperty("nameText").objectReferenceValue = weaponSelectRefs.NameText;
        serializedObject.FindProperty("descriptionText").objectReferenceValue = weaponSelectRefs.DescriptionText;
        serializedObject.FindProperty("statsText").objectReferenceValue = weaponSelectRefs.StatsText;
        serializedObject.FindProperty("backButton").objectReferenceValue = weaponSelectRefs.BackButton;
        serializedObject.FindProperty("startButton").objectReferenceValue = weaponSelectRefs.StartButton;
        SetObjectArray(serializedObject.FindProperty("optionButtons"), weaponSelectRefs.OptionButtons);
        SetObjectArray(serializedObject.FindProperty("optionTexts"), weaponSelectRefs.OptionTexts);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定 HUD 面板控制器引用。
    /// </summary>
    private static void BindHudPanelController(GameHudPanelController controller, HudRefs hudRefs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("levelText").objectReferenceValue = hudRefs.LevelText;
        serializedObject.FindProperty("goldText").objectReferenceValue = hudRefs.GoldText;
        serializedObject.FindProperty("heartHealthView").objectReferenceValue = hudRefs.HeartHealthView;
        serializedObject.FindProperty("healthText").objectReferenceValue = null;
        serializedObject.FindProperty("floorTimerText").objectReferenceValue = hudRefs.FloorTimerText;
        serializedObject.FindProperty("killText").objectReferenceValue = hudRefs.KillText;
        serializedObject.FindProperty("scoreText").objectReferenceValue = hudRefs.ScoreText;
        serializedObject.FindProperty("timeText").objectReferenceValue = hudRefs.TimeText;
        serializedObject.FindProperty("experienceFillImage").objectReferenceValue = hudRefs.ExperienceFillImage;
        serializedObject.FindProperty("ammoFillImage").objectReferenceValue = hudRefs.AmmoFillImage;
        serializedObject.FindProperty("energyFillImage").objectReferenceValue = hudRefs.EnergyFillImage;
        serializedObject.FindProperty("chargeFillImage").objectReferenceValue = hudRefs.ChargeFillImage;
        serializedObject.FindProperty("ammoText").objectReferenceValue = hudRefs.AmmoText;
        serializedObject.FindProperty("energyText").objectReferenceValue = hudRefs.EnergyText;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定升级面板控制器引用。
    /// </summary>
    private static void BindLevelUpPanelController(GameLevelUpPanelController controller, OfferPanelRefs refs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = refs.TitleText;
        serializedObject.FindProperty("remainingText").objectReferenceValue = refs.SubtitleText;
        serializedObject.FindProperty("refreshButton").objectReferenceValue = refs.RefreshButton;
        SetObjectArray(serializedObject.FindProperty("offerViews"), refs.OfferViews);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定武器强化面板控制器引用。
    /// </summary>
    private static void BindWeaponUpgradePanelController(GameWeaponUpgradePanelController controller, OfferPanelRefs refs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = refs.TitleText;
        serializedObject.FindProperty("currentWeaponText").objectReferenceValue = refs.SubtitleText;
        SetObjectArray(serializedObject.FindProperty("offerViews"), refs.OfferViews);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定商店面板控制器引用。
    /// </summary>
    private static void BindShopPanelController(GameShopPanelController controller, ShopRefs refs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = refs.TitleText;
        serializedObject.FindProperty("goldText").objectReferenceValue = refs.GoldText;
        serializedObject.FindProperty("descriptionText").objectReferenceValue = refs.DescriptionText;
        serializedObject.FindProperty("emptyText").objectReferenceValue = refs.EmptyText;
        SetObjectArray(serializedObject.FindProperty("itemOfferViews"), GetRange(refs.OfferViews, 0, 3));
        SetObjectArray(serializedObject.FindProperty("relicOfferViews"), GetRange(refs.OfferViews, 3, 3));
        serializedObject.FindProperty("refreshButton").objectReferenceValue = refs.RefreshButton;
        serializedObject.FindProperty("nextFloorButton").objectReferenceValue = refs.NextButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定结算面板控制器引用。
    /// </summary>
    private static void BindResultPanelController(GameResultPanelController controller, ResultRefs refs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = refs.TitleText;
        serializedObject.FindProperty("avatarImage").objectReferenceValue = refs.AvatarImage;
        serializedObject.FindProperty("characterText").objectReferenceValue = refs.CharacterText;
        serializedObject.FindProperty("weaponText").objectReferenceValue = refs.WeaponText;
        serializedObject.FindProperty("statsText").objectReferenceValue = refs.StatsText;
        serializedObject.FindProperty("restartButton").objectReferenceValue = refs.RestartButton;
        serializedObject.FindProperty("returnTitleButton").objectReferenceValue = refs.ReturnTitleButton;
        serializedObject.FindProperty("quitButton").objectReferenceValue = refs.QuitButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定暂停面板控制器引用。
    /// </summary>
    private static void BindPausePanelController(GamePausePanelController controller, PauseRefs refs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("titleText").objectReferenceValue = refs.TitleText;
        serializedObject.FindProperty("statsText").objectReferenceValue = refs.StatsText;
        serializedObject.FindProperty("weaponText").objectReferenceValue = refs.WeaponText;
        serializedObject.FindProperty("itemsText").objectReferenceValue = refs.ItemsText;
        serializedObject.FindProperty("resumeButton").objectReferenceValue = refs.ResumeButton;
        serializedObject.FindProperty("restartButton").objectReferenceValue = refs.RestartButton;
        serializedObject.FindProperty("settingsButton").objectReferenceValue = refs.SettingsButton;
        serializedObject.FindProperty("quitButton").objectReferenceValue = refs.QuitButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定战斗 UI 控制器的基础引用。
    /// </summary>
    private static void BindCombatController(
        GameCombatCanvasController controller,
        GameStateManager gameStateManager,
        HudRefs hudRefs,
        OfferPanelRefs levelUpRefs,
        OfferPanelRefs weaponRefs,
        ShopRefs shopRefs,
        ResultRefs resultRefs,
        PauseRefs pauseRefs)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("gameStateManager").objectReferenceValue = gameStateManager;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 将生成的战斗 UI 控制器写入场景里的 GameCombatController。
    /// </summary>
    /// <param name="combatController">生成的战斗 UI 控制器。</param>
    private static void BindCombatControllerToGame(GameFlowCanvasController flowController, GameCombatCanvasController combatController)
    {
        GameCombatController gameCombatController = Object.FindObjectOfType<GameCombatController>();
        if (gameCombatController == null)
            return;

        SerializedObject serializedObject = new SerializedObject(gameCombatController);
        SerializedProperty flowProperty = serializedObject.FindProperty("flowCanvasController");
        if (flowProperty != null)
            flowProperty.objectReferenceValue = flowController;

        SerializedProperty combatProperty = serializedObject.FindProperty("combatCanvasController");
        if (combatProperty != null)
            combatProperty.objectReferenceValue = combatController;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameCombatController);
    }

    /// <summary>
    /// 创建覆盖全屏的暗色弹窗页面。
    /// </summary>
    /// <param name="name">页面名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <returns>页面根节点。</returns>
    private static RectTransform CreateOverlayPanel(string name, Transform parent)
    {
        RectTransform panel = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0.62f));
        Image frame = CreateImage("PanelFrame", panel, new Color(0.08f, 0.12f, 0.18f, 0.96f));
        SetAnchor(frame.rectTransform, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.9f), Vector2.zero, Vector2.zero);
        frame.transform.SetAsFirstSibling();
        return panel;
    }

    /// <summary>
    /// 创建带背景色的页面。
    /// </summary>
    /// <param name="name">页面名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <param name="color">背景颜色。</param>
    /// <returns>页面根节点。</returns>
    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform panel = CreateRect(name, parent);
        Stretch(panel);
        CreateImage("Background", panel, color).transform.SetAsFirstSibling();
        Stretch(panel.Find("Background").GetComponent<RectTransform>());
        return panel;
    }

    /// <summary>
    /// 创建卡片容器。
    /// </summary>
    /// <param name="name">卡片名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <param name="anchorMin">锚点最小值。</param>
    /// <param name="anchorMax">锚点最大值。</param>
    /// <returns>卡片根节点。</returns>
    private static RectTransform CreateCard(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform card = CreateRect(name, parent);
        Image image = card.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.17f, 0.24f, 0.92f);
        SetAnchor(card, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        return card;
    }

    /// <summary>
    /// 创建 UI 文本。
    /// </summary>
    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        TextMeshProUGUI text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// 创建普通按钮。
    /// </summary>
    /// <param name="name">按钮名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <param name="label">按钮文本。</param>
    /// <param name="fontSize">文本字号。</param>
    /// <returns>按钮组件。</returns>
    private static Button CreateButton(string name, Transform parent, string label, int fontSize)
    {
        Image image = CreateImage(name, parent, new Color(0.18f, 0.38f, 0.62f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text labelText = CreateText("Label", image.rectTransform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(labelText.rectTransform);
        return button;
    }

    /// <summary>
    /// 创建透明全屏按钮。
    /// </summary>
    /// <param name="name">按钮名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <returns>按钮组件。</returns>
    private static Button CreateTransparentButton(string name, Transform parent)
    {
        Image image = CreateImage(name, parent, new Color(1f, 1f, 1f, 0f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    /// <summary>
    /// 创建左侧选项按钮网格。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="namePrefix">按钮命名前缀。</param>
    /// <param name="buttons">输出按钮数组。</param>
    /// <param name="texts">输出按钮文本数组。</param>
    private static void CreateOptionGrid(RectTransform parent, string namePrefix, Button[] buttons, TMP_Text[] texts)
    {
        int columns = 2;
        int rows = Mathf.CeilToInt(buttons.Length / (float)columns);
        float startX = 0.08f;
        float startY = 0.62f;
        float cellWidth = 0.4f;
        float cellHeight = 0.16f;
        float gapX = 0.04f;
        float gapY = 0.04f;

        for (int i = 0; i < buttons.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float left = startX + column * (cellWidth + gapX);
            float top = startY - row * (cellHeight + gapY);

            Button button = CreateButton($"{namePrefix}_{i}", parent, $"选项 {i + 1}", 22);
            SetAnchor(
                button.GetComponent<RectTransform>(),
                new Vector2(left, top),
                new Vector2(left + cellWidth, top + cellHeight),
                Vector2.zero,
                Vector2.zero);

            buttons[i] = button;
            texts[i] = button.GetComponentInChildren<TMP_Text>();
        }
    }

    /// <summary>
    /// 创建 UI 图片。
    /// </summary>
    /// <param name="name">图片名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <param name="color">图片颜色。</param>
    /// <returns>图片组件。</returns>
    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// <summary>
    /// 创建进度条填充图片，并绑定项目内纯色 Sprite 以保证 Filled 类型生效。
    /// </summary>
    /// <param name="name">图片名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <param name="color">图片颜色。</param>
    /// <returns>图片组件。</returns>
    private static Image CreateFillImage(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        image.sprite = LoadSolidSprite();
        return image;
    }

    /// <summary>
    /// 创建 HUD 心形血量视图，心槽在场景中预先搭建并由组件刷新贴图状态。
    /// </summary>
    /// <param name="parent">父级节点。</param>
    /// <param name="heartCount">默认心槽数量。</param>
    /// <returns>心形血量视图组件。</returns>
    private static GameHeartHealthView CreateHeartHealthView(Transform parent, int heartCount)
    {
        RectTransform root = CreateRect("HeartHealthView", parent);
        SetAnchor(root, new Vector2(0.02f, 0.845f), new Vector2(0.22f, 0.9f), Vector2.zero, Vector2.zero);

        HorizontalLayoutGroup layoutGroup = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 6f;

        GameHeartSlotView redHeartSlotPrefab = AssetDatabase.LoadAssetAtPath<GameHeartSlotView>(RedHeartPrefabPath);
        for (int i = 0; i < heartCount; i++)
        {
            Image heartImage = CreateImage($"HeartSlot_Red_{i + 1}", root, Color.white);
            heartImage.raycastTarget = false;
            heartImage.preserveAspect = true;

            RectTransform heartRect = heartImage.rectTransform;
            heartRect.sizeDelta = new Vector2(34f, 34f);

            LayoutElement layoutElement = heartImage.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 34f;
            layoutElement.preferredHeight = 34f;
            layoutElement.minWidth = 34f;
            layoutElement.minHeight = 34f;

            GameHeartSlotView slotView = heartImage.gameObject.AddComponent<GameHeartSlotView>();
            BindHeartSlotView(slotView, AssetDatabase.LoadAssetAtPath<Sprite>(FullHeartSpritePath), AssetDatabase.LoadAssetAtPath<Sprite>(HalfHeartSpritePath), LoadEmptyHeartSprite());
        }

        GameHeartHealthView view = root.gameObject.AddComponent<GameHeartHealthView>();
        BindHeartHealthView(
            view,
            root,
            redHeartSlotPrefab,
            AssetDatabase.LoadAssetAtPath<Sprite>(FullHeartSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(HalfHeartSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(EmptyHeartSpritePath));
        return view;
    }

    /// <summary>
    /// 加载进度条使用的纯色 Sprite。
    /// </summary>
    /// <returns>纯色 Sprite。</returns>
    private static Sprite LoadSolidSprite()
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
    }

    /// <summary>
    /// 加载空心槽图片，兼容旧路径和当前资源路径。
    /// </summary>
    /// <returns>空心槽图片。</returns>
    private static Sprite LoadEmptyHeartSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyHeartSpritePath);
        if (sprite != null)
            return sprite;

        return AssetDatabase.LoadAssetAtPath<Sprite>(FallbackEmptyHeartSpritePath);
    }

    /// <summary>
    /// 确保 UI 心槽 prefab 占位资源存在。
    /// </summary>
    private static void EnsureHeartSlotPrefabs(bool forceRebuild)
    {
        EnsureFolder("Assets/Game/Prefabs");
        EnsureFolder(HeartPrefabFolder);

        Sprite fullHeartSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FullHeartSpritePath);
        Sprite halfHeartSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HalfHeartSpritePath);
        Sprite emptyHeartSprite = LoadEmptyHeartSprite();

        CreateHeartSlotPrefab(RedHeartPrefabPath, "HeartSlot_Red", fullHeartSprite, halfHeartSprite, emptyHeartSprite, SpecialHealthType.Blue, new Color(1f, 0.2f, 0.24f, 1f), false, forceRebuild);
        CreateHeartSlotPrefab(BlueHeartPrefabPath, "HeartSlot_Blue", fullHeartSprite, halfHeartSprite, emptyHeartSprite, SpecialHealthType.Blue, new Color(0.25f, 0.62f, 1f, 1f), true, forceRebuild);
        CreateHeartSlotPrefab(PinkHeartPrefabPath, "HeartSlot_Pink", fullHeartSprite, halfHeartSprite, emptyHeartSprite, SpecialHealthType.Pink, new Color(1f, 0.45f, 0.75f, 1f), true, forceRebuild);
        CreateHeartSlotPrefab(GlassHeartPrefabPath, "HeartSlot_Glass", fullHeartSprite, halfHeartSprite, emptyHeartSprite, SpecialHealthType.Glass, new Color(0.72f, 0.95f, 1f, 0.92f), true, forceRebuild);
        CreateHeartSlotPrefab(ExplosiveHeartPrefabPath, "HeartSlot_Explosive", fullHeartSprite, halfHeartSprite, emptyHeartSprite, SpecialHealthType.Explosive, new Color(1f, 0.55f, 0.16f, 1f), true, forceRebuild);
    }

    /// <summary>
    /// 创建单个心槽 prefab。
    /// </summary>
    /// <param name="path">prefab 保存路径。</param>
    /// <param name="name">prefab 名称。</param>
    /// <param name="fullHeartSprite">满心图片。</param>
    /// <param name="halfHeartSprite">半心图片。</param>
    /// <param name="emptyHeartSprite">空心图片。</param>
    /// <param name="type">特殊血类型。</param>
    /// <param name="color">特殊血颜色。</param>
    /// <param name="special">是否特殊血心槽。</param>
    /// <param name="forceRebuild">是否强制删除并重建。</param>
    private static void CreateHeartSlotPrefab(
        string path,
        string name,
        Sprite fullHeartSprite,
        Sprite halfHeartSprite,
        Sprite emptyHeartSprite,
        SpecialHealthType type,
        Color color,
        bool special,
        bool forceRebuild)
    {
        if (!forceRebuild && AssetDatabase.LoadAssetAtPath<GameHeartSlotView>(path) != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            AssetDatabase.DeleteAsset(path);

        GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(GameHeartSlotView));
        RectTransform rectTransform = slotObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(60f, 60f);

        Image image = slotObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.sprite = fullHeartSprite != null ? fullHeartSprite : emptyHeartSprite;
        image.color = special ? color : new Color(1f, 0.2f, 0.24f, 1f);

        LayoutElement layoutElement = slotObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 60f;
        layoutElement.preferredHeight = 60f;
        layoutElement.minWidth = 60f;
        layoutElement.minHeight = 60f;

        GameHeartSlotView slotView = slotObject.GetComponent<GameHeartSlotView>();
        BindHeartSlotView(slotView, fullHeartSprite, halfHeartSprite, emptyHeartSprite);
        if (special)
        {
            slotView.SetSpecialColor(type, color);
            slotView.SetHeartType(type);
        }
        else
        {
            slotView.SetRedHeart();
        }

        PrefabUtility.SaveAsPrefabAsset(slotObject, path);
        Object.DestroyImmediate(slotObject);
    }

    /// <summary>
    /// 绑定心槽视图基础图片资源。
    /// </summary>
    /// <param name="slotView">心槽视图。</param>
    /// <param name="fullHeartSprite">满心图片。</param>
    /// <param name="halfHeartSprite">半心图片。</param>
    /// <param name="emptyHeartSprite">空心图片。</param>
    private static void BindHeartSlotView(GameHeartSlotView slotView, Sprite fullHeartSprite, Sprite halfHeartSprite, Sprite emptyHeartSprite)
    {
        SerializedObject serializedObject = new SerializedObject(slotView);
        serializedObject.FindProperty("fullHeartSprite").objectReferenceValue = fullHeartSprite;
        serializedObject.FindProperty("halfHeartSprite").objectReferenceValue = halfHeartSprite;
        serializedObject.FindProperty("emptyHeartSprite").objectReferenceValue = emptyHeartSprite;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 确保指定资源目录存在。
    /// </summary>
    /// <param name="folderPath">资源目录路径。</param>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// 创建 RectTransform 节点。
    /// </summary>
    /// <param name="name">节点名称。</param>
    /// <param name="parent">父级节点。</param>
    /// <returns>RectTransform 组件。</returns>
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// 给页面添加 CanvasGroup 和 GameCanvasPage。
    /// </summary>
    /// <param name="panel">页面根节点。</param>
    /// <returns>页面控制组件。</returns>
    private static GameCanvasPage AddPage(RectTransform panel)
    {
        CanvasGroup canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        GameCanvasPage page = panel.gameObject.AddComponent<GameCanvasPage>();
        SerializedObject serializedObject = new SerializedObject(page);
        serializedObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return page;
    }

    /// <summary>
    /// 设置卡片内三段文本的默认布局。
    /// </summary>
    private static void SetVerticalCardText(TMP_Text categoryText, TMP_Text nameText, TMP_Text descriptionText)
    {
        SetAnchor(categoryText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
        SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.7f), Vector2.zero, Vector2.zero);
        SetAnchor(descriptionText.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.48f), Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// 设置 RectTransform 为铺满父级。
    /// </summary>
    /// <param name="rectTransform">目标 RectTransform。</param>
    private static void Stretch(RectTransform rectTransform)
    {
        SetAnchor(rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// 设置 RectTransform 锚点和偏移。
    /// </summary>
    private static void SetAnchor(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// 设置路由数组中的单个状态绑定。
    /// </summary>
    private static void SetPageBinding(SerializedProperty bindings, int index, GameStateManager.GameState state, GameCanvasPage page)
    {
        SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
        binding.FindPropertyRelative("state").enumValueIndex = (int)state;
        binding.FindPropertyRelative("page").objectReferenceValue = page;
    }

    /// <summary>
    /// 绑定通用选项视图的私有序列化字段。
    /// </summary>
    private static void BindOfferView(GameOfferView view, Image backgroundImage, TMP_Text nameText, TMP_Text descriptionText, Button chooseButton)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        serializedObject.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
        serializedObject.FindProperty("nameText").objectReferenceValue = nameText;
        serializedObject.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        serializedObject.FindProperty("chooseButton").objectReferenceValue = chooseButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定心形血量视图中的心槽 prefab 和图片资源。
    /// </summary>
    /// <param name="view">心形血量视图。</param>
    /// <param name="heartSlotRoot">心槽父节点。</param>
    /// <param name="redHeartSlotPrefab">红心槽 prefab。</param>
    private static void BindHeartHealthView(
        GameHeartHealthView view,
        Transform heartSlotRoot,
        GameHeartSlotView redHeartSlotPrefab,
        Sprite fullHeartSprite,
        Sprite halfHeartSprite,
        Sprite emptyHeartSprite)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        serializedObject.FindProperty("fullHeartSprite").objectReferenceValue = fullHeartSprite;
        serializedObject.FindProperty("halfHeartSprite").objectReferenceValue = halfHeartSprite;
        serializedObject.FindProperty("emptyHeartSprite").objectReferenceValue = emptyHeartSprite;
        serializedObject.FindProperty("heartSlotRoot").objectReferenceValue = heartSlotRoot;
        serializedObject.FindProperty("redHeartSlotPrefab").objectReferenceValue = redHeartSlotPrefab;
        BindSpecialHeartPrefabs(serializedObject.FindProperty("specialHeartSlotPrefabs"));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 绑定特殊血心槽 prefab 配置。
    /// </summary>
    /// <param name="property">特殊血 prefab 数组属性。</param>
    private static void BindSpecialHeartPrefabs(SerializedProperty property)
    {
        property.arraySize = 4;
        SetSpecialHeartPrefab(property, 0, SpecialHealthType.Blue, BlueHeartPrefabPath);
        SetSpecialHeartPrefab(property, 1, SpecialHealthType.Pink, PinkHeartPrefabPath);
        SetSpecialHeartPrefab(property, 2, SpecialHealthType.Glass, GlassHeartPrefabPath);
        SetSpecialHeartPrefab(property, 3, SpecialHealthType.Explosive, ExplosiveHeartPrefabPath);
    }

    /// <summary>
    /// 绑定单个特殊血心槽 prefab 配置。
    /// </summary>
    /// <param name="property">特殊血 prefab 数组属性。</param>
    /// <param name="index">数组索引。</param>
    /// <param name="type">特殊血类型。</param>
    /// <param name="prefabPath">prefab 路径。</param>
    private static void SetSpecialHeartPrefab(SerializedProperty property, int index, SpecialHealthType type, string prefabPath)
    {
        SerializedProperty element = property.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("type").enumValueIndex = (int)type;
        element.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameHeartSlotView>(prefabPath);
    }

    /// <summary>
    /// 绑定商店商品视图的私有序列化字段。
    /// </summary>
    private static void BindShopOfferView(
        GameShopOfferView view,
        Image backgroundImage,
        TMP_Text nameText,
        TMP_Text descriptionText,
        TMP_Text costText,
        TMP_Text lockStateText,
        Button buyButton,
        Button lockButton)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        serializedObject.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
        serializedObject.FindProperty("nameText").objectReferenceValue = nameText;
        serializedObject.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        serializedObject.FindProperty("costText").objectReferenceValue = costText;
        serializedObject.FindProperty("lockStateText").objectReferenceValue = lockStateText;
        serializedObject.FindProperty("buyButton").objectReferenceValue = buyButton;
        serializedObject.FindProperty("lockButton").objectReferenceValue = lockButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 设置对象数组序列化字段。
    /// </summary>
    private static void SetObjectArray<T>(SerializedProperty property, T[] values) where T : Object
    {
        if (property == null)
            return;

        if (values == null)
            values = new T[0];

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    /// <summary>
    /// 从对象数组中截取指定长度的子数组。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <param name="values">源数组。</param>
    /// <param name="startIndex">起始索引。</param>
    /// <param name="count">截取数量。</param>
    /// <returns>子数组。</returns>
    private static T[] GetRange<T>(T[] values, int startIndex, int count) where T : Object
    {
        if (values == null || count <= 0)
            return new T[0];

        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            int sourceIndex = startIndex + i;
            if (sourceIndex >= 0 && sourceIndex < values.Length)
                result[i] = values[sourceIndex];
        }

        return result;
    }

    /// <summary>
    /// 从指定目录加载指定类型资源。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <param name="folder">资源目录。</param>
    /// <returns>加载到的资源数组。</returns>
    private static T[] LoadAssets<T>(string folder) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        T[] assets = new T[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return assets;
    }

    private readonly struct CharacterSelectRefs
    {
        public readonly TMP_Text TitleText;
        public readonly TMP_Text NameText;
        public readonly TMP_Text DescriptionText;
        public readonly TMP_Text StatsText;
        public readonly Button ConfirmButton;
        public readonly Button[] OptionButtons;
        public readonly TMP_Text[] OptionTexts;

        /// <summary>
        /// 保存角色选择页引用。
        /// </summary>
        public CharacterSelectRefs(
            TMP_Text titleText,
            TMP_Text nameText,
            TMP_Text descriptionText,
            TMP_Text statsText,
            Button confirmButton,
            Button[] optionButtons,
            TMP_Text[] optionTexts)
        {
            TitleText = titleText;
            NameText = nameText;
            DescriptionText = descriptionText;
            StatsText = statsText;
            ConfirmButton = confirmButton;
            OptionButtons = optionButtons;
            OptionTexts = optionTexts;
        }
    }

    private readonly struct WeaponSelectRefs
    {
        public readonly TMP_Text TitleText;
        public readonly TMP_Text NameText;
        public readonly TMP_Text DescriptionText;
        public readonly TMP_Text StatsText;
        public readonly Button BackButton;
        public readonly Button StartButton;
        public readonly Button[] OptionButtons;
        public readonly TMP_Text[] OptionTexts;

        /// <summary>
        /// 保存武器选择页引用。
        /// </summary>
        public WeaponSelectRefs(
            TMP_Text titleText,
            TMP_Text nameText,
            TMP_Text descriptionText,
            TMP_Text statsText,
            Button backButton,
            Button startButton,
            Button[] optionButtons,
            TMP_Text[] optionTexts)
        {
            TitleText = titleText;
            NameText = nameText;
            DescriptionText = descriptionText;
            StatsText = statsText;
            BackButton = backButton;
            StartButton = startButton;
            OptionButtons = optionButtons;
            OptionTexts = optionTexts;
        }
    }

    private readonly struct HudRefs
    {
        public readonly TMP_Text LevelText;
        public readonly TMP_Text GoldText;
        public readonly GameHeartHealthView HeartHealthView;
        public readonly TMP_Text FloorTimerText;
        public readonly TMP_Text KillText;
        public readonly TMP_Text ScoreText;
        public readonly TMP_Text TimeText;
        public readonly Image ExperienceFillImage;
        public readonly Image AmmoFillImage;
        public readonly Image EnergyFillImage;
        public readonly Image ChargeFillImage;
        public readonly TMP_Text AmmoText;
        public readonly TMP_Text EnergyText;

        /// <summary>
        /// 保存 HUD 引用。
        /// </summary>
        public HudRefs(
            TMP_Text levelText,
            TMP_Text goldText,
            GameHeartHealthView heartHealthView,
            TMP_Text floorTimerText,
            TMP_Text killText,
            TMP_Text scoreText,
            TMP_Text timeText,
            Image experienceFillImage,
            Image ammoFillImage,
            Image energyFillImage,
            Image chargeFillImage,
            TMP_Text ammoText,
            TMP_Text energyText)
        {
            LevelText = levelText;
            GoldText = goldText;
            HeartHealthView = heartHealthView;
            FloorTimerText = floorTimerText;
            KillText = killText;
            ScoreText = scoreText;
            TimeText = timeText;
            ExperienceFillImage = experienceFillImage;
            AmmoFillImage = ammoFillImage;
            EnergyFillImage = energyFillImage;
            ChargeFillImage = chargeFillImage;
            AmmoText = ammoText;
            EnergyText = energyText;
        }
    }

    private readonly struct OfferPanelRefs
    {
        public readonly TMP_Text TitleText;
        public readonly TMP_Text SubtitleText;
        public readonly Button RefreshButton;
        public readonly GameOfferView[] OfferViews;

        /// <summary>
        /// 保存选项页面引用。
        /// </summary>
        public OfferPanelRefs(TMP_Text titleText, TMP_Text subtitleText, Button refreshButton, GameOfferView[] offerViews)
        {
            TitleText = titleText;
            SubtitleText = subtitleText;
            RefreshButton = refreshButton;
            OfferViews = offerViews;
        }
    }

    private readonly struct ShopRefs
    {
        public readonly TMP_Text TitleText;
        public readonly TMP_Text GoldText;
        public readonly TMP_Text DescriptionText;
        public readonly TMP_Text EmptyText;
        public readonly GameShopOfferView[] OfferViews;
        public readonly Button RefreshButton;
        public readonly Button NextButton;

        /// <summary>
        /// 保存商店页面引用。
        /// </summary>
        public ShopRefs(
            TMP_Text titleText,
            TMP_Text goldText,
            TMP_Text descriptionText,
            TMP_Text emptyText,
            GameShopOfferView[] offerViews,
            Button refreshButton,
            Button nextButton)
        {
            TitleText = titleText;
            GoldText = goldText;
            DescriptionText = descriptionText;
            EmptyText = emptyText;
            OfferViews = offerViews;
            RefreshButton = refreshButton;
            NextButton = nextButton;
        }
    }

    private readonly struct ResultRefs
    {
        public readonly TMP_Text TitleText;
        public readonly Image AvatarImage;
        public readonly TMP_Text CharacterText;
        public readonly TMP_Text WeaponText;
        public readonly TMP_Text StatsText;
        public readonly Button RestartButton;
        public readonly Button ReturnTitleButton;
        public readonly Button QuitButton;

        /// <summary>
        /// 保存结算页面引用。
        /// </summary>
        public ResultRefs(
            TMP_Text titleText,
            Image avatarImage,
            TMP_Text characterText,
            TMP_Text weaponText,
            TMP_Text statsText,
            Button restartButton,
            Button returnTitleButton,
            Button quitButton)
        {
            TitleText = titleText;
            AvatarImage = avatarImage;
            CharacterText = characterText;
            WeaponText = weaponText;
            StatsText = statsText;
            RestartButton = restartButton;
            ReturnTitleButton = returnTitleButton;
            QuitButton = quitButton;
        }
    }

    private readonly struct PauseRefs
    {
        public readonly TMP_Text TitleText;
        public readonly TMP_Text StatsText;
        public readonly TMP_Text WeaponText;
        public readonly TMP_Text ItemsText;
        public readonly Button ResumeButton;
        public readonly Button RestartButton;
        public readonly Button SettingsButton;
        public readonly Button QuitButton;

        /// <summary>
        /// 保存暂停页面引用。
        /// </summary>
        public PauseRefs(
            TMP_Text titleText,
            TMP_Text statsText,
            TMP_Text weaponText,
            TMP_Text itemsText,
            Button resumeButton,
            Button restartButton,
            Button settingsButton,
            Button quitButton)
        {
            TitleText = titleText;
            StatsText = statsText;
            WeaponText = weaponText;
            ItemsText = itemsText;
            ResumeButton = resumeButton;
            RestartButton = restartButton;
            SettingsButton = settingsButton;
            QuitButton = quitButton;
        }
    }
}



