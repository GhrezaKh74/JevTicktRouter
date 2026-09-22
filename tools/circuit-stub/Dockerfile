# syntax=docker/dockerfile:1

# ------------------------------------------------------------------------------------------------
# The circuit stub, containerised, so the SelfHosted path can be exercised with no download, no
# GPU, and no Python on the host.
#
# It answers the same POST /v1/systemone as the real server, from keyword heuristics. It is a TEST
# FIXTURE, not a model -- see README.md. It exists so the wiring can be demonstrated in seconds.
#
#   docker compose --profile circuit-stub up --build     # the intended way
#
#   docker build -t jevticketrouter-circuit-stub tools/circuit-stub
#   docker run --rm -p 8901:8901 jevticketrouter-circuit-stub
#
# The stub is standard library only, so there is nothing to install and the image is the base plus
# one file.
# ------------------------------------------------------------------------------------------------

FROM python:3.12-alpine

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

COPY circuit_stub.py /opt/circuit-stub/circuit_stub.py

# Nothing here writes to disk, so the process has no business owning its own code.
RUN adduser -D -u 10001 stub

USER stub
WORKDIR /opt/circuit-stub

EXPOSE 8901

# 0.0.0.0, not the script's 127.0.0.1 default: inside a container, loopback is reachable only from
# the container itself, so the app next door would never connect.
CMD ["python", "circuit_stub.py", "--host", "0.0.0.0", "--port", "8901"]

HEALTHCHECK --interval=10s --timeout=5s --start-period=5s --retries=3 \
    CMD ["python", "-c", "import urllib.request,sys;sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8901/health',timeout=3).status==200 else 1)"]
