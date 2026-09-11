using NpbRankingPrediction.Core.Models;

namespace NpbRankingPrediction.Core.DataFiles;

/// <summary>
/// キーは予想者の背番号(文字列)。シーズン開始前に仲間内の予想を集めて手動でJSON編集する想定。
/// </summary>
public sealed record PredictionsFile(int Season, Dictionary<string, PredictionEntry> Predictors);
