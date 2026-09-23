# 当前接续状态（所有 Agent 共用）

更新：2026-09-23。MVP 完成并合入 main（T01～T11），推送 GitHub 私有仓库。分支 task/T11-offline-delivery 已并入 main。剩余 T12 为正式发布前验收，从 main 开新分支。

## MVP 范围（已交付）

- 正式两步：DN_NOTE_SET 设一次（production 标准包、图幅、单位比例、DOCX、报告目录，随图保存）+ DN_NOTE 日常点一次位置；无警告不追问；取消/失败零新增；一次 U 撤销。
- 排版：中文禁则换行、固定行槽、三图幅分栏、自动续页、上下标（项目标定 0.7/0.35/0.7/0.2）、工程字符透传、缺字形阻断（①-⑳ 等实测缺失）。
- 限额：块/字符/页/对象上限，超出 E_RESOURCE_LIMIT 拒绝不截断。
- 运行报告：本地 JSON（InputHash、分阶段耗时、页/对象数、告警、包围盒、Committed）。
- 候选包：build.ps1 -Target Package → bundle 22 文件 PACKAGE_VERIFY_OK；离线构建（--locked-mode）；INSTALL.md 部署说明。

## 证据索引

- T06/T10 日志：正式命令闭环、撤销、UCS、上下标 18/29 对象、字符清单截图、拒绝路径、DEV 回归。
- T11 日志：新版取消四组证据（T11_MANUAL_CHECK_OK，目录 artifacts/t11-manual/20260923-223055-503）；旧版 20 次热运行 P95=373ms；两页确定性。
- 核心测试双框架 143/143，插件编译 0 警告（每次构建）。

## 未完成（T12，正式发布前验收）

Word/WPS 同义核对（CAL-06，需用户提供两份同义样本）；三图幅业务样本本机+外机；一次真实打印确认；取消时延 P95 标定（CAL-08）；断网运行实测；结构专业核验。standards/published 仍为空——本机 production 测试包（artifacts 下）不是发布资产。

## 分支与远程

main 现含 T01～T11（合自 task/T11-offline-delivery）；任务分支保留。恢复动作：从 main 开 task/T12-acceptance 继续。
