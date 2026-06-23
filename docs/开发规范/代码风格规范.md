# 代码风格规范（Unity / C#）

## 1. 通用规则

- 使用 UTF-8，换行 LF。
- 使用 4 个空格缩进，不使用 Tab。
- 一行只做一件事，避免超长语句。
- 仅在必要时写注释，注释解释“为什么”，不是“做什么”。
- 所有新增 C# 类必须补充 `/// <summary>` 中文注释，说明脚本职责。
- 所有新增方法必须补充 `///` 中文注释，说明方法作用与关键行为。

## 2. 命名规范

- 类、方法、属性、公共字段：`PascalCase`
- 局部变量、参数：`camelCase`
- 私有字段：`_camelCase`
- 常量：`PascalCase`
- 接口：`I` 前缀，如 `IInteractable`

示例：

- `public class ClueManager`
- `private Rigidbody2D _rb;`
- `public void SwitchLoop(int loopId)`

## 3. Unity 脚本结构建议

建议顺序：

1. `using`
2. 类声明
3. 字段分组标记（`[Header("中文分组名")]`）
4. 序列化字段（`[SerializeField] private ...`）
5. 私有字段
5. 生命周期方法（`Awake/Start/Update/FixedUpdate`）
6. Public 方法
7. Private 方法

## 4. 字段可见性

- 默认 `private`。
- Inspector 需要配置时优先使用 `[SerializeField] private`，避免 `public` 泄漏。
- `public` 字段仅用于必须对外暴露的数据。
- Inspector 字段需按用途使用 `[Header("中文分类")]` 进行分组。

## 5. 方法设计

- 单一职责，尽量短小。
- 对外 API 参数做基本合法性校验。
- 避免深层嵌套，超过 3 层时考虑提前返回。

## 6. 错误处理与日志

- 可恢复错误：优先安全返回，并输出 `Debug.LogWarning`。
- 不可恢复错误：使用 `Debug.LogError` 并阻断后续逻辑。
- 日志包含上下文信息（对象名、线索ID、周目ID等）。

## 7. 性能与稳定性（基础）

- `Update` 中避免不必要的 `GetComponent`、字符串拼接与频繁分配。
- 重复访问的组件在 `Awake/Start` 缓存。
- 输入与物理逻辑按职责放在 `Update/FixedUpdate`。

## 8. 提交前自检

- 是否遵守 `.editorconfig`
- 是否有未使用变量/using
- 是否会引发空引用
- 是否影响现有流程
