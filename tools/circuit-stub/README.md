# circuit stub

A stand-in for [circuit](https://github.com/Barneyjm/circuit)'s `s1proto` server, so the
**SelfHosted** provider can be exercised without a GPU.

> **This is a test fixture, not a model.** The answers come from keyword matching, not judgement.
> It exists to prove the wiring — the question set, the answer mapper, the rule engine, the provider
> badge — on a machine that cannot run the real weights. Never point anything real at it.

Only the Python standard library is used, so there is nothing to install.

## Run it, with Docker

From the repository root, and this is everything:

```bash
AI_PROVIDER=SelfHosted docker compose --profile circuit-stub up --build
```

The app comes up on <http://localhost:8080> in a few seconds. The stub joins the network under the
alias `circuit`, so nothing else needs configuring: swapping in the real server later is a change of
profile, not of settings.

## Run it, with Python

```bash
python tools/circuit-stub/circuit_stub.py          # http://localhost:8901
```

Then, in another terminal, from the repository root:

```bash
AI_PROVIDER=SelfHosted SELF_HOSTED_BASE_URL=http://localhost:8901 \
  dotnet run --project backend/JevTicketRouter.Api
```

On Windows PowerShell:

```powershell
$env:AI_PROVIDER          = "SelfHosted"
$env:SELF_HOSTED_BASE_URL = "http://localhost:8901"
dotnet run --project backend/JevTicketRouter.Api
```

The header badge reads **Self-hosted** and `GET /api/health` reports `"provider": "SelfHosted"`.

## What it exercises

The stub answers confidently when it recognises the text and vaguely when it does not, so both
branches of the confidence rule are reachable:

| Ticket | Expected outcome |
| --- | --- |
| Persian fault report | `TechnicalIssue` → `ApplicationSupport`, auto-routed, no rules fire |
| English access request | `AccessRequest` → `IdentityAccess`, auto-routed |
| Phishing report quoting a card number | `SecurityConcern` → `Security`, **Critical**, sensitive data detected, escalated and redacted |
| Something vague | Confidence collapses, `LOW_CONFIDENCE_ESCALATION` fires |

## Contract

Built from circuit's published schema, so a response is shaped exactly as the real server's:

```jsonc
{
  "model": "circuit-8b",
  "answers": {
    "contains_sensitive_data": { "type": "noul", "noul": 0.04 },          // no confidence, by design
    "category": { "type": "choice", "choice": "...", "probabilities": {}, "confidence": 0.89 },
    "priority": { "type": "score", "score": 1.1, "legend": {}, "probabilities": {}, "confidence": 0.82 }
  },
  "usage": { "input_tokens": 312, "output_tokens": 0 },
  "request_id": "..."
}
```

Confidence uses circuit's own measure, `1 - H(p)/log(N)`.

## Running the real thing instead

```bash
AI_PROVIDER=SelfHosted docker compose --profile circuit up --build
```

That builds and runs circuit itself, weights and all — see `tools/circuit/README.md`. Or, outside
Docker:

```bash
git clone https://github.com/Barneyjm/circuit && cd circuit
uv sync
S1_MODEL=lora:runs/circuit-1.7b uv run python -m s1proto    # same path, same port
```

Nothing in the application changes either way — only which process is listening on 8901.
