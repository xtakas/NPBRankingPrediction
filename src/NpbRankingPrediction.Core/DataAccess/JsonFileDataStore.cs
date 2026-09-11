using System.Text.Json;
using NpbRankingPrediction.Core.DataFiles;

namespace NpbRankingPrediction.Core.DataAccess;

public sealed class JsonFileDataStore(string dataRootDirectory) : IDataStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<GamesFile> LoadGamesAsync(int season, CancellationToken ct = default)
        => await LoadAsync<GamesFile>(GamesPath(season), ct) ?? new GamesFile(season, []);

    public Task SaveGamesAsync(GamesFile games, CancellationToken ct = default)
        => SaveAsync(GamesPath(games.Season), games, ct);

    public async Task<StandingsFile> LoadStandingsAsync(int season, CancellationToken ct = default)
        => await LoadAsync<StandingsFile>(StandingsPath(season), ct) ?? new StandingsFile(season, []);

    public Task SaveStandingsAsync(StandingsFile standings, CancellationToken ct = default)
        => SaveAsync(StandingsPath(standings.Season), standings, ct);

    public Task<PredictionsFile?> LoadPredictionsAsync(int season, CancellationToken ct = default)
        => LoadAsync<PredictionsFile>(PredictionsPath(season), ct);

    public async Task<ScoresFile> LoadScoresAsync(int season, CancellationToken ct = default)
        => await LoadAsync<ScoresFile>(ScoresPath(season), ct) ?? new ScoresFile(season, []);

    public Task SaveScoresAsync(ScoresFile scores, CancellationToken ct = default)
        => SaveAsync(ScoresPath(scores.Season), scores, ct);

    public async Task<SeasonsFile> LoadSeasonsAsync(CancellationToken ct = default)
        => await LoadAsync<SeasonsFile>(SeasonsPath(), ct) ?? new SeasonsFile([]);

    public Task SaveSeasonsAsync(SeasonsFile seasons, CancellationToken ct = default)
        => SaveAsync(SeasonsPath(), seasons, ct);

    private string SeasonsPath() => Path.Combine(dataRootDirectory, "seasons.json");

    private string SeasonDir(int season) => Path.Combine(dataRootDirectory, season.ToString());

    private string GamesPath(int season) => Path.Combine(SeasonDir(season), "games.json");

    private string StandingsPath(int season) => Path.Combine(SeasonDir(season), "standings.json");

    private string PredictionsPath(int season) => Path.Combine(SeasonDir(season), "predictions.json");

    private string ScoresPath(int season) => Path.Combine(SeasonDir(season), "scores.json");

    private static async Task<T?> LoadAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
    }

    private static async Task SaveAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Options, ct);
    }
}
