# circuit, in a container

Packaging for [circuit](https://github.com/Barneyjm/circuit) — open-weights System One models that
answer typed questions with calibrated probabilities and serve TypeSafe's `POST /v1/systemone`
contract verbatim. JevTicketRouter's **SelfHosted** provider talks to it with the same client, the
same question set, and the same answer mapper as the hosted API. Only the address changes.

circuit ships no container files of its own; these are ours. Nothing here is a fork — the image
fetches the upstream package at a pinned commit and installs its own published dependencies.

## Run it

From the repository root:

```bash
AI_PROVIDER=SelfHosted docker compose --profile circuit up --build
```

That is the whole setup. No API key, no account, and no outbound call once the weights are cached.
The app comes up on <http://localhost:8080> with the header badge reading **Self-hosted**, and
`GET /api/health` reports `"provider": "SelfHosted"`.

The first start is slow and looks idle. Watch it:

```bash
docker compose --profile circuit logs -f circuit
```

| Stage | What happens | Roughly |
| --- | --- | --- |
| Build | PyTorch and transformers are installed | 2–5 min |
| First boot | the run is fetched (71 MB), then the base model (3.45 GB) | download-bound |
| Every boot | the base model is loaded into memory | 30–90 s on CPU |
| Answering | one batched request, five questions | a few seconds on CPU, well under one on a GPU |

The container is `starting`, not unhealthy, until `/healthz` answers — which happens only once the
model is loaded, so the health check is a real readiness signal rather than "the process exists".

## Which model

| `CIRCUIT_MODEL` | Base | Download | Accuracy on circuit's validation split |
| --- | --- | --- | --- |
| `circuit-1.7b` (default) | Qwen3-1.7B-Base | ~3.5 GB | 0.897, ECE 0.016 |
| `circuit-8b` | Qwen3-8B-Base | ~16.6 GB | 0.899, ECE 0.020 |

The small one is the default because it is within 0.002 of the large one on circuit's own numbers
while needing a fifth of the disk. Switch with `CIRCUIT_MODEL=circuit-8b docker compose --profile
circuit up`; the same variable also sets the model name recorded on every decision, so the two
cannot drift apart.

Each model is pinned to the revision circuit's own deployment pins — `v1.2` for the 1.7B, `v1.1`
for the 8B. An unpinned repository would change the served model the moment new weights were
pushed, which is not something a triage service should learn about in production.

The vision and audio runs are deliberately absent: the ticket questions are text, and the image
installs no image or audio codecs. Set `CIRCUIT_REPO_ID` (and `CIRCUIT_REVISION`) to serve a run
this packaging does not know about — your own fine-tune, say — or `S1_MODEL` to serve one already
on disk, which skips the download entirely.

## CPU by default, GPU when you have one

The image installs PyTorch's CPU wheels, because a CPU is the one thing every machine has. circuit
picks its device itself — CUDA if torch can see a card, then MPS, then CPU — so a GPU needs two
changes: the CUDA wheels at build time, and the card passed through at run time.

```bash
TORCH_INDEX_URL=https://pypi.org/simple docker compose --profile circuit build
```

Then uncomment the `deploy.resources.reservations.devices` block on the `circuit` service in
`docker-compose.yml`. That needs the [NVIDIA Container
Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
on the host. The CUDA wheels are several GB, against roughly 250 MB for the CPU ones.

## What is in the image

| | |
| --- | --- |
| Base | `python:3.12-slim`, matching circuit's `.python-version` |
| Source | `s1proto` only, from commit `ff4123d`, fetched in a throwaway stage so `git` stays out of the final image |
| Dependencies | the serving subset, pinned to circuit's own `uv.lock` — see `requirements.txt` |
| User | non-root (`circuit`, uid 10001) |
| Weights | the `circuit-weights` volume, not a layer |
| Health check | `python -c urllib...` against `/healthz`, so no extra package is installed for it |

The training half of the project — `datasets`, `wandb`, `google-genai` — is not installed. This
image answers questions; it does not train. Neither are `librosa` and `soundfile`, which only the
audio models need and which `s1proto.media` imports lazily.

Weights live in a named volume rather than in a layer for two reasons: 3.5 GB of model in an image
layer makes the image impractical to move, and it welds one set of weights to one build.
`docker compose down` keeps the volume; `docker compose down -v` discards it and the next start
downloads again.

## Security

`S1_API_KEY` is unset by default, which makes circuit accept any non-empty bearer token — the right
default for a server on a private network that nothing else can reach. To require a real one, set
`CIRCUIT_API_KEY` and `SELF_HOSTED_API_KEY` to the same value; the first configures the server, the
second the client.

`SELF_HOSTED_BASE_URL` is checked before any request is made: loopback, RFC 1918, link-local, and
single-label or internal host names are accepted, and a public address is refused unless
`SelfHosted__AllowPublicEndpoint` is switched on. A self-hosted model exists so restricted ticket
data stays inside the network, and a typo in a hostname should not quietly undo that.

## Without a GPU, without the download

`tools/circuit-stub` serves the same contract from keyword rules, in an image that is the Python
base plus one file:

```bash
AI_PROVIDER=SelfHosted docker compose --profile circuit-stub up --build
```

It is a test fixture, not a model. It exists to make the wiring demonstrable in seconds.

## Why not Ollama

These models cannot be run through Ollama or llama.cpp. The calibrated probabilities come from a
pointer readout head sitting on top of the LoRA, and a GGUF conversion keeps the base weights while
dropping that head — which is the whole point of the model. circuit's own server is required.
`AI_PROVIDER=Local` is the mode for Ollama and other OpenAI-compatible endpoints, and it works
differently: it asks a general instruct model for JSON.
