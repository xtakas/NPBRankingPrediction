using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.DataFiles;

public sealed record StandingsFile(int Season, List<StandingsSnapshot> Snapshots);
