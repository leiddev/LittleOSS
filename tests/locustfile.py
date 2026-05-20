"""
Locust 分布式压力测试脚本
使用方式: locust -f tests/locustfile.py --host=http://localhost:5048
"""

from locust import HttpUser, task, between, events
import random
import io


class OSSUser(HttpUser):
    """模拟普通用户行为"""
    wait_time = between(0.5, 2.0)

    def on_start(self):
        """用户开始时的初始化"""
        self.headers = {
            "X-AccessKey-Id": "oss-key-001",
            "X-AccessKey-Secret": "secret-xxx"
        }
        self.uploaded_files = []
        self.region = "cn-east-1"

    @task(5)
    def upload_small_file(self):
        """上传小文件（频率最高）"""
        content = b"X" * random.randint(1024, 10240)  # 1-10KB
        file_obj = io.BytesIO(content)
        filename = f"locust_upload_{random.randint(1000, 9999)}.txt"

        with self.client.put(
            f"/api/{self.region}/files",
            files={"file": (filename, file_obj, "text/plain")},
            headers=self.headers,
            catch_response=True
        ) as response:
            if response.status_code == 201:
                file_id = response.json().get("fileId")
                if file_id:
                    self.uploaded_files.append(file_id)
                    response.success()
            elif response.status_code == 413:
                response.success()  # 配额超限也视为正常
            else:
                response.failure(f"Unexpected status: {response.status_code}")

    @task(3)
    def list_files(self):
        """列出文件"""
        self.client.get(
            f"/api/{self.region}/files?take=20",
            headers=self.headers,
            name="/api/[region]/files"
        )

    @task(2)
    def download_file(self):
        """下载文件"""
        if not self.uploaded_files:
            return

        file_id = random.choice(self.uploaded_files)
        self.client.get(
            f"/api/{self.region}/files/{file_id}",
            headers=self.headers,
            name="/api/[region]/files/[fileId]"
        )

    @task(1)
    def delete_file(self):
        """删除文件"""
        if not self.uploaded_files:
            return

        file_id = self.uploaded_files.pop(random.randint(0, len(self.uploaded_files) - 1))
        self.client.delete(
            f"/api/{self.region}/files/{file_id}",
            headers=self.headers,
            name="/api/[region]/files/[fileId]"
        )

    @task(1)
    def check_quota(self):
        """检查配额"""
        self.client.get(
            f"/api/{self.region}/quota",
            headers=self.headers,
            name="/api/[region]/quota"
        )

    def on_stop(self):
        """用户结束时清理"""
        for file_id in self.uploaded_files:
            try:
                self.client.delete(
                    f"/api/{self.region}/files/{file_id}",
                    headers=self.headers
                )
            except Exception:
                pass


# ============================================================================
# 统计事件处理器
# ============================================================================

@events.test_stop.add_listener
def on_test_stop(environment, **kwargs):
    """测试结束时打印统计"""
    stats = environment.stats
    print("\n" + "=" * 60)
    print("压力测试统计报告")
    print("=" * 60)
    print(f"总请求数: {stats.total.num_requests}")
    print(f"失败请求: {stats.total.num_failures}")
    print(f"平均响应时间: {stats.total.avg_response_time:.2f}ms")
    print(f"P50 响应时间: {stats.total.get_response_time_percentile(0.5):.2f}ms")
    print(f"P95 响应时间: {stats.total.get_response_time_percentile(0.95):.2f}ms")
    print(f"P99 响应时间: {stats.total.get_response_time_percentile(0.99):.2f}ms")
    print(f"每秒请求数(RPS): {stats.total.total_rps:.2f}")
    print("=" * 60)
