using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据 GameStateManager 的流程状态切换 CanvasGroup 页面。
/// </summary>
public sealed class GameCanvasPageRouter : MonoBehaviour
{
    [Serializable]
    private sealed class PageBinding
    {
        [Header("页面绑定")]
        [SerializeField] private GameStateManager.GameState state;
        [SerializeField] private GameCanvasPage page;

        /// <summary>
        /// 绑定的流程状态。
        /// </summary>
        public GameStateManager.GameState State => state;

        /// <summary>
        /// 绑定的 Canvas 页面。
        /// </summary>
        public GameCanvasPage Page => page;
    }

    [Header("流程引用")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("页面列表")]
    [SerializeField] private PageBinding[] pageBindings;

    private readonly List<GameCanvasPage> _registeredPages = new List<GameCanvasPage>();
    private readonly HashSet<GameCanvasPage> _visiblePages = new HashSet<GameCanvasPage>();
    private readonly HashSet<GameCanvasPage> _interactivePages = new HashSet<GameCanvasPage>();

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (gameStateManager == null)
            Debug.LogError("[GameUI] 缺少 GameStateManager，页面路由无法工作。");
    }

    private void OnEnable()
    {
        if (gameStateManager != null)
            gameStateManager.StateChanged += OnStateChanged;
    }

    private void Start()
    {
        if (gameStateManager == null)
            return;

        ShowState(gameStateManager.CurrentState);
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
            gameStateManager.StateChanged -= OnStateChanged;
    }

    /// <summary>
    /// 手动刷新到当前流程状态，供 Inspector 按钮或外部逻辑调用。
    /// </summary>
    public void Refresh()
    {
        if (gameStateManager == null)
            return;

        ShowState(gameStateManager.CurrentState);
    }

    /// <summary>
    /// 响应流程状态变化，并显示对应页面。
    /// </summary>
    /// <param name="previousState">上一个状态。</param>
    /// <param name="nextState">下一个状态。</param>
    private void OnStateChanged(GameStateManager.GameState previousState, GameStateManager.GameState nextState)
    {
        ShowState(nextState);
    }

    /// <summary>
    /// 显示指定状态对应页面，其他页面全部隐藏。
    /// </summary>
    /// <param name="state">需要显示的流程状态。</param>
    private void ShowState(GameStateManager.GameState state)
    {
        if (pageBindings == null)
            return;

        CollectPagesForState(state);
        ApplyPageVisibility();
    }

    /// <summary>
    /// 收集所有已注册页面和当前状态需要显示的页面。
    /// </summary>
    /// <param name="state">当前流程状态。</param>
    private void CollectPagesForState(GameStateManager.GameState state)
    {
        _registeredPages.Clear();
        _visiblePages.Clear();
        _interactivePages.Clear();

        for (int i = 0; i < pageBindings.Length; i++)
        {
            PageBinding binding = pageBindings[i];
            if (binding == null || binding.Page == null)
                continue;

            if (!_registeredPages.Contains(binding.Page))
                _registeredPages.Add(binding.Page);

            if (binding.State == state)
            {
                _visiblePages.Add(binding.Page);
                _interactivePages.Add(binding.Page);
            }

            if (state == GameStateManager.GameState.Paused && binding.State == GameStateManager.GameState.Playing)
                _visiblePages.Add(binding.Page);
        }
    }

    /// <summary>
    /// 根据收集结果统一应用页面显隐，避免同一个页面多状态绑定时被后续绑定覆盖。
    /// </summary>
    private void ApplyPageVisibility()
    {
        for (int i = 0; i < _registeredPages.Count; i++)
        {
            GameCanvasPage page = _registeredPages[i];
            if (page == null)
                continue;

            page.SetVisible(_visiblePages.Contains(page), _interactivePages.Contains(page));
        }
    }
}
