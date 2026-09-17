# JevTicketRouter

Structured support-ticket triage built on **.NET 10** and **React 19**, using **TypeSafe Jev** to make
fast, typed decisions and deterministic **.NET business rules** to make the final call.

A user submits a support ticket in Persian or English. The backend asks Jev five scoped questions in a
single batched call, then a deterministic rule engine decides what actually happens to the ticket.
Every field in the response says whether the value came from the model or from a rule.

> **Runs without an API key.** With no `TYPESAFE_API_KEY` configured the backend starts in a clearly
> labelled **Mock mode** and returns deterministic sample answers, so you can clone the repository and
> see the whole flow immediately.

---

## Screenshots

| Dashboard | Persian technical issue |
| --- | --- |
| ![The dashboard before any ticket has been triaged](docs/screenshots/01-dashboard.png) | ![A Persian ticket auto-routed to Application Support](docs/screenshots/02-persian-technical.png) |

| Security case: escalated and redacted | Developer details |
| --- | --- |
| ![A phishing report escalated to human review with its text redacted](docs/screenshots/03-security-redacted.png) | ![The developer panel showing the sanitised Jev response, the rules that fired, and the final API response](docs/screenshots/04-developer-details.png) |

<p align="center">
  <img src="docs/screenshots/05-mobile.png" alt="The dashboard on a narrow mobile viewport" width="320" />
</p>

---

## Why this design

### Why Jev makes the structured calls

A support ticket is unstructured text, but routing it is a set of small, closed decisions: which
category, which team, how urgent, is anything sensitive, does a human need to look. Jev is a
*System One* model built for exactly that — you send a state and typed questions, and you get back
values your code can branch on, plus a probability distribution and a calibrated confidence for each.

This project uses all three TypeSafe primitives, one per decision, and asks them **in a single
request**. The API evaluates every question in parallel and in isolation against the same state, so
batching is both cheaper and faster than one call per question, with no change to the answers.

| Decision | Primitive | Why |
| --- | --- | --- |
| `category` | **Choice** | One option from a closed set, with a probability per option. |
| `targetTeam` | **Choice** | Same shape: a fixed roster of teams. |
| `priority` | **Score** | The four levels are *ordered*, which is what Score is for. The answer also gives a probability-weighted position between levels. |
| `containsSensitiveData` | **Noul** | A yes/no judgement, returned as a probability from 0 to 1. |
| `needsHumanReview` | **Noul** | Same: a yes/no judgement the model can express doubt about. |

Choice and Score answers carry a `confidence` value; **Noul answers deliberately do not** — the
TypeSafe API returns only a probability for them. The UI reflects that honestly and shows
`not reported` rather than inventing a number.

### Why deterministic .NET rules have the final say

The model is a good classifier, not an authority. Routing a ticket has consequences — a missed
security incident, an exposed account number, a critical outage sitting in the wrong queue — and those
consequences should be governed by code you can read, test, and audit, not by a probability.

So Jev *proposes* and `TriageRuleEngine` *disposes*:

1. **Security or critical ⇒ human review.** If the category is `SecurityConcern` or the priority is
   `Critical`, `needsHumanReview` is forced to `true` regardless of what the model thought.
2. **Low confidence ⇒ human review.** If Jev's confidence in category, target team, or priority falls
   below **0.75**, the ticket goes to a human. This is the model being allowed to say "I don't know",
   and the code acting on it.
3. **Sensitive data ⇒ redaction.** If the ticket is flagged as containing sensitive data, the
   description is masked in the structured logs and replaced with a placeholder before the response
   ever leaves the server.

Every rule that fires is recorded as an `AppliedRule` with its id, what it states, and what it
changed. The API returns those, and the UI shows them. Nothing about the outcome is unexplainable.

**Provenance is a first-class field.** Each decision comes back as:

```jsonc
{
  "value": true,            // the final, authoritative value
  "modelValue": false,      // what Jev proposed
  "confidence": null,       // Jev's confidence, or null for noul-backed fields
  "origin": "BusinessRule", // JevModel | BusinessRule
  "wasOverridden": true     // true when a rule changed Jev's answer
}
```

---

## Architecture

Clean architecture, with dependencies pointing inward only:

```
JevTicketRouter.Api             ASP.NET Core minimal API, OpenAPI, ProblemDetails, CORS
        │  depends on
        ▼
JevTicketRouter.Infrastructure  JevHttpClient, MockJevClient, options, resilience, DI
        │  depends on
        ▼
JevTicketRouter.Application     IJevClient, question set, answer mapper, DTOs, validation,
        │  depends on          TicketTriageService (orchestration)
        ▼
JevTicketRouter.Domain          Entities, enums, TriageRuleEngine, redaction. No dependencies.
```

**The flow of one request:**

```
POST /api/tickets/triage
   │
   ├─ FluentValidation ──────────── invalid ─▶ 400 ValidationProblemDetails
   │
   ├─ JevTriageQuestions.BuildRequest()      one state + five typed questions
   │
   ├─ IJevClient.EvaluateAsync()             ── live ─▶ POST https://api.typesafe.ai/v1/systemone
   │                                         └─ mock ─▶ deterministic sample answers
   │
   ├─ JevAnswerMapper.Map()                  raw answers ─▶ domain assessment (rejects malformed)
   │
   ├─ TriageRuleEngine.Apply()               ◀── the final authority
   │
   └─ redact ─▶ structured log + 200 response
```

`IJevClient` is a thin, faithful wrapper over the documented HTTP contract, which keeps it trivial to
substitute in tests and makes the live/mock choice invisible to everything above it. TypeSafe ships
Python and JavaScript SDKs but **no .NET SDK**, so the backend calls the HTTP API directly through
`IHttpClientFactory`, with an exponential-backoff retry handler for `429` and `529` as the docs
prescribe.

---

## Features

**Backend**
- Clean architecture with enforced dependency boundaries and nullable reference types everywhere.
- `POST /api/tickets/triage` and `GET /api/health`.
- A single batched Jev call using all three primitives (Choice, Score, Noul).
- Deterministic rule engine with full provenance and an audit trail of applied rules.
- Sensitive-data redaction in both logs and API responses, plus a defence-in-depth pattern mask
  (long digit runs, emails, bearer tokens, `password:`-style assignments) that applies even to
  tickets the model did *not* flag.
- `CancellationToken` throughout, configurable timeouts, resilient retries, RFC 7807 ProblemDetails.
- Structured logging that never contains the ticket description or the API key.
- Mock mode when no key is configured, clearly labelled in logs, the API, and the UI.
- OpenAPI document with worked request and response examples, served through Swagger UI.
- 106 tests across the rules, validation, redaction, answer mapping, both clients, and the HTTP API.

**Frontend**
- Responsive dark-mode dashboard in Material UI, no template, no utility CSS framework.
- Ticket form with React Hook Form + Zod, mirroring the server's validation rules.
- Three one-click demo tickets, all fictional.
- Result card with category, target team, priority, per-decision confidence meters, sensitive-data
  and human-review status, and the final routing summary.
- A `rule` badge wherever a deterministic rule decided a field, so model output is never mistaken for
  a business decision.
- Collapsible developer panel: sanitised Jev response, the rules that fired, and the final API
  response — never the raw description when sensitive data was detected.
- Loading, empty, and readable error states; server-side validation errors surfaced inline.
- Persian and English ticket text both render correctly.
- 46 tests with Vitest and React Testing Library.

---

## Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | **10.0.401** or newer | `dotnet --version` |
| [Node.js](https://nodejs.org/) | **22.x LTS** or newer | `node --version` |
| npm | 10 or newer | ships with Node |
| TypeSafe API key | optional | [console.typesafe.ai](https://console.typesafe.ai/settings/keys). Without one the app runs in Mock mode. |

---

## Setup on Windows

Open **PowerShell** in the folder where you keep your projects.

```powershell
# 1. Clone and enter the repository
git clone https://github.com/GhrezaKh74/JevTicktRouter.git
cd JevTicktRouter

# 2. Restore and build the backend
dotnet restore
dotnet build

# 3. Run the backend tests
dotnet test

# 4. Install the frontend dependencies
cd frontend\jev-ticket-router-web
npm install
cd ..\..
```

Then start the two halves in **two separate PowerShell windows**.

**Window 1 — backend** (from the repository root):

```powershell
dotnet run --project backend/JevTicketRouter.Api
```

The API listens on <http://localhost:5217>. Swagger UI is at <http://localhost:5217/swagger>.

**Window 2 — frontend**:

```powershell
cd frontend\jev-ticket-router-web
npm run dev
```

Open <http://localhost:5173>. The Vite dev server proxies `/api` to the backend, so there is no CORS
setup to do while developing.

---

## Configuring your TypeSafe API key

The key is read from **.NET User Secrets** first, then from the `TYPESAFE_API_KEY` environment
variable. It is never read from a file in this repository, and never written to one.

### Option A — .NET User Secrets (recommended for local development)

User Secrets are stored in your Windows user profile, outside the repository, so they cannot be
committed by accident.

The project already has a `UserSecretsId`, so there is nothing to initialise.

```powershell
cd backend\JevTicketRouter.Api

# Store your key
dotnet user-secrets set "Jev:ApiKey" "<paste-your-key-here>"

# Verify it was stored (prints the key, so do this in a private terminal)
dotnet user-secrets list
```

To remove it again:

```powershell
dotnet user-secrets remove "Jev:ApiKey"
```

On Windows the store lives at
`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`.

### Option B — environment variable

The variable name is the one from the TypeSafe docs.

```powershell
# Current PowerShell session only
$env:TYPESAFE_API_KEY = "<paste-your-key-here>"
dotnet run --project backend/JevTicketRouter.Api

# Persist for your Windows user account (new terminals only)
setx TYPESAFE_API_KEY "<paste-your-key-here>"
```

On macOS or Linux:

```bash
export TYPESAFE_API_KEY="<paste-your-key-here>"
dotnet run --project backend/JevTicketRouter.Api
```

Any other setting can be overridden the same way, using `__` for nesting — for example
`Triage__MinimumConfidence=0.9` or `Jev__ForceMockMode=true`. See `.env.example` and
`appsettings.example.json` for the full set.

Restart the backend after changing the key. The startup log states which mode it came up in.

---

## Running the app

From the repository root:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project backend/JevTicketRouter.Api
```

From `frontend/jev-ticket-router-web`:

```bash
npm install
npm run dev
```

| URL | What it is |
| --- | --- |
| <http://localhost:5173> | The dashboard |
| <http://localhost:5217/swagger> | Swagger UI |
| <http://localhost:5217/openapi/v1.json> | The raw OpenAPI document |
| <http://localhost:5217/api/health> | Status and current Jev mode |

### Trying the API directly

```bash
curl -X POST http://localhost:5217/api/tickets/triage \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Access to the reporting portal for a new analyst",
    "description": "Our new analyst needs read-only access to the quarterly reporting portal.",
    "requesterRole": "InternalSupport"
  }'
```

---

## Running the tests

**Backend** (106 tests — rules, validation, redaction, answer mapping, both Jev clients, and the HTTP
API end to end):

```bash
dotnet test
```

**Frontend** (46 tests):

```bash
cd frontend/jev-ticket-router-web
npm test              # one run
npm run test:watch    # watch mode
npm run test:coverage # with a coverage report
```

**The full frontend quality gate:**

```bash
npm run typecheck     # tsc, strict
npm run lint          # ESLint, type-aware rules
npm run format:check  # Prettier
npm run build         # production build
```

---

## Mock mode

When no `TYPESAFE_API_KEY` is configured, `MockJevClient` is registered instead of `JevHttpClient` and
**no network call is made**. It returns answers in exactly the shape the real API documents — a
probability distribution and a derived confidence for Choice and Score, a bare probability for Noul —
so nothing downstream can tell the difference, and the rule engine, redaction, and UI all behave
identically.

Scoring is keyword-driven over Persian and English vocabulary, with confidence derived from the shape
of the distribution the same way the API describes it. **It is a demo aid, not a model**, and makes no
attempt to reproduce Jev's judgement. It is deterministic, so the same ticket always produces the same
answer, which is what makes it useful in tests.

You can tell which mode you are in three ways:
- the **Mock mode** / **Live Jev** badge in the dashboard header,
- `jevMode` in `GET /api/health`,
- a warning on the first line of the backend's startup log.

Mock mode can be forced on even when a key is present, which is handy for offline demos:

```powershell
$env:Jev__ForceMockMode = "true"
```

---

## Security notes

- **Never commit `.env`, `secrets.json`, or an API key.** All three are covered by `.gitignore`, but
  the habit matters more than the file. `.env.example` and `appsettings.example.json` contain
  placeholders only and are the only such files that belong in version control.
- The API key is read only from User Secrets or the environment. It is never written to
  `appsettings.json`, never logged, never included in an error message, and never returned by any
  endpoint — including the error paths, which are tested for it.
- The `Authorization` header is set per request rather than on the shared `HttpClient`, so rotating
  the key takes effect without a restart of the connection pool.
- When a ticket is flagged as sensitive, the description is replaced with a placeholder **on the
  server**, before logging and before serialisation. The client never receives an unredacted copy, so
  the developer panel has nothing to leak.
- A second, always-on masking pass covers secret-shaped content — long digit runs, email addresses,
  bearer tokens, `password:`/`api_key=` assignments — so an *unflagged* ticket still cannot push a
  credential into the log stream.
- Ticket descriptions are never logged as text, only as a length.
- CORS is restricted to the configured origins (the Vite dev server by default).
- Swagger UI is served in all environments because this is a portfolio project. **Lock that down
  before any real deployment.**
- The demo tickets use entirely fictional data. No real banking information, customer records,
  account numbers, or credentials appear anywhere in this repository.

---

## Selected dependency versions

**Backend** — .NET SDK `10.0.401`, target framework `net10.0`

| Package | Version |
| --- | --- |
| Microsoft.AspNetCore.OpenApi | 10.0.12 |
| Swashbuckle.AspNetCore.SwaggerUI | 10.2.3 |
| FluentValidation | 12.1.1 |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 |
| Microsoft.Extensions.Http | 10.0.12 |
| Microsoft.Extensions.Http.Resilience | 10.10.0 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.12 |
| xunit | 2.9.3 |
| xunit.runner.visualstudio | 3.1.4 |
| Microsoft.NET.Test.Sdk | 17.14.1 |
| FluentAssertions | 7.2.0 |
| NSubstitute | 6.2.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 |
| coverlet.collector | 6.0.4 |

**Frontend** — Node 22 LTS

| Package | Version |
| --- | --- |
| react / react-dom | 19.3.0 |
| typescript | 6.0.3 |
| vite | 8.3.0 |
| @vitejs/plugin-react | 6.1.1 |
| @mui/material | 9.4.0 |
| @mui/icons-material | 9.4.0 |
| @emotion/react | 11.14.0 |
| @emotion/styled | 11.14.1 |
| @tanstack/react-query | 5.103.1 |
| react-hook-form | 7.88.0 |
| zod | 4.6.5 |
| @hookform/resolvers | 5.9.1 |
| vitest | 5.0.1 |
| @vitest/coverage-v8 | 5.0.1 |
| @testing-library/react | 16.3.3 |
| @testing-library/jest-dom | 7.0.1 |
| @testing-library/user-event | 14.6.7 |
| jsdom | 30.1.0 |
| eslint | 10.10.0 |
| typescript-eslint | 8.70.0 |
| prettier | 3.9.7 |

`FluentAssertions` is pinned to the **7.x** line, which is Apache-2.0. Version 8 moved to a licence
that requires a paid subscription for commercial use.

---

## Project layout

```
.
├── backend/
│   ├── JevTicketRouter.Api/             Minimal API, OpenAPI, ProblemDetails, composition root
│   ├── JevTicketRouter.Application/     IJevClient, TypeSafe contracts, questions, mapper, DTOs
│   ├── JevTicketRouter.Domain/          Entities, enums, TriageRuleEngine, redaction
│   ├── JevTicketRouter.Infrastructure/  HTTP + mock Jev clients, options, resilience
│   ├── JevTicketRouter.Tests/           xUnit + FluentAssertions + NSubstitute
│   └── Directory.Build.props            Shared compiler settings, warnings as errors
├── frontend/
│   └── jev-ticket-router-web/           React 19 + TypeScript + Vite + MUI
├── docs/screenshots/
├── .editorconfig
├── .env.example
├── .gitignore
├── appsettings.example.json
└── README.md
```

---

## Reference

Built against the official TypeSafe documentation at <https://docs.typesafe.ai>:
[API reference](https://docs.typesafe.ai/api),
[primitives](https://docs.typesafe.ai/primitives),
[confidence](https://docs.typesafe.ai/confidence),
[state](https://docs.typesafe.ai/concepts/state),
[models](https://docs.typesafe.ai/models).

## Licence

MIT.
