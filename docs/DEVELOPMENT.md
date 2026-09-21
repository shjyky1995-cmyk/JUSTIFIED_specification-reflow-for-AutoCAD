# 开发与交接指南

## 接续任务
先读 AGENTS.md、TASKS.md、ARCHITECTURE.md 及对应日志；检查 git status。
从已检查的 main 创建 task/<编号>-<简称> 分支，更新领取状态。
不要提交或清理他人的改动；中断时提交本任务 checkpoint 并保留任务分支。
并行任务用 git worktree add，集成前重新检查共享契约和相关测试。

## 构建入口（由 T02 实现）
在仓库根目录运行 Windows PowerShell：
```powershell
powershell -NoProfile -File scripts/build.ps1 -Target Core
powershell -NoProfile -File scripts/build.ps1 -Target All
powershell -NoProfile -File scripts/build.ps1 -Target Package
```
Core 不查找 CAD，运行独立核心测试；All 自动发现 2021，也可传 -AutoCadDir。
首次开发还原依赖需要网络；构建包包含运行依赖，目标 CAD 电脑不需联网还原 NuGet。
CAD 引用使用本地安装目录，CopyLocal=false，不分发 Autodesk DLL。
禁止降低系统安全策略来加载插件；手工测试在 AutoCAD 正常信任/加载流程完成。

## Git 提交与回滚
完成一个小步后运行相关检查、追加日志，再仅暂存本任务路径并提交。
```powershell
git status --short
git diff --check
git add <本任务文件>
git commit -m "feat(T03): 完成模型往返校验"
git log --all --oneline --grep="(T03)"
```
任务已验收后切回 main，以 --ff-only 合并；不能快进时在任务分支解决集成并复测，不自动覆盖。
回滚前检查工作区、确认目标提交与后续依赖，使用 git revert <提交SHA>，复测后追加日志并提交。
不要以 reset --hard 删除历史；阶段发布回退还需要恢复匹配的旧安装包与标准版本。
阶段通过后建立注释标签（如 m0-foundation），配置 GitHub 私有 origin 后推送 main 和该标签。
未提供地址时不创建或猜测远程仓库，结束报告中说明尚未远程备份。

## 日志模板
每任务一个 docs/devlog/Txx.md，按日期追加：输入输出、修改原因、修改范围、REQ/AC、
执行检查与结果、真实宿主范围、未完成项、下一操作。当前提交的 SHA 用任务编号查询。
验收证据指明环境与样本，不提交未经脱敏的业务正文；机器生成原始日志放 artifacts/。

## 外部电脑最小检查
记录 Windows、AutoCAD 2021 具体版本、安装包 hash；离线安装包后在空白图执行诊断命令。
将实际输出与预期比较，记录加载失败与安全提示，不直接修改安全设置。
最小加载通过仅证明插件与依赖可用；字体、实体、事务、三模板、打印与性能按后续任务核验。
