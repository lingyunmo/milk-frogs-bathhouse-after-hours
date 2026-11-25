# 协作流程

1. 用 Issue 说明目标和验收方式。美术变更写明对应角色、制作阶段和参考视角。
2. 从最新的 `main` 创建短期分支，例如 `art/frog-skin` 或 `codex/project-setup`。
3. Unity 资源和对应的 `.meta` 一起提交。美术源文件使用 Git LFS 保存原始精度。Blender 外部资源使用相对路径或打包到工程中。
4. 创建关联 Issue 的 PR。提供验证结果，美术改动附预览。等待 `Verify` 通过后以 squash 方式合并。单人维护期间允许作者合并通过检查的 PR。
