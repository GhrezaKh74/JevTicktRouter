#!/usr/bin/env python3
"""
A stand-in for circuit's s1proto server, for testing the SelfHosted provider without a GPU.

The real models (https://github.com/Barneyjm/circuit) need weights and a graphics card. This
serves the same `POST /v1/systemone` contract with deterministic answers, so the whole path --
the question set, the answer mapper, the rule engine, the provider badge -- can be exercised on
any machine.

It is a TEST FIXTURE, not a model. The answers are keyword heuristics, not judgements, and it
must never be pointed at by anything real.

Built from circuit's own published schema:
    NoulAnswer  {type, noul}                                        -- no confidence, by design
    ChoiceAnswer{type, choice, probabilities, confidence}
    ScoreAnswer {type, score, legend, probabilities, confidence}
    response    {model, answers, usage, request_id}

Confidence uses circuit's formula, 1 - H(p)/log(N).

Only the Python standard library is used, so there is nothing to install.

    python circuit_stub.py                 # serves on http://localhost:8901
    python circuit_stub.py --port 9000     # or elsewhere

Then, from the repository root:

    AI_PROVIDER=SelfHosted SELF_HOSTED_BASE_URL=http://localhost:8901 \\
      dotnet run --project backend/JevTicketRouter.Api
"""

from __future__ import annotations

import argparse
import json
import math
import uuid
from http.server import BaseHTTPRequestHandler, HTTPServer

# Words that push a ticket towards a given option. Deliberately crude: this exists to make the
# answers vary sensibly with the input, not to classify anything well.
HINTS: dict[str, tuple[str, ...]] = {
    "SecurityConcern": ("phish", "suspicious", "fraud", "malware", "breach", "فیشینگ", "مشکوک"),
    "AccessRequest": ("access", "permission", "account", "role", "password", "دسترسی", "رمز"),
    "TechnicalIssue": ("error", "fail", "broken", "slow", "crash", "خطا", "کار نمی"),
    "Security": ("phish", "suspicious", "fraud", "malware", "breach", "فیشینگ", "مشکوک"),
    "IdentityAccess": ("access", "permission", "account", "role", "password", "دسترسی", "رمز"),
    "ApplicationSupport": ("error", "fail", "broken", "slow", "crash", "خطا", "کار نمی"),
    "Infrastructure": ("printer", "network", "server", "vpn", "شبکه", "سرور", "پرینتر"),
}


def confidence(probabilities: list[float]) -> float:
    """circuit's own measure: 1 - H(p)/log(N). 1.0 on one option, 0.0 when uniform."""
    n = len(probabilities)
    if n < 2:
        return 1.0
    h = -sum(p * math.log(p) for p in probabilities if p > 0.0)
    return min(1.0, max(0.0, 1.0 - h / math.log(n)))


def flatten(state: object) -> str:
    """Collapses the state object into one lower-cased string for matching."""
    if isinstance(state, str):
        return state.lower()
    if isinstance(state, dict):
        return " ".join(flatten(v) for v in state.values())
    if isinstance(state, list):
        return " ".join(flatten(v) for v in state)
    return ""


def pick(options: list[str], text: str) -> tuple[str, int]:
    """The option with the most keyword hits, and how many it got."""
    best, best_hits = options[0], 0
    for option in options:
        hits = sum(1 for word in HINTS.get(option, ()) if word in text)
        if hits > best_hits:
            best, best_hits = option, hits
    return best, best_hits


def answer(question: dict, text: str) -> dict:
    kind = question.get("type")

    if kind == "noul":
        instructions = str(question.get("instructions", "")).lower()
        if "sensitive" in instructions:
            hit = any(w in text for w in ("card number", "national id", "password is", "کد ملی"))
            return {"type": "noul", "noul": 0.93 if hit else 0.04}
        hit = any(w in text for w in HINTS["SecurityConcern"])
        return {"type": "noul", "noul": 0.88 if hit else 0.07}

    if kind == "choice":
        options = list(question.get("criteria", {}))
        if len(options) < 2:
            raise ValueError("choice needs at least 2 options")
        chosen, hits = pick(options, text)
        # A clear keyword hit answers confidently; nothing recognisable stays spread out, which
        # leaves confidence under the threshold and exercises the escalation rule. The fixture is
        # meant to show both paths, not just the happy one.
        share = 0.97 if hits else 0.34
        rest = (1.0 - share) / (len(options) - 1)
        probabilities = {o: (share if o == chosen else rest) for o in options}
        return {
            "type": "choice",
            "choice": chosen,
            "probabilities": probabilities,
            "confidence": confidence(list(probabilities.values())),
        }

    if kind == "score":
        levels = list(question.get("criteria", []))
        if len(levels) < 2:
            raise ValueError("score needs at least 2 levels")
        # Urgent-sounding tickets land high, everything else near the low end.
        urgent = any(w in text for w in ("urgent", "critical", "immediately", "فوری", "بحرانی"))
        recognised = urgent or any(
            w in text for group in HINTS.values() for w in group
        )
        peak = len(levels) - 1 if urgent else 1
        share = 0.95 if recognised else 0.30
        probabilities = {
            str(i): (share if i == peak else (1.0 - share) / (len(levels) - 1))
            for i in range(len(levels))
        }
        score = sum(int(k) * v for k, v in probabilities.items())
        return {
            "type": "score",
            "score": round(score, 3),
            "legend": {str(i): level for i, level in enumerate(levels)},
            "probabilities": probabilities,
            "confidence": confidence(list(probabilities.values())),
        }

    raise ValueError(f"unsupported question type: {kind!r}")


class Handler(BaseHTTPRequestHandler):
    server_version = "circuit-stub/1.0"

    def do_POST(self) -> None:  # noqa: N802 - the base class fixes this name
        if self.path.rstrip("/") != "/v1/systemone":
            self._json(404, {"detail": "not found"})
            return

        if not self.headers.get("Authorization", "").startswith("Bearer "):
            self._json(401, {"detail": "missing bearer token"})
            return

        try:
            body = json.loads(self._read_body())
        except (ValueError, OSError) as exc:
            self._json(400, {"detail": f"unreadable body: {exc}"})
            return

        if "state" not in body or "questions" not in body:
            self._json(422, {"detail": "state and questions are required"})
            return

        text = flatten(body["state"])

        try:
            answers = {qid: answer(q, text) for qid, q in body["questions"].items()}
        except ValueError as exc:
            self._json(422, {"detail": str(exc)})
            return

        self._json(
            200,
            {
                "model": body.get("model") or "circuit-stub",
                "answers": answers,
                "usage": {"input_tokens": max(1, len(text) // 4), "output_tokens": 0},
                "request_id": uuid.uuid4().hex,
            },
        )

    def do_GET(self) -> None:  # noqa: N802
        if self.path.rstrip("/") == "/health":
            self._json(200, {"status": "ok", "stub": True})
        else:
            self._json(404, {"detail": "not found"})

    def _read_body(self) -> bytes:
        length = self.headers.get("Content-Length")
        if length is not None:
            return self.rfile.read(int(length))

        # The .NET client streams its JSON, so there is no Content-Length: read the chunks.
        chunks = []
        while True:
            size = int(self.rfile.readline().strip(), 16)
            if size == 0:
                self.rfile.readline()
                break
            chunks.append(self.rfile.read(size))
            self.rfile.readline()
        return b"".join(chunks)

    def _json(self, status: int, payload: dict) -> None:
        raw = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def log_message(self, fmt: str, *args: object) -> None:
        print(f"  {self.command} {self.path} -> {args[1] if len(args) > 1 else ''}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8901)
    parser.add_argument("--host", default="127.0.0.1")
    args = parser.parse_args()

    print(f"circuit stub listening on http://{args.host}:{args.port}/v1/systemone")
    print("This is a TEST FIXTURE. The answers are keyword heuristics, not a model.\n")

    HTTPServer((args.host, args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
