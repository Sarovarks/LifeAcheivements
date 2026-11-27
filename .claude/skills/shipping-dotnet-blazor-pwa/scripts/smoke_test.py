"""Drive a deployed API the way a stranger would, and say where it broke.

A template, not a finished script: replace the STEPS section with the flow you actually care
about. The shape is the point — register, act, read back, and print enough that a failure
names its own step instead of leaving you to guess.

Why this exists: a green test suite says nothing about what is live. Tests run against an
in-memory database with handlers constructed directly, so they cannot see dependency injection
wiring, routing, auth, database-specific SQL translation, or whether the deploy happened at
all. One run of this answers all of those in about a minute.

Usage:
    python smoke_test.py https://your-api.example.com

Before running it against a system real people use, ask. Creating accounts is a side effect,
even a throwaway one — and prefer obviously disposable addresses so cleanup is a one-line
WHERE clause later.
"""
import json
import secrets
import string
import sys
import time
import urllib.error
import urllib.request

API = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://localhost:5100"

STAMP = time.strftime("%H%M%S")
EMAIL_A = f"smoke-a-{STAMP}@example.com"
EMAIL_B = f"smoke-b-{STAMP}@example.com"

# Generated here and never printed. Throwaway accounts still deserve a real password, and a
# transcript is not a place for one.
PASSWORD = "".join(secrets.choice(string.ascii_letters + string.digits) for _ in range(20)) + "!Aa1"

OK = (200, 201, 204)
failures = []


def call(method, path, token=None, body=None, timeout=120):
    """Return (status, parsed_body). Never raises: every status is information."""
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(API + path, data=data, method=method)
    request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", "Bearer " + token)

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read().decode()
            if not raw.strip():
                return response.status, None
            try:
                return response.status, json.loads(raw)
            except json.JSONDecodeError:
                return response.status, raw[:300]      # health endpoints often answer in text
    except urllib.error.HTTPError as error:
        raw = error.read().decode()
        try:
            return error.code, json.loads(raw)
        except json.JSONDecodeError:
            return error.code, raw[:300]
    except urllib.error.URLError as error:
        return 0, str(error)


def step(label, status, body=None):
    ok = status in OK
    print(f"  {label:<46} {status} {'ok' if ok else 'FAILED'}")
    if not ok:
        failures.append(f"{label} -> {status}")
        if body:
            print(f"      {body}")
    return ok


def expect(label, condition, detail=""):
    """For assertions about content rather than status codes."""
    print(f"  {label:<46} {'ok' if condition else 'FAILED'}  {detail}")
    if not condition:
        failures.append(label)
    return condition


# ---------------------------------------------------------------- warm up --
# A free-tier instance sleeps after idling, so the first call may take a minute.
print(f"waking {API}")
status, _ = call("GET", "/health/live")
step("health/live", status)

status, version = call("GET", "/health/version")
if status in OK and isinstance(version, dict):
    print(f"    running commit {version.get('commit')} on {version.get('branch')}")
    print("    compare with: git rev-parse HEAD")
else:
    print("    no /health/version - add one; it is how you tell which build is live")

status, _ = call("GET", "/health/ready")
step("health/ready (database reachable)", status)


# ------------------------------------------------------------------ STEPS --
# Replace everything below with the flow you care about. This example registers two
# accounts and has one act on the other's data, which is the shape of most real checks.

print("\nregister and sign in")


def sign_up(email, name):
    status, _ = call("POST", "/auth/signup",
                     body={"email": email, "password": PASSWORD, "displayName": name})
    step(f"signup {name}", status)
    status, body = call("POST", "/auth/login", body={"email": email, "password": PASSWORD})
    step(f"login {name}", status)
    return (body or {}).get("accessToken")


token_a = sign_up(EMAIL_A, "Smoke A")
token_b = sign_up(EMAIL_B, "Smoke B")

if not (token_a and token_b):
    print("\ncould not sign in; stopping before anything else")
    sys.exit(1)

print("\nthe flow under test")
# e.g.
# status, items = call("GET", "/things", token_a)
# step("A lists things", status)
# status, created = call("POST", "/things", token_a, body={"name": "x"})
# step("A creates a thing", status)
# status, seen = call("GET", "/things", token_b)
# expect("B can see A's thing", any(t["id"] == created["id"] for t in seen))

print(f"\ntest accounts left behind: {EMAIL_A}, {EMAIL_B}")

if failures:
    print(f"\n{len(failures)} failure(s):")
    for failure in failures:
        print(f"  - {failure}")
    sys.exit(1)

print("\nall checks passed")
