EF Core migrations generated via:

    dotnet ef migrations add InitialCreate --project ../../backend/src/ChronosMesh.Infrastructure --startup-project ../../backend/src/ChronosMesh.Api -o ../../database/migrations

land in this folder. `../schema.sql` is the plain-SQL reference kept in
sync with the EF Core model for tooling that isn't .NET.
