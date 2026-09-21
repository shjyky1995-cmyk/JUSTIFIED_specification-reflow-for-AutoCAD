# JUSTIFIED_specification-reflow-for-AutoCAD
工程设计说明智能编制与 CAD 排版系统。正式项目名与 GitHub 仓库一致，后续保持不变。
V1 主链路：DOCX → Document Model → 排版引擎 → AutoCAD DBText。
当前是 M0 基础骨架，不具备正式生成设计说明的能力。
当前任务分支 task/T02-foundation：构建和独立测试通过，真实 CAD 加载待验收。

## 接续入口
手工验证请打开 [测试文件/开始测试.md](测试文件/开始测试.md)，测试 DLL 在旁边的「程序」文件夹。

1. 阅读 [AGENTS.md](AGENTS.md)。
2. 在 [任务清单](docs/TASKS.md) 找到下一任务及前置条件。
3. 阅读 [架构](docs/ARCHITECTURE.md)、该任务日志和 [PRD](CAD_DesignNote_PRD_V1.0.md) 对应条款。
4. 按 [开发指南](docs/DEVELOPMENT.md) 构建、测试、提交。

## 基准
AutoCAD 2021 / Windows x64；插件 net48，通用类库 netstandard2.0。
本机和外部电脑均需真实宿主测试；通过编译不代表业务验收。
院标和三图幅参数见 [标定台账](docs/CALIBRATION.md)，未标定不得发布。
