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

Options:
  --url URL       MCP endpoint (default: http://localhost:8080/mcp, env UNITY_MCP_URL)
  --out DIR       where to save image results (default: ./Temp/mcp_out)
  --timeout SEC   per-call timeout in seconds (default: 300)
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


async def _run(ns):
    async with Client(ns.url, read_timeout_seconds=ns.timeout) as client:
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

        result = await client.call_tool(ns.tool, _load_args(ns.args), read_timeout_seconds=ns.timeout)
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
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    d = sub.add_parser("describe")
    d.add_argument("tool")
    c = sub.add_parser("call")
    c.add_argument("tool")
    c.add_argument("args", nargs="?", default=None)
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
