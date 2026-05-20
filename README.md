# LittleOSS

轻量级对象存储服务（Lite Object Storage Service）。

## 功能特性

- 多区域存储支持
- 文件上传、下载、删除、列表查询
- 存储配额管理
- 基于 AccessKey 的认证授权
- 并发控制与区域锁

## 快速开始

### 环境要求

- .NET 9.0 SDK
- SQLite

### 配置

在 `appsettings.json` 中配置 AccessKey 和区域：

```json
{
  "Oss": {
    "Regions": [ "cn-east-1" ],
    "MaxFileSizeBytes": 104857600,
    "DefaultQuotaBytes": 10737418240,
    "StorageRoot": "./storage"
  },
  "AccessKeys": [
    {
      "KeyId": "oss-key-001",
      "SecretKey": "secret-xxx",
      "AllowedRegions": [ "cn-east-1" ]
    }
  ]
}
```

### 运行

```bash
dotnet run
```

服务启动后访问 `http://localhost:5048`。

## API 文档

所有 API 需通过请求头携带认证信息：

| 请求头 | 说明 |
|--------|------|
| `X-AccessKey-Id` | AccessKey ID |
| `X-AccessKey-Secret` | AccessKey 密钥 |

### 查询配额

```
GET /api/{region}/quota
```

响应示例：

```json
{
  "region": "cn-east-1",
  "usedBytes": 3093534,
  "quotaBytes": 10737418240,
  "usedPercentage": 0.0288107804954052
}
```

### 列出文件

```
GET /api/{region}/files?skip=0&take=100
```

响应示例：

```json
{
  "total": 5,
  "files": [
    {
      "fileId": "78bd4c8eb33542178924278edf7b3f27",
      "originalFileName": "test.tar.gz",
      "region": "cn-east-1",
      "fileSizeBytes": 3093534,
      "contentType": "application/gzip",
      "uploadTime": "2026-05-20T03:30:00Z"
    }
  ]
}
```

### 上传文件

```
PUT /api/{region}/files
Content-Type: multipart/form-data
```

响应示例：

```json
{
  "fileId": "78bd4c8eb33542178924278edf7b3f27",
  "originalFileName": "test.tar.gz",
  "region": "cn-east-1",
  "fileSizeBytes": 3093534,
  "contentType": "application/gzip",
  "uploadTime": "2026-05-20T03:30:00Z"
}
```

返回 `201 Created`，响应头 `Location` 指向下载链接。

### 下载文件

```
GET /api/{region}/files/{fileId}
```

### 删除文件

```
DELETE /api/{region}/files/{fileId}
```

成功删除返回 `204 No Content`。

## 错误码

| 状态码 | 错误码 | 说明 |
|--------|--------|------|
| 400 | `INVALID_REGION` | 区域无效 |
| 401 | - | 认证失败 |
| 403 | `ACCESS_DENIED` | 无权访问该区域 |
| 404 | `FILE_NOT_FOUND` | 文件不存在 |
| 413 | `FILE_TOO_LARGE` | 文件超过大小限制 |
| 413 | `QUOTA_EXCEEDED` | 存储配额不足 |
| 500 | `UPLOAD_FAILED` / `DELETE_FAILED` | 服务器内部错误 |

## 项目结构

```
LittleOSS/
├── Authentication/          # 认证处理
├── Controllers/            # API 控制器
├── Services/               # 业务服务层
├── Models/                 # 数据模型
├── Data/                   # 数据库上下文
├── Options/                # 配置选项
└── Program.cs              # 程序入口
```
