using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.DataFiles;

public sealed record GamesFile(int Season, List<GameResult> Games);
