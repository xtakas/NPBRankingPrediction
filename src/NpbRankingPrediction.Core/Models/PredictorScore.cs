namespace NpbRankingPrediction.Core.Models;

/// <summary>PredictorId は PredictionsFile.Predictors のキー(背番号)。</summary>
public sealed record PredictorScore(
    string PredictorId,
    int CentralMatches,
    int PacificMatches,
    int TotalMatches,
    bool IsCarryoverClear);

public sealed record ScoreSnapshot(DateOnly Date, List<PredictorScore> Predictors);
