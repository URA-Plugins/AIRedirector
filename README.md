# AIRedirector

`AIRedirector` 启动配置的 UmaAI 子进程，并在 `AIRedirector` workspace 显示 `UmaAI.exe` stdout 原始行。

UmaAI 子进程 stdout 按 UTF-8 读取，对应 `chcp 65001` 输出。

UmaAI 子进程的工作目录设置为对应 exe 文件所在目录。
