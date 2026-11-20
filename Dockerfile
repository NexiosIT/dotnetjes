FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so package downloads are cached between builds.
COPY global.json ./
COPY src/Booking.Domain/Booking.Domain.csproj src/Booking.Domain/
COPY src/Booking.Application/Booking.Application.csproj src/Booking.Application/
COPY src/Booking.Infrastructure/Booking.Infrastructure.csproj src/Booking.Infrastructure/
COPY src/Booking.Web/Booking.Web.csproj src/Booking.Web/
RUN dotnet restore src/Booking.Web/Booking.Web.csproj

COPY src/ src/
# No --no-restore: the restore above runs before the .razor files exist, so it skips the package
# that ships _framework/blazor.web.js (white screen). Publish restores again with all sources present.
RUN dotnet publish src/Booking.Web/Booking.Web.csproj -c Release -o /app
# The runtime image has no shell, so create the data-protection keys folder here.
RUN mkdir /keys

# Chiseled: no shell or package manager, runs as non-root ($APP_UID).
# "-extra" adds tzdata + ICU — the app needs tzdata for FindSystemTimeZoneById("Europe/Brussels").
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .
# Owned by the app user so a fresh named volume mounted here is writable too.
COPY --from=build --chown=$APP_UID /keys /keys
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booking.Web.dll"]
