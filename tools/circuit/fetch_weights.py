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
        from huggingface_hub import snapshot_download

        snapshot_download(repo_id, revision=revision, local_dir=str(run_dir))
        log("run fetched")

    # The base model is much larger than the run and is fetched by circuit's loader, not here, so
    # say which one and where it will go. Otherwise the first boot looks like it has hung.
    base = json.loads((run_dir / "config.json").read_text(encoding="utf-8")).get("base", "?")
    log(f"base model: {base} (cached in {os.environ.get('HF_HOME', '~/.cache/huggingface')})")

    print(run_dir)


if __name__ == "__main__":
    main()
