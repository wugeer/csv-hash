# CSV Hash

CSV Hash 是一个基于 Tauri + Rust 的桌面客户端，用于对 CSV 文件中的指定字段做 SHA-256 不可逆脱敏。

## 功能

- 选择本地 CSV 文件
- 自动读取 CSV 表头
- 勾选一个或多个需要加密的字段
- 指定 CSV 分隔符，默认是英文逗号
- 支持常见分隔符：逗号、分号、竖线、制表符
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

## 本地开发

需要先安装 Node.js、Rust 和 Tauri 所需的系统依赖。

安装前端依赖：

```bash
npm install
```

启动桌面客户端：

```bash
npm run tauri -- dev
```

只构建前端：

```bash
npm run build
```

运行 Rust 测试：

```bash
cargo test -p csv_hash
```

## 本地打包

Linux：

```bash
npm run tauri -- build --bundles appimage,deb
```

Windows 推荐在 Windows runner 或 Windows 本机打包：

```powershell
npm run tauri -- build --bundles nsis
```

macOS：

```bash
npm run tauri -- build
```

Windows 包已配置 WebView2 离线安装模式，安装包会内置 WebView2 安装器，用户不需要手动安装 WebView2。

## 自动发布

仓库包含 GitHub Actions workflow：

```text
.github/workflows/release.yml
```

推送 `v*` tag 时会自动构建多平台包并发布到 GitHub Release。

示例：

```bash
git tag v0.1.0
git push origin v0.1.0
```

自动构建平台：

- Linux x64：AppImage、deb
- Windows x64：NSIS 安装包
- macOS x64
- macOS arm64

如果 Release 上传失败，请检查仓库设置：

```text
Settings -> Actions -> General -> Workflow permissions -> Read and write permissions
```

## 注意事项

- 加密方式是 SHA-256 哈希，不可逆。
- CSV 必须包含表头。
- 分隔符必须是单字节字符；制表符可在界面中选择，或自定义输入 `\t`。
- Linux AppImage 是 Linux 上最接近开箱即用的分发方式，但仍可能受目标系统基础库兼容性影响。
