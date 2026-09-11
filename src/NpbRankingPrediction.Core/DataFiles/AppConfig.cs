namespace NpbRankingPrediction.Core.DataFiles;

/// <summary>
/// サイト全体の表示設定(シーズンに依存しない)。data/config.json に対応。
/// ファイルが存在しない場合は両方nullとして扱い、呼び出し側で妥当な既定値にフォールバックする。
/// </summary>
public sealed record AppConfig(int? LeaderboardTopCount, int? ChartDefaultRangeDays);
