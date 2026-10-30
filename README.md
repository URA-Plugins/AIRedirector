# AIRedirector

`AIRedirector` 是 URA 的 Windows 插件，启动为 UAF、Cook、Mecha 或 Legend 配置的 `UmaAI.exe`，并在 `AIRedirector` workspace 显示子进程的 UTF-8 stdout。每个进程以其 exe 所在目录作为工作目录。

## 运行要求

独立的进程启动、配置和原始输出功能不依赖安装 `LegendScenarioAnalyzer`。

正式包 `LegendScenarioAnalyzer v2026.9.26` 引用 Terminal.Gui `2.4.17`，不能在使用 `2.5.0` 的 Host 中加载。该包留在 `Plugins` 中可能连带阻止 AIRedirector 桥接初始化；运行此版本前需将该包移出 `Plugins`。

## 配置

配置界面使用 Host 提供的 Terminal.Gui `IApplication`。场景开关控制对应路径项；文件选择器只接受现有 exe。只有“保存”会将草稿写入 `PluginData/AIRedirector/settings.json`，取消或关闭界面不会写入配置。启用场景的程序路径无效时，插件初始化失败并报告路径。

## Legend 联动

manifest 将 `LegendScenarioAnalyzer` 声明为软联动。共享插件上下文中存在 Legend 时，AIRedirector 将训练评分与可定位的训练建议追加到训练卡，将心得候选评分与可定位建议追加到心得卡；行动评分、汇总和无法定位到卡片的普通建议写入非空时标题为 `AI` 的 Extra section，重要行动建议写入 Important。解析到可展示输出后会刷新对应训练记录。

## 构建

仓库通过 NuGet 包引用 Host API，通过 Git submodule 固定 Legend 源码。克隆后在仓库根执行：

```powershell
git -c core.longpaths=true submodule update --init --recursive
dotnet build .\AIRedirector.csproj -c Release -m:1 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PlatformTarget=AnyCPU -p:DeployUraPluginToLocalAppDataOnBuild=false
```

Host-dependent smoke 位于 `tests/AIRedirectorSmoke`，覆盖配置、Legend 输出、真实子进程 stdout，以及 `DisposeAsync` 后的子进程退出和面板清理。

smoke 使用与编译 SDK 共享依赖版本一致的宿主源码。SDK `2026.10.31.1` 使用 Terminal.Gui `2.5.0`、MessagePack `3.1.12`。

默认 smoke 执行本仓发布边界检查、闲置生命周期、配置与子进程检查，并从 fresh ZIP 实际加载 AIR 与 Legend，核对目标育成更新、共享加载上下文和停机清理。测试工具源码由 `tests/Support/` 持有。

本地运行默认 smoke 前，将 `URA_PLUGIN_SMOKE_PACKAGE_ROOT` 设为本轮 Release 构建 ZIP 所在的绝对目录；本仓及上述依赖必须各有唯一 ZIP。CI 使用共用 workflow 的 `GITHUB_WORKSPACE/artifacts`，缺包或重复包使测试失败。

## 验证与发布

在 Windows 仓库根执行 `act workflow_dispatch --artifact-server-path "$env:TEMP/ura-act-artifacts"`。本地与 GitHub 使用同一份 workflow；版本 tag 触发 GitHub Release 发布。环境要求、共用 workflow 本地映射和发布规则见 [URA plugin workflows](https://github.com/URA-Plugins/.github/blob/v1/README.md)。
