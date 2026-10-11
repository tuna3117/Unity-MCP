"""pick_instance: tool calls from this repo must reach this repo's Unity editor, or fail loudly."""
import importlib.util
import os

import pytest

_spec = importlib.util.spec_from_file_location("mcp_call", os.path.join(os.path.dirname(__file__), "mcp_call.py"))
mcp_call = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mcp_call)


def doc(*instances):
    return {"instances": list(instances)}


def test_picks_the_named_editor_among_several():
    d = doc({"name": "Unity MCP", "hash": "aaa111", "id": "Unity MCP@aaa111"},
            {"name": "hugi-smash-and-stop", "hash": "bbb222", "id": "hugi-smash-and-stop@bbb222"})
    assert mcp_call.pick_instance(d, "hugi-smash-and-stop") == "hugi-smash-and-stop@bbb222"


def test_builds_the_id_from_name_and_hash_when_id_is_missing():
    d = doc({"name": "hugi-smash-and-stop", "hash": "bbb222"})
    assert mcp_call.pick_instance(d, "hugi-smash-and-stop") == "hugi-smash-and-stop@bbb222"


def test_accepts_the_nested_data_shape():
    d = {"data": {"instances": [{"name": "hugi-smash-and-stop", "id": "hugi-smash-and-stop@c"}]}}
    assert mcp_call.pick_instance(d, "hugi-smash-and-stop") == "hugi-smash-and-stop@c"


def test_a_lone_editor_of_another_project_is_an_error_not_a_silent_reroute():
    d = doc({"name": "Unity MCP", "hash": "aaa111", "id": "Unity MCP@aaa111"})
    with pytest.raises(SystemExit) as e:
        mcp_call.pick_instance(d, "hugi-smash-and-stop")
    assert "unity_up.sh" in str(e.value)


def test_no_editor_is_an_error():
    with pytest.raises(SystemExit):
        mcp_call.pick_instance(doc(), "hugi-smash-and-stop")


@pytest.mark.parametrize("cmd,uri,inst,expected", [
    ("call", None, "p", True),
    ("read", "mcpforunity://editor/state", "p", True),
    ("list", None, "p", True),
    ("read", "mcpforunity://instances", "p", False),
    ("describe", None, "p", False),
    ("call", None, "-", False),
    ("read", "mcpforunity://editor/state", "-", False),
])
def test_routing_plan(cmd, uri, inst, expected):
    assert mcp_call.routing_plan(cmd, uri, inst) is expected
