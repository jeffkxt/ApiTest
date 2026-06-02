# Api Test

> 极致轻量的 Windows 桌面 API 测试工具 — 单文件 87KB，双击秒开，零依赖

![](https://img.shields.io/badge/version-1.0-blue) ![](https://img.shields.io/badge/.NET-4.8-purple) ![](https://img.shields.io/badge/size-87KB-green) ![](https://img.shields.io/badge/dependencies-0-brightgreen)

## 为什么又造一个轮子？

Postman 越来越大（安装包 400MB+），Insomnia 要登录，Bruno 要 Node.js... 我就想要一个**双击就能用、不需要安装任何东西、体积不到 100KB** 的接口调试工具。

## 特性

- **零依赖** — 无 NuGet、无 Node.js、无 Runtime，Windows 10/11 自带 .NET 4.8 直接运行
- **单文件 87KB** — 一个 exe，拷贝到任何电脑都能用
- **双击秒开** — 冷启动 < 100ms
- **支持所有 HTTP 方法** — GET / POST / PUT / DELETE / PATCH
- **自定义 Headers** — 纯文本编辑，每行 `Key: Value`
- **Body 多模式** — none / JSON / form-data(支持文件上传) / x-www-form-urlencoded / binary
- **流式响应** — 大响应不卡 UI，自动 JSON 格式化
- **环境变量** — 多套环境切换，`{{baseUrl}}` 自动替换
- **Basic Auth** — 一键设置用户名密码
- **超时控制** — 可编辑超时秒数，0 = 无限等待
- **文件响应** — 自动识别二进制文件，弹出保存对话框
- **历史记录** — 自动保存每次请求+响应，最多 100 条，去重
- **收藏夹** — 分组管理常用接口，每个接口独立记忆最后一次的入参和出参
- **Swagger 导入** — 支持 OpenAPI 2.0 / 3.0，本地文件或 URL，智能合并重复接口
- **Postman 环境导入** — 导入 Postman 环境 JSON
- **左右分栏可拖动** — 面板宽度自由调整
- **配色现代** — Indigo 主题，GET/POST/PUT/DELETE/PATCH 颜色区分

## 截图

```
┌──────────────────────────────────────────────────────────────────┐
│ Api Test v1.0                                          — □ ✕    │
├──────────────────────────────────────────────────────────────────┤
│ 导入 ▼  设置 ▼                             存储: D:\...\setting │
├──────────────────────────────────────────────────────────────────┤
│ 环境: [开发环境 ▼] [管理]  超时(s): [30]  [Auth]                │
├──────────────┬───────────────────────────────────────────────────┤
│ [历史|收藏]   │ [GET ▼] [{{baseUrl}}/users/1              ] [发送]│
│              │                                                   │
│ ● 示例分组   │ Headers                                           │
│  ├ GET - 获取│ User-Agent: ApiTest/1.0                            │
│  │   用户列表 │ Accept: application/json                          │
│  ├ POST -    │                                                   │
│    创建用户  │ Body [JSON ▼] [格式化]                             │
│              │ { "name": "test" }                                 │
│              │                                                   │
│              │ Response [格式化] [保存]                            │
│              │ ● 200 OK | 45ms | 234B                             │
│              │ { "id": 1, "name": "test" }                        │
├──────────────┴───────────────────────────────────────────────────┤
│ ● 就绪                                                           │
└──────────────────────────────────────────────────────────────────┘
```

## 快速开始

1. 下载 `UltraLightApiTester.exe`
2. 双击运行
3. 输入 URL，点击发送

首次运行自动在 exe 同目录创建 `setting/` 文件夹存储数据。

## 编译

```bash
# 需要 .NET SDK 或 Visual Studio（提供 Roslyn 编译器）
cd UltraLightApiTester
build.bat
```

无 Visual Studio？直接用 csc.exe：

```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe /out:ApiTest.exe ^
  /reference:"System.dll" ^
  /reference:"System.Windows.Forms.dll" ^
  /reference:"System.Drawing.dll" ^
  /reference:"System.Net.Http.dll" ^
  /reference:"System.Core.dll" ^
  /reference:"System.Web.Extensions.dll" ^
  /optimize+ /debug- ^
  Program.cs Models\*.cs Services\*.cs UI\*.cs
```

## 项目结构

```
UltraLightApiTester/
├── Program.cs                  # 入口
├── Models/
│   ├── SavedRequest.cs         # 请求/响应数据模型
│   ├── SwaggerModels.cs        # Swagger 解析模型
│   ├── EnvironmentConfig.cs    # 环境配置
│   └── SettingsConfig.cs       # 设置
├── Services/
│   ├── HttpSingleton.cs        # HttpClient 单例
│   ├── JsonStore.cs            # 零依赖 JSON 序列化
│   ├── HistoryService.cs       # 历史管理
│   ├── FavoriteService.cs      # 收藏管理
│   ├── EnvironmentService.cs   # 环境变量
│   ├── SettingsService.cs      # 设置管理
│   └── SwaggerParser.cs        # OpenAPI 解析
└── UI/
    ├── MainForm.cs             # 主窗口（手工布局，无设计器）
    ├── SwaggerImportDialog.cs  # Swagger 导入对话框
    └── EnvironmentDialog.cs    # 环境编辑对话框
```

## 技术栈

| 组件 | 选择 | 原因 |
|------|------|------|
| 运行时 | .NET Framework 4.8 | Windows 10/11 自带 |
| UI | WinForms (纯代码) | 最轻，无设计器，启动最快 |
| HTTP | `System.Net.Http.HttpClient` | 单例 + 连接池 |
| JSON | 手写解析器 | 0 依赖 |
| 存储 | JSON 文件 | 简单可读 |

## 许可

MIT
