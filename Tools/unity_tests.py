#!/usr/bin/env python3
"""Compile, then run Unity tests through the MCP bridge; exit 0 only if every selected test passed.

Usage (project root):
  ~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Full.Name ...] [--no-compile]
Full.Name is a fixture or test full name (e.g. Project.Tests.FlowTimelineTests). Stops Play Mode first. Compile errors
(error CS...) fail the run (exit 2) before any test starts. Prints "<mode> passed/total" and every failure.
"""
import json
import os
import subprocess
import sys
import time
import functools

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UV = os.path.expanduser("~/.local/bin/uv")
UNITY_APP = "/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app"
print = functools.partial(print, flush=True)   # progress shows up even when the output is redirected


def call(tool, args, tries=6):
    """One MCP tool call; retries while the bridge is down (domain reload after a compile)."""
    last = ""
    for _ in range(tries):
        p = subprocess.run([UV, "run", "-q", "Tools/mcp_call.py", "call", tool, json.dumps(args)],
                           cwd=ROOT, capture_output=True, text=True, timeout=1500)
        text = p.stdout
        i = text.find("{")
        if i >= 0:
            try:
                return json.JSONDecoder().raw_decode(text[i:])[0]
            except json.JSONDecodeError:
                pass
        last = (text + p.stderr)[-800:]
        time.sleep(5)
    sys.exit(f"[unity_tests] {tool} failed: {last}")


def strings(node):
    if isinstance(node, str):
        yield node
    elif isinstance(node, dict):
        for v in node.values():
            yield from strings(v)
    elif isinstance(node, list):
        for v in node:
            yield from strings(v)


def find_key(node, key):
    if isinstance(node, dict):
        if key in node:
            return node[key]
        for v in node.values():
            found = find_key(v, key)
            if found is not None:
                return found
    elif isinstance(node, list):
        for v in node:
            found = find_key(v, key)
            if found is not None:
                return found
    return None


def wait_idle(timeout=240):
    """Waits until Unity is not compiling and the bridge is ready ("Refresh requested" returns before the compile ends)."""
    time.sleep(3)
    end = time.time() + timeout
    quiet = 0
    while time.time() < end:
        p = subprocess.run([UV, "run", "-q", "Tools/mcp_call.py", "read", "mcpforunity://editor/state"],
                           cwd=ROOT, capture_output=True, text=True, timeout=120)
        i = p.stdout.find("{")
        if i >= 0:
            try:
                state = json.JSONDecoder().raw_decode(p.stdout[i:])[0]
                # ready_for_tools also goes false on compile errors or a "stale_status" while Unity is unfocused.
                if find_key(state, "is_compiling") is False:
                    quiet += 1
                    if quiet >= 2:
                        return
                else:
                    quiet = 0
            except json.JSONDecodeError:
                pass
        time.sleep(3)
    print("[unity_tests] warning: editor still busy after the wait")


def compile_errors():
    call("refresh_unity", {"mode": "force", "scope": "all", "compile": "request", "wait_for_ready": True})
    wait_idle()
    reply = call("read_console", {"types": ["error"], "filter_text": "error CS"})
    return sorted({s.strip() for s in strings(reply) if "error CS" in s})


def main(argv):
    compile_first = "--no-compile" not in argv
    args = [a for a in argv if a != "--no-compile"]
    if not args or args[0] not in ("EditMode", "PlayMode"):
        sys.exit(__doc__)
    mode, names = args[0], args[1:]
    # Without focus the Test Runner may find 0 PlayMode tests and asset refreshes stall (seen 2026-09-29).
    subprocess.run(["open", "-a", UNITY_APP], check=False)
    call("manage_editor", {"action": "stop"})
    if compile_first:
        errs = compile_errors()
        if errs:
            print("[unity_tests] compile errors:")
            for e in errs[:20]:
                print("  " + e)
            return 2
    req = {"mode": mode}
    if names:
        req["test_names"] = names
    for attempt in range(2):
        data = run_job(req)
        if data.get("error"):
            print(f"[unity_tests] {mode}: {data['error']}")
            return 1
        result = data.get("result") or {}
        summary = result.get("summary")
        if summary is None:
            # A job with failures comes back without "result": the failures are in "progress".
            prog = data.get("progress") or {}
            fails = prog.get("failures_so_far") or []
            total = prog.get("total") or prog.get("completed") or len(fails)
            print(f"{mode} {max(0, total - len(fails))}/{total} passed (failed)")
            for f in fails:
                print(f"  FAIL {f.get('full_name')}: {(f.get('message') or '').strip()[:400]}")
            return 1
        if summary.get("total", 0) == 0 and attempt == 0:
            # Stale test list (seen after imports): refresh once and run again.
            call("refresh_unity", {"mode": "force", "scope": "all", "compile": "request", "wait_for_ready": True})
            wait_idle()
            continue
        break
    passed, failed = summary.get("passed", 0), summary.get("failed", 0)
    print(f"{mode} {passed}/{summary.get('total', 0)} passed ({summary.get('resultState', data.get('status'))})")
    for r in result.get("results") or []:
        if r.get("state") == "Passed":
            continue
        name = r.get("fullName") or r.get("name") or json.dumps(r)[:200]
        print(f"  FAIL {name}: {(r.get('message') or '').strip()[:400]}")
    return 0 if failed == 0 and passed > 0 else 1


def run_job(req):
    job = None
    for attempt in range(8):
        reply = call("run_tests", req)
        job = (reply.get("data") or {}).get("job_id")
        if job:
            break
        if attempt == 4:
            # A job left "running" after an unfocused/cancelled run blocks new ones until the next domain reload.
            print("[unity_tests] bridge still busy with an old job: requesting a script reload", flush=True)
            call("execute_code", {"action": "execute", "code": "UnityEditor.EditorUtility.RequestScriptReload(); return 1;"})
            wait_idle()
        time.sleep(10)
    if not job:
        sys.exit(f"[unity_tests] run_tests returned no job id: {reply}")
    data = {}
    for _ in range(40):
        data = call("get_test_job", {"job_id": job, "wait_timeout": 60, "include_failed_tests": True}).get("data") or {}
        if data.get("status") != "running":
            break
    return data


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
