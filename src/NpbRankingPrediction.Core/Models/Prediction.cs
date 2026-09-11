namespace NpbRankingPrediction.Core.Models;

/// <summary>
/// PredictionsFile.Predictorsのキーは背番号(文字列)。
/// Nameはpredictions.json(必ずコミットされる)上は空文字にしておき、Web側で
/// ローカル専用のPredictorNamesFile(.gitignore対象)の値をマージして実名として使う。
/// マージされなければ空文字のまま=背番号のみの表示になる。
/// Central/Pacific は予想順位(1位から6位の順)にチームCodeを並べたもの。
/// </summary>
public sealed record PredictionEntry(string Name, List<string> Central, List<string> Pacific);
