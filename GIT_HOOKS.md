# Git hooks 启用指南

本仓库包含版本化的 `.githooks/pre-commit` 和 `.githooks/pre-push`，用于阻止 Git 提交元数据暴露私人邮箱。出于安全考虑，Git 不会在克隆仓库时自动启用其中的 hooks，因此每个新克隆都必须初始化一次。

## 一键启用

先在 GitHub 的 **Settings → Emails** 中找到自己的 noreply 邮箱，然后在仓库根目录运行以下命令。

PowerShell 7（Windows、Linux、macOS）：

```powershell
pwsh -File ./scripts/enable-git-hooks.ps1 -Email "<ID>+<USERNAME>@users.noreply.github.com"
```

Windows PowerShell 5.1：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\enable-git-hooks.ps1 -Email "<ID>+<USERNAME>@users.noreply.github.com"
```

脚本会完成以下操作：

1. 确认脚本位于本仓库的有效 Git 工作区中。
2. 验证 noreply 邮箱，并将其写入当前仓库的本地 `user.email` 配置。
3. 设置当前克隆的 `core.hooksPath=.githooks`。
4. 运行 `pre-commit` 自检，确认 hooks 可以执行。

脚本不会输出邮箱值。如果当前有效的 `user.email` 已经是 GitHub noreply 地址，可以省略 `-Email`：

```powershell
pwsh -File ./scripts/enable-git-hooks.ps1
```

## 验证结果

运行以下命令：

```powershell
git config --local --get core.hooksPath
git hook run pre-commit
```

第一条命令应输出 `.githooks`，第二条命令应以退出码 `0` 完成。

## 防护范围

- `pre-commit`：提交前检查当前 `user.email`，仅允许 GitHub noreply 地址。
- `pre-push`：推送前检查所有待推送提交的 author/committer 邮箱。
- GitHub Actions：对 push 和 Pull Request 的全部可达提交再次执行同一规则，并在错误日志中隐藏邮箱值。
- GitHub 账号设置：建议开启 **Keep my email addresses private** 和 **Block command line pushes that expose my email**。

不要使用 `--no-verify` 绕过本地 hooks。即使本地 hooks 被跳过，GitHub Actions 仍会拒绝不符合规则的提交，但提交一旦推送到公开仓库或 PR 引用中，就可能已经短暂公开。

## 故障排查

如果脚本提示邮箱无效，请重新运行并传入 GitHub noreply 邮箱：

```powershell
pwsh -File ./scripts/enable-git-hooks.ps1 -Email "<ID>+<USERNAME>@users.noreply.github.com"
```

如果 PowerShell 找不到 `pwsh`，请在 Windows 上使用前面的 `powershell -ExecutionPolicy Bypass` 命令，或安装 PowerShell 7。

如需确认当前克隆是否已启用 hooks：

```powershell
git config --local --get core.hooksPath
```

该配置是每个克隆独立保存的；重新克隆后需要再次运行启用脚本。
