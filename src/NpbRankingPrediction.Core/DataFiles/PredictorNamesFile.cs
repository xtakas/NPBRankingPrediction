namespace NpbRankingPrediction.Core.DataFiles;

/// <summary>
/// 予想者の背番号→実名の対応表。predictions.json(スコア計算にも使うため必ずコミットする)とは分離し、
/// このファイルは .gitignore 対象のローカル専用データとする。
/// ローカルの data/{season}/ 配下に置いて `dotnet run` すると実名付きで表示され、
/// GitHubにはpushされないため GitHub Pages 上では自動的に背番号のみの表示になる。
/// </summary>
public sealed record PredictorNamesFile(int Season, Dictionary<string, string> Names);
