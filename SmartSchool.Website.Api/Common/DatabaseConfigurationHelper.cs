using System;
using Npgsql;

namespace SmartSchool.Website.Api.Common;

public static class DatabaseConfigurationHelper
{
    /// <summary>
    /// Parses either a standard ADO.NET connection string or a postgresql:// URI into a valid Npgsql connection string.
    /// </summary>
    public static string ResolveConnectionString(string? rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        rawConnectionString = rawConnectionString.Trim();

        if (!rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return rawConnectionString;
        }

        var uri = new Uri(rawConnectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(database))
        {
            database = "postgres";
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            Database = database,
            SslMode = SslMode.Require
        };

        // Parse query parameters if any (e.g. ?sslmode=require)
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var query = uri.Query.TrimStart('?');
            var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    var key = kv[0].Trim().ToLowerInvariant();
                    var val = kv[1].Trim().ToLowerInvariant();
                    if (key == "sslmode")
                    {
                        if (Enum.TryParse<SslMode>(val, true, out var sslMode))
                        {
                            builder.SslMode = sslMode;
                        }
                    }
                }
            }
        }

        return builder.ConnectionString;
    }
}
