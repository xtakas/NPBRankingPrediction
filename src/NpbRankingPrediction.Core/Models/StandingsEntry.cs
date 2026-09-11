namespace NpbRankingPrediction.Core.Models;

public sealed record StandingsEntry(
    string TeamCode,
    int Rank,
    int Wins,
    int Losses,
    int Draws,
    double WinPercentage,
    double GamesBehind);

public sealed record StandingsSnapshot(
    DateOnly Date,
    List<StandingsEntry> Central,
    List<StandingsEntry> Pacific);
