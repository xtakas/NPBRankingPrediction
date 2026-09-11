namespace NpbRankingPrediction.Core.DataFiles;

/// <summary>
/// IsFinal はそのシーズンのレギュラーシーズンが終了し、順位・的中結果が確定したかどうか。
/// スクレイパーに --finalize を付けて実行すると true になる(以降そのシーズンは再スクレイピングしない)。
/// LastScrapedAtUtc はスクレイパーが最後に正常終了した日時(UTC)。新しい試合結果の有無に関わらず、
/// 実行するたびに更新される。「cronが実際に動き続けているか」をWeb側で判定するために使う
/// (順位そのものの最終更新日は StandingsFile の最終スナップショット日付を使う)。
/// </summary>
public sealed record SeasonInfo(int Season, bool IsFinal, DateTimeOffset? LastScrapedAtUtc = null);

/// <summary>
/// フロントエンドがシーズン切替プルダウンを描画するための一覧。
/// 静的ホスティングではディレクトリ一覧を動的取得できないため、このマニフェストで代替する。
/// </summary>
public sealed record SeasonsFile(List<SeasonInfo> Seasons);
