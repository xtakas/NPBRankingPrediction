using NpbRankingPrediction.Core.DataAccess;
using NpbRankingPrediction.Core.DataFiles;
using NpbRankingPrediction.Core.Scoring;
using NpbRankingPrediction.Core.Standings;
using NpbRankingPrediction.Scraper;

var options = CliOptions.Parse(args);

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NpbRankingPredictionScraper/1.0 (+personal, non-commercial use)");

var scraper = new NpbScheduleScraper(httpClient);
var dataStore = new JsonFileDataStore(options.DataDirectory);

Console.WriteLine($"season={options.Season} months={string.Join(',', options.Months)} backfill={options.Backfill} finalize={options.Finalize} dataDir={options.DataDirectory}");

var seasonsBefore = await dataStore.LoadSeasonsAsync();
var existingSeasonInfo = seasonsBefore.Seasons.FirstOrDefault(s => s.Season == options.Season);

if (existingSeasonInfo is { IsFinal: true } && !options.Finalize)
{
    Console.WriteLine($"{options.Season}年シーズンは既に確定済みのため更新をスキップしました(再取得する場合は --finalize を付けて実行してください)。");
    return;
}

var existing = await dataStore.LoadGamesAsync(options.Season);
var mergedById = existing.Games
    .Where(g => g.GameId is not null)
    .ToDictionary(g => g.GameId!);

foreach (var month in options.Months)
{
    Console.WriteLine($"Fetching schedule_{month:D2}_detail.html ...");
    var monthGames = await scraper.FetchMonthAsync(options.Season, month);
    foreach (var game in monthGames.Where(g => g.GameId is not null))
    {
        mergedById[game.GameId!] = game;
    }

    Console.WriteLine($"  -> {monthGames.Count} completed games found");
}

var gamesFile = new GamesFile(
    options.Season,
    mergedById.Values.OrderBy(g => g.Date).ThenBy(g => g.GameId, StringComparer.Ordinal).ToList());
await dataStore.SaveGamesAsync(gamesFile);
Console.WriteLine($"games.json updated: {gamesFile.Games.Count} games total");

var standingsSnapshots = StandingsCalculator.Calculate(gamesFile.Games);
var standingsFile = new StandingsFile(options.Season, standingsSnapshots);
await dataStore.SaveStandingsAsync(standingsFile);
Console.WriteLine($"standings.json updated: {standingsFile.Snapshots.Count} daily snapshots");

var predictions = await dataStore.LoadPredictionsAsync(options.Season);
if (predictions is null)
{
    Console.WriteLine("predictions.json が未作成のため scores.json の更新はスキップしました。");
}
else
{
    var scoreSnapshots = ScoringCalculator.Calculate(standingsFile, predictions);
    var scoresFile = new ScoresFile(options.Season, scoreSnapshots);
    await dataStore.SaveScoresAsync(scoresFile);
    Console.WriteLine($"scores.json updated: {scoresFile.Snapshots.Count} daily snapshots");
}

var isFinal = options.Finalize || (existingSeasonInfo?.IsFinal ?? false);
var updatedSeasonInfo = new SeasonInfo(options.Season, isFinal, DateTimeOffset.UtcNow);
var updatedSeasons = seasonsBefore.Seasons
    .Where(s => s.Season != options.Season)
    .Append(updatedSeasonInfo)
    .OrderByDescending(s => s.Season)
    .ToList();
await dataStore.SaveSeasonsAsync(new SeasonsFile(updatedSeasons));
Console.WriteLine($"seasons.json updated: {string.Join(',', updatedSeasons.Select(s => $"{s.Season}({(s.IsFinal ? "確定" : "進行中")})"))}");

internal sealed record CliOptions(int Season, IReadOnlyList<int> Months, bool Backfill, bool Finalize, string DataDirectory)
{
    public static CliOptions Parse(string[] args)
    {
        var season = DateTime.Now.Year;
        var backfill = false;
        var finalize = false;
        var dataDirectory = "data";
        int? fromMonth = null;
        int? toMonth = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--season":
                    season = int.Parse(args[++i]);
                    break;
                case "--backfill":
                    backfill = true;
                    break;
                case "--finalize":
                    finalize = true;
                    break;
                case "--data-dir":
                    dataDirectory = args[++i];
                    break;
                case "--from-month":
                    fromMonth = int.Parse(args[++i]);
                    break;
                case "--to-month":
                    toMonth = int.Parse(args[++i]);
                    break;
                default:
                    throw new ArgumentException($"unknown argument: {args[i]}");
            }
        }

        var currentMonth = DateTime.Now.Month;
        List<int> months;
        if (fromMonth.HasValue || toMonth.HasValue)
        {
            months = Enumerable.Range(fromMonth ?? 3, (toMonth ?? currentMonth) - (fromMonth ?? 3) + 1).ToList();
        }
        else if (backfill)
        {
            // NPBのレギュラーシーズンは例年3月開幕。開幕月〜現在の月まで遡って取得する。
            months = Enumerable.Range(3, Math.Max(1, currentMonth - 3 + 1)).ToList();
        }
        else
        {
            months = [currentMonth];
        }

        return new CliOptions(season, months, backfill, finalize, dataDirectory);
    }
}
