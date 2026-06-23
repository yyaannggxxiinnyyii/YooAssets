using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 预定义程序集工具类，用于与预定义程序集交互。
/// 允许获取当前 AppDomain 中实现特定接口的所有类型。
/// 更多详情，请访问 <see href="https://docs.unity3d.com/2023.3/Documentation/Manual/ScriptCompileOrderFolders.html">Unity 文档</see>
/// </summary>
public static class PredefinedAssemblyUtil {
    /// <summary>
    /// 定义预定义程序集类型的枚举，用于导航和识别。
    /// </summary>    
    enum AssemblyType {
        AssemblyCSharp,
        AssemblyCSharpEditor,
        AssemblyCSharpEditorFirstPass,
        AssemblyCSharpFirstPass
    }

    /// <summary>
    /// 将程序集名称映射到对应的 AssemblyType。
    /// </summary>
    /// <param name="assemblyName">程序集的名称。</param>
    /// <returns>对应的 AssemblyType，如果没有匹配项则返回 null。</returns>
    static AssemblyType? GetAssemblyType(string assemblyName) {
        return assemblyName switch {
            "Assembly-CSharp" => AssemblyType.AssemblyCSharp,
            "Assembly-CSharp-Editor" => AssemblyType.AssemblyCSharpEditor,
            "Assembly-CSharp-Editor-firstpass" => AssemblyType.AssemblyCSharpEditorFirstPass,
            "Assembly-CSharp-firstpass" => AssemblyType.AssemblyCSharpFirstPass,
            _ => null
        };
    }

    /// <summary>
    /// 遍历给定程序集，查找所有实现指定接口的类型，并将其添加到集合中。
    /// </summary>
    /// <param name="assemblyTypes">程序集中所有类型的 Type 对象数组。</param>
    /// <param name="interfaceType">要检查的接口 Type。</param>
    /// <param name="results">要添加结果的类型集合。</param>
    static void AddTypesFromAssembly(Type[] assemblyTypes, Type interfaceType, ICollection<Type> results) {
        if (assemblyTypes == null) return;
        for (int i = 0; i < assemblyTypes.Length; i++) {
            Type type = assemblyTypes[i];
            if (type != interfaceType && interfaceType.IsAssignableFrom(type)) {
                results.Add(type);
            }
        }
    }
    
    /// <summary>
    /// 从当前 AppDomain 的所有程序集中获取实现指定接口的所有类型。
    /// </summary>
    /// <param name="interfaceType">要获取所有实现类型的接口 Type。</param>
    /// <returns>实现该接口的所有类型的列表。</returns>    
    public static List<Type> GetTypes(Type interfaceType) {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        Dictionary<AssemblyType, Type[]> assemblyTypes = new Dictionary<AssemblyType, Type[]>();
        List<Type> types = new List<Type>();
        for (int i = 0; i < assemblies.Length; i++) {
            AssemblyType? assemblyType = GetAssemblyType(assemblies[i].GetName().Name);
            if (assemblyType != null) {
                assemblyTypes.Add((AssemblyType) assemblyType, assemblies[i].GetTypes());
            }
        }
        
        assemblyTypes.TryGetValue(AssemblyType.AssemblyCSharp, out var assemblyCSharpTypes);
        AddTypesFromAssembly(assemblyCSharpTypes, interfaceType, types);

        assemblyTypes.TryGetValue(AssemblyType.AssemblyCSharpFirstPass, out var assemblyCSharpFirstPassTypes);
        AddTypesFromAssembly(assemblyCSharpFirstPassTypes, interfaceType, types);
        
        return types;
    }
}
