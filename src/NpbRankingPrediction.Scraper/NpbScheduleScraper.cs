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

        foreach (var row in rows)
        {
            var dateHeader = row.SelectSingleNode("./th");
            if (dateHeader is not null)
            {
                currentDate = ParseDate(dateHeader.InnerText, season);
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

            games.Add(new GameResult(currentDate.Value, homeTeam.Code, awayTeam.Code, score1, score2, gameId));
        }

        return games;
    }

    // headerTextは "9/1（火）" のような形式
    private static DateOnly ParseDate(string headerText, int season)
    {
        var datePart = headerText.Split('（', '(')[0].Trim();
        var parts = datePart.Split('/');
        var month = int.Parse(parts[0]);
        var day = int.Parse(parts[1]);
        return new DateOnly(season, month, day);
    }
}
