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
                var remaining = Math.Max(0, RegularSeasonGameCount - (wins + losses + draws));
                return new TeamTally(t, wins, losses, draws, winPercentage, remaining);
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

                // 自分より上の全チーム・下の全チームそれぞれとの2者間比較で判定する
                // (隣接チームだけでなく、2つ以上離れたチームが割り込んで逆転する可能性も排除する)。
                var isRankConfirmed = true;
                for (var j = 0; j < ordered.Count && isRankConfirmed; j++)
                {
                    if (j == index)
                    {
                        continue;
                    }

                    isRankConfirmed = j < index
                        ? IsBoundaryConfirmed(ordered[j], x)
                        : IsBoundaryConfirmed(x, ordered[j]);
                }

                return new StandingsEntry(x.Team.Code, index + 1, x.Wins, x.Losses, x.Draws, x.WinPercentage, gamesBehind, isRankConfirmed);
            })
            .ToList();
    }

    // NPBのレギュラーシーズンは143試合制。対戦相手までは考慮しないため、
    // 実際の残り対戦カード(スケジュール)を取得しない前提での近似値として使う。
    private const int RegularSeasonGameCount = 143;

    private readonly record struct TeamTally(Team Team, int Wins, int Losses, int Draws, double WinPercentage, int Remaining);

    /// <summary>
    /// higherが残り全敗、lowerが残り全勝という最悪ケースでもhigherの勝率がlowerを上回っているかどうか。
    /// これが真であれば、両チームの間の順位はこの先の結果によらず変わらないとみなす(簡易判定)。
    /// </summary>
    private static bool IsBoundaryConfirmed(TeamTally higher, TeamTally lower)
    {
        var higherWorstWins = higher.Wins;
        var higherWorstLosses = higher.Losses + higher.Remaining;
        var higherWorstDecided = higherWorstWins + higherWorstLosses;
        var higherWorstPct = higherWorstDecided == 0 ? 0.0 : (double)higherWorstWins / higherWorstDecided;

        var lowerBestWins = lower.Wins + lower.Remaining;
        var lowerBestLosses = lower.Losses;
        var lowerBestDecided = lowerBestWins + lowerBestLosses;
        var lowerBestPct = lowerBestDecided == 0 ? 0.0 : (double)lowerBestWins / lowerBestDecided;

        // 両チームとも残り試合が無ければ、この時点の勝率がそのまま最終成績(同率はRank()側のタイブレークで
        // 既に決着済み)なので、勝率が同じでも確定とみなしてよい。一方でも残り試合があるうちは、
        // 逆転の可能性を残すため厳密な不等号のままにする。
        if (higher.Remaining == 0 && lower.Remaining == 0)
        {
            return higherWorstPct >= lowerBestPct;
        }

        return higherWorstPct > lowerBestPct;
    }
}
