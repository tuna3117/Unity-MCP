import importlib.util
import os

spec = importlib.util.spec_from_file_location("unity_tests", os.path.join(os.path.dirname(__file__), "unity_tests.py"))
ut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ut)

NAMES = ["Project.Tests.A.One", "Project.Tests.A.Two", "Project.Tests.B.Case(1)", "Project.Tests.B.Case(2)"]


def test_a_fixture_name_matches_its_tests():
    assert ut.unmatched(["Project.Tests.A"], NAMES) == []


def test_a_test_name_and_a_parameterised_case_match():
    assert ut.unmatched(["Project.Tests.A.One", "Project.Tests.B.Case"], NAMES) == []


def test_a_name_with_no_test_is_reported():
    assert ut.unmatched(["Project.Tests.A", "Project.Tests.Missing"], NAMES) == ["Project.Tests.Missing"]


def test_a_prefix_is_not_a_namespace_match():
    assert ut.unmatched(["Project.Tests.A.On"], NAMES) == ["Project.Tests.A.On"]


def test_every_test_must_pass():
    assert ut.all_passed({"total": 4, "passed": 4, "failed": 0})
    assert not ut.all_passed({"total": 4, "passed": 3, "failed": 0, "skipped": 1}), "skipped is not a pass"
    assert not ut.all_passed({"total": 4, "passed": 3, "failed": 0, "inconclusive": 1})
    assert not ut.all_passed({"total": 0, "passed": 0, "failed": 0}), "no tests is not a pass"
