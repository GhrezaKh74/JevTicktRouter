#!/usr/bin/env python3
"""
Puts a circuit run on disk, then names it.

A run is the small part: an `adapter/` LoRA, a `head.pt` pointer readout, and a `config.json` that
names the base model. circuit's loader reads the config and pulls the base itself, into HF_HOME.
Both land in the same volume, so a container restart costs nothing and the download happens once.

Progress goes to stderr; the last line of stdout is the run directory, which the entrypoint passes
to the server as `S1_MODEL=lora:<dir>`.

    python fetch_weights.py                    # CIRCUIT_MODEL, or circuit-1.7b
    CIRCUIT_MODEL=circuit-8b python fetch_weights.py
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

# The text models, at the revisions circuit's own deployment pins. An unpinned repository would
# change the served model the moment new weights are pushed, which is not something a triage
# service should discover in production.
#
# The vision and audio runs are deliberately absent: this image installs no image or audio codecs,
# and the ticket questions are text. Point CIRCUIT_REPO_ID at one if you add those dependencies.
RUNS: dict[str, tuple[str, str]] = {
    "circuit-1.7b": ("jbarney/circuit-1.7b", "v1.2"),
    "circuit-8b": ("jbarney/circuit-8b", "v1.1"),
}


def log(message: str) -> None:
    print(message, file=sys.stderr, flush=True)


def resolve() -> tuple[str, str]:
    """The repository and revision to fetch, from the model name or from an explicit override."""
    repo_id = os.environ.get("CIRCUIT_REPO_ID", "").strip()
    if repo_id:
        # An escape hatch for a run this file does not know about: your own fine-tune, or a model
        # published after this image was built. Unpinned unless you say otherwise.
        return repo_id, os.environ.get("CIRCUIT_REVISION", "").strip() or "main"

    name = os.environ.get("CIRCUIT_MODEL", "circuit-1.7b").strip()
    if name not in RUNS:
        known = ", ".join(sorted(RUNS))
        raise SystemExit(
            f"CIRCUIT_MODEL={name!r} is not a run this image knows. Choose one of: {known}. "
            f"For anything else set CIRCUIT_REPO_ID (and CIRCUIT_REVISION) instead."
        )
    return RUNS[name]


def unreachable(exc: Exception) -> str:
    """What to tell someone whose container cannot reach the Hub."""
    endpoint = os.environ.get("HF_ENDPOINT", "https://huggingface.co")
    proxy = os.environ.get("HTTPS_PROXY") or os.environ.get("HTTP_PROXY") or ""
    # The wording depends on the resolver: glibc says "Temporary failure in name resolution"
    # (EAI_AGAIN) or "Name or service not known" (EAI_NONAME), musl and macOS differ again.
    message = str(exc).lower()
    dns_failed = any(
        clue in message
        for clue in (
            "name resolution",
            "name or service not known",
            "getaddrinfo",
            "nodename nor servname",
            "failed to resolve",
        )
    )

    lines = [
        "",
        f"Could not reach {endpoint} to download the weights, and none are cached yet.",
        "",
    ]

    if dns_failed:
        lines += [
            "The container could not resolve the name at all, which is almost always Docker's own",
            "DNS rather than this machine's connection. In order of likelihood:",
            "",
            "  1. Restart Docker Desktop. Its resolver stops forwarding after a VPN or network",
            "     change more often than it should.",
            "  2. Give the container a resolver directly:  CIRCUIT_DNS=1.1.1.1 docker compose",
            "     --profile circuit up",
            "  3. Set it for every container in Docker Desktop -> Settings -> Docker Engine:",
            '     {"dns": ["1.1.1.1", "8.8.8.8"]}',
        ]
    else:
        lines += [
            "The name resolved but the connection did not complete. If this network reaches the",
            "internet through a proxy, set HTTP_PROXY and HTTPS_PROXY; if the Hub itself is blocked",
            "here, set HF_ENDPOINT to a mirror.",
        ]

    lines += [
        "",
        "Or skip the container's network entirely: download the weights anywhere you like and",
        "point CIRCUIT_WEIGHTS at the folder holding them. tools/circuit/README.md has the two",
        "commands.",
        "",
        f"Underlying error: {type(exc).__name__}: {str(exc).splitlines()[0]}",
        "",
    ]

    if proxy:
        lines.insert(1, f"(a proxy is configured: {proxy})")

    return "\n".join(lines)


def complete(run_dir: Path) -> bool:
    """True when the run holds everything circuit's LoRA scorer reads: config, head, adapter."""
    adapter = run_dir / "adapter"
    return (
        (run_dir / "config.json").is_file()
        and (run_dir / "head.pt").is_file()
        and adapter.is_dir()
        and any(adapter.iterdir())
    )


def main() -> None:
    repo_id, revision = resolve()
    weights = Path(os.environ.get("CIRCUIT_WEIGHTS_DIR", "/weights"))
    run_dir = weights / "runs" / f"{repo_id.replace('/', '__')}@{revision}"

    # Re-downloading is skipped only when all three pieces the loader needs are present. A single
    # marker file would be fetched early and left behind by an interrupted download, and the server
    # would then fail on a half-written run instead of finishing the job. snapshot_download resumes
    # from whatever incomplete blobs it finds, so the retry is cheap.
    if complete(run_dir):
        log(f"run already present: {run_dir}")
    else:
        log(f"fetching {repo_id}@{revision} -> {run_dir}")
        import httpx
        from huggingface_hub import snapshot_download
        from huggingface_hub.errors import LocalEntryNotFoundError

        # LocalEntryNotFoundError is what the Hub client raises when it cannot reach the endpoint
        # and has nothing cached; httpx.TransportError is what escapes when a proxy or the DNS
        # fails before the client wraps it. Both mean the same thing here.
        try:
            snapshot_download(repo_id, revision=revision, local_dir=str(run_dir))
        except (LocalEntryNotFoundError, httpx.TransportError) as exc:
            # The raw traceback is forty lines of httpx internals ending in a DNS error, which
            # reads like a bug in this script rather than a container that cannot resolve a name.
            raise SystemExit(unreachable(exc)) from exc

        log("run fetched")

    # The base model is much larger than the run and is fetched by circuit's loader, not here, so
    # say which one and where it will go. Otherwise the first boot looks like it has hung.
    base = json.loads((run_dir / "config.json").read_text(encoding="utf-8")).get("base", "?")
    log(f"base model: {base} (cached in {os.environ.get('HF_HOME', '~/.cache/huggingface')})")

    print(run_dir)


if __name__ == "__main__":
    main()
