using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.DataFiles;

public sealed record ScoresFile(int Season, List<ScoreSnapshot> Snapshots);
