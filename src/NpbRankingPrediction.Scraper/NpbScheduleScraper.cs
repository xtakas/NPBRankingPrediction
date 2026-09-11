using AngleSharp.Html.Parser;
using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Scraper;

/// <summary>
/// npb.jp の月別試合結果ページ(https://npb.jp/games/{season}/schedule_{month}_detail.html)を
/// パースし、その月に確定した試合結果一覧を返す。
/// </summary>
public sealed class NpbScheduleScraper(HttpClient httpClient)
{
    private static readonly HtmlParser Parser = new();

    public async Task<List<GameResult>> FetchMonthAsync(int season, int month, CancellationToken ct = default)
    {
        var url = $"https://npb.jp/games/{season}/schedule_{month:D2}_detail.html";
        var html = await httpClient.GetStringAsync(url, ct);
        return Parse(html, season);
    }

    /// <summary>
    /// npb.jpの全ページに埋め込まれている「本日の試合速報」ウィジェット(#header_score)から、
    /// 月別ページより速く反映される当日の結果を取得する。月別ページ(schedule_MM_detail.html)は
    /// 更新に数時間〜翌日までのタイムラグがあるため、その間を埋める用途。
    /// </summary>
    public async Task<LiveScoreboardResult> FetchTodayLiveGamesAsync(CancellationToken ct = default)
    {
        var html = await httpClient.GetStringAsync("https://npb.jp/", ct);
        return ParseLiveScoreboard(html);
    }

    internal static LiveScoreboardResult ParseLiveScoreboard(string html)
    {
        var document = Parser.ParseDocument(html);
        var today = TodayJst;

        // 実際の対戦カード(href="/scores/2026/0911/xxx/")だけを拾い、日付ボックス(アンカー無し)や
        // 「もっと見る」リンク(href="/scores/"だけで対戦カードを含まない)を除外する。
        var gameAnchors = document.QuerySelectorAll("#header_score .score_box a[href^='/scores/20']");
        if (gameAnchors.Length == 0)
        {
            // ウィジェット自体が見つからない(ページ構造の変更等)場合は安全側に倒し、何も反映しない。
            return new LiveScoreboardResult(today, [], AllGamesFinished: false);
        }

        var games = new List<GameResult>();
        var allGamesFinished = true;

        foreach (var anchor in gameAnchors)
        {
            var logos = anchor.QuerySelectorAll("img");
            if (logos.Length < 2)
            {
                continue;
            }

            var team1 = TeamCatalog.FindByFullName(logos[0].GetAttribute("alt") ?? "");
            var team2 = TeamCatalog.FindByFullName(logos[1].GetAttribute("alt") ?? "");
            if (team1 is null || team2 is null)
            {
                continue;
            }

            var stateText = anchor.QuerySelector(".state")?.TextContent ?? "";
            var isFinished = stateText.Contains("試合終了");
            var isPostponed = stateText.Contains("中止") || stateText.Contains("順延");

            if (!isFinished && !isPostponed)
            {
                // まだ試合中(または未開始)の試合が1つでもあれば、その日はまだ全試合終了していないと判断する。
                allGamesFinished = false;
                continue;
            }

            if (isPostponed)
            {
                continue;
            }

            var scoreText = anchor.QuerySelector(".score")?.TextContent.Trim() ?? "";
            var scoreParts = scoreText.Split('-');
            if (scoreParts.Length != 2 || !int.TryParse(scoreParts[0], out var score1) || !int.TryParse(scoreParts[1], out var score2))
            {
                // 「試合終了」なのにスコアが読み取れないのは想定外の形式なので、安全側に倒してこの日を保留する。
                allGamesFinished = false;
                continue;
            }

            var gameId = anchor.GetAttribute("href")?.Trim('/');
            games.Add(new GameResult(today, team1.Code, team2.Code, score1, score2, gameId));
        }

        return new LiveScoreboardResult(today, games, allGamesFinished);
    }

    private static readonly TimeZoneInfo JstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    /// <summary>実行しているマシンのローカル時刻に関わらず、npb.jp基準(JST)の「今日」。</summary>
    public static DateOnly TodayJst => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, JstTimeZone).DateTime);

    internal static List<GameResult> Parse(string html, int season)
    {
        var document = Parser.ParseDocument(html);

        var rows = document.QuerySelectorAll("#schedule_detail table tbody tr");
        var games = new List<GameResult>();
        if (rows.Length == 0)
        {
            return games;
        }

        DateOnly? currentDate = null;
        var syntheticIdSequenceByMatchup = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var dateHeader = row.Children.FirstOrDefault(c => c.LocalName == "th");
            if (dateHeader is not null)
            {
                // 表記ゆれ等でパースに失敗した行は日付を更新せず(直前の日付を維持したまま)この行だけ
                // スキップする。これにより月内の1行の異常で全体の取得が落ちることを防ぐ。
                var parsedDate = TryParseDate(dateHeader.TextContent, season);
                if (parsedDate is null)
                {
                    continue;
                }

                currentDate = parsedDate;
            }

            if (currentDate is null)
            {
                continue;
            }

            // 中止・順延の試合は結果が存在しないためスキップ
            if (row.QuerySelector(".cancel") is not null)
            {
                continue;
            }

            var team1 = row.QuerySelector(".team1")?.TextContent.Trim();
            var team2 = row.QuerySelector(".team2")?.TextContent.Trim();
            if (string.IsNullOrEmpty(team1) || string.IsNullOrEmpty(team2))
            {
                continue;
            }

            var homeTeam = TeamCatalog.FindByCode(team1);
            var awayTeam = TeamCatalog.FindByCode(team2);
            if (homeTeam is null || awayTeam is null)
            {
                continue;
            }

            // 未実施(予定)の試合はscore1/score2が"&nbsp;"のみでスコアが入っていないため自然に除外される
            var score1Text = row.QuerySelector(".score1")?.TextContent.Trim();
            var score2Text = row.QuerySelector(".score2")?.TextContent.Trim();
            if (!int.TryParse(score1Text, out var score1) || !int.TryParse(score2Text, out var score2))
            {
                continue;
            }

            var gameId = row.QuerySelector("a[href*='/scores/']")?.GetAttribute("href")?.Trim('/');

            if (gameId is null)
            {
                // /scores/ リンクが無い試合はgameIdがnullのままだと、Program.cs側のマージ処理
                // (GameId is not null でフィルタ)により永久に無視されてしまう。日付+対戦カード+
                // その日の何試合目かから決定的な合成IDを組み立て、ダブルヘッダーにも対応する。
                var matchupKey = $"{currentDate.Value:yyyyMMdd}-{homeTeam.Code}-{awayTeam.Code}";
                syntheticIdSequenceByMatchup.TryGetValue(matchupKey, out var sequence);
                syntheticIdSequenceByMatchup[matchupKey] = sequence + 1;
                gameId = $"synthetic-{matchupKey}-{sequence}";
            }

            games.Add(new GameResult(currentDate.Value, homeTeam.Code, awayTeam.Code, score1, score2, gameId));
        }

        return games;
    }

    // headerTextは "9/1（火）" のような形式
    private static DateOnly? TryParseDate(string headerText, int season)
    {
        try
        {
            var datePart = headerText.Split('（', '(')[0].Trim();
            var parts = datePart.Split('/');
            var month = int.Parse(parts[0]);
            var day = int.Parse(parts[1]);
            return new DateOnly(season, month, day);
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }
}

/// <summary>
/// AllGamesFinished は当日ウィジェットに載っている全試合が「試合終了」または「中止」で、
/// まだ進行中・未開始の試合が1つも無いかどうか。呼び出し側はこれがtrueのときだけGamesを
/// games.jsonへ反映することで、1日の途中の暫定結果が順位表に混ざらないようにする。
/// </summary>
public sealed record LiveScoreboardResult(DateOnly? Date, List<GameResult> Games, bool AllGamesFinished);
