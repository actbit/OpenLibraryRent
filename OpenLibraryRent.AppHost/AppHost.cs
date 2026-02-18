var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("openlibraryrent-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("openlibraryrent-db");

builder.AddProject<Projects.OpenLibraryRent>("openlibraryrent")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
