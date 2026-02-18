var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("openlibraryrent-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("openlibraryrent-db");

// Redis（オプション - コメントアウトを外すと有効化）
// var redis = builder.AddRedis("redis")
//     .WithDataVolume("openlibraryrent-redis-data");

var app = builder.AddProject<Projects.OpenLibraryRent>("openlibraryrent")
    .WithReference(database)
    .WaitFor(database);

// Redis有効化時に参照を追加
// app.WithReference(redis);

builder.Build().Run();
