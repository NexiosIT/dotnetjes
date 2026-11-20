# Dark & bold Tailwind UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle every page of the booking app in the approved "Dark & bold" look with Tailwind CSS v4, replacing Bootstrap, with no behaviour change.

**Architecture:** An MSBuild target in `Booking.Web.csproj` downloads the pinned Tailwind standalone CLI for the build machine and compiles `Styles/app.css` → `wwwroot/app.css` before static web assets are collected, so `dotnet build/run/publish` and the Docker SDK stage all produce the CSS. Razor markup switches from Bootstrap classes to Tailwind utilities; `@code` logic stays as is apart from two small computed values (free counters, active nav link).

**Tech Stack:** .NET 10 Blazor (Interactive Server, prerender off), Tailwind CSS v4.3.4 standalone CLI, Docker (SDK `mcr.microsoft.com/dotnet/sdk:10.0` build stage, chiseled runtime).

**Spec:** `docs/superpowers/specs/2026-11-20-dark-tailwind-ui-design.md` — read it, and open the mockups it names (`docs/superpowers/specs/mockups/dark-ui-book.html` direction **C** column, `dark-ui-admin.html`). Their Tailwind classes are the visual source of truth; they are Tailwind 3 on a CDN, so apply the v4 renames (`bg-gradient-to-r` → `bg-linear-to-r`, `shadow-sm`→`shadow-xs`, `rounded` scale, `ring` default width 1px — always write `ring-1`).

## Global Constraints

- Tailwind **v4.3.4**, standalone CLI from `https://github.com/tailwindlabs/tailwindcss/releases/download/v4.3.4/<asset>`; assets: `tailwindcss-windows-x64.exe`, `tailwindcss-linux-x64`, `tailwindcss-linux-arm64`, `tailwindcss-macos-arm64`, `tailwindcss-macos-x64`. No Node, no npm.
- Binary cached under `$(BaseIntermediateOutputPath)tailwind/4.3.4/` (inside `obj/`, already git-ignored); downloaded only when missing; `chmod +x` on non-Windows.
- Input `src/Booking.Web/Styles/app.css`; output `src/Booking.Web/wwwroot/app.css`, generated, git-ignored, `--minify` in Release.
- `App.razor` keeps linking `@Assets["app.css"]`; Bootstrap link and `wwwroot/lib/bootstrap/` are removed (Task 5).
- Palette (spec): page `bg-slate-950 text-slate-100`; panels `bg-white/5 ring-1 ring-white/10 rounded-xl`; gradient fuchsia-400 → cyan-300 (logo text), fuchsia-500 → cyan-500 (summary button); fuchsia = desks/primary, cyan = parking, emerald = free/success, rose = errors.
- Behaviour, texts and messages unchanged: "Pick who you are to book.", booking messages from `BookingMessages`, "In the office: …", "Nobody booked yet.", admin validation messages, `Sent {n} emails.` etc.
- Only markup/CSS in Person B's files (`Day.razor`, `SlotRow.razor`, `UserPicker.razor`) except the computed counters in Task 3. Tell Person B before starting.
- No changes to `Booking.Core`, `Program.cs`, the Dockerfile's stages or compose.
- `dotnet test tests/Booking.Core.Tests` stays green after every task (no UI unit tests exist; UI tasks are verified by a browser click-through — Playwright + installed Edge from a scratch folder is fine; do not commit Node tooling).

## Review Focus

1. **Clean clone / Docker gets no CSS** — `wwwroot/app.css` generated after static-asset discovery is missing from `MapStaticAssets` → unstyled page or 404. Expect styles on a clean clone and in `docker compose up --build`. *(Task 1 Step 4–5)*
2. **Disabled and busy states vanish** — Tailwind's preflight drops Bootstrap's disabled look, so disabled ◀ ▶, Book/Cancel while busy, and "Sending…" must still look and act disabled (`disabled:opacity-40 disabled:cursor-not-allowed`). *(Task 3 Step 3, Task 4 Step 3)*
3. **Picker dropdown** without Bootstrap's `.dropdown-menu` — must open above the page content, close on outside click and on pick, and highlight the current person. *(Task 2 Step 3)*
4. **Phone width (375 px)** — no horizontal page scroll on any page; admin tables scroll inside their panel (`overflow-x-auto`). *(Task 3 Step 3, Task 4 Step 3)*
5. **Long names / emails** (the DB allows 150+ chars) — they wrap or truncate inside rows and table cells; buttons stay visible. *(Task 3 Step 3, Task 4 Step 3)*

---

### Task 1: Tailwind build pipeline

**Files:**
- Create: `src/Booking.Web/Styles/app.css`
- Create: `src/Booking.Web/Tailwind.targets` (imported by the csproj)
- Modify: `src/Booking.Web/Booking.Web.csproj` (import the targets), `src/Booking.Web/Components/App.razor:18` (`<body class="bg-slate-950 text-slate-100 antialiased">`), `.gitignore` (add `src/Booking.Web/wwwroot/app.css`)
- Delete from git: `src/Booking.Web/wwwroot/app.css` (`git rm --cached`; the file is regenerated)

**Interfaces:**
- Produces: `Styles/app.css` containing `@import "tailwindcss";`, `@source "../Components";`, and the template rules still needed, rewritten without Bootstrap: `h1:focus { outline: none; }`, `.blazor-error-boundary` (+ `::after`), `.valid.modified:not([type=checkbox])`, `.invalid`, `.validation-message`, and the `#blazor-error-ui` rules if they live in `MainLayout.razor.css` (move them here; Task 2 deletes that file). Drop every `.btn*`, `.form-*`, `.content`, `a` colour rule.
- Produces: MSBuild target `TailwindBuild` that runs before static web assets are resolved (hook `BeforeTargets="ResolveStaticWebAssetsConfiguration;AssignTargetPaths"` or equivalent) and, after generating, re-adds the file: `<Content Remove="wwwroot/app.css" /><Content Include="wwwroot/app.css" />` so it is picked up even when it did not exist at evaluation time. Inputs: `Styles/app.css`, `Components/**/*.razor`; outputs: `wwwroot/app.css` (incremental). Command: `"<cli>" -i Styles/app.css -o wwwroot/app.css` (+ `--minify` when `$(Configuration)` is `Release`), working directory = project dir. Select the asset with `$([MSBuild]::IsOSPlatform(...))` and `$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)`; fail the build with a clear `<Error>` on an unsupported OS/arch.

- [ ] **Step 1: Write the check first** — with no target yet, run from the repo root:
  `rm -f src/Booking.Web/wwwroot/app.css && dotnet build src/Booking.Web && grep -c "bg-slate-950" src/Booking.Web/wwwroot/app.css`
  Expected: FAIL — `No such file` (nothing generates it).
- [ ] **Step 2: Implement** `Styles/app.css`, `Tailwind.targets`, the csproj import, the `<body>` classes, the `.gitignore` line and `git rm --cached src/Booking.Web/wwwroot/app.css`.
- [ ] **Step 3: Run the Step 1 command again.** Expected: build succeeds, count ≥ 1. Run it a second time: the CLI is not downloaded again (no download message), build still succeeds.
- [ ] **Step 4: Clean-clone check** — `rm -rf src/Booking.Web/obj src/Booking.Web/bin src/Booking.Web/wwwroot/app.css && dotnet publish src/Booking.Web -c Release -o <scratch>/pub`; expected: `<scratch>/pub/wwwroot/app.css` exists and contains `bg-slate-950`. Then `dotnet run --project src/Booking.Web` (db up) and `curl -s http://localhost:<port>/` → the HTML links an `app.*.css` URL; `curl` that URL → 200 and contains `bg-slate-950` (Review Focus 1).
- [ ] **Step 5: Docker check** — `docker compose up --build -d`; fetch the CSS URL from `http://localhost:8080/` the same way → 200, contains `bg-slate-950`.
- [ ] **Step 6: Run** `dotnet test tests/Booking.Core.Tests` → all pass.
- [ ] **Step 7: Commit** — `git commit -m "build: generate app.css with the Tailwind standalone CLI"`

### Task 2: Top bar layout and picker

**Files:**
- Modify: `src/Booking.Web/Components/Layout/MainLayout.razor`, `Components/Layout/NavMenu.razor` (becomes the top bar), `Components/Layout/UserPicker.razor` (markup only)
- Delete: `Components/Layout/MainLayout.razor.css`, `Components/Layout/NavMenu.razor.css`

**Interfaces:**
- Produces: `MainLayout` = `<NavMenu />` top bar (full width, `border-b border-white/10`) + `<main class="mx-auto max-w-5xl px-4 sm:px-6 py-6">@Body</main>` + the `#blazor-error-ui` div (styled via Task 1 CSS). No sidebar, no hamburger.
- Produces: `NavMenu` top bar: wordmark "desk&parking" (`bg-linear-to-r from-fuchsia-400 to-cyan-300 bg-clip-text text-transparent font-semibold`) linking to `/`; links **Book** (`/`, `NavLinkMatch.All`) and **Admin** (`admin/resources`), active = `bg-white/10 text-white`, inactive `text-slate-400`; Admin is active on any path starting with `admin/` (inject `NavigationManager`, compute from `ToBaseRelativePath(Uri)`, re-render on `LocationChanged`; dispose the handler). `<UserPicker />` on the right.
- Produces: `UserPicker` pill button `rounded-full bg-white/10 px-3 py-1 text-sm` with the same text as today; dropdown panel `absolute right-0 mt-2 z-50 min-w-40 rounded-xl bg-slate-900 ring-1 ring-white/10 shadow-xl py-1`; current person `bg-fuchsia-500/20 text-fuchsia-200`; the outside-click overlay stays (`fixed inset-0 z-40`). Wrapper `relative`.

- [ ] **Step 1: Implement** the three components and delete the two `.razor.css` files.
- [ ] **Step 2: Build + tests** — `dotnet build` (0 warnings from the changed files) and `dotnet test tests/Booking.Core.Tests` → pass.
- [ ] **Step 3: Click-through** (app running, Edge, 1280 px and 375 px): top bar shows wordmark, Book, Admin, picker; no sidebar; on `/admin/people` Admin is highlighted; picker opens above page content, picking Ben changes the label to "You: Ben" and closes it; clicking outside closes it; current person highlighted; at 375 px `document.documentElement.scrollWidth <= innerWidth` (Review Focus 3, 4).
- [ ] **Step 4: Commit** — `git commit -m "feat(ui): dark top bar and picker"`

### Task 3: Book page

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Day.razor`, `Components/Pages/SlotRow.razor`

**Interfaces:**
- Consumes: `DayOverview.Desks/Parking` (`ResourceSlot.BookingId` null = free), existing `_busy`, `CanGoBack`, `CanGoForward`, `_message`, `_personId`.
- Produces (Day.razor `@code`, the only logic added): `private static (int Free, int Total) Count(IReadOnlyList<ResourceSlot> slots) => (slots.Count(s => s.BookingId is null), slots.Count);`
- Produces (markup, from mockup C): header — weekday `text-xs text-slate-400` (`dddd`, invariant culture) above date `text-3xl font-black tracking-tight` (`d MMM`); round ‹ › buttons `h-9 w-9 rounded-full bg-white/10` with `disabled:opacity-40 disabled:cursor-not-allowed`, same `aria-label`s. Counters: two tiles `rounded-xl ring-1 p-3`, desks `from-fuchsia-500/20 ring-fuchsia-500/30`, parking `from-cyan-500/20 ring-cyan-500/30`, text `{Free}/{Total}` + "desks free" / "parking free". Prompt banner `rounded-xl bg-white/5 ring-1 ring-white/10 px-4 py-2.5`; message banner rose (`bg-rose-500/10 ring-1 ring-rose-500/30 text-rose-200`, keep `role="alert"`). Lists in `grid gap-6 md:grid-cols-2`, each under a heading `text-xs uppercase tracking-wider text-slate-500` ("Desks" / "Parking") and a `ul` `divide-y divide-white/5 rounded-xl bg-white/5`. The "In the office: …" / "Nobody booked yet." line below, `text-sm text-slate-400`, same texts.
- Produces (SlotRow): new parameter `[Parameter] public string Accent { get; set; } = "fuchsia";` (`"cyan"` for parking, passed by Day.razor) selecting from two fixed class sets (write both full class strings — Tailwind can't see interpolated names). Row `flex items-center gap-3 px-3 py-2 text-sm`; code `font-bold w-8 shrink-0`; name `flex-1 min-w-0 truncate text-slate-400`; free → emerald pill "free" + **Book** (`rounded-lg bg-fuchsia-500 px-3 py-1 text-xs font-semibold` / cyan for parking); yours → row tint `bg-fuchsia-500/10` (cyan), "You" in accent colour + **Cancel** (`rounded-lg ring-1 ring-white/20 px-3 py-1 text-xs`); taken → whole row `text-slate-500`, "taken by {name}" `truncate max-w-[40%]`. All buttons keep their `disabled` conditions and get `disabled:opacity-40 disabled:cursor-not-allowed`.

- [ ] **Step 1: Implement** Day.razor and SlotRow.razor.
- [ ] **Step 2: Build + tests** → pass.
- [ ] **Step 3: Click-through** (1280 px and 375 px): with nobody picked, the prompt shows and Book buttons are disabled and look disabled; pick Anna, book D1 → row turns "You" + Cancel, desk counter drops by one; book P3 → cyan "You" row; cancel both → back to free; on the first bookable day ◀ looks and is disabled (Review Focus 2); book a slot as Anna in one tab and click Book on the same slot as Ben in a second tab → the rose banner shows the "just taken" message; add a person with a 150-character name in admin, book as them → the row stays one line with the name truncated and the button visible (Review Focus 5); at 375 px lists stack and there is no horizontal scroll (Review Focus 4).
- [ ] **Step 4: Commit** — `git commit -m "feat(ui): dark & bold book page"`

### Task 4: Admin pages

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Admin/AdminTabs.razor`, `Admin/Resources.razor`, `Admin/People.razor` (markup only; keep all `@code`, including the `if (sending) return;` guard)

**Interfaces:**
- Produces (from `dark-ui-admin.html`): page title block `text-xs text-slate-400` "Admin" over `text-3xl font-black tracking-tight` page name (move the `<h1>` styling, keep the text "Desks & parking" / "People"). AdminTabs: pill tabs `inline-flex rounded-xl bg-white/5 p-1` with `NavLink` items, active `bg-fuchsia-500 font-semibold` (use `ActiveClass`), inactive `text-slate-400`; summary button `rounded-lg bg-linear-to-r from-fuchsia-500 to-cyan-500 px-4 py-2 text-sm font-semibold disabled:opacity-40 disabled:cursor-not-allowed`, result text emerald when ok, rose otherwise, keep `role="status"`. Add form in a panel `rounded-xl bg-white/5 ring-1 ring-white/10 p-4 flex flex-wrap items-end gap-3`; inputs/selects `rounded-lg bg-slate-900 ring-1 ring-white/10 px-3 py-2 focus:ring-fuchsia-400 focus:outline-none`, labels `text-xs text-slate-400`, keep ids/`for`. Validation banner rose, as in Task 3. Table wrapper `rounded-xl bg-white/5 overflow-x-auto`; `table w-full text-sm`; header `text-xs uppercase tracking-wider text-slate-500`; rows `divide-y divide-white/5`; inactive rows `text-slate-500`; type pill fuchsia for Desk, cyan for Parking; email cell `break-all`. Active toggle: keep the `<input type="checkbox">` with `aria-label="Active"`, styled as a switch with `appearance-none` + `peer`/`checked:` classes (track `h-5 w-9 rounded-full bg-slate-700 checked:bg-emerald-500`, knob via a sibling span or `before:` pseudo-element), visible focus ring. Edit/Save/Cancel as in the mockup.

- [ ] **Step 1: Implement** the three files.
- [ ] **Step 2: Build + tests** → pass.
- [ ] **Step 3: Click-through** (1280 px and 375 px): add `D9` → appears; add `d9` → rose banner "A resource with code D9 already exists."; toggle P4 off → switch shows off, row dimmed, still off after reload, toggle back on; edit D9's name → saved; People: email `gert` → "Email must contain '@'."; a 150-char email wraps inside its cell; Tab key reaches the switch with a visible focus ring and Space toggles it; "Send today's summary now" (Mailpit up) → "Sent N emails." in emerald; a fast double-click still sends one batch (Mailpit count); while sending the button looks disabled (Review Focus 2); at 375 px the table scrolls inside its panel and the page has no horizontal scroll (Review Focus 4).
- [ ] **Step 4: Commit** — `git commit -m "feat(ui): dark & bold admin pages"`

### Task 5: Remaining pages, remove Bootstrap, final check

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Error.razor`, `Components/Pages/NotFound.razor`, `Components/Layout/ReconnectModal.razor.css` (dark dialog: `slate-900` panel, `slate-100` text, fuchsia button; keep every class name and id the `.js` file uses), `Components/App.razor` (remove the Bootstrap `<link>`)
- Delete: `src/Booking.Web/wwwroot/lib/` (Bootstrap)

**Interfaces:**
- Produces: Error page — rose heading, text in `text-slate-300`, request id in `font-mono`; NotFound — "Not Found" heading + text + a link back to Book (`text-fuchsia-300 hover:underline`). Texts unchanged otherwise.

- [ ] **Step 1: Write the check first** — `grep -rnE 'class="[^"]*\b(btn|form-control|form-select|form-label|alert|list-group|col-auto|row|d-flex|text-muted|navbar|nav-link|nav-item|dropdown-menu|table)\b' src/Booking.Web/Components; ls src/Booking.Web/wwwroot/lib` → Expected now: matches/listing found (FAIL).
- [ ] **Step 2: Implement** the files above; remove any Bootstrap class the grep finds.
- [ ] **Step 3: Run the Step 1 check again** → no matches, `lib` gone.
- [ ] **Step 4: Build + tests** → `dotnet build` 0 warnings, `dotnet test tests/Booking.Core.Tests` all pass.
- [ ] **Step 5: Final click-through in Docker** — `docker compose up --build -d`; at http://localhost:8080 repeat Task 2 Step 3, Task 3 Step 3 and Task 4 Step 3 quickly; open `/does-not-exist` → dark Not Found page; `docker compose stop app` while a page is open → dark reconnect dialog; `docker compose start app`. No console errors.
- [ ] **Step 6: Commit** — `git commit -m "feat(ui): restyle remaining pages and drop Bootstrap"`
