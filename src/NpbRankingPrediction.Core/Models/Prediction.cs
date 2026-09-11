namespace NpbRankingPrediction.Core.Models;

/// <summary>
/// PredictionsFile.Predictorsのキーは背番号(文字列)。実名はここには持たず、
/// ローカル専用のPredictorNamesFile(.gitignore対象、背番号→実名)側でのみ管理する。
/// Central/Pacific は予想順位(1位から6位の順)にチームCodeを並べたもの。
/// </summary>
public sealed record PredictionEntry(List<string> Central, List<string> Pacific);
