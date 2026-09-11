namespace NpbRankingPrediction.Core.Models;

/// <summary>
/// IsRankConfirmed は「残り試合の結果によらずこの順位が変わり得ないか」の簡易判定結果。
/// 対戦相手までは考慮しない近似値(StandingsCalculator参照)であり、シーズン終了(確定)とは別の概念。
/// </summary>
public sealed record StandingsEntry(
    string TeamCode,
    int Rank,
    int Wins,
    int Losses,
    int Draws,
    double WinPercentage,
    double GamesBehind,
    bool IsRankConfirmed = false);

public sealed record StandingsSnapshot(
    DateOnly Date,
    List<StandingsEntry> Central,
    List<StandingsEntry> Pacific);
