#!/bin/sh
# Fetch the run if it is not in the volume yet, then hand the process over to circuit's server.
#
# S1_MODEL set from outside wins and nothing is downloaded, so a run mounted from the host -- or one
# you trained yourself -- can be served without changing the image.
set -eu

if [ -n "${S1_MODEL:-}" ]; then
    echo "serving S1_MODEL=${S1_MODEL} (set from the environment, skipping the download)" >&2
else
    RUN_DIR="$(python /opt/circuit/fetch_weights.py)"
    S1_MODEL="lora:${RUN_DIR}"
    export S1_MODEL
fi

echo "circuit listening on 0.0.0.0:${PORT:-8901}/v1/systemone" >&2

# The base model loads before the first answer, which takes a while and is silent. The health
# check waits for it; /healthz only replies once the scorer is up.
exec python -m s1proto "$@"
