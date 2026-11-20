# dotnetjes

Desk & parking booking for the office. Colleagues log in with the organisation's Keycloak, book a desk and/or a parking spot for a weekday up to two weeks ahead, and see who's in the office. EV drivers can join a waitlist for a charger spot and get an email when one frees up. Admins manage desks, parking spots and people, and a summary email goes out every weekday morning.

Built with .NET 10, Blazor (Interactive Server), EF Core + PostgreSQL and Tailwind CSS.

## Run it

### With Docker (everything included)

1. Copy `.env.example` to `.env` and fill in the Keycloak settings (see [Keycloak](#keycloak)).
2. `docker compose up --build`
3. Open http://localhost:8080. Emails (daily summary, waitlist) land in Mailpit at http://localhost:8025.

Postgres data is kept in `.container-data/postgres/`. Already running Postgres locally on 5432? Start with `DB_PORT=5433 docker compose up --build`.

### With `dotnet run`

Needs the .NET 10 SDK, plus Postgres and an SMTP server on localhost. The easiest way is to start just those from compose:

```bash
docker compose up -d db mailpit
dotnet user-secrets set "Keycloak:Authority" "https://sso.example.org/realms/<realm>" --project src/Booking.Web
dotnet user-secrets set "Keycloak:ClientId" "<client-id>" --project src/Booking.Web
dotnet user-secrets set "Keycloak:ClientSecret" "<secret>" --project src/Booking.Web
dotnet run --project src/Booking.Web
```

Then open http://localhost:5017. The app refuses to start without the three Keycloak settings. Database migrations run automatically at startup.

## Keycloak

Every page requires a login. People are created in the app the first time they log in; their name and email come from Keycloak.

The Keycloak client must be confidential with the standard flow, and allow these redirect URIs:

| | Login | Logout |
|---|---|---|
| Docker | `http://localhost:8080/signin-oidc` | `http://localhost:8080/signout-callback-oidc` |
| `dotnet run` | `http://localhost:5017/signin-oidc` | `http://localhost:5017/signout-callback-oidc` |
| Production | `https://<prod-host>/signin-oidc` | `https://<prod-host>/signout-callback-oidc` |

**Admins:** create the client role `Admin` on this client and assign it to the admins (directly or via a group). Any case works. It takes effect at the next login; logins last 10 hours.

## Configuration

| Setting | What it does | Default |
|---|---|---|
| `Keycloak:Authority`, `:ClientId`, `:ClientSecret` | The Keycloak client. Never commit the secret: use user-secrets or `.env`. | — (required) |
| `ConnectionStrings:Booking` | Postgres | `localhost:5432`, db/user/password `booking` |
| `Smtp:Host`, `:Port`, `:From` | Mail server for all emails (no login, no TLS) | `localhost:1025`, from `booking@example.com` |
| `Summary:SendAt` | Time of the daily summary (office time) | `07:00` |
| `App:TimeZone` | Office time zone; decides what "today" is | `Europe/Brussels` |
| `App:PublicUrl` | Link in waitlist emails (`APP_PUBLIC_URL` in `.env`); empty → no link | Docker: `http://localhost:8080` |
| `Booking:MaxDaysAhead` | How far ahead people can book | `14` |

**Production** runs behind an HTTPS reverse proxy: set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on the app container, or Keycloak gets an `http://` redirect address and rejects the login.

## Tests

```bash
dotnet test
```

`Booking.Core.Tests` start a throwaway Postgres container with Testcontainers, so **Docker must be running**. `Booking.Web.Tests` need nothing.

## Project layout

```
src/Booking.Core   booking rules, waitlist, emails, EF Core model and migrations (no web dependencies)
src/Booking.Web    Blazor UI, Keycloak login, SMTP, background services, DI setup (Setup/*Feature.cs)
tests/             one test project per src project
docs/superpowers   design docs (specs/) and implementation plans (plans/) for each feature
```

## Styling (Tailwind CSS)

The UI uses Tailwind CSS v4. `dotnet build` compiles `src/Booking.Web/Styles/app.css` into `wwwroot/app.css` (generated, not committed) with the Tailwind standalone CLI, so no Node is needed.

- The first build downloads the CLI (~110 MB) into `obj/tailwind/`, so it needs internet. Pass `-p:TailwindCli=<path>` to use one that is already installed.
- Hot reload does not run the build: after adding new Tailwind classes, rebuild to get their CSS.
