"""
LittleOSS Web API 综合测试套件
包含：功能测试、边界测试、并发测试、压力测试
"""

import io
import hashlib
import time
import statistics
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Optional

import pytest
import httpx


# ============================================================================
# 配置
# ============================================================================

BASE_URL = "http://localhost:5048"
TEST_REGION = "cn-east-1"
VALID_ACCESS_KEY_ID = "oss-key-001"
VALID_SECRET_KEY = "secret-xxx"
INVALID_ACCESS_KEY_ID = "invalid-key"
INVALID_SECRET_KEY = "invalid-secret"


# ============================================================================
# 辅助函数
# ============================================================================

def get_auth_headers(
    access_key_id: str = VALID_ACCESS_KEY_ID,
    secret_key: str = VALID_SECRET_KEY
) -> dict:
    """生成认证请求头"""
    return {
        "X-AccessKey-Id": access_key_id,
        "X-AccessKey-Secret": secret_key
    }


def create_test_file(
    content: bytes = b"Hello, OSS!",
    filename: str = "test.txt"
) -> tuple[io.BytesIO, str, str]:
    """创建测试文件，返回 (file_obj, filename, md5)"""
    file_obj = io.BytesIO(content)
    md5 = hashlib.md5(content).hexdigest()
    return file_obj, filename, md5


def create_large_file(size_mb: int = 1) -> tuple[io.BytesIO, str, int]:
    """创建指定大小的测试文件"""
    content = b"X" * (size_mb * 1024 * 1024)
    file_obj = io.BytesIO(content)
    return file_obj, f"large_file_{size_mb}mb.bin", len(content)


# ============================================================================
# Pytest Fixtures
# ============================================================================

@pytest.fixture(scope="session")
def base_url() -> str:
    """API 基础 URL"""
    return BASE_URL


@pytest.fixture(scope="session")
def test_region() -> str:
    """测试用区域名称"""
    return TEST_REGION


@pytest.fixture(scope="session")
def auth_headers() -> dict:
    """有效认证头"""
    return get_auth_headers()


# ============================================================================
# 功能测试
# ============================================================================

class TestBasicFunctionality:
    """基础功能测试"""

    @pytest.fixture(autouse=True)
    def setup(self, request: pytest.FixtureRequest):
        """每个测试前的设置"""
        self.client = httpx.Client(base_url=BASE_URL, timeout=30.0)
        self.created_files: list[str] = []
        yield
        # 清理：删除测试中创建的文件
        for file_id in self.created_files:
            try:
                self.client.delete(
                    f"/api/{TEST_REGION}/files/{file_id}",
                    headers=get_auth_headers()
                )
            except Exception:
                pass
        self.client.close()

    def test_01_upload_file_success(self):
        """测试：成功上传文件"""
        file_obj, filename, _ = create_test_file()
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        assert response.status_code == 201, f"Expected 201, got {response.status_code}: {response.text}"
        data = response.json()
        assert "fileId" in data
        assert data["originalFileName"] == filename
        assert data["region"] == TEST_REGION
        self.created_files.append(data["fileId"])

    def test_02_list_files_success(self):
        """测试：成功列出文件"""
        # 先上传一个文件
        file_obj, filename, _ = create_test_file(b"List test content", "list_test.txt")
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        assert response.status_code == 201
        file_id = response.json()["fileId"]
        self.created_files.append(file_id)

        # 列出文件
        response = self.client.get(
            f"/api/{TEST_REGION}/files",
            headers=get_auth_headers()
        )
        assert response.status_code == 200
        data = response.json()
        assert "total" in data
        assert "files" in data
        assert isinstance(data["files"], list)

    def test_03_download_file_success(self):
        """测试：成功下载文件"""
        content = b"Download test content"
        file_obj, filename, expected_md5 = create_test_file(content, "download_test.txt")

        # 上传
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        assert response.status_code == 201
        file_id = response.json()["fileId"]
        self.created_files.append(file_id)

        # 下载
        response = self.client.get(
            f"/api/{TEST_REGION}/files/{file_id}",
            headers=get_auth_headers()
        )
        assert response.status_code == 200
        downloaded_md5 = hashlib.md5(response.content).hexdigest()
        assert downloaded_md5 == expected_md5

    def test_04_delete_file_success(self):
        """测试：成功删除文件"""
        file_obj, filename, _ = create_test_file(b"Delete test", "delete_test.txt")

        # 上传
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        file_id = response.json()["fileId"]

        # 删除
        response = self.client.delete(
            f"/api/{TEST_REGION}/files/{file_id}",
            headers=get_auth_headers()
        )
        assert response.status_code == 204

        # 验证文件已被删除
        response = self.client.get(
            f"/api/{TEST_REGION}/files/{file_id}",
            headers=get_auth_headers()
        )
        assert response.status_code == 404

    def test_05_get_quota_success(self):
        """测试：成功获取配额信息"""
        response = self.client.get(
            f"/api/{TEST_REGION}/quota",
            headers=get_auth_headers()
        )
        assert response.status_code == 200
        data = response.json()
        assert "region" in data
        assert "usedBytes" in data
        assert "quotaBytes" in data
        assert "usedPercentage" in data
        assert data["region"] == TEST_REGION


# ============================================================================
# 边界测试
# ============================================================================

class TestEdgeCases:
    """边界条件和错误处理测试"""

    @pytest.fixture(autouse=True)
    def setup(self):
        self.client = httpx.Client(base_url=BASE_URL, timeout=30.0)
        self.created_files: list[str] = []
        yield
        for file_id in self.created_files:
            try:
                self.client.delete(
                    f"/api/{TEST_REGION}/files/{file_id}",
                    headers=get_auth_headers()
                )
            except Exception:
                pass
        self.client.close()

    def test_10_auth_invalid_credentials(self):
        """测试：无效凭证被拒绝"""
        file_obj, filename, _ = create_test_file()
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers(INVALID_ACCESS_KEY_ID, INVALID_SECRET_KEY)
        )
        assert response.status_code == 401

    def test_11_auth_missing_headers(self):
        """测试：缺少认证头被拒绝"""
        file_obj, filename, _ = create_test_file()
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")}
        )
        assert response.status_code == 401

    def test_12_invalid_region(self):
        """测试：无效区域被拒绝"""
        file_obj, filename, _ = create_test_file()
        response = self.client.put(
            "/api/invalid-region/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        assert response.status_code == 400
        assert "INVALID_REGION" in response.text

    def test_13_empty_file_rejected(self):
        """测试：空文件被拒绝"""
        file_obj, filename, _ = create_test_file(b"", "empty.txt")
        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "application/octet-stream")},
            headers=get_auth_headers()
        )
        assert response.status_code == 400
        assert "INVALID_FILE" in response.text

    def test_14_nonexistent_file_not_found(self):
        """测试：获取不存在的文件返回 404"""
        response = self.client.get(
            f"/api/{TEST_REGION}/files/nonexistent-file-id-12345",
            headers=get_auth_headers()
        )
        assert response.status_code == 404
        assert "FILE_NOT_FOUND" in response.text

    def test_15_delete_nonexistent_file(self):
        """测试：删除不存在的文件返回 404"""
        response = self.client.delete(
            f"/api/{TEST_REGION}/files/nonexistent-file-id-12345",
            headers=get_auth_headers()
        )
        assert response.status_code == 404

    def test_16_pagination_skip_take(self):
        """测试：分页参数 skip/take"""
        response = self.client.get(
            f"/api/{TEST_REGION}/files?skip=0&take=10",
            headers=get_auth_headers()
        )
        assert response.status_code == 200
        data = response.json()
        assert len(data["files"]) <= 10

    def test_17_pagination_max_take_limit(self):
        """测试：分页参数 take 最大限制 1000"""
        response = self.client.get(
            f"/api/{TEST_REGION}/files?take=5000",
            headers=get_auth_headers()
        )
        assert response.status_code == 200
        data = response.json()
        assert len(data["files"]) <= 1000


# ============================================================================
# 性能测试
# ============================================================================

class TestPerformance:
    """性能基准测试"""

    @pytest.fixture(autouse=True)
    def setup(self):
        self.client = httpx.Client(base_url=BASE_URL, timeout=60.0)
        self.created_files: list[str] = []
        yield
        for file_id in self.created_files:
            try:
                self.client.delete(
                    f"/api/{TEST_REGION}/files/{file_id}",
                    headers=get_auth_headers()
                )
            except Exception:
                pass
        self.client.close()

    def test_20_upload_performance_benchmark(self):
        """测试：上传性能基准"""
        sizes = [1, 10, 50]  # KB
        results = {}

        for size_kb in sizes:
            content = b"X" * (size_kb * 1024)
            file_obj, filename, _ = create_test_file(content, f"perf_{size_kb}kb.txt")

            start = time.perf_counter()
            response = self.client.put(
                f"/api/{TEST_REGION}/files",
                files={"file": (filename, file_obj, "application/octet-stream")},
                headers=get_auth_headers()
            )
            elapsed = time.perf_counter() - start

            if response.status_code == 201:
                file_id = response.json()["fileId"]
                self.created_files.append(file_id)
                throughput_mbps = (size_kb / 1024) / elapsed
                results[size_kb] = {"time_ms": elapsed * 1000, "throughput_mbps": throughput_mbps}
                print(f"  {size_kb}KB upload: {elapsed*1000:.2f}ms ({throughput_mbps:.2f} MB/s)")

        assert len(results) > 0, "No successful uploads for benchmark"

    def test_21_download_performance_benchmark(self):
        """测试：下载性能基准"""
        # 先上传一个大文件
        content = b"X" * (1024 * 1024)  # 1MB
        file_obj, filename, _ = create_test_file(content, "download_benchmark.bin")

        response = self.client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "application/octet-stream")},
            headers=get_auth_headers()
        )
        assert response.status_code == 201
        file_id = response.json()["fileId"]
        self.created_files.append(file_id)

        # 下载基准测试
        times = []
        for _ in range(5):
            start = time.perf_counter()
            response = self.client.get(
                f"/api/{TEST_REGION}/files/{file_id}",
                headers=get_auth_headers()
            )
            elapsed = time.perf_counter() - start
            times.append(elapsed * 1000)

        print(f"  Download 1MB avg: {statistics.mean(times):.2f}ms (min: {min(times):.2f}ms, max: {max(times):.2f}ms)")
        assert statistics.mean(times) < 1000, "Download too slow (>1s avg)"

    def test_22_list_files_performance(self):
        """测试：列表查询性能"""
        times = []
        for _ in range(10):
            start = time.perf_counter()
            response = self.client.get(
                f"/api/{TEST_REGION}/files?take=100",
                headers=get_auth_headers()
            )
            elapsed = time.perf_counter() - start
            times.append(elapsed * 1000)

        avg = statistics.mean(times)
        p95 = sorted(times)[int(len(times) * 0.95)]
        print(f"  List 100 files: avg={avg:.2f}ms, p95={p95:.2f}ms")
        assert avg < 500, f"List query too slow ({avg:.2f}ms avg)"


# ============================================================================
# 并发测试
# ============================================================================

class TestConcurrency:
    """并发安全测试"""

    @pytest.fixture(autouse=True)
    def setup(self):
        self.base_url = BASE_URL
        self.created_files: list[str] = []
        yield
        # 清理
        client = httpx.Client(timeout=30.0)
        for file_id in self.created_files:
            try:
                client.delete(
                    f"{self.base_url}/api/{TEST_REGION}/files/{file_id}",
                    headers=get_auth_headers()
                )
            except Exception:
                pass
        client.close()

    def _upload_worker(self, index: int) -> dict:
        """上传工作线程"""
        client = httpx.Client(base_url=self.base_url, timeout=30.0)
        content = f"Concurrent file {index}".encode()
        file_obj, filename, _ = create_test_file(content, f"concurrent_{index}.txt")

        try:
            response = client.put(
                f"/api/{TEST_REGION}/files",
                files={"file": (filename, file_obj, "text/plain")},
                headers=get_auth_headers()
            )
            result = {
                "index": index,
                "status_code": response.status_code,
                "success": response.status_code == 201,
                "file_id": response.json().get("fileId") if response.status_code == 201 else None
            }
            if result["file_id"]:
                self.created_files.append(result["file_id"])
            return result
        finally:
            client.close()

    def _delete_worker(self, file_id: str, index: int) -> dict:
        """删除工作线程"""
        client = httpx.Client(base_url=self.base_url, timeout=30.0)
        try:
            response = client.delete(
                f"/api/{TEST_REGION}/files/{file_id}",
                headers=get_auth_headers()
            )
            return {"index": index, "status_code": response.status_code, "success": response.status_code == 204}
        finally:
            client.close()

    def _read_worker(self, file_id: str, index: int) -> dict:
        """读取工作线程"""
        client = httpx.Client(base_url=self.base_url, timeout=30.0)
        try:
            start = time.perf_counter()
            response = client.get(
                f"/api/{TEST_REGION}/files/{file_id}",
                headers=get_auth_headers()
            )
            elapsed = time.perf_counter() - start
            return {
                "index": index,
                "status_code": response.status_code,
                "success": response.status_code == 200,
                "time_ms": elapsed * 1000
            }
        finally:
            client.close()

    def test_30_concurrent_uploads(self):
        """测试：多线程并发上传"""
        num_workers = 20

        with ThreadPoolExecutor(max_workers=num_workers) as executor:
            futures = [executor.submit(self._upload_worker, i) for i in range(num_workers)]
            results = [f.result() for f in as_completed(futures)]

        successes = sum(1 for r in results if r["success"])
        success_rate = successes / num_workers

        print(f"  Concurrent uploads: {successes}/{num_workers} ({success_rate*100:.1f}%)")
        assert success_rate >= 0.95, f"Too many upload failures ({success_rate*100:.1f}% < 95%)"

    def test_31_concurrent_reads(self):
        """测试：多线程并发读取同一文件"""
        # 先上传一个文件
        client = httpx.Client(base_url=self.base_url, timeout=30.0)
        file_obj, filename, _ = create_test_file(b"Shared content" * 1000, "shared.txt")
        response = client.put(
            f"/api/{TEST_REGION}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=get_auth_headers()
        )
        file_id = response.json()["fileId"]
        self.created_files.append(file_id)
        client.close()

        # 并发读取
        num_workers = 50
        with ThreadPoolExecutor(max_workers=num_workers) as executor:
            futures = [executor.submit(self._read_worker, file_id, i) for i in range(num_workers)]
            results = [f.result() for f in as_completed(futures)]

        successes = sum(1 for r in results if r["success"])
        avg_time = statistics.mean(r["time_ms"] for r in results)

        print(f"  Concurrent reads: {successes}/{num_workers}, avg={avg_time:.2f}ms")
        assert successes == num_workers, f"Some reads failed: {num_workers - successes} failures"

    def test_32_concurrent_upload_delete_race(self):
        """测试：上传与删除竞态"""
        num_operations = 30

        def mixed_worker(index: int) -> dict:
            client = httpx.Client(base_url=self.base_url, timeout=30.0)
            try:
                if index % 2 == 0:
                    # 偶数索引：上传
                    file_obj, filename, _ = create_test_file(b"Race test", f"race_{index}.txt")
                    response = client.put(
                        f"/api/{TEST_REGION}/files",
                        files={"file": (filename, file_obj, "text/plain")},
                        headers=get_auth_headers()
                    )
                    return {"op": "upload", "index": index, "status": response.status_code}
                else:
                    # 奇数索引：删除第一个上传的文件
                    if self.created_files:
                        file_id = self.created_files[0]
                        response = client.delete(
                            f"/api/{TEST_REGION}/files/{file_id}",
                            headers=get_auth_headers()
                        )
                        return {"op": "delete", "index": index, "status": response.status_code, "file_id": file_id}
                    return {"op": "delete", "index": index, "status": "skipped", "file_id": None}
            finally:
                client.close()

        with ThreadPoolExecutor(max_workers=15) as executor:
            futures = [executor.submit(mixed_worker, i) for i in range(num_operations)]
            results = [f.result() for f in as_completed(futures)]

        uploads = [r for r in results if r["op"] == "upload"]
        deletes = [r for r in results if r["op"] == "delete"]

        print(f"  Race condition test: {len(uploads)} uploads, {len(deletes)} deletes")
        unexpected = [r for r in results if r["status"] not in [201, 204, 404, "skipped"]]
        if unexpected:
            print(f"  Unexpected results: {unexpected}")
        assert all(r["status"] in [201, 204, 404, "skipped"] for r in results), "Unexpected status codes"

    def test_33_concurrent_list_consistency(self):
        """测试：并发操作时列表一致性"""
        # 预创建一些文件
        client = httpx.Client(base_url=self.base_url, timeout=30.0)

        for i in range(5):
            file_obj, filename, _ = create_test_file(b"Consistency test", f"consistency_{i}.txt")
            response = client.put(
                f"/api/{TEST_REGION}/files",
                files={"file": (filename, file_obj, "text/plain")},
                headers=get_auth_headers()
            )
            if response.status_code == 201:
                self.created_files.append(response.json()["fileId"])
        client.close()

        # 并发读取列表
        def list_worker() -> dict:
            client = httpx.Client(base_url=self.base_url, timeout=30.0)
            try:
                response = client.get(
                    f"/api/{TEST_REGION}/files",
                    headers=get_auth_headers()
                )
                return {"status": response.status_code, "total": response.json().get("total", -1)}
            finally:
                client.close()

        with ThreadPoolExecutor(max_workers=10) as executor:
            futures = [executor.submit(list_worker) for _ in range(20)]
            results = [f.result() for f in as_completed(futures)]

        totals = [r["total"] for r in results if r["status"] == 200]
        unique_totals = set(totals)

        print(f"  List consistency: observed totals = {unique_totals}")
        assert len(unique_totals) <= 2, "Inconsistent list counts detected"


# ============================================================================
# 压力测试场景
# ============================================================================

class TestStressScenarios:
    """压力测试场景"""

    def test_40_sustained_load(self):
        """测试：持续负载（模拟真实使用场景）"""
        import dataclasses

        @dataclasses.dataclass
        class Counters:
            request_count: int = 0
            success_count: int = 0
            lock: threading.Lock = dataclasses.field(default_factory=threading.Lock)

        duration_seconds = 10
        client = httpx.Client(base_url=BASE_URL, timeout=30.0)
        counters = Counters()
        created_files: list[str] = []
        upload_failures: list[str] = []

        start_time = time.perf_counter()

        def post_upload(file_id: str) -> None:
            """并行执行 List + Download + Delete"""
            results: dict[str, int] = {}
            lock = threading.Lock()

            def list_files():
                r = client.get(f"/api/{TEST_REGION}/files", headers=get_auth_headers())
                with lock:
                    results["list"] = r.status_code

            def download_file():
                r = client.get(f"/api/{TEST_REGION}/files/{file_id}", headers=get_auth_headers())
                with lock:
                    results["download"] = r.status_code

            def delete_file():
                r = client.delete(f"/api/{TEST_REGION}/files/{file_id}", headers=get_auth_headers())
                with lock:
                    results["delete"] = r.status_code

            with ThreadPoolExecutor(max_workers=3) as executor:
                futures = [
                    executor.submit(list_files),
                    executor.submit(download_file),
                    executor.submit(delete_file),
                ]
                for f in as_completed(futures):
                    f.result()

            with counters.lock:
                counters.request_count += 3
                if results.get("download") == 200:
                    counters.success_count += 1
                if results.get("delete") == 204:
                    counters.success_count += 1

        try:
            while time.perf_counter() - start_time < duration_seconds:
                file_obj, filename, _ = create_test_file(b"Sustained load test", "sustained.txt")

                try:
                    response = client.put(
                        f"/api/{TEST_REGION}/files",
                        files={"file": (filename, file_obj, "text/plain")},
                        headers=get_auth_headers()
                    )
                except httpx.HTTPError as e:
                    with counters.lock:
                        counters.request_count += 1
                    upload_failures.append(f"exc:{str(e)[:50]}")
                    continue

                with counters.lock:
                    counters.request_count += 1

                if response.status_code == 201:
                    file_id = response.json()["fileId"]
                    with counters.lock:
                        counters.success_count += 1
                    created_files.append(file_id)
                    post_upload(file_id)
                else:
                    upload_failures.append(f"{response.status_code}:{response.text[:80]}")

        finally:
            for file_id in created_files:
                try:
                    client.delete(
                        f"/api/{TEST_REGION}/files/{file_id}",
                        headers=get_auth_headers()
                    )
                except Exception:
                    pass
            client.close()

        elapsed = time.perf_counter() - start_time
        rps = counters.request_count / elapsed
        success_rate = counters.success_count / counters.request_count if counters.request_count > 0 else 0

        print(f"  Sustained load: {counters.request_count} requests in {elapsed:.1f}s ({rps:.1f} req/s), success={success_rate*100:.1f}%")
        if upload_failures:
            from collections import Counter as C2
            print(f"  Upload failures: {dict(C2(upload_failures))}")
        assert success_rate >= 0.90, f"Success rate too low: {success_rate*100:.1f}%"

    def test_41_burst_traffic(self):
        """测试：突发流量（模拟突然的高并发请求）"""
        client = httpx.Client(base_url=BASE_URL, timeout=30.0)
        created_files = []

        try:
            # 快速上传 50 个文件
            with ThreadPoolExecutor(max_workers=10) as executor:
                def burst_upload(i: int) -> bool:
                    file_obj, filename, _ = create_test_file(b"Burst", f"burst_{i}.txt")
                    response = client.put(
                        f"/api/{TEST_REGION}/files",
                        files={"file": (filename, file_obj, "text/plain")},
                        headers=get_auth_headers()
                    )
                    if response.status_code == 201:
                        created_files.append(response.json()["fileId"])
                        return True
                    return False

                futures = [executor.submit(burst_upload, i) for i in range(50)]
                results = [f.result() for f in as_completed(futures)]

            success_count = sum(1 for r in results if r)
            print(f"  Burst traffic: {success_count}/50 uploads succeeded")
            assert success_count >= 45, f"Burst handling poor: {success_count}/50"
        finally:
            for file_id in created_files:
                try:
                    client.delete(
                        f"/api/{TEST_REGION}/files/{file_id}",
                        headers=get_auth_headers()
                    )
                except Exception:
                    pass
            client.close()
