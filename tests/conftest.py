"""
Pytest 配置文件
"""

import pytest


def pytest_configure(config):
    """注册自定义标记"""
    config.addinivalue_line(
        "markers", "functional: 功能测试"
    )
    config.addinivalue_line(
        "markers", "edge: 边界测试"
    )
    config.addinivalue_line(
        "markers", "performance: 性能测试"
    )
    config.addinivalue_line(
        "markers", "concurrency: 并发测试"
    )
    config.addinivalue_line(
        "markers", "stress: 压力测试"
    )


def pytest_collection_modifyitems(config, items):
    """为测试用例自动添加标记"""
    for item in items:
        if "TestBasicFunctionality" in item.nodeid:
            item.add_marker(pytest.mark.functional)
        elif "TestEdgeCases" in item.nodeid:
            item.add_marker(pytest.mark.edge)
        elif "TestPerformance" in item.nodeid:
            item.add_marker(pytest.mark.performance)
        elif "TestConcurrency" in item.nodeid:
            item.add_marker(pytest.mark.concurrency)
        elif "TestStressScenarios" in item.nodeid:
            item.add_marker(pytest.mark.stress)
