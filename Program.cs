using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

string connectionString = "Data Source=licenseKeys.db";

// Tạo bảng nếu chưa tồn tại
using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = @"CREATE TABLE IF NOT EXISTS Licenses (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        LicenseKey TEXT UNIQUE NOT NULL,
        TimeExpireDaily TEXT NOT NULL,
        TimeExpire200v TEXT NOT NULL,
        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
    );";
    command.ExecuteNonQuery();
}

app.MapGet("/validate/{licenseKey}", (string licenseKey) =>
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Name, TimeExpireDaily FROM Licenses WHERE LicenseKey = @licenseKey";
    command.Parameters.AddWithValue("@licenseKey", licenseKey);

    using var reader = command.ExecuteReader();
    if (reader.Read())
    {
        return Results.Json(new { Name = reader.GetString(0), TimeExpire = reader.GetString(1) });
    }
    return Results.Json(null);
});

app.MapGet("/list", () =>
{
    var licenses = new List<License>();
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Name, LicenseKey, TimeExpireDaily, TimeExpire200v FROM Licenses";
    
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        licenses.Add(new License(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
    return Results.Json(licenses);
});

app.MapGet("/count", () =>
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM Licenses";
    
    int count = Convert.ToInt32(command.ExecuteScalar());
    return Results.Json(new { TotalLicenses = count });
});

app.MapGet("/search/{name}", (string name) =>
{
    var licenses = new List<License>();
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Name, LicenseKey, TimeExpireDaily, TimeExpire200v FROM Licenses WHERE Name LIKE @name";
    command.Parameters.AddWithValue("@name", "%" + name + "%");
    
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        licenses.Add(new License(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
    return Results.Json(licenses);
});

app.MapGet("/filterByExpireDate/{date}", (string date) =>
{
    var licenses = new List<License>();
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT Name, LicenseKey, TimeExpireDaily, TimeExpire200v FROM Licenses WHERE TimeExpireDaily = @date";
    command.Parameters.AddWithValue("@date", date);
    
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        licenses.Add(new License(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
    return Results.Json(licenses);
});

app.MapGet("/isValid/{licenseKey}", (string licenseKey) =>
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT TimeExpireDaily FROM Licenses WHERE LicenseKey = @licenseKey";
    command.Parameters.AddWithValue("@licenseKey", licenseKey);

    using var reader = command.ExecuteReader();
    if (reader.Read())
    {
        DateTime expireDate = DateTime.Parse(reader.GetString(0));
        return Results.Json(new { IsValid = expireDate > DateTime.UtcNow });
    }
    return Results.Json(new { IsValid = false });
});

app.MapPost("/add", (License license) =>
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO Licenses (Name, LicenseKey, TimeExpireDaily, TimeExpire200v) VALUES (@name, @licenseKey, @timeExpireDaily, @timeExpire200v)";
    command.Parameters.AddWithValue("@name", license.Name);
    command.Parameters.AddWithValue("@licenseKey", license.LicenseKey);
    command.Parameters.AddWithValue("@timeExpireDaily", license.TimeExpireDaily);
    command.Parameters.AddWithValue("@timeExpire200v", license.TimeExpire200v);
    
    try
    {
        command.ExecuteNonQuery();
        return Results.Ok("License added successfully");
    }
    catch (Exception ex)
    {
        return Results.Problem("Error adding license: " + ex.Message);
    }
});

app.UseSwagger();
app.UseSwaggerUI();
app.Run();

public record License(string Name, string LicenseKey, string TimeExpireDaily, string TimeExpire200v);
