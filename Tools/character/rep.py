#!/usr/bin/env python3
"""Minimal Replicate runner for the character pipeline (plain HTTP, hard timeouts).

Usage:
  rep.py run <owner/name[:version]> <input.json> [--out DIR] [--name NAME]
Input JSON values that start with "@" are local files: uploaded via the Files API and replaced by URLs.
Every output URL is downloaded into --out; <name>.koken.json (provenance) is written next to it.
Token: REPLICATE_API_TOKEN env or ~/projects/copy/.env.
"""
import hashlib, json, os, re, subprocess, sys, time, traceback, urllib.request
from pathlib import Path

API = "https://api.replicate.com/v1"

def load_env():
    if os.environ.get("REPLICATE_API_TOKEN"):
        return
    env = Path.home() / "projects/copy/.env"
    if env.exists():
        for line in env.read_text().splitlines():
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip().strip('"\''))

def tok():
    return os.environ["REPLICATE_API_TOKEN"]

def http(method, url, body=None, timeout=60):
    req = urllib.request.Request(url, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Authorization": f"Bearer {tok()}", "Content-Type": "application/json", "User-Agent": "curl/8.0"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        print("HTTP", e.code, e.read().decode()[:2000], file=sys.stderr, flush=True)
        raise

def mask(text):
    return re.sub(r"r8_[A-Za-z0-9]+", "r8_***", text)


def install_masking():
    """Uncaught errors print through here with any Replicate token masked."""
    def hook(kind, value, tb):
        sys.stderr.write(mask("".join(traceback.format_exception(kind, value, tb))))
    sys.excepthook = hook


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def upload(p):
    # the header goes in on stdin (-H @-): a token in argv shows up in ps and in CalledProcessError messages
    out = subprocess.run(["curl", "-s", "--max-time", "120", "-X", "POST", f"{API}/files",
                          "-H", "@-", "-F", f"content=@{p}"],
                         input=f"Authorization: Bearer {tok()}\n", capture_output=True, text=True, check=True).stdout
    d = json.loads(out)
    url = d["urls"]["get"]
    print(f"uploaded {Path(p).name} -> {url}", file=sys.stderr, flush=True)
    return url

def main():
    load_env()
    model, inp_path = sys.argv[2], sys.argv[3]
    args = sys.argv[4:]
    out = Path(args[args.index("--out") + 1]) if "--out" in args else Path("out")
    name = args[args.index("--name") + 1] if "--name" in args else Path(inp_path).stem
    out.mkdir(parents=True, exist_ok=True)
    inp = json.loads(Path(inp_path).read_text())
    files = {}
    def prep(v):
        if isinstance(v, str) and v.startswith("@"):
            files[v[1:]] = sha(v[1:]); return upload(v[1:])
        if isinstance(v, list):
            return [prep(x) for x in v]
        return v
    inp = {k: prep(v) for k, v in inp.items()}
    t0 = time.time()
    print(f"running {model} ...", file=sys.stderr, flush=True)
    if ":" in model:
        pred = http("POST", f"{API}/predictions", {"version": model.split(":")[1], "input": inp})
    else:
        pred = http("POST", f"{API}/models/{model}/predictions", {"input": inp})
    last = None
    while pred["status"] not in ("succeeded", "failed", "canceled"):
        time.sleep(5)
        try:
            pred = http("GET", pred["urls"]["get"], timeout=30)
        except Exception as e:
            print(f"  poll error {e}", file=sys.stderr, flush=True); continue
        if pred["status"] != last:
            print(f"  {pred['status']} ({time.time()-t0:.0f}s)", file=sys.stderr, flush=True); last = pred["status"]
    if pred["status"] != "succeeded":
        print("FAILED:", pred.get("error"), file=sys.stderr)
        print((pred.get("logs") or "")[-3000:], file=sys.stderr)
        sys.exit(1)
    written = {}
    def grab(key, url):
        if not (isinstance(url, str) and url.startswith("http")):
            return
        ext = os.path.splitext(url.split("?")[0])[1] or ".bin"
        dest = out / (f"{name}{ext}" if key == "" else f"{name}.{key}{ext}")
        subprocess.run(["curl", "-sL", "--max-time", "300", "-o", str(dest), url], check=True)
        written[key or "output"] = str(dest)
        print(f"  {key or 'output'} -> {dest} ({dest.stat().st_size//1024} KB)", file=sys.stderr, flush=True)
    o = pred.get("output")
    if isinstance(o, str): grab("", o)
    elif isinstance(o, list):
        for i, u in enumerate(o): grab(str(i) if len(o) > 1 else "", u)
    elif isinstance(o, dict):
        for k, u in o.items(): grab(k, u)
    prov = {"name": name, "model": model, "prediction": pred["id"], "input": inp, "input_files": files,
            "outputs": written, "seconds": round(time.time() - t0, 1), "metrics": pred.get("metrics"),
            "time": time.strftime("%Y-%m-%dT%H:%M:%S")}
    (out / f"{name}.koken.json").write_text(json.dumps(prov, indent=2))
    print(json.dumps(written))

if __name__ == "__main__":
    install_masking()
    main()
