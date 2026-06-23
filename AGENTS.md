# 项目工作要求

第一次进入对话时先查看并遵守以下规范文档，读取中文文档时必须显式使用 `-Encoding UTF8`：

- docs/开发规范/协同开发规范.md
- docs/开发规范/代码风格规范.md
- docs/开发规范/AI协同开发规范.md

所有的[SerializeField] private字段都要加Tooltip

如果用户的需求存在不明确、不具体、可能有歧义的地方，先提出问题让用户确认，再进行实现。

处理开发、修改、排查、评审任务、开发过程中优先遵守项目已有结构、命名、代码风格和 Unity 资源组织方式。

- 不默认偷偷运行时创建关键玩法对象、交互对象、房间、门、商店商品等可配置内容。
- 需要测试前，我会先列清楚你要在 Unity 里准备什么：创建哪个对象/Prefab、挂哪个脚本、拖哪些引用、放到哪个父对象下。
- 如果对象结构比较复杂，我可以先生成一个“模板 Prefab/模板对象”，你再基于模板改造。
- 自动帮你挂脚本时，优先在 GameManager 下创建中文名空对象，例如“房间转场效果”“关卡流程控制”“场景门生成”等，再把脚本挂上去并自动补引
用。

--
本项目已在 `E:\UnityProgram\YooAssets\Assets` 初始化 CodeGraph 索引。

处理代码理解、调用链分析、影响范围评估、修改前阅读相关脚本时，优先使用 CodeGraph MCP，并显式指定：

`projectPath = E:\UnityProgram\YooAssets\Assets`

不要使用项目根目录 `E:\UnityProgram\YooAssets` 的 CodeGraph 索引。业务脚本路径在 CodeGraph 中通常以 `Game/...`、`Editor/...`、
`AOT/...` 等相对 `Assets` 的路径出现。

当 CodeGraph 查询结果疑似不是最新，或 `codegraph status "E:\UnityProgram\YooAssets\Assets"` 显示 Pending Changes 且没有 daemon 自动同步时，执行：  `codegraph sync "E:\UnityProgram\YooAssets\Assets"`
CodeGraph 主要用于理解代码结构、符号关系、调用方、影响范围；简单文本搜索和文件列表仍可使用 `rg`。
--

- 同一个系统、强关联的脚本可以挂在同一个对象上；弱关联或独立职责的脚本单独一个对象，方便你查找和整理。
- 运行时自动创建只用于非常内部的表现层或兜底，不作为主要搭建方式；如果用了，我会提前说明。
- 如果有旧系统更新需要重构代码，不要做旧兼容兜底，这样会是代码量变多， 立即失效就立即失效 重新配置就行 没配置直接报错就行 ，添加了兜底不仅发现不了问题 还会增加代码量。旧字段会直接移除，新规则缺配置就报错/返回空，逼着配置问题尽早暴露；不会保留两套入口