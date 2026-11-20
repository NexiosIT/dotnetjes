FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so package downloads are cached between builds.
COPY global.json ./
COPY src/Booking.Core/Booking.Core.csproj src/Booking.Core/
COPY src/Booking.Web/Booking.Web.csproj src/Booking.Web/
RUN dotnet restore src/Booking.Web/Booking.Web.csproj

COPY src/ src/
RUN dotnet publish src/Booking.Web/Booking.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booking.Web.dll"]
