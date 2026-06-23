# 2D Demo 设计说明

## 主流程

- Demo 入口场景为 `Assets/Game/Scenes/MainScene.unity`，当前通过 Build Settings 直接启动，暂时不经过 YooAssets / HybridCLR 热更新加载流程。
- `GameStateManager` 负责维护主流程状态：`Title`、`CharacterSelect`、`WeaponSelect`、`Playing`、`Paused`、`LevelUp`、`WeaponUpgrade`、`Shop`、`GameOver`、`Victory`。
- `GameFlowView` 使用临时 IMGUI 绘制开始界面和初始选择界面，用于先跑通最小流程。后续正式 UI 可以替换为 Canvas / TMP 实现，但状态入口保持不变。
- `DemoCombatController` 挂在 `DemoContentRoot` 上，监听 `Playing` 状态后生成运行时战斗对象，不依赖热更新资源和 Prefab。
- 当前流程为：标题界面按任意键进入初始选择界面，点击“开始游戏”进入 `Playing` 战斗状态。
- 战斗最小闭环包含玩家移动、怪物追踪、自动寻找最近怪物攻击、投射物命中、掉落经验 / 金币、升级、基础 HUD 和楼层倒计时。
- 玩家可按 `Space` 朝当前移动方向翻滚一小段距离，翻滚期间不会受到伤害。
- 战斗区域当前使用 `DemoCombatController` 的 `arenaSize` 和 `arenaInnerPadding` 配置生成边界，玩家移动会被限制在边界内部，敌人刷新点也会被限制在边界内部。
- 战斗 HUD 暂用 IMGUI 绘制，左上角包含等级、心格血量和金币；心格按 1 颗心 = 2 点血、半颗心 = 1 点血显示，并在最大生命较高时自动换行。
- 每层暂定持续 30 秒，倒计时结束后清理当前战斗对象；结算顺序为武器强化选择、角色属性强化选择、定期商店、下一层或通关。
- 玩家升级只增加待强化选择次数，不直接提升属性；每次强化选择可以从 3 个随机属性强化中选择 1 个。
- 玩家达到 5 / 10 / 15 / 20 级时额外获得一次武器强化选择，用于强化当前武器或投射物表现，例如子弹反弹、穿透、弹道数、射速和子弹体积。
- 商店暂用 IMGUI 实现，不再每层固定进入，当前由 `shopFloorInterval` 控制定期开放；商店可出售道具和遗物，道具提供属性、子弹体积等直接数值效果，遗物提供事件驱动和机制型效果。
- 结算暂用 IMGUI 实现，`GameOver` 和 `Victory` 共用同一结算界面，显示本局统计和玩家当前属性，并提供重新开始、返回标题和退出游戏入口。
- 暂停暂用 IMGUI 实现，`Playing` 状态按 `Esc` 进入 `Paused`，通过 `Time.timeScale = 0` 停止战斗时间和对象移动，并显示当前属性、武器、道具和菜单按钮。

## 数据层

- 生命系统拆分为红血槽位和临时特殊血。`GamePlayerController.MaxHealth` 只表示红血上限，`CurrentHealth` 只表示当前红血；2 点红血等于 1 个红心槽，奇数上限允许最后一个半槽。
- 蓝血、粉血、玻璃心和爆炸心等临时血量使用 `SpecialHealthType` 与 `SpecialHealthSegment` 记录，不增加红心槽。玩家通过 `AddSpecialHealth`、`RemoveSpecialHealth`、`ClearSpecialHealth` 和 `GetSpecialHealthSegments` 管理临时血。
- 玩家受伤时默认先从后获得的临时特殊血段开始扣除，剩余伤害再扣红血；玻璃心破碎、爆炸心触发等特殊规则后续在特殊血扣除点扩展。
- HUD 心槽显示由 `GameHeartHealthView` 统一实例化 `GameHeartSlotView` 槽位 prefab，红心槽在前，临时特殊血槽追加在红心槽后，并由 `HorizontalLayoutGroup` 自动排列。
- `Assets/Game/Scripts/Data` 保存 Demo 数据结构，静态配置优先使用 `ScriptableObject`，运行时数据使用可序列化 C# 数据类。
- 已建立静态数据：`CharacterData`、`WeaponData`、`EnemyData`、`ItemData`、`RelicData`、`FloorData`、`ShopItemData`、`UpgradeOptionData`。
- 已建立运行时数据：`PlayerRuntimeStats`、`RunStatistics`、`Inventory`、`WeaponRuntimeState`。
- `FloorData` 已接入 `DemoCombatController`，当前楼层持续时间、刷怪间隔、敌人生命倍率、敌人速度倍率和最大楼层数优先读取 `floorDataList`。
- 默认楼层资产位于 `Assets/Game/ScriptableObjects/Floors`，当前提供 `Floor_01`、`Floor_02`、`Floor_03` 三层示例配置。
- `UpgradeOptionData` 已接入 `DemoCombatController`，升级强化界面优先从 `upgradeOptionDataList` 随机抽取 3 个强化选项。
- 默认强化资产位于 `Assets/Game/ScriptableObjects/UpgradeOptions`，当前提供生命、攻击、移速、暴击率等示例配置；未配置时 fallback 池包含最大生命、攻击力、移速、暴击率、暴击伤害、射击间隔、穿透层数、反弹层数和弹道数。
- `ShopItemData` 已接入 `DemoCombatController`，商店界面优先从 `shopItemDataList` 随机抽取 3 个商品。
- 商店支持使用次数刷新当前商品，不再消耗金币刷新。每次进入商店时，玩家会获得角色配置的基础商店刷新次数；刷新会重新从商品池随机抽取道具槽和遗物槽，已购买商品记录不受影响。后续角色被动或遗物可以增加每次进入商店的基础刷新次数。
- 商店道具和遗物分池刷新。道具品质权重为普通 70、稀有 20、罕见 8、史诗 2、传说 0.5；遗物暂不使用普通和诅咒品质，品质权重为普通 0、稀有 30、罕见 30、史诗 25、传说 15。
- 商店商品价格使用固定默认价格，不再随楼层增长。道具默认价格为普通 3、稀有 5、罕见 8、史诗 12、传说 18；遗物默认价格为普通 10、稀有 16、罕见 24、史诗 36、传说 54。单个 `ShopItemData` 可以启用覆盖价格，启用后使用该商品自己的固定价格。
- 商店商品支持锁定，锁定且未购买的商品会在刷新时保留，未锁定槽位重新随机抽取。
- 默认商店商品资产位于 `Assets/Game/ScriptableObjects/ShopItems`，当前提供多个道具和遗物示例配置；旧武器商品资产仍保留，但商店运行时会过滤武器商品。
- 默认道具资产位于 `Assets/Game/ScriptableObjects/Items`，默认遗物资产位于 `Assets/Game/ScriptableObjects/Relics`，`ShopItemData` 会引用具体 `ItemData` 或 `RelicData`，购买记录统一显示为商品名称。
- `ItemData` 购买效果已接入商店，道具可配置多条 `ItemEffectEntry`，根据 `ItemEffectType` 立即应用最大生命、移动速度、移动速度倍率、攻击力、攻击力倍率、经验倍率、暴击率、暴击伤害、射击间隔、射速、弹速、弹体存在时间、穿透、反弹、弹道数、武器实例数、弹夹容量、换弹时间、最大能量、能量恢复、击杀能量、准度偏差、后坐力、击退距离和子弹体积等直接数值效果；道具允许负面数值效果，但不包含条件触发或战斗规则。
- `RelicData` 通过 `EffectKey` 绑定具体遗物效果实现，当前支持被动最大生命、金币收益倍率、爆裂弹芯和裂变弹芯等机制；金币倍率会累积不足 1 的小数收益，避免低掉落值时效果被取整吞掉。
- `CharacterData` 已接入玩家创建流程，`DemoPlayerController` 会使用 `startingCharacterData` 初始化生命、移动速度、攻击、攻击间隔、暴击率、暴击伤害、穿透层数、反弹层数和弹道数。
- `WeaponData` 已接入自动攻击流程，`DemoCombatController` 会使用 `startingWeaponData` 提供武器名称、基础伤害、攻击间隔、投射物速度和投射物存在时间。
- 武器实例数与弹道数已拆分：普通攻击、短按技能和长按技能会让每个武器实例从各自枪口点同步发射；玩家优先从 `CharacterData.PlayerPrefab` 创建，武器实例优先从 `WeaponData.WeaponPrefab` 创建并挂到玩家对象下的 `WeaponRoot`，由 `GamePlayerWeaponLayoutController` 统一调整位置、镜像和旋转；枪口点优先读取武器预制体内的 `MuzzlePoint` 子节点，武器图形使用 `WeaponData.Icon`，图形缩放不影响枪口点；弹道数表示每把武器单次发射的投射物数量，弹夹当前按一次齐射消耗 1 发。
- 准星反馈拆分为鼠标交互和实际开火两条动画入口：非战斗界面鼠标左键、鼠标右键按下触发 `CursorPulse` 点击反馈；`Playing` 战斗状态下左键点击反馈由真实开火接管，普通攻击成功发射时触发 `Fire` 开火反馈，并把 `FireSpeed` 设置为 `1 / 当前实时开火间隔`，用于配合 1 秒标准 Fire Clip 按当前射速完整播放。
- `WeaponData.AttackType` 已接入自动攻击流程，当前支持投射物、近距离环绕命中和范围脉冲三种模式；环绕和范围攻击先使用临时范围闪烁反馈，后续再替换为正式武器表现。
- 默认角色和武器资产位于 `Assets/Game/ScriptableObjects/Characters` 与 `Assets/Game/ScriptableObjects/Weapons`，当前包含初始训练飞弹、商店用重型飞弹、环绕刃和脉冲核心。
- `EnemyData` 已接入怪物刷新流程，`DemoCombatController` 会从当前 `FloorData.EnemyPool` 随机选择敌人数据，并用其初始化生命、速度、接触伤害、掉落和分数。
- 默认敌人资产位于 `Assets/Game/ScriptableObjects/Enemies`，当前提供 `Enemy_RedBubble` 示例配置。
- 关卡房间与难度系统第一阶段已新增静态数据结构：`StageData`、`RoomType`、`StageRoomOptionData`、`RoomOptionRule`、`RoomOptionPoolEntry`、`RoomOptionData`、`CombatRoomProfile`、`EnemySpawnProfile`、`EnemyPoolSegment`、`EnemySpawnEntry`、`BossData`、`BossSkillData` 和 `StageRunState`。
- `StageData` 当前作为关卡房间流程的主配置入口，覆盖关卡基础信息、普通/精英战斗房倍率、预算刷怪规则、Boss 配置、门选项规则和难度曲线；已在可关闭的 Stage 模式中逐步接入，旧 `FloorData` 流程仍保留兼容。
- 关卡房间与难度系统第二阶段已新增独立查询和推进层：`StageData` 可按房间节点查询 Boss、敌人池、门规则并计算预算和难度倍率；`GameStageProgressController` 可独立初始化 `StageRunState`、生成门选项，并在玩家选择任意门时推进一个房间节点。
- Boss 节点现在只由 `BossData.CombatNode` 决定，不再单独维护中 Boss / 最终 Boss 层字段；`BossData.IsFinalBoss` 仅用于后续 Boss 血条、镜头或奖励表现区分，不作为通关条件。Stage 通关条件为当前房间节点达到 `StageData.TotalCombatNodeCount` 并完成该房间。
- 门规则已改为 SO 池配置：`StageRoomOptionData` 保存可复用房间门数据，`RoomOptionRule` 只配置适用范围、规则权重、生成门数量和 `RoomOptionPoolEntry` 权重池。若下一房间节点存在 BossData，则强制只生成 1 个 Boss 门，忽略普通门池。
- 普通/精英战斗房的固定预算刷怪机制已新增计划层和运行时接入：`EnemySpawnPlan` 保存有限敌人批次计划，`GameStageEnemySpawnPlanGenerator` 根据 `EnemySpawnProfile` 的总预算、批次比例和 `EnemyPoolSegment` 权重生成计划；Stage 模式下 `GameEnemySpawnController` 会按计划分批生成敌人，并支持刷怪截止、提前刷下一批、软/硬上限和清场提前结束。
- Boss 房运行时已开始接入：Stage Boss 房会根据 `BossData.BossEnemyData` 生成 Boss 本体，Boss 死亡后直接发放 `BossData` 配置的分数、金币和经验，清理未击杀召唤物并完成当前战斗节点；`BossSkillData` 已支持 `OnStart`、`OnCooldown` 和 `OnHealthPercent` 触发 `SummonEnemies` 召唤敌人。
- Stage 评分分项已开始接入：普通/精英房记录击杀分、清场奖励、速度奖励和无伤奖励；Boss 房记录 Boss 本体分、Boss 无伤奖励、Boss 用时、Boss 受伤次数和召唤物击杀数；结算界面会显示这些分项，但仍不做评级、不按评分发奖励。
- 关卡房间与难度系统第三阶段已新增场景门交互骨架：`StageRoomDoorView` 负责靠近检测、Tooltip 文本和按 `E` 选择门；`GameStageRoomDoorController` 可根据 `GameStageProgressController` 生成的 `RoomOptionData` 创建/清理门对象，并将选择结果提交给 Stage 进度层。该骨架尚未接入当前战斗结束流程。
- 关卡房间与难度系统第四阶段已在 `GameCombatController` 中新增可关闭的 Stage 流程入口：`useStageFlow` 默认关闭，关闭时仍完全使用现有 `FloorData` 楼层流程；开启且配置 `StageData` 后，战斗计时结束会清理战斗对象，先处理待完成的武器强化和角色升级 UI，再生成 Stage 场景门，玩家选门后回到下一段战斗房。
- 战斗结束进入升级、武器强化、商店、胜负结算或 Stage 选门交互时，会隐藏玩家身上的武器图形、HUD 弹夹/能量条和准星弹夹进度；每次结算会停止换弹/蓄力状态并补满当前弹夹，下一回合或下一战斗房开始时再恢复武器显示。
- 第四阶段仍保守复用旧战斗运行时和旧楼层计时器：普通/精英房敌人生成已开始读取 `StageData` 的计划、时长和难度倍率，Boss 房已生成 Boss 本体并接入召唤技能，Stage 评分分项已接入结算展示；场景式商店内容继续复用 `GameShopController`，中转房入口和离开后目标战斗房由 `StageRoomOptionData` 生成的运行时 `RoomOptionData` 决定。
- `FloorData` 继续保留并驱动当前楼层流程，后续迁移时再逐步让 `StageData` 接管房间推进、固定预算刷怪和 Boss 层。
- 其他战斗内容仍优先使用 `DemoCombatController` 内的临时配置，后续再逐步切换为 ScriptableObject 驱动，避免一次性改动影响已跑通的 MVP 闭环。

## 属性词条与道具数值规则

- 升级词条和道具共用同一套直接数值属性池，区别只在获取来源：升级词条由升级选择获得，道具由商店购买获得。
- 升级强化和武器强化选择界面会显示一块当前属性摘要，内容与暂停面板的核心属性展示保持一致，方便选择词条时对照当前构筑状态。
- 通用属性池包含：最大生命、附加攻击力、攻击力倍率、移动速度倍率、经验倍率、射速倍率、换弹速度、弹夹容量、精确度、后坐力控制、冲击力、弹速倍率、子弹体积、子弹存在时间、暴击率、暴击伤害倍率。
- 最大生命和附加攻击力使用固定值；其余属性使用倍率增量，配置值 0.2 表示 +20%。
- 最终伤害基数公式为：(角色攻击力 + 武器攻击力 + 附加攻击力) * 攻击力倍率 * 其他倍率。
- 射速使用倍率模型：最终射速 = 基础射速 * 射速倍率；攻击间隔 = 1 / 最终射速。代码中基于 WeaponData.AttackInterval 换算为 AttackInterval / AttackSpeedMultiplier。
- 换弹速度、精确度、后坐力控制为正向展示属性，底层分别换算为更短换弹时间、更小开火扩散和更小后坐力。精确度采用线性抵消散射公式：最终散射角 = 基础散射角 * max(0, 1 - 精准度加成)，其中精准度加成 = 精准度倍率 - 1；例如 +30% 精准度会让 10 度基础散射变为 7 度，+100% 精准度会抵消到 0 度。
- 弹夹容量按倍率四舍五入；存在弹夹容量加成时，最终容量至少比基础容量 +1。
- 穿透、反弹、弹道数不再属于升级词条和道具通用属性池，后续由遗物或武器强化提供。

### 品阶数值表

| 属性 | 普通 | 稀有 | 罕见 | 史诗 | 传说 |
|---|---:|---:|---:|---:|---:|
| 最大生命 | 无 | 无 | +2 血 | +4 血 | +6 血 |
| 附加攻击力 | +2 | +4 | +6 | +9 | +12 |
| 攻击力倍率 | +5% | +9% | +15% | +32% | +55% |
| 移动速度倍率 | +6% | +12% | +20% | +30% | +45% |
| 经验倍率 | +10% | +20% | +35% | +50% | +75% |
| 射速倍率 | +5% | +9% | +15% | +32% | +55% |
| 换弹速度 | +5% | +10% | +20% | +35% | +50% |
| 弹夹容量 | +10% | +15% | +30% | +50% | +75% |
| 精确度 | +5% | +10% | +20% | +35% | +50% |
| 后坐力控制 | +5% | +10% | +20% | +35% | +50% |
| 冲击力 | +10% | +20% | +35% | +50% | +75% |
| 弹速倍率 | +5% | +10% | +20% | +35% | +50% |
| 子弹体积 | +3% | +8% | +15% | +25% | +40% |
| 子弹存在时间 | +5% | +10% | +20% | +35% | +50% |
| 暴击率 | +3% | +6% | +10% | +15% | +22% |
| 暴击伤害倍率 | +10% | +20% | +35% | +50% | +75% |

## 暂停面板属性 Tooltip 计划

- 暂停面板主属性文本保持简洁，例如“攻击力 +300%”。
- 鼠标悬停属性文本时显示 Tooltip，Tooltip 展示最终值、计算公式和来源明细。
- 攻击力 Tooltip 示例：

```text
攻击力
最终攻击力：177

计算：
(12 + 35 + 12) * 200% * 1.5 = 177

来源：
角色攻击力：12
武器攻击力：35
附加攻击力：12
攻击力倍率：200%
特殊总倍率：1.5
```

- Tooltip 数据建议由属性计算层输出结构化说明，UI 只负责展示，避免暂停面板硬编码公式。
## 房间转场与房间 Prefab 方案

- Stage 选门进入下一房间时已接入 `GameRoomTransitionController` 圆形黑幕转场：以玩家当前世界坐标换算为屏幕圆心，先收拢黑幕，遮满后执行房间切换回调，再从同一圆心打开黑幕。
- `GameRoomTransitionController` 建议挂在 `GameManager` 下的独立对象上，例如 `RoomTransitionController`；`GameCombatController` 只引用或查找已存在的控制器，不再自动把该脚本挂到战斗流程对象上。
- `GameRoomTransitionController` 会在自身对象下创建专用 `RoomTransitionCanvas` 和全屏 `RoomTransitionImage`，优先使用 `Assets/Game/Shaders/CircleRoomTransition.shader` 做圆形透明洞口；Shader 缺失时退回普通黑屏淡入淡出，避免流程卡死。
- `GameCombatController` 当前在黑幕遮满时推进 Stage 房间流程、清理战斗对象、重置玩家到 `roomSpawnPoint`（为空时使用世界原点），并调用 `GameCameraController.ForceSnapToTarget()` 让相机在黑幕内贴到玩家位置。黑幕重新打开前玩家控制保持关闭，避免黑屏期间移动或提前开火。
- 当前仍不为普通/精英/Boss/商店创建独立 Unity Scene。推荐下一步使用“同一 Unity Scene + 房间 Prefab 替换”的方式：把地图背景、碰撞边界、装饰、商品摆放点、门摆放点和玩家出生点做进房间 Prefab，在黑幕遮满回调中销毁旧房间 Prefab 并实例化目标房间 Prefab。
- 房间 Prefab 适合解决战斗大房间与商店/事件/宝箱小房间的地面、布局、可移动范围差异；后续只需要让房间 Prefab 暴露 `GameArenaController` 或房间边界配置，再由现有玩家边界和相机边界同步逻辑刷新即可。
- 商店、事件、宝箱等中转房已开始接入房间 Prefab 和场景交互物，当前通过真实房间停留与离开门进入后续战斗房。
## Stage 房间 Prefab 切换框架

- 已新增 `GameStageRoomView` 作为房间 Prefab 根节点脚本，用于标记房间类型、`GameArenaController`、`PlayerSpawnPoint`、`DoorRoot` 和房间自定义相机边界。脚本只查找已有子节点，不创建关键玩法内容。
- 已新增 `GameStageRoomPrefabController` 作为独立房间实例控制器，建议挂在 `GameManager/房间Prefab切换` 这类独立对象上。它按 `RoomType` 实例化配置的房间 Prefab，并销毁旧房间实例。
- `GameCombatController` 已接入房间 Prefab 控制器引用：Stage 开局和选门进入下一战斗房时，会先尝试加载当前房间类型对应的 Prefab，再同步当前房间的地图边界、玩家出生点、门生成根节点和相机边界。未配置房间 Prefab 时保留现有场景地图和旧流程。
- `GameCameraController` 支持优先使用当前房间 Prefab 内的 `PolygonCollider2D` 作为 Cinemachine 相机边界；未配置时回退到 `GameArenaController.WorldBounds` 生成的矩形边界。当前玩家移动范围仍由 `GameArenaController` 的矩形范围限制，多边形移动边界后续单独接入。
- 玩家场景阻挡采用“手动移动 + CircleCast 查询阻挡层”的轻量方案：`GamePlayerController` 仍直接设置 `transform.position`，但移动前会按 `Scene Collision Mask` 检查门、宝箱、浆果丛等静态阻挡物，命中时尝试单轴滑动。检测圆心支持 `Scene Collision Offset` 偏移，用于让阻挡主体贴合角色脚底或身体下半部分。交互仍保留距离判定，不依赖物理解算。
- 门、商店商品和功能房奖励物默认不再自动创建交互 `CircleCollider2D`，交互范围由各自 `Interact Radius` 的 Scene Gizmo 显示；若对象需要阻挡玩家，需要在 Prefab 中手动添加非 Trigger 的 `Collider2D` 并放入 `Scene Collision Mask` 对应层。
- 敌人与战斗房场地道具已接入统一安全随机点工具 `GameRandomSpawnUtility`：随机点会在当前房间 `ArenaMin/ArenaMax` 内生成，并支持玩家最小距离、边界留白和阻挡层重叠检测。`GameEnemySpawnController` 的普通敌人生成优先使用安全随机点，避免房间较小时被 Clamp 到玩家附近。
- 战斗房可通过 `StageCombatFieldPropSpawner` 生成浆果丛等中立场地道具。生成器应挂在战斗房 Prefab 内，配置 `Berry Bush Prefab`、生成数量、玩家安全距离和阻挡层；`GameStageRoomView` 会自动发现该生成器，战斗房开始时由 `GameCombatController` 触发生成，战斗结束或切房时清理。
- `StageBerryBushFieldProp` 是第一版浆果丛：被玩家投射物命中后按配置恢复固定生命和百分比生命，然后移除自身。它通过 `IProjectileHitTarget` 接入投射物命中系统，不计入敌人列表、击杀数、金币掉落或 Boss 召唤物统计。
- `GameStageRoomDoorController` 新增 `SetDoorRoot`，房间 Prefab 切换后门会生成到当前房间的 `DoorRoot` 下。
- `Door Visual Id` 已接入门外观系统：`GameStageRoomDoorController.Visual Configs` 按 ID 映射门 Sprite、颜色、Tooltip 偏移和可选动画控制器。生成门时优先使用 `RoomOptionData.DoorVisualId` 查找配置，找不到时按房间类型回退到 `door_normal`、`door_elite`、`door_boss`、`door_shop`、`door_event`、`door_treasure` 等默认 ID。未配置任何外观时继续使用门 Prefab 默认表现和房间类型临时颜色。
- 房间 Prefab 建议放在 `Assets/Game/Prefabs/Rooms`。每个房间 Prefab 推荐结构：

```text
Room_Combat_Template
  Arena              挂 GameArenaController
  PlayerSpawnPoint  玩家进入点
  DoorRoot          战斗结束后门生成根节点
  CameraBounds      挂 PolygonCollider2D，用于限制相机视野
  Props             地面、装饰、墙体等美术内容
```

- Stage 商店中转房已接入最小闭环：选择 `RoomType.Shop` 门后会切换到商店房 Prefab，准备当前商店数据，并在房间 `DoorRoot` 下生成一个“离开商店”门；玩家靠近离开门按 `E` 后会通过黑幕转场进入该商店门配置的 `TransitExitTargetCombatRoomType` 战斗房。
- 当前所有房间都会占用房间节点：普通、精英、Boss、商店、事件和宝箱门被选择后都会推进 B1/B30 进度；中转房离开进入绑定战斗房时不再额外推进节点。若推进后的节点是 Boss 节点，则 Boss 房优先覆盖门上配置的房间类型。
- 商店相关配置归属已收敛：`StageData` 不再维护商店商品池、品质权重、默认价格或商品摆放点；这些内容分别由 `GameShopController` 和商店房 Prefab 上的 `StageShopOfferLayoutController` / `StageShopOfferPoint` 管理。
- 商店房支持场景悬浮商品：推荐在商店房中配置 `StageShopOfferLayoutController`，提供商品槽位 Prefab 后，运行时会根据 `GameShopController.GetOfferCount()` 自动生成对应数量的 `StageShopOfferPoint` 并居中排布；每个点位子对象挂 `StageShopOfferView`。玩家靠近按 `E` 可直接购买，购买后默认隐藏当前槽位可视内容但保留槽位对象，商店刷新并绑定新商品后会重新显示。
- 商品槽位 Tooltip 推荐使用 `World Space Canvas` 搭建背景框和 `TextMeshProUGUI`：`StageShopOfferView` 的名称、价格和提示文本字段使用 `TMP_Text`，可同时绑定世界空间 `TextMeshPro` 或 Canvas 下的 `TextMeshProUGUI`；`Tooltip Root` 建议绑定到包含背景框和提示文本的根节点，用于统一显隐。
- `Event` 和 `Treasure` 当前作为通用功能房处理：房间 Prefab 只负责空间、边界、出生点、门根和 `StageRoomFunctionObjectSpawner`，进入房间后由生成器按权重随机实例化一个功能对象 Prefab。宝箱、回血机、属性训练器、后续神秘商人都应做成独立功能对象 Prefab，而不是每种功能单独做一个房间 Prefab。
- 第一版通用功能对象使用 `StageRoomRewardInteractableView`：玩家靠近按 `E` 后按配置发放金币、经验、红血回复、最大生命、攻击力、射速、换弹、准度、弹速、弹体大小、暴击等数值奖励。功能对象完成后才生成中转房离开门；未配置生成器或没有可用功能对象时会直接生成离开门，避免流程卡死。
- `StageData` 已新增 Editor 校验工具：在 StageData Inspector 中点击“校验当前 StageData”，或通过 `Tools/Stage/校验所有 StageData` 批量检查关卡基础信息、Boss 节点、门规则覆盖、房间选项和敌人池覆盖。工具只输出 Console 错误/警告并显示汇总，不会自动修改配置。
- 仍保留手动商品点兜底：未配置 `StageShopOfferLayoutController` 时，`GameCombatController` 会使用 `GameStageRoomView.ShopOfferPoints` 中手动摆放的 `StageShopOfferPoint`。
- 现阶段商店房 Prefab 至少需要配置根节点 `GameStageRoomView`、小房间 `GameArenaController`、`PlayerSpawnPoint`、`DoorRoot`、`CameraBounds` 和商品布局控制器或商品摆放点。普通/精英/Boss 可先复用同一个战斗房 Prefab；商店房配置更小的 `GameArenaController` 范围和独立的商品区域。
- 功能房 Prefab 建议复用同一个小房间结构，并新增：

```text
FunctionRoot                    功能对象父节点
FunctionSpawnPoint              功能对象生成点
StageRoomFunctionObjectSpawner  挂在 FunctionRoot 或独立子对象上
```

- 功能对象 Prefab 示例：

```text
Function_Chest
  StageRoomRewardInteractableView
  AvailableVisual               未开启宝箱表现
  UsedVisual                    已开启宝箱表现，可选
  TooltipCanvas                 World Space Canvas，可选
```
