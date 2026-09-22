# 开发与交接指南

## 接续任务
固定开发、验收和短指令语义见 [WORKFLOW](WORKFLOW.md)；当前唯一下一任务见 [NEXT_AGENT](NEXT_AGENT.md)。
先检查Git状态，再按AGENTS规定读取入口；领取一个小任务，开发→检查→记录→提交→固定模板交付。用户回复“继续下一步”时按接续状态直接执行，无需重新规划。
默认从已验收main开task分支；当前T07依赖T06的先行开发按NEXT_AGENT记录的已提交基线建立分支，不把未验收祖先直接合入main。
不清理他人改动；中断保存checkpoint并更新接续。并行仅在明确授权且使用独立worktree时进行。

## 构建入口
在仓库根目录运行 Windows PowerShell：
```powershell
powershell -NoProfile -File scripts/build.ps1 -Target Core
powershell -NoProfile -File scripts/build.ps1 -Target All
powershell -NoProfile -File scripts/build.ps1 -Target Package
```
Core 不查找 CAD，运行独立核心测试；All 自动发现 2021，也可传 -AutoCadDir。
首次开发还原依赖需要网络；构建包包含运行依赖，目标 CAD 电脑不需联网还原 NuGet。
CAD 引用使用本地安装目录，CopyLocal=false，不分发 Autodesk DLL。
开发工具须安装 global.json 中的 SDK 8.0.425；net48 测试要求 Windows .NET Framework 4.8。
锁文件已入库，默认 locked-mode；只有有意变更依赖时用 -UpdateLocks，并审查锁文件差异。
产物位于 artifacts/packages/<时间戳>/；原始 TRX 与 CAD 输出仅留本地，验收摘要进入日志。
Core 入口已经验证不依赖 CAD 引用；All/Package 验证 AutoCAD DLL 为 24.0。
真实加载脚本 scripts/test-cad.ps1 接收 -AutoCadDir 和 -PluginPath，不修改信任设置。
若控制台拒绝未信任路径，在 AutoCAD 正常 NETLOAD 流程人工验证；切勿将未加载记录为通过。
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
