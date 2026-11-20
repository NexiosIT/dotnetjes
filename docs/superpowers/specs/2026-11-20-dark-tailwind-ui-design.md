# Dark & bold UI with Tailwind — design

**Date:** 2026-11-20 · **Status:** approved · **Branch:** `feat/dark-ui`

## Intent

Make the app look polished for daily use and for the demo, by restyling every page in the "Dark & bold" direction with Tailwind CSS. The person asked for a fancier UI on Tailwind and handed the rest of the calls to Claude; they picked direction C from three mockups and approved the admin mockup.

Mockups (open in a browser; they load Tailwind 3 from a CDN only for display):
- `docs/superpowers/specs/mockups/dark-ui-book.html` — direction **C, "Dark & bold"** (right-hand column) is the one chosen. A and B are the rejected directions.
- `docs/superpowers/specs/mockups/dark-ui-admin.html` — admin pages.

## Goals

- Every page (Book, Admin · Desks & parking, Admin · People, the header picker, error/not-found pages, the reconnect dialog) in the Dark & bold style.
- Tailwind is the only CSS framework; Bootstrap and the template's leftover CSS are removed.
- Works at phone width (≈375 px) and on desktop.
- `dotnet build`, `dotnet run` and `docker compose up --build` keep working with no extra manual step.

## Non-goals

- New features or behaviour changes. Booking rules, admin rules, messages and the email are untouched.
- A light theme or a theme switch.
- The office-map idea (direction B): the app has no desk positions.
- Restyling the summary email.

## Anti-goals (a failure even if it looks good)

- Any behaviour regression: Book, Cancel, day navigation, the picker, add/edit/toggle in admin, "Send today's summary now".
- Styles silently missing in Docker or after a clean clone (e.g. generated CSS not rebuilt).
- Losing the guards that exist today: disabled buttons while busy, the double-click guard on the summary button, readable validation and booking messages.

## Constraints

- .NET 10 Blazor Web App, Interactive Server, prerender off (unchanged).
- Docker runs a chiseled ASP.NET image: no shell, no Node at runtime. The SDK image builds.
- Team machines are Windows; Docker build is Linux x64.
- Files touched belong to others: `Day.razor`, `SlotRow.razor`, `UserPicker.razor` (Person B) and the shared layout. Person B is told before work starts. Markup changes only — no changes to their `@code` logic beyond what styling needs.

## Approaches considered

1. **Tailwind standalone CLI run by MSBuild — chosen.** A target in `Booking.Web.csproj` downloads a pinned Tailwind v4 standalone binary for the current OS (cached under `obj/` or a tools folder, git-ignored) and generates `wwwroot/app.css` before build. Works the same in `dotnet build`, in Visual Studio/Rider and in the Docker SDK stage. Cost: first build needs internet (it already does for NuGet).
2. **Generate CSS by hand and commit it.** No build change, but easy to forget after editing classes → stale styles. Rejected.
3. **Tailwind Play CDN script in the page.** Zero setup, but meant for prototyping, needs internet at runtime and shows a flash of unstyled content. Rejected.

## The design

**Look** (from the mockups): `slate-950` background, `slate-100` text; panels `bg-white/5` with `ring-1 ring-white/10`, rounded-xl; accent gradient fuchsia-400 → cyan-300 for the logo and the summary button; fuchsia for desks and primary actions, cyan for parking; emerald for "free"/success; rose for errors.

**Layout.** The template sidebar is removed. A top bar holds the gradient "desk&parking" wordmark, links **Book** and **Admin** (active one highlighted, Admin active on both admin pages), and the picker pill "Anna ▾" on the right. Content is centred with a max width; single column on phones.

**Book page.**
- Header: small weekday above a large date ("Friday" / "20 Nov"), round ‹ › buttons, disabled outside the bookable range as today.
- Two counters: "5/8 desks free", "2/4 parking free" (counted from the day's slots).
- "Pick who you are to book." prompt and booking messages as banners (info / rose).
- Desks and Parking lists: rows with code, name, and either a green "free" pill + **Book**, "You" highlighted (fuchsia tint for desks, cyan for parking) + **Cancel**, or the booker's name dimmed. On desktop the two lists sit side by side; on phones they stack.
- "In the office: …" line kept, styled.

**Picker.** Pill button; dropdown panel in the dark style; current person highlighted; closes on outside click as today.

**Admin pages.** As in the admin mockup: page title, the summary button (gradient) with its result text beside it, pill tabs (Desks & parking | People), the add form in a panel, rose validation banner, table with type pills, an Active toggle that looks like a switch (still a checkbox underneath for accessibility), Edit/Save/Cancel buttons.

**Other pages.** Error and NotFound pages and the reconnect dialog restyled to match.

## Calls left to the builder

- The exact Tailwind v4 version (pin one that is at least two weeks old) and where the binary is cached.
- Whether Tailwind's input file lives at `src/Booking.Web/Styles/app.css` or similar, and whether generated `wwwroot/app.css` is git-ignored (preferred: yes, generated on build).
- Small class-level choices where the mockups are silent (focus rings, hover states, spacing on phones).
- How to verify: build + tests + a click-through in a browser at desktop and phone width.
