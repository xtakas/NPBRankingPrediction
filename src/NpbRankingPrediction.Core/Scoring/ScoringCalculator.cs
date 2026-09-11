using NpbRankingPrediction.Core.DataFiles;
using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.Scoring;

public static class ScoringCalculator
{
    /// <summary>
    /// セ・パ合計12球団中、この数以上を完全一致させないとキャリーオーバーになる企画ルール。
    /// </summary>
    public const int CarryoverThreshold = 6;

    public static List<ScoreSnapshot> Calculate(StandingsFile standings, PredictionsFile predictions)
    {
        var result = new List<ScoreSnapshot>(standings.Snapshots.Count);

        foreach (var snapshot in standings.Snapshots)
        {
            var predictorScores = predictions.Predictors
                .Select(kv => Score(kv.Key, kv.Value, snapshot))
                .ToList();

            result.Add(new ScoreSnapshot(snapshot.Date, predictorScores));
        }

        return result;
    }

    private static PredictorScore Score(string predictorId, PredictionEntry prediction, StandingsSnapshot snapshot)
    {
        var centralMatches = CountMatches(snapshot.Central, prediction.Central);
        var pacificMatches = CountMatches(snapshot.Pacific, prediction.Pacific);
        var total = centralMatches + pacificMatches;

        return new PredictorScore(predictorId, centralMatches, pacificMatches, total, total >= CarryoverThreshold);
    }

    private static int CountMatches(List<StandingsEntry> actual, List<string> predictedOrder)
    {
        var matches = 0;
        foreach (var entry in actual)
        {
            var predictedRank = predictedOrder.IndexOf(entry.TeamCode) + 1;
            if (predictedRank == entry.Rank)
            {
                matches++;
            }
        }

        return matches;
    }
}
