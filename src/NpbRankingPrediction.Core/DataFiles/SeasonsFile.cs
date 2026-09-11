namespace NpbRankingPrediction.Core.DataFiles;

/// <summary>
/// IsFinal はそのシーズンのレギュラーシーズンが終了し、順位・的中結果が確定したかどうか。
/// スクレイパーに --finalize を付けて実行すると true になる(以降そのシーズンは再スクレイピングしない)。
/// LastScrapedAtUtc はスクレイパーが最後に正常終了した日時(UTC)。新しい試合結果の有無に関わらず、
/// 実行するたびに更新される。「cronが実際に動き続けているか」をWeb側で判定するために使う
/// (順位そのものの最終更新日は StandingsFile の最終スナップショット日付を使う)。
/// LastFullyFinishedDate は、npb.jpの試合速報ウィジェットで「その日の全試合が終了(または中止)」と
/// 確認できた最後の日付(JST)。当日分がこの日付と一致する間は、cronが日中に何度実行されても
/// npb.jpへ再アクセスしない(21〜24時台に複数回実行する運用を想定)。
/// </summary>
public sealed record SeasonInfo(int Season, bool IsFinal, DateTimeOffset? LastScrapedAtUtc = null, DateOnly? LastFullyFinishedDate = null);

/// <summary>
/// フロントエンドがシーズン切替プルダウンを描画するための一覧。
/// 静的ホスティングではディレクトリ一覧を動的取得できないため、このマニフェストで代替する。
/// </summary>
public sealed record SeasonsFile(List<SeasonInfo> Seasons);
