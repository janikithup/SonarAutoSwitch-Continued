using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sonar.AutoSwitch.Services.Win32;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Services;

public class SteelSeriesSonarService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _connectionString;
    private int? _lastWorkingPort;

    public SteelSeriesSonarService()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"SteelSeries\GG\apps\sonar\db\database.db")
        }.ToString();
    }

    public static SteelSeriesSonarService Instance { get; } = new();

    public IEnumerable<SonarGamingConfiguration> AvailableGamingConfigurations =>
        GetGamingConfigurations().OrderBy(s => s.Name);

    public IEnumerable<SonarGamingConfiguration> GetGamingConfigurations()
    {
        // Get all the available profiles from SQLite
        using var sqliteConnection = new SqliteConnection(_connectionString);
        sqliteConnection.Open();

        using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "select id, name, vad from configs where vad == 1";
        using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
        while (sqliteDataReader.Read())
        {
            string id = sqliteDataReader.GetString(0);
            string name = sqliteDataReader.GetString(1);
            yield return new SonarGamingConfiguration(id, name);
        }
    }

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sonar.AutoSwitch", "debug.log");

    private static void Log(string message)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); }
        catch { }
    }

    public async Task ChangeSelectedGamingConfiguration(SonarGamingConfiguration sonarGamingConfiguration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sonarGamingConfiguration.Id))
            return;

        var sw = Stopwatch.StartNew();
        Process[] processesByName = Process.GetProcessesByName("SteelSeriesSonar");
        if (processesByName.Length <= 0 || cancellationToken.IsCancellationRequested)
            return;

        IEnumerable<int> potentialPorts = processesByName.SelectMany(p => NetworkHelper.GetPortById(p.Id, false));
        Log($"PortScan: {sw.ElapsedMilliseconds}ms, cachedPort={_lastWorkingPort?.ToString() ?? "none"}");

        potentialPorts = _lastWorkingPort != null ? potentialPorts.Prepend(_lastWorkingPort.Value) : potentialPorts;

        bool switched = false;
        foreach (int potentialPort in potentialPorts.Distinct())
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            var putSw = Stopwatch.StartNew();
            HttpResponseMessage? httpResponseMessage = await _httpClient.PutAsync(
                $"http://localhost:{potentialPort}/configs/{sonarGamingConfiguration.Id}/select",
                new StringContent(""),
                cancellationToken).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null);
            Log($"PUT :{potentialPort} → {httpResponseMessage?.StatusCode.ToString() ?? "null"} [{putSw.ElapsedMilliseconds}ms]");
            if (httpResponseMessage?.StatusCode == HttpStatusCode.OK)
            {
                _lastWorkingPort = potentialPort;
                switched = true;
                break;
            }
        }
        if (!switched)
            _lastWorkingPort = null;
        Log($"ChangeConfig: {(switched ? "ok" : "failed")} [{sw.ElapsedMilliseconds}ms total]");
    }

    public string GetSelectedGamingConfiguration()
    {
        // Get all the available profiles from SQLite
        using var sqliteConnection = new SqliteConnection(_connectionString);
        sqliteConnection.Open();

        using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "select config_id, vad from selected_config where vad == 1";
        using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
        if (!sqliteDataReader.Read())
            throw new InvalidOperationException("Unable to check for selected gaming profile");
        return sqliteDataReader.GetString(0);
    }
}