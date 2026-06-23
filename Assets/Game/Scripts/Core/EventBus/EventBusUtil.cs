using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 包含与 Unity 应用程序中事件总线和事件类型相关的方法和属性。
/// </summary>
public static class EventBusUtil {
    public static IReadOnlyList<Type> EventTypes { get; set; }
    public static IReadOnlyList<Type> EventBusTypes { get; set; }
    
#if UNITY_EDITOR
    public static PlayModeStateChange PlayModeState { get; set; }
    
    /// <summary>
    /// 初始化 Unity 编辑器相关的 EventBusUtil 组件。
    /// [InitializeOnLoadMethod] 属性会在脚本加载或编辑器进入 Play Mode 时被调用。
    /// 这对于初始化编辑状态和 Play Mode 中需要的类字段或状态很有用。
    /// 该方法设置编辑器 playModeStateChanged 事件的订阅者，
    /// 以便在编辑器的 Play Mode 发生变化时执行相应的操作。
    /// </summary>    
    [InitializeOnLoadMethod]
    public static void InitializeEditor() {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
    
    static void OnPlayModeStateChanged(PlayModeStateChange state) {
        PlayModeState = state;
        if (state == PlayModeStateChange.ExitingPlayMode) {
            ClearAllBuses();
        }
    }
#endif

    /// <summary>
    /// 在场景加载前的运行时初始化 EventBusUtil 类。
    /// [RuntimeInitializeOnLoadMethod] 属性指示 Unity 在游戏加载后但任何场景加载前执行此方法，
    /// 在 Play Mode 和 Build 运行后都会执行。这保证了在任何游戏对象、脚本或组件启动前，
    /// 必要的总线相关类型和事件的初始化已完成。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize() {
        EventTypes = PredefinedAssemblyUtil.GetTypes(typeof(IEvent));
        EventBusTypes = InitializeAllBuses();
    }

    static List<Type> InitializeAllBuses() {
        List<Type> eventBusTypes = new List<Type>();
        
        var typedef = typeof(EventBus<>);
        foreach (var eventType in EventTypes) {
            var busType = typedef.MakeGenericType(eventType);
            eventBusTypes.Add(busType);
            Debug.Log($"初始化事件总线：<{eventType.Name}>");
        }
        
        return eventBusTypes;
    }

    /// <summary>
    /// 清空（移除所有监听器）应用程序中的所有事件总线。
    /// </summary>
    public static void ClearAllBuses() {
        Debug.Log("清空所有事件总线...");
        for (int i = 0; i < EventBusTypes.Count; i++) {
            var busType = EventBusTypes[i];
            var clearMethod = busType.GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic);
            clearMethod?.Invoke(null, null);
        }
    }
}