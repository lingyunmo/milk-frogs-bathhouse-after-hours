# 协作流程

1. 用 Issue 说明目标和验收方式。美术变更写明对应角色、制作阶段和参考视角。
2. 从最新的 `main` 创建短期分支，例如 `art/frog-skin` 或 `codex/project-setup`。
3. Unity 资源和对应的 `.meta` 一起提交。美术源文件使用 Git LFS 保存原始精度。Blender 外部资源使用相对路径或打包到工程中。
4. 创建关联 Issue 的 PR。提供验证结果，美术改动附预览。等待 `Verify` 通过后以 squash 方式合并。单人维护期间允许作者合并通过检查的 PR。

## 本地 Windows 打包

- 在仓库根目录运行 `.\Tools\build_windows.ps1`。试玩版固定输出到 `Builds/TitleSparks/AfterHours.exe`，新构建直接更新这个目录。目标播放器运行时，关闭后再打包。
- 需要独立验收版本时运行 `.\Tools\build_windows.ps1 -QA`。它使用已经准备好的 `.local/art-playtest` 工程及独立存档，更新 `.local/title-sparks-qa/Smoke/AfterHours.exe`。验收前把待测代码和资源同步到该工程。
- 两种构建各覆盖 `.local` 下固定的日志。用 `-WhatIf` 查看输出路径，用 `-UnityExe` 指定其他位置的编辑器。复用既有目录，版本记录放在 Git。
