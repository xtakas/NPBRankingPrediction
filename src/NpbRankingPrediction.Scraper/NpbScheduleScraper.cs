using HtmlAgilityPack;
using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Scraper;

/// <summary>
/// npb.jp の月別試合結果ページ(https://npb.jp/games/{season}/schedule_{month}_detail.html)を
/// パースし、その月に確定した試合結果一覧を返す。
/// </summary>
public sealed class NpbScheduleScraper(HttpClient httpClient)
{
    public async Task<List<GameResult>> FetchMonthAsync(int season, int month, CancellationToken ct = default)
    {
        var url = $"https://npb.jp/games/{season}/schedule_{month:D2}_detail.html";
        var html = await httpClient.GetStringAsync(url, ct);
        return Parse(html, season);
    }

    internal static List<GameResult> Parse(string html, int season)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//div[@id='schedule_detail']//table/tbody/tr");
        var games = new List<GameResult>();
        if (rows is null)
        {
            return games;
        }

        DateOnly? currentDate = null;
        var syntheticIdSequenceByMatchup = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var dateHeader = row.SelectSingleNode("./th");
            if (dateHeader is not null)
            {
                // 表記ゆれ等でパースに失敗した行は日付を更新せず(直前の日付を維持したまま)この行だけ
                // スキップする。これにより月内の1行の異常で全体の取得が落ちることを防ぐ。
                var parsedDate = TryParseDate(dateHeader.InnerText, season);
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
            if (row.SelectSingleNode(".//div[@class='cancel']") is not null)
            {
                continue;
            }

            var team1 = row.SelectSingleNode(".//div[@class='team1']")?.InnerText.Trim();
            var team2 = row.SelectSingleNode(".//div[@class='team2']")?.InnerText.Trim();
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
            var score1Text = row.SelectSingleNode(".//div[@class='score1']")?.InnerText.Trim();
            var score2Text = row.SelectSingleNode(".//div[@class='score2']")?.InnerText.Trim();
            if (!int.TryParse(score1Text, out var score1) || !int.TryParse(score2Text, out var score2))
            {
                continue;
            }

            var gameId = row.SelectSingleNode(".//a[contains(@href,'/scores/')]")
                ?.GetAttributeValue("href", null)
                ?.Trim('/');

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
