# 协同开发规范（BOOOM）

本文档用于程序协同开发时统一流程与质量标准。

## 1. 分支策略

- `main`：仅存放可运行、可演示的稳定版本。
- 功能开发一律从 `main` 拉分支，不直接在 `main` 开发。

分支命名：

- `feature/<模块>-<简述>`，如：`feature/loop-clue-unlock`
- `fix/<模块>-<问题>`，如：`fix/player-rotation-jitter`
- `refactor/<模块>-<简述>`
- `docs/<简述>`

## 2. 提交规范（Conventional Commits）

提交信息格式：

`<type>(<scope>): <subject>`

常用 type：

- `feat`：新功能
- `fix`：修复
- `refactor`：重构（不改功能）
- `docs`：文档
- `style`：格式调整（不改逻辑）
- `test`：测试
- `chore`：构建/工具/杂项

示例：

- `feat(loop): add character perspective switch`
- `fix(interaction): prevent duplicate clue pickup`
- `docs(workflow): add branch naming rules`

约束：

- 单次提交只做一件事。
- `subject` 使用动词原形，简短明确。
- 禁止 `update`, `修改一下`, `test` 等无意义提交信息。

## 3. 每日协同流程

1. 开工前同步：`git pull --rebase origin main`
2. 本地开发并自测（确保可运行）
3. 小步提交（每 30-90 分钟一个逻辑点）
4. 推送分支并发起合并请求（PR）

## 4. PR（合并请求）规范

PR 标题：

- 与主要提交保持一致，例如：`feat(loop): add clue unlock gate`

PR 描述至少包含：

- 背景与目标
- 改动点列表
- 自测结果（如何验证）
- 风险与回滚方案

合并前检查：

- [ ] 项目可正常打开与运行
- [ ] 无新增编译错误
- [ ] 无无关文件改动
- [ ] 已同步最新 `main` 并解决冲突

## 5. Unity 协作注意事项

- 不提交 `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`。
- 尽量避免两人同时改同一 Scene/Prefab。
- 若必须改同一场景，先沟通“编辑时间窗”，减少冲突。
- 场景大改前先提交一个可回滚点。

## 6. 代码评审关注点

- 是否符合 `docs/CODE_STYLE.md`
- 是否有空引用与边界条件处理
- 命名是否清晰、职责是否单一
- 是否引入不必要复杂度
