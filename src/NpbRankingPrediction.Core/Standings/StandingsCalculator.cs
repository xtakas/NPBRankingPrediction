using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.Standings;

public static class StandingsCalculator
{
    /// <summary>
    /// 試合結果ログから、試合が行われた日付ごとの累積成績・順位スナップショットを算出する。
    /// </summary>
    public static List<StandingsSnapshot> Calculate(IEnumerable<GameResult> games)
    {
        var playedGames = games.Where(g => g.HasResult).ToList();

        var record = TeamCatalog.All.ToDictionary(t => t.Code, _ => (Wins: 0, Losses: 0, Draws: 0));

        var snapshots = new List<StandingsSnapshot>();

        foreach (var dateGroup in playedGames.GroupBy(g => g.Date).OrderBy(g => g.Key))
        {
            foreach (var game in dateGroup)
            {
                ApplyResult(record, game);
            }

            snapshots.Add(BuildSnapshot(dateGroup.Key, record));
        }

        return snapshots;
    }

    private static void ApplyResult(Dictionary<string, (int Wins, int Losses, int Draws)> record, GameResult game)
    {
        if (game.IsDraw)
        {
            record[game.HomeTeamCode] = record[game.HomeTeamCode] with { Draws = record[game.HomeTeamCode].Draws + 1 };
            record[game.AwayTeamCode] = record[game.AwayTeamCode] with { Draws = record[game.AwayTeamCode].Draws + 1 };
            return;
        }

        var winner = game.WinnerTeamCode!;
        var loser = game.LoserTeamCode!;
        record[winner] = record[winner] with { Wins = record[winner].Wins + 1 };
        record[loser] = record[loser] with { Losses = record[loser].Losses + 1 };
    }

    private static StandingsSnapshot BuildSnapshot(DateOnly date, Dictionary<string, (int Wins, int Losses, int Draws)> record)
    {
        return new StandingsSnapshot(
            date,
            Rank(TeamCatalog.CentralTeams, record),
            Rank(TeamCatalog.PacificTeams, record));
    }

    // NPBの実際の順位決定は同率時に規定回避重複対戦成績などの細かい規約があるが、
    // 本ツールでは勝率→勝数→チームCodeの単純な比較で暫定順位を出す。
    private static List<StandingsEntry> Rank(IReadOnlyList<Team> teams, Dictionary<string, (int Wins, int Losses, int Draws)> record)
    {
        var ordered = teams
            .Select(t =>
            {
                var (wins, losses, draws) = record[t.Code];
                var decidedGames = wins + losses;
                var winPercentage = decidedGames == 0 ? 0.0 : (double)wins / decidedGames;
                return (Team: t, Wins: wins, Losses: losses, Draws: draws, WinPercentage: winPercentage);
            })
            .OrderByDescending(x => x.WinPercentage)
            .ThenByDescending(x => x.Wins)
            .ThenBy(x => x.Team.Code, StringComparer.Ordinal)
            .ToList();

        var leader = ordered[0];

        return ordered
            .Select((x, index) =>
            {
                // ゲーム差 = ((首位の勝数 - 自チームの勝数) + (自チームの敗数 - 首位の敗数)) / 2
                var gamesBehind = ((leader.Wins - x.Wins) + (x.Losses - leader.Losses)) / 2.0;
                return new StandingsEntry(x.Team.Code, index + 1, x.Wins, x.Losses, x.Draws, x.WinPercentage, gamesBehind);
            })
            .ToList();
    }
}
