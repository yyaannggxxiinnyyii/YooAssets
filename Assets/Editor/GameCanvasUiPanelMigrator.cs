using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 给当前场景 UI 面板补齐面板控制器，并按面板节点内的控件名称重新绑定引用。
/// </summary>
public static class GameCanvasUiPanelMigrator
{
    private const string RootName = "GameCanvasRoot";
    private const string MenuPath = "Tools/Game UI/迁移当前场景 UI 面板控制器";

    /// <summary>
    /// 给当前场景 UI 面板补齐面板控制器，并按节点名称绑定字段。
    /// </summary>
    [MenuItem(MenuPath)]
    public static void MigrateCurrentScene()
    {
        Transform root = FindCanvasRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("迁移失败", $"当前场景中没有找到 {RootName}。", "确定");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Migrate Game UI Panel Controllers");

        MigrateTitlePanel(root);
        MigrateCharacterSelectPanel(root);
        MigrateWeaponSelectPanel(root);
        MigrateHudPanel(root);
        MigrateLevelUpPanel(root);
        MigrateWeaponUpgradePanel(root);
        MigrateShopPanel(root);
        MigrateResultPanel(root);
        MigratePausePanel(root);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[GameUI] 当前场景 UI 面板控制器迁移完成。");
    }

    /// <summary>
    /// 查找当前场景 UI 根节点。
    /// </summary>
    /// <returns>UI 根节点。</returns>
    private static Transform FindCanvasRoot()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform root = canvases[i].transform.Find(RootName);
            if (root != null)
                return root;
        }

        GameObject rootObject = GameObject.Find(RootName);
        return rootObject != null ? rootObject.transform : null;
    }

    /// <summary>
    /// 迁移标题面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateTitlePanel(Transform root)
    {
        Transform panel = FindPanel(root, "TitlePanel");
        if (panel == null)
            return;

        GameTitlePanelController target = EnsureComponent<GameTitlePanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<TMP_Text>(targetObject, "hintText", panel, "HintText");
        SetObjectReference<Button>(targetObject, "clickButton", panel, "ClickCatcher");
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移角色选择面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateCharacterSelectPanel(Transform root)
    {
        Transform panel = FindPanel(root, "CharacterSelectPanel");
        if (panel == null)
            return;

        GameCharacterSelectPanelController target = EnsureComponent<GameCharacterSelectPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "CharacterSelectTitleText");
        SetObjectReference<TMP_Text>(targetObject, "nameText", panel, "NameText");
        SetObjectReference<TMP_Text>(targetObject, "descriptionText", panel, "DescriptionText");
        SetObjectReference<TMP_Text>(targetObject, "statsText", panel, "StatsText");
        SetObjectReference<Button>(targetObject, "confirmButton", panel, "ConfirmCharacterButton");
        SetComponentArray<Button>(targetObject, "optionButtons", panel, "CharacterOption_", 6);
        SetOptionTextArray(targetObject, "optionTexts", panel, "CharacterOption_", 6);
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移武器选择面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateWeaponSelectPanel(Transform root)
    {
        Transform panel = FindPanel(root, "WeaponSelectPanel");
        if (panel == null)
            return;

        GameWeaponSelectPanelController target = EnsureComponent<GameWeaponSelectPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "WeaponSelectTitleText");
        SetObjectReference<TMP_Text>(targetObject, "nameText", panel, "NameText");
        SetObjectReference<TMP_Text>(targetObject, "descriptionText", panel, "DescriptionText");
        SetObjectReference<TMP_Text>(targetObject, "statsText", panel, "StatsText");
        SetObjectReference<Button>(targetObject, "backButton", panel, "BackButton");
        SetObjectReference<Button>(targetObject, "startButton", panel, "StartGameButton");
        SetComponentArray<Button>(targetObject, "optionButtons", panel, "WeaponOption_", 6);
        SetOptionTextArray(targetObject, "optionTexts", panel, "WeaponOption_", 6);
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移 HUD 面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateHudPanel(Transform root)
    {
        Transform panel = FindPanel(root, "HudPanel");
        if (panel == null)
            return;

        GameHudPanelController target = EnsureComponent<GameHudPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "levelText", panel, "LevelText");
        SetObjectReference<TMP_Text>(targetObject, "goldText", panel, "GoldText");
        SetDirectComponentReference<GameHeartHealthView>(targetObject, "heartHealthView", panel);
        SetObjectReference<TMP_Text>(targetObject, "floorTimerText", panel, "FloorTimerText");
        SetObjectReference<TMP_Text>(targetObject, "killText", panel, "KillText");
        SetObjectReference<TMP_Text>(targetObject, "scoreText", panel, "ScoreText");
        SetObjectReference<TMP_Text>(targetObject, "timeText", panel, "TimeText");
        SetObjectReference<Image>(targetObject, "experienceFillImage", panel, "ExpBarFill");
        SetObjectReference<Image>(targetObject, "ammoFillImage", panel, "AmmoBarFill");
        SetObjectReference<Image>(targetObject, "energyFillImage", panel, "EnergyBarFill");
        SetObjectReference<Image>(targetObject, "chargeFillImage", panel, "ChargeBarFill");
        SetObjectReference<TMP_Text>(targetObject, "ammoText", panel, "AmmoText");
        SetObjectReference<TMP_Text>(targetObject, "energyText", panel, "EnergyText");
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移升级面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateLevelUpPanel(Transform root)
    {
        Transform panel = FindPanel(root, "LevelUpPanel");
        if (panel == null)
            return;

        GameLevelUpPanelController target = EnsureComponent<GameLevelUpPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<TMP_Text>(targetObject, "remainingText", panel, "SubtitleText");
        SetObjectReference<Button>(targetObject, "refreshButton", panel, "RefreshButton");
        SetComponentArray<GameOfferView>(targetObject, "offerViews", panel, "OfferCard_", 3);
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移武器强化面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateWeaponUpgradePanel(Transform root)
    {
        Transform panel = FindPanel(root, "WeaponUpgradePanel");
        if (panel == null)
            return;

        GameWeaponUpgradePanelController target = EnsureComponent<GameWeaponUpgradePanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<TMP_Text>(targetObject, "currentWeaponText", panel, "SubtitleText");
        SetComponentArray<GameOfferView>(targetObject, "offerViews", panel, "OfferCard_", 3);
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移商店面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateShopPanel(Transform root)
    {
        Transform panel = FindPanel(root, "ShopPanel");
        if (panel == null)
            return;

        GameShopPanelController target = EnsureComponent<GameShopPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<TMP_Text>(targetObject, "goldText", panel, "GoldText");
        SetObjectReference<TMP_Text>(targetObject, "descriptionText", panel, "DescriptionText");
        SetObjectReference<TMP_Text>(targetObject, "emptyText", panel, "EmptyText");
        SetComponentArray<GameShopOfferView>(targetObject, "itemOfferViews", panel, "ShopCard_", 0, 3);
        SetComponentArray<GameShopOfferView>(targetObject, "relicOfferViews", panel, "ShopCard_", 3, 3);
        BindShopOfferViews(panel);
        SetObjectReference<Button>(targetObject, "refreshButton", panel, "RefreshButton");
        SetObjectReference<Button>(targetObject, "nextFloorButton", panel, "NextFloorButton");
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移结算面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigrateResultPanel(Transform root)
    {
        Transform panel = FindPanel(root, "ResultPanel");
        if (panel == null)
            return;

        GameResultPanelController target = EnsureComponent<GameResultPanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<Image>(targetObject, "avatarImage", panel, "AvatarImage");
        SetObjectReference<TMP_Text>(targetObject, "characterText", panel, "CharacterText");
        SetObjectReference<TMP_Text>(targetObject, "weaponText", panel, "WeaponText");
        SetObjectReference<TMP_Text>(targetObject, "statsText", panel, "StatsText");
        SetObjectReference<TMP_Text>(targetObject, "characterStatsText", panel, "CharacterStatsText");
        SetObjectReference<TMP_Text>(targetObject, "weaponStatsText", panel, "WeaponStatsText");
        SetObjectReference<Button>(targetObject, "restartButton", panel, "RestartButton");
        SetObjectReference<Button>(targetObject, "returnTitleButton", panel, "ReturnTitleButton");
        SetObjectReference<Button>(targetObject, "quitButton", panel, "QuitButton");
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 迁移暂停面板绑定。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    private static void MigratePausePanel(Transform root)
    {
        Transform panel = FindPanel(root, "PausePanel");
        if (panel == null)
            return;

        GamePausePanelController target = EnsureComponent<GamePausePanelController>(panel);
        SerializedObject targetObject = new SerializedObject(target);
        SetObjectReference<TMP_Text>(targetObject, "titleText", panel, "TitleText");
        SetObjectReference<TMP_Text>(targetObject, "statsText", panel, "StatsText");
        SetObjectReference<TMP_Text>(targetObject, "weaponText", panel, "WeaponText");
        SetObjectReference<TMP_Text>(targetObject, "itemsText", panel, "ItemsText");
        SetObjectReference<Button>(targetObject, "resumeButton", panel, "ResumeButton");
        SetObjectReference<Button>(targetObject, "restartButton", panel, "RestartButton");
        SetObjectReference<Button>(targetObject, "settingsButton", panel, "SettingsButton");
        SetObjectReference<Button>(targetObject, "quitButton", panel, "QuitButton");
        targetObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 查找指定名称的面板节点。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    /// <param name="panelName">面板名称。</param>
    /// <returns>面板节点。</returns>
    private static Transform FindPanel(Transform root, string panelName)
    {
        Transform directChild = root.Find(panelName);
        if (directChild != null)
            return directChild;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == panelName)
                return children[i];
        }

        Debug.LogWarning($"[GameUI] 未找到面板节点：{panelName}");
        return null;
    }

    /// <summary>
    /// 获取或添加指定组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="target">目标节点。</param>
    /// <returns>目标组件。</returns>
    private static T EnsureComponent<T>(Transform target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        return Undo.AddComponent<T>(target.gameObject);
    }

    /// <summary>
    /// 按子节点名称绑定组件引用字段。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">字段名。</param>
    /// <param name="root">查找根节点。</param>
    /// <param name="childName">子节点名称。</param>
    private static void SetObjectReference<T>(SerializedObject targetObject, string fieldName, Transform root, string childName) where T : Component
    {
        SerializedProperty property = targetObject.FindProperty(fieldName);
        if (property == null)
            return;

        Transform child = FindChild(root, childName);
        property.objectReferenceValue = child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 绑定目标节点自身组件引用字段。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">字段名。</param>
    /// <param name="root">目标节点。</param>
    private static void SetDirectComponentReference<T>(SerializedObject targetObject, string fieldName, Transform root) where T : Component
    {
        SerializedProperty property = targetObject.FindProperty(fieldName);
        if (property == null)
            return;

        property.objectReferenceValue = root.GetComponent<T>();
    }

    /// <summary>
    /// 按连续命名绑定组件数组。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">数组字段名。</param>
    /// <param name="root">查找根节点。</param>
    /// <param name="namePrefix">子节点前缀。</param>
    /// <param name="count">数组长度。</param>
    private static void SetComponentArray<T>(SerializedObject targetObject, string fieldName, Transform root, string namePrefix, int count) where T : Component
    {
        SetComponentArray<T>(targetObject, fieldName, root, namePrefix, 0, count);
    }

    /// <summary>
    /// 按连续命名绑定组件数组。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">数组字段名。</param>
    /// <param name="root">查找根节点。</param>
    /// <param name="namePrefix">子节点前缀。</param>
    /// <param name="startIndex">子节点起始编号。</param>
    /// <param name="count">数组长度。</param>
    private static void SetComponentArray<T>(SerializedObject targetObject, string fieldName, Transform root, string namePrefix, int startIndex, int count) where T : Component
    {
        SerializedProperty property = targetObject.FindProperty(fieldName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            Transform child = FindChild(root, $"{namePrefix}{startIndex + i}");
            property.GetArrayElementAtIndex(i).objectReferenceValue = child != null ? child.GetComponent<T>() : null;
        }
    }

    /// <summary>
    /// 按连续命名绑定按钮子文本数组。
    /// </summary>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">数组字段名。</param>
    /// <param name="root">查找根节点。</param>
    /// <param name="namePrefix">按钮节点前缀。</param>
    /// <param name="count">数组长度。</param>
    private static void SetOptionTextArray(SerializedObject targetObject, string fieldName, Transform root, string namePrefix, int count)
    {
        SerializedProperty property = targetObject.FindProperty(fieldName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            Transform child = FindChild(root, $"{namePrefix}{i}");
            property.GetArrayElementAtIndex(i).objectReferenceValue =
                child != null ? child.GetComponentInChildren<TMP_Text>(true) : null;
        }
    }

    /// <summary>
    /// 绑定商店商品卡片内部视图字段。
    /// </summary>
    /// <param name="panel">商店面板节点。</param>
    private static void BindShopOfferViews(Transform panel)
    {
        for (int i = 0; i < 6; i++)
        {
            Transform card = FindChild(panel, $"ShopCard_{i}");
            if (card == null)
                continue;

            GameShopOfferView view = card.GetComponent<GameShopOfferView>();
            if (view == null)
                continue;

            SerializedObject serializedObject = new SerializedObject(view);
            serializedObject.FindProperty("backgroundImage").objectReferenceValue = card.GetComponent<Image>();
            SetChildObjectReference<TMP_Text>(serializedObject, "nameText", card, "NameText");
            SetChildObjectReference<TMP_Text>(serializedObject, "descriptionText", card, "DescriptionText");
            SetChildObjectReference<TMP_Text>(serializedObject, "costText", card, "CostText");
            SetChildObjectReference<TMP_Text>(serializedObject, "lockStateText", card, "LockStateText");
            SetChildObjectReference<Button>(serializedObject, "buyButton", card, "BuyButton");
            SetChildObjectReference<Button>(serializedObject, "lockButton", card, "LockButton");
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 按子节点名称绑定指定组件到序列化字段。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="targetObject">目标序列化对象。</param>
    /// <param name="fieldName">字段名。</param>
    /// <param name="root">查找根节点。</param>
    /// <param name="childName">子节点名称。</param>
    private static void SetChildObjectReference<T>(SerializedObject targetObject, string fieldName, Transform root, string childName) where T : Component
    {
        SerializedProperty property = targetObject.FindProperty(fieldName);
        if (property == null)
            return;

        Transform child = FindChild(root, childName);
        property.objectReferenceValue = child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 在层级内查找指定名称子节点。
    /// </summary>
    /// <param name="root">根节点。</param>
    /// <param name="childName">子节点名称。</param>
    /// <returns>子节点。</returns>
    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }
}
