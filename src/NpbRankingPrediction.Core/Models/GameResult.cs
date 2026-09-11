using System.Text.Json.Serialization;

namespace NpbRankingPrediction.Core.Models;

/// <summary>
/// GameId は npb.jp のスコアページURL("scores/2026/0901/g-db-20")をそのまま使い、
/// 日次スクレイピングを再実行しても同じ試合を一意に識別してdedupできるようにする。
/// </summary>
public sealed record GameResult(
    DateOnly Date,
    string HomeTeamCode,
    string AwayTeamCode,
    int? HomeScore,
    int? AwayScore,
    string? GameId)
{
    [JsonIgnore]
    public bool HasResult => HomeScore.HasValue && AwayScore.HasValue;

    [JsonIgnore]
    public bool IsDraw => HasResult && HomeScore == AwayScore;

    [JsonIgnore]
    public string? WinnerTeamCode =>
        !HasResult || IsDraw ? null : HomeScore > AwayScore ? HomeTeamCode : AwayTeamCode;

    [JsonIgnore]
    public string? LoserTeamCode =>
        !HasResult || IsDraw ? null : HomeScore > AwayScore ? AwayTeamCode : HomeTeamCode;
}
