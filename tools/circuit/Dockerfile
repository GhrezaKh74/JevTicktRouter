# syntax=docker/dockerfile:1

# ------------------------------------------------------------------------------------------------
# circuit — the self-hosted System One server, containerised.
#
# circuit (https://github.com/Barneyjm/circuit) serves the same POST /v1/systemone contract as the
# hosted TypeSafe API from open weights, so JevTicketRouter's SelfHosted provider talks to it with
# the same client and the same answer mapper. It ships no Dockerfile of its own; this is ours.
#
# The image carries the server, not the weights. Those are fetched on first boot into a volume --
# the run (a LoRA adapter plus a pointer readout head, tens of megabytes) and then the base model
# it names. Baking 3.5 GB of weights into a layer would make the image unusable to move around,
# and it would pin the model to the image.
#
#   docker compose --profile circuit up --build        # the intended way; see docker-compose.yml
#
#   docker build -t jevticketrouter-circuit tools/circuit
#   docker run --rm -p 8901:8901 -v circuit-weights:/weights jevticketrouter-circuit
#
# CPU by default, which is the only thing every machine has. That serves circuit-1.7b at a few
# seconds per ticket -- slow, but it works with nothing installed. For a CUDA card, build with the
# ordinary PyPI index (those wheels bundle CUDA) and pass the GPU through:
#
#   docker build --build-arg TORCH_INDEX_URL=https://pypi.org/simple -t jevticketrouter-circuit tools/circuit
#   docker run --rm --gpus all -p 8901:8901 -v circuit-weights:/weights jevticketrouter-circuit
#
# circuit picks the device itself: cuda when torch can see one, then mps, then cpu.
# ------------------------------------------------------------------------------------------------


# --- Stage 1: fetch circuit's source at a pinned commit -----------------------------------------
FROM python:3.12-slim AS source

# circuit publishes no releases or tags, so a commit is the only honest pin. Bump it deliberately.
ARG CIRCUIT_REPO=https://github.com/Barneyjm/circuit.git
ARG CIRCUIT_REF=ff4123d74ddbc5519723fef5d3b68cfca0943ac2

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# fetch of a single commit rather than a clone: no history, and the pin is checked by the fetch
# itself instead of trusting whatever a branch points at today.
RUN git init -q . \
    && git remote add origin "${CIRCUIT_REPO}" \
    && git fetch -q --depth 1 origin "${CIRCUIT_REF}" \
    && git checkout -q FETCH_HEAD \
    && rm -rf .git


# --- Stage 2: runtime ---------------------------------------------------------------------------
FROM python:3.12-slim AS runtime

# https://download.pytorch.org/whl/cpu  -> CPU wheels, ~250 MB, runs anywhere (the default)
# https://pypi.org/simple               -> the CUDA build, several GB, needs an NVIDIA card
# https://download.pytorch.org/whl/rocm6.4 and friends for AMD.
ARG TORCH_INDEX_URL=https://download.pytorch.org/whl/cpu

ENV PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

# Torch first and on its own, from whichever index was chosen. Everything else comes from PyPI:
# the PyTorch index is not a full mirror, so resolving the whole list against it would fail.
COPY requirements-torch.txt requirements.txt /tmp/requirements/
RUN python -m pip install --index-url "${TORCH_INDEX_URL}" -r /tmp/requirements/requirements-torch.txt \
    && python -m pip install -r /tmp/requirements/requirements.txt \
    && rm -rf /tmp/requirements

# The package only, not the training scripts or the datasets beside it in the repository.
COPY --from=source /src/s1proto /opt/circuit/s1proto
COPY fetch_weights.py entrypoint.sh /opt/circuit/

# A named volume mounted at /weights inherits this ownership when Docker first creates it, so the
# non-root user can write the download without a chown at startup.
RUN useradd --create-home --uid 10001 circuit \
    && mkdir -p /weights \
    && chown -R circuit:circuit /weights /opt/circuit \
    && chmod +x /opt/circuit/entrypoint.sh

USER circuit
WORKDIR /opt/circuit

ENV PYTHONPATH=/opt/circuit \
    PORT=8901 \
    CIRCUIT_MODEL=circuit-1.7b \
    CIRCUIT_WEIGHTS_DIR=/weights \
    HF_HOME=/weights/huggingface \
    HF_HUB_ENABLE_HF_TRANSFER=0 \
    OMP_NUM_THREADS=4

EXPOSE 8901

# /healthz answers only once the scorer has finished loading, which makes it a real readiness
# signal. The start period is long because the first boot downloads the weights and every boot
# loads the base model; until then the container is starting, not unhealthy.
#
# Python rather than curl, so the image needs no extra package for its own health check.
HEALTHCHECK --interval=30s --timeout=10s --start-period=900s --retries=3 \
    CMD ["python", "-c", "import urllib.request,sys;sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8901/healthz',timeout=8).status==200 else 1)"]

ENTRYPOINT ["/opt/circuit/entrypoint.sh"]
