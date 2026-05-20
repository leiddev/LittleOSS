# LittleOSS API 测试套件

本目录包含 LittleOSS Web API 的完整测试套件，包括功能测试、边界测试、性能测试、并发测试和压力测试。

## 目录结构

```
tests/
├── README.md           # 本文档
├── conftest.py         # Pytest 配置
├── test_api.py         # 综合测试用例
├── locustfile.py       # Locust 压力测试脚本
└── requirements.txt   # 测试依赖
```

## 快速开始

### 1. 安装依赖

```bash
cd tests
pip install -r requirements.txt
```

### 2. 启动 API 服务

确保 LittleOSS API 服务正在运行：

```bash
cd ../
dotnet run
```

服务默认运行在 `http://localhost:5048`。

### 3. 运行测试

#### 运行所有测试

```bash
pytest test_api.py -v
```

#### 生成 HTML 报告

```bash
pytest test_api.py --html=report.html --self-contained-html
```

#### 并行运行测试

```bash
pytest test_api.py -n auto
```

#### 按类别运行测试

```bash
# 只运行功能测试
pytest test_api.py -m functional -v

# 只运行并发测试
pytest test_api.py -m concurrency -v

# 只运行性能测试
pytest test_api.py -m performance -v

# 跳过压力测试
pytest test_api.py -m "not stress" -v
```

## 测试类别

### 功能测试 (TestBasicFunctionality)

| 测试用例 | 说明 |
|---------|------|
| `test_01_upload_file_success` | 测试成功上传文件 |
| `test_02_list_files_success` | 测试成功列出文件 |
| `test_03_download_file_success` | 测试成功下载文件（含 MD5 校验）|
| `test_04_delete_file_success` | 测试成功删除文件 |
| `test_05_get_quota_success` | 测试成功获取配额信息 |

### 边界测试 (TestEdgeCases)

| 测试用例 | 说明 |
|---------|------|
| `test_10_auth_invalid_credentials` | 无效凭证被拒绝 |
| `test_11_auth_missing_headers` | 缺少认证头被拒绝 |
| `test_12_invalid_region` | 无效区域被拒绝 |
| `test_13_empty_file_rejected` | 空文件被拒绝 |
| `test_14_nonexistent_file_not_found` | 不存在的文件返回 404 |
| `test_15_delete_nonexistent_file` | 删除不存在的文件返回 404 |
| `test_16_pagination_skip_take` | 分页参数测试 |
| `test_17_pagination_max_take_limit` | 分页最大限制 1000 |

### 性能测试 (TestPerformance)

| 测试用例 | 说明 |
|---------|------|
| `test_20_upload_performance_benchmark` | 上传性能基准（1KB/10KB/50KB）|
| `test_21_download_performance_benchmark` | 下载性能基准（1MB）|
| `test_22_list_files_performance` | 列表查询性能（P95 < 500ms）|

### 并发测试 (TestConcurrency)

| 测试用例 | 说明 |
|---------|------|
| `test_30_concurrent_uploads` | 20 线程并发上传，成功率 >= 95% |
| `test_31_concurrent_reads` | 50 线程并发读取同一文件 |
| `test_32_concurrent_upload_delete_race` | 上传与删除竞态条件测试 |
| `test_33_concurrent_list_consistency` | 并发操作时列表一致性测试 |

### 压力测试 (TestStressScenarios)

| 测试用例 | 说明 |
|---------|------|
| `test_40_sustained_load` | 持续负载测试（10 秒混合操作）|
| `test_41_burst_traffic` | 突发流量测试（50 个并发上传）|

## 压力测试 (Locust)

Locust 提供了更真实的分布式压力测试能力。

### 运行方式

#### 交互模式（推荐开发调试）

```bash
locust -f locustfile.py --host=http://localhost:5048
```

然后在浏览器打开 http://localhost:8089

#### 无 UI 模式（CI/CD）

```bash
# 运行 60 秒，模拟 100 用户，每秒增加 10 用户
locust -f locustfile.py --host=http://localhost:5048 \
    --headless -u 100 -r 10 -t 60s \
    --csv=results/stress_test
```

#### 分布式模式（高负载）

```bash
# 主节点
locust -f locustfile.py --host=http://localhost:5048 \
    --master --expect-workers 4

# 4 个 Worker 节点（每台机器）
locust -f locustfile.py --host=http://localhost:5048 --worker
```

### Locust 测试场景权重

| 操作 | 权重 | 说明 |
|------|------|------|
| 上传小文件 | 5 | 1-10KB 文件 |
| 列出文件 | 3 | 分页查询 |
| 下载文件 | 2 | 读取已上传文件 |
| 删除文件 | 1 | 清理测试数据 |
| 检查配额 | 1 | 查询使用情况 |

### 输出指标

- **RPS**: 每秒请求数
- **P50/P95/P99**: 响应时间百分位数
- **失败率**: 请求失败百分比
- **平均响应时间**: 所有请求的平均延迟

## 配置说明

### 修改 API 地址

在 `test_api.py` 开头修改配置：

```python
BASE_URL = "http://localhost:5048"  # 修改为你的 API 地址
```

### 修改测试区域

```python
TEST_REGION = "us-east-1"  # 修改为你的测试区域
```

### 修改认证凭证

```python
VALID_ACCESS_KEY_ID = "your-access-key-id"
VALID_SECRET_KEY = "your-secret-key"
```

### 修改 Locust 用户数

无 UI 模式下：

```bash
locust -f locustfile.py -u 500 -r 50 -t 120s  # 500 用户，每秒增加 50
```

## 常见问题

### Q: 测试失败显示 "Connection refused"

确保 API 服务已启动：

```bash
dotnet run
# 确认输出: Now listening on: http://localhost:5048
```

### Q: 配额相关测试失败

如果配额测试失败，可能是因为测试数据占用了配额。可以在 `appsettings.json` 中调整配额设置。

### Q: 并发测试不稳定

并发测试依赖时序，可能偶尔失败。可以增加重试次数：

```bash
pytest test_api.py::TestConcurrency -v --tb=short
```

### Q: 如何只运行快速测试？

```bash
pytest test_api.py -m "functional or edge" -v
```

## 测试覆盖率目标

| 类别 | 最低成功率 | 说明 |
|------|-----------|------|
| 功能测试 | 100% | 所有核心功能必须正常 |
| 边界测试 | 100% | 错误处理必须完善 |
| 性能测试 | 95% | 允许 5% 波动 |
| 并发测试 | 95% | 并发安全必须保证 |
| 压力测试 | 90% | 允许 10% 失败 |

## CI/CD 集成

### GitHub Actions 示例

```yaml
name: API Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'
      - uses: actions/setup-python@v5
        with:
          python-version: '3.11'
      - name: Start API
        run: dotnet run &
      - name: Install deps
        run: pip install -r tests/requirements.txt
      - name: Run tests
        run: pytest tests/test_api.py -v --junitxml=report.xml
```

## 报告生成

### HTML 报告

```bash
pytest test_api.py --html=report.html --self-contained-html
```

### JUnit XML 报告（CI/CD）

```bash
pytest test_api.py --junitxml=test-results.xml
```

### JSON 报告

```bash
pytest test_api.py --json-report --json-report-file=report.json
```
