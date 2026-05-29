# CSV Hash

CSV Hash 是一个简单的 Windows 桌面工具，用于对 CSV 文件中的指定字段做 SHA-256 不可逆脱敏。

当前发布版本使用 C# WinForms + .NET Framework 4.8 实现，目标是兼容 Windows 10/11，并尽量保持包体积小。它不依赖 Tauri/WebView2。

## 功能

- 选择本地 CSV 文件
- 自动读取 CSV 表头
- 勾选一个或多个需要加密的字段
- 指定 CSV 分隔符，默认是英文逗号
- 支持逗号、分号、竖线、制表符和自定义分隔符
- 输出文件保存到原 CSV 同目录
- 输出文件名格式：`entry-原文件名`

## 示例

输入文件 `users.csv`：

```csv
id,name,phone,email,city
1,张三,13800138000,zhangsan@example.com,北京
2,李四,13900139000,lisi@example.com,上海
3,王五,13700137000,wangwu@example.com,广州
```

在客户端中选择 `name` 和 `phone` 字段后，会生成：

```text
entry-users.csv
```

未选择的字段会保持原值，选择的字段会被替换为 SHA-256 十六进制摘要。

## Windows 构建

需要 Windows 机器或 GitHub Actions Windows runner，并安装 Visual Studio Build Tools / MSBuild。

本地构建：

```powershell
msbuild windows\CsvHash.WinForms\CsvHash.WinForms.csproj /p:Configuration=Release /p:Platform=AnyCPU /m
```

输出：

```text
windows\CsvHash.WinForms\bin\Release\CSV.Hash.exe
```

打包 portable zip：

```powershell
Compress-Archive -Path windows\CsvHash.WinForms\bin\Release\CSV.Hash.exe -DestinationPath CSV.Hash_windows-portable.zip -Force
```

## 自动发布

仓库包含 GitHub Actions workflow：

```text
.github/workflows/release.yml
```

推送 `v*` tag 时会自动构建 Windows portable zip 并发布到 GitHub Release。

示例：

```bash
git tag v0.1.0
git push origin v0.1.0
```

Release 产物：

```text
CSV.Hash_v0.1.0_windows-portable.zip
```

如果 Release 上传失败，请检查仓库设置：

```text
Settings -> Actions -> General -> Workflow permissions -> Read and write permissions
```

## 注意事项

- 加密方式是 SHA-256 哈希，不可逆。
- CSV 必须包含表头。
- 分隔符必须是单字符；制表符可在界面中选择，或自定义输入 `\t`。
- Windows 10/11 通常内置 .NET Framework 4.8；如果目标系统关闭或缺少 .NET Framework 4.8，需要在系统功能中启用或安装。

## 目录说明

```text
windows/CsvHash.WinForms/   Windows 发布版本源码
src-tauri/                  旧 Tauri 实现，当前不作为 Release 目标
```
