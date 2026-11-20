# dotnetjes

## Styling (Tailwind CSS)

The UI uses Tailwind CSS v4. `dotnet build` compiles `src/Booking.Web/Styles/app.css` into `wwwroot/app.css` (generated, not committed) with the Tailwind standalone CLI, so no Node is needed.

- The first build downloads the CLI (~110 MB) into `obj/tailwind/`, so it needs internet. Pass `-p:TailwindCli=<path>` to use one that is already installed.
- Hot reload does not run the build: after adding new Tailwind classes, rebuild to get their CSS.
