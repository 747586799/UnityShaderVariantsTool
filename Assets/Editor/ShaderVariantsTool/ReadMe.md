# ShaderVariants 编辑器工具

## 功能概述

ShaderVariants 编辑器工具是供团队使用的 Unity Shader 变体自动收集工具，主要功能包括：

1. **自动收集上传** - 自动保存收集到的 Shader 变体并上传到 FTP 服务器
2. **一键下载合并** - 下载所有成员上传的变体文件，合并到当前使用的 ShaderVariantCollection
3. **可视化编辑** - 方便的 ShaderVariantCollection 文件编辑工具，可手动管理 Shader 变体

## 打开方式

菜单：`Custom Editor` → `Shader Variants Editor`

## 界面布局

工具采用左右分栏布局：

### 左侧配置面板

| 模块 | 功能 |
|------|------|
| ShaderVariants 文件 | 选择/加载要编辑的 .shadervariants 文件 |
| 统计 | 显示当前 Shader 和 Variant 数量 |
| 优化选项 | 配置自定义移除/保留规则 |
| Merge ShaderVariants | FTP 配置、上传下载操作 |
| 保存所有配置 | 保存配置到 JSON 文件 |

### 右侧 Shader 列表

| 功能 | 说明 |
|------|------|
| 搜索栏 | 按 Shader 名、路径、关键词过滤 |
| Shader 列表 | 显示所有 Shader 及其变体 |
| 批量操作 | 全选/清空/移除 Shader |
| 快捷操作 | 复制名称/路径、添加到移除规则 |
| 底部操作栏 | 刷新、保存更改、取消选择 |

## 使用流程

### 1. 编辑 ShaderVariants

1. 选择 ShaderVariants 文件路径，点击"加载"
2. 在右侧列表中选择要删除的变体
3. 点击"保存更改"应用修改

### 2. 配置优化规则

1. 在"优化选项"中添加：
   - **移除 Shader 名字**：包含指定名字的 Shader 会被标记删除
   - **移除路径**：路径包含指定字符串的 Shader 会被标记删除
   - **保留路径**：路径包含指定字符串的 Shader 不会被删除
2. 点击"应用优化规则"标记待删除的变体
3. 点击"保存更改"应用修改

### 3. 上传当前收集到的变体到 FTP

1. 配置 FTP 服务器、路径、用户名、密码
2. 勾选"启用自动上传"可在退出播放模式时自动检查上传
3. 点击"保存 ShaderVariants 并上传"手动上传

### 4. 从 FTP 下载合并

1. 点击"刷新文件列表"获取 FTP 上的文件
2. 选择临时下载路径
3. 点击"下载所有并合并"下载并合并所有文件
4. 合并结果保存到当前配置的 ShaderVariants 文件

## 配置文件

配置保存在 `Assets/Editor/ShaderVariants/ShaderVariantsEditorConfig.json`

```json
{
  "shaderVariantsPath": "Assets/BundleRes/Shader/ShaderVariants.shadervariants",
  "customRemoveShaderNames": [],
  "customRemovePaths": [],
  "customKeepPaths": [],
  "ftpServer": "ftp://192.168.1.16/",
  "ftpRemotePath": "ShaderVariants/DiggingPlanet/",
  "ftpUsername": "anonymous",
  "ftpPassword": "",
  "enableAutoUpload": false
}
```

## 核心类说明

| 类名 | 说明 |
|------|------|
| `ShaderVariantsEditorWindow` | 编辑器窗口 UI |
| `ShaderVariantsHelper` | 工具类，提供所有核心功能 |
| `ShaderVariantsEditorConfig` | 配置数据类 |
| `AutoUploadShaderVariantTool` | 自动上传工具（退出播放模式时触发） |
| `ShaderVariantDatas` | Shader 变体数据层，处理解析、合并、比对 |
| `ShaderVariantTimeHelper` | 时间工具类（本地化） |
| `ShaderVariantFileHelper` | 文件工具类（本地化） |
| `ShaderVariantListExtensions` | List 扩展方法（本地化） |

## 独立性说明

本工具已设计为独立模块，所有依赖的工具类都已内聚在 `ShaderVariantsEditor` 命名空间下：
- `ShaderVariantTimeHelper` - 时间戳计算
- `ShaderVariantFileHelper` - 文件操作
- `ShaderVariantListExtensions` - List 深拷贝扩展

不需要依赖项目中的其他工具类，可直接复制 `Assets/Editor/ShaderVariants/` 目录到其他 Unity 项目使用。

## 注意事项

1. 修改配置后需要点击"保存所有配置"才会持久化
2. 删除变体操作需要点击"保存更改"才生效
3. FTP 下载合并会删除临时文件，请确保重要数据已备份
4. 自动上传功能需要勾选"启用自动上传上传"才生效
