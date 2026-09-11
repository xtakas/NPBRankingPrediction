using NpbRankingPrediction.Core.DataAccess;
using NpbRankingPrediction.Core.DataFiles;
using NpbRankingPrediction.Core.Models;
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

var todayJst = NpbScheduleScraper.TodayJst;
if (options.IsDefaultRun && existingSeasonInfo?.LastFullyFinishedDate == todayJst)
{
    // 21〜24時台に複数回cronを回す運用を想定し、当日分がすでに「全試合終了」と確認済みなら
    // npb.jpへは一切アクセスしない(--backfill/--from-month等の明示的な実行では常に取得する)。
    Console.WriteLine($"{todayJst}分は既に全試合終了として記録済みのため、npb.jpへのアクセスをスキップします。");
    var skippedSeasonInfo = existingSeasonInfo with { LastScrapedAtUtc = DateTimeOffset.UtcNow };
    var skippedSeasons = seasonsBefore.Seasons
        .Where(s => s.Season != options.Season)
        .Append(skippedSeasonInfo)
        .OrderByDescending(s => s.Season)
        .ToList();
    await dataStore.SaveSeasonsAsync(new SeasonsFile(skippedSeasons));
    return;
}

var existing = await dataStore.LoadGamesAsync(options.Season);
var mergedById = existing.Games
    .Where(g => g.GameId is not null)
    .ToDictionary(g => g.GameId!);

foreach (var month in options.Months)
{
    Console.WriteLine($"Fetching schedule_{month:D2}_detail.html ...");

    List<GameResult> monthGames;
    try
    {
        monthGames = await scraper.FetchMonthAsync(options.Season, month);
    }
    catch (HttpRequestException ex)
    {
        // オフシーズンの月(まだページが存在しない等)や一時的な通信障害でも、他の月の取得や
        // lastScrapedAtUtcの更新まで止めたくないため、この月だけスキップして継続する。
        Console.WriteLine($"  -> 取得に失敗したためこの月をスキップします: {ex.Message}");
        continue;
    }

    foreach (var game in monthGames.Where(g => g.GameId is not null))
    {
        mergedById[game.GameId!] = game;
    }

    Console.WriteLine($"  -> {monthGames.Count} completed games found");
}

// 月別ページ(schedule_MM_detail.html)は当日分の反映に数時間〜翌日までタイムラグがあるため、
// npb.jpの「本日の試合速報」ウィジェットから当日分を追加で取りにいく。その日の全試合が
// 「試合終了」(または中止)になっているときだけ反映し、試合途中の暫定結果は順位表に混ぜない。
Console.WriteLine("Fetching today's live scoreboard ...");
LiveScoreboardResult? live = null;
try
{
    live = await scraper.FetchTodayLiveGamesAsync();
    if (live.AllGamesFinished)
    {
        foreach (var game in live.Games.Where(g => g.GameId is not null))
        {
            mergedById[game.GameId!] = game;
        }

        Console.WriteLine($"  -> {live.Games.Count} game(s) finished today ({live.Date}), merged");
    }
    else
    {
        Console.WriteLine($"  -> today's games ({live.Date}) are not all finished yet, skipping for now");
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"  -> failed to fetch live scoreboard, skipping: {ex.Message}");
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
var lastFullyFinishedDate = live?.AllGamesFinished == true
    ? live.Date
    : existingSeasonInfo?.LastFullyFinishedDate;
var updatedSeasonInfo = new SeasonInfo(options.Season, isFinal, DateTimeOffset.UtcNow, lastFullyFinishedDate);
var updatedSeasons = seasonsBefore.Seasons
    .Where(s => s.Season != options.Season)
    .Append(updatedSeasonInfo)
    .OrderByDescending(s => s.Season)
    .ToList();
await dataStore.SaveSeasonsAsync(new SeasonsFile(updatedSeasons));
Console.WriteLine($"seasons.json updated: {string.Join(',', updatedSeasons.Select(s => $"{s.Season}({(s.IsFinal ? "確定" : "進行中")})"))}");

internal sealed record CliOptions(int Season, IReadOnlyList<int> Months, bool Backfill, bool Finalize, string DataDirectory, bool IsDefaultRun)
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
        var isDefaultRun = !backfill && !fromMonth.HasValue && !toMonth.HasValue;
        List<int> months;
        if (fromMonth.HasValue || toMonth.HasValue)
        {
            var effectiveFrom = fromMonth ?? 3;
            var effectiveTo = toMonth ?? currentMonth;
            if (effectiveTo < effectiveFrom)
            {
                throw new ArgumentException($"--to-month ({effectiveTo}) must not be less than --from-month ({effectiveFrom}).");
            }

            months = Enumerable.Range(effectiveFrom, effectiveTo - effectiveFrom + 1).ToList();
        }
        else if (backfill)
        {
            // NPBのレギュラーシーズンは例年3月開幕。開幕月〜現在の月まで遡って取得する。
            months = Enumerable.Range(3, Math.Max(1, currentMonth - 3 + 1)).ToList();
        }
        else
        {
            // 月初にcronが走った直後は前月末の試合がまだ未取得のことがあるため、前月分も併せて取得する。
            // 既存games.jsonとはgameIdでマージされるので、重複取得しても安全(冪等)。
            var startMonth = Math.Max(3, currentMonth - 1);
            months = Enumerable.Range(startMonth, currentMonth - startMonth + 1).ToList();
        }

        return new CliOptions(season, months, backfill, finalize, dataDirectory, isDefaultRun);
    }
}
