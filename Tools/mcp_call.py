# /// script
# requires-python = ">=3.10"
# dependencies = ["mcp>=2.0"]
# ///
"""Minimal MCP client for the MCP for Unity HTTP server (mcp python SDK 2.x).

Usage (from the project root):
  uv run Tools/mcp_call.py list                      # list tools
  uv run Tools/mcp_call.py describe <tool>           # show a tool's input schema
  uv run Tools/mcp_call.py call <tool> '<json args>' # call a tool
  uv run Tools/mcp_call.py call <tool> @args.json    # args from a file
  uv run Tools/mcp_call.py read <uri>                # read a resource, e.g. mcpforunity://instances

Options:
  --url URL       MCP endpoint (default: http://localhost:8080/mcp, env UNITY_MCP_URL)
  --out DIR       where to save image results (default: ./Temp/mcp_out)
  --timeout SEC   per-call timeout in seconds (default: 300)
  --instance NAME  route call/read/list to the editor of this project folder (pins the session with set_active_instance; the instances resource and describe are global) (default: this repo's folder name, env UNITY_MCP_PROJECT; '-' = no routing)
"""
import argparse
import asyncio
import base64
import json
import os
import sys
import time

from mcp import Client


def _load_args(raw):
    if not raw:
        return {}
    if raw.startswith("@"):
        with open(raw[1:], "r", encoding="utf-8") as f:
            return json.load(f)
    return json.loads(raw)


def _print_text(text):
    try:
        print(json.dumps(json.loads(text), indent=2, ensure_ascii=False))
    except Exception:
        print(text)


def pick_instance(doc, name):
    """Name@hash of the connected editor whose project folder is `name`.

    The bridge serves every open Unity editor on one URL and needs `unity_instance` (Name@hash) on each tool call once
    two are connected. A call from this repo must never land in another project's editor, so a missing match is an
    error even when only one (other) editor is connected.
    """
    instances = doc.get("instances") or (doc.get("data") or {}).get("instances") or []
    for inst in instances:
        if inst.get("name") == name:
            if inst.get("id"):
                return inst["id"]
            if inst.get("hash"):
                return f"{inst['name']}@{inst['hash']}"
    names = [i.get("name") for i in instances]
    raise SystemExit(f"[mcp_call] no Unity editor for project '{name}' (connected: {names}); run Tools/unity_up.sh")


INSTANCES_URI = "mcpforunity://instances"


def routing_plan(cmd, uri, instance):
    """True when this command must first find this project's editor and pin the session to it.

    `describe` only lists the server's tool schemas (the same for every editor) and the instances resource is global,
    so neither is routed; `call`, `read` and `list` are, unless routing is off (instance '-').
    """
    if not instance or instance == "-":
        return False
    if cmd == "describe":
        return False
    if cmd == "read" and uri == INSTANCES_URI:
        return False
    return cmd in ("call", "read", "list")


async def _pin(client, instance):
    """Looks up this project's editor and pins the session to it (set_active_instance); returns its Name@hash."""
    listing = await client.read_resource(INSTANCES_URI)
    text = next((getattr(b, "text", None) for b in (getattr(listing, "contents", None) or []) if getattr(b, "text", None)), "{}")
    inst = pick_instance(json.loads(text), instance)
    res = await client.call_tool("set_active_instance", {"instance": inst})
    if getattr(res, "is_error", None) or getattr(res, "isError", False):
        raise SystemExit(f"[mcp_call] set_active_instance failed for {inst}: {[getattr(b, 'text', b) for b in res.content or []]}")
    return inst


async def _run(ns):
    async with Client(ns.url, read_timeout_seconds=ns.timeout) as client:
        pinned = None
        if routing_plan(ns.cmd, getattr(ns, "uri", None), ns.instance):
            pinned = await _pin(client, ns.instance)
        if ns.cmd == "list":
            res = await client.list_tools()
            tools = getattr(res, "tools", res)
            for t in tools:
                desc = (t.description or "").strip().splitlines()
                print(f"{t.name}: {desc[0] if desc else ''}")
            print(f"\n{len(tools)} tools")
            return 0

        if ns.cmd == "describe":
            res = await client.list_tools()
            tools = getattr(res, "tools", res)
            for t in tools:
                if t.name == ns.tool:
                    print(t.description or "")
                    schema = getattr(t, "input_schema", None) or getattr(t, "inputSchema", None)
                    print(json.dumps(schema, indent=2, default=str))
                    return 0
            print(f"tool not found: {ns.tool}", file=sys.stderr)
            return 2

        if ns.cmd == "read":
            res = await client.read_resource(ns.uri)
            for block in getattr(res, "contents", None) or []:
                text = getattr(block, "text", None)
                if text is not None:
                    _print_text(text)
                else:
                    print(repr(block))
            return 0

        args = _load_args(ns.args)
        if pinned and "unity_instance" not in args:
            args["unity_instance"] = pinned
        result = await client.call_tool(ns.tool, args, read_timeout_seconds=ns.timeout)
        os.makedirs(ns.out, exist_ok=True)
        had_text = False
        for i, block in enumerate(result.content or []):
            btype = getattr(block, "type", "")
            if btype == "text":
                had_text = True
                _print_text(block.text)
            elif btype == "image":
                mime = getattr(block, "mime_type", None) or getattr(block, "mimeType", None) or ""
                ext = "png" if "png" in mime else "jpg"
                path = os.path.join(ns.out, f"{ns.tool}_{int(time.time())}_{i}.{ext}")
                with open(path, "wb") as f:
                    f.write(base64.b64decode(block.data))
                print(f"[image saved] {os.path.abspath(path)}")
            else:
                print(repr(block))
        sc = getattr(result, "structured_content", None) or getattr(result, "structuredContent", None)
        if sc and not had_text:
            print(json.dumps(sc, indent=2, ensure_ascii=False))
        if getattr(result, "is_error", None) or getattr(result, "isError", False):
            print("[tool returned isError=true]", file=sys.stderr)
            return 1
        return 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--url", default=os.environ.get("UNITY_MCP_URL", "http://localhost:8080/mcp"))
    p.add_argument("--out", default="Temp/mcp_out")
    p.add_argument("--timeout", type=float, default=300)
    default_instance = os.environ.get("UNITY_MCP_PROJECT") or os.path.basename(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    p.add_argument("--instance", default=default_instance)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    d = sub.add_parser("describe")
    d.add_argument("tool")
    c = sub.add_parser("call")
    c.add_argument("tool")
    c.add_argument("args", nargs="?", default=None)
    r = sub.add_parser("read")
    r.add_argument("uri")
    ns = p.parse_args()
    try:
        return asyncio.run(_run(ns))
    except BaseException as e:  # noqa: BLE001 - unwrap ExceptionGroups for readable errors
        errs = []
        stack = [e]
        while stack:
            cur = stack.pop()
            subs = getattr(cur, "exceptions", None)
            if subs:
                stack.extend(subs)
            else:
                errs.append(f"{type(cur).__name__}: {cur}")
        print("[mcp_call error] " + " | ".join(errs), file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
