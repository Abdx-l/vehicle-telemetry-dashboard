# Vehicle Telemetry Dashboard

ASP.NET Core/EF Core fleet ingestion, deterministic 50-vehicle simulator, trip start/idle closure, Haversine distance, duplicate rejection, and a React/Leaflet fleet map. SQLite is used for a portable local run.

## Run

`dotnet test Tests/Tests.csproj`; start API with `dotnet run --urls http://localhost:5000`, then another terminal: `dotnet run -- --simulate`. UI: `cd web && npm install && npm run dev`.
