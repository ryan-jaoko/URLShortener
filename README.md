# URL Shortener

A full-stack URL shortener with user accounts, click tracking, and rate limiting. Paste a long URL, get a short link, and see how often it gets used.

- **Frontend:** Vue (TypeScript) single-page app
- **Backend:** ASP.NET (C#) API with token-based auth
- **Storage:** Database accessed through a single database context

## Features

- Shorten long URLs into short links that redirect to the original destination
- Register and log in; links are tied to your account
- Pick up where you left off: a link you were shortening before logging in is resumed after you authenticate
- Per-link statistics, including recorded clicks
- Rate limiting on both the API and login attempts, with the client surfacing blocks to the user
- Short-link redirects handled directly by the backend, so visitors never need to load the web app

## Architecture

The system has three layers: a Vue web client, a thin API access layer inside the client, and the backend services.

```
Visitor
  ├── uses the web client ──► API access ──► Backend services ──► Database
  ├── registers / logs in ──► Auth controller
  └── follows a short link ─► Short-link redirect ──► Destination site
```

### Web client (`Vue`)

| Piece | File | Responsibility |
| --- | --- | --- |
| App shell | `App.vue` | Root component; routes to views and reads rate-limit block state |
| Home view | `HomeView.vue` | Renders the link list, stats panel, and shorten form |
| Shorten form | `ShortenForm.vue` | Submits links; checks session and rate limits |
| Link list | `LinkList.vue` | Displays your links |
| Stats panel | `StatsPanel.vue` | Displays link statistics |
| Auth view | `AuthView.vue` | Login/registration; resumes a pending link after auth; checks attempt limits |

Shared client state lives in three small stores:

- **Link state** (`links.ts`): links and statistics
- **Auth state** (`auth.ts`): the current session
- **Rate-limit state** (`rateLimit.ts`): records rate-limit blocks so the UI can respond to them

### API access (client side)

- **URLs API** (`urls.ts`): link-related requests
- **Auth API** (`auth.ts`): login and registration requests
- **HTTP client** (`client.ts`): shared request layer; reports rate-limit blocks back to the rate-limit state

### Backend services (`ASP.NET`)

| Piece | File | Responsibility |
| --- | --- | --- |
| URL controller | `Urls.cs` | Creates and reads links |
| Auth controller | `auth.cs` | Registration, login, and token issuing (via `TokenService.cs`) |
| Short-link redirect | `Program.cs` | Resolves a short code, records the click, redirects to the destination |
| Rate-limit middleware | middleware + `Rate-limit service` | Checks incoming requests against limits |
| Request models | `Request.cs` | Shared request types used by the controllers and middleware |
| Database context | `database.cs` | Reads and writes links and click tracking |

## How it works

**Shortening a link**
1. The shorten form checks your session and current rate-limit state.
2. The link state calls the URLs API, which sends the request through the HTTP client.
3. Backend middleware checks the rate limit, then the URL controller stores the link through the database context.

**Following a short link**
1. A visitor opens the short URL, which hits the backend directly.
2. The redirect handler looks up the link, records the click, and redirects to the destination site.

**Logging in**
1. The auth view submits credentials through the Auth API.
2. The auth controller validates them and issues a token via the token service.
3. If you were in the middle of shortening a link, it is resumed after login.

## Getting started

> Adjust the commands below to match your project layout and tooling.

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) and npm
- A database supported by your database context

### Backend

```bash
cd backend
dotnet restore
dotnet run
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

### Configuration

Set the following (for example in `appsettings.json` or environment variables):

| Setting | Description |
| --- | --- |
| Database connection string | Where the database context connects |
| Token signing key | Secret used by the token service to sign auth tokens |
| Rate-limit settings | Request and login-attempt limits |
| Frontend API base URL | Where the HTTP client sends requests |

## Project structure

```
.
├── frontend/
│   └── src/
│       ├── App.vue
│       ├── views/        # HomeView, AuthView
│       ├── components/   # ShortenForm, LinkList, StatsPanel
│       ├── stores/       # links, auth, rateLimit
│       └── api/          # client, urls, auth
└── backend/
    ├── Controllers/      # Urls, auth
    ├── Services/         # TokenService, rate-limit service
    ├── Models/           # Request models
    ├── Data/             # Database context
    └── Program.cs        # App setup and short-link redirect
```

## License

Add your license here.
