namespace NpbRankingPrediction.Core.Models;

/// <summary>
/// Code はnpb.jpの試合結果ページに表示される略称(例: "巨人")と一致させ、
/// スクレイパー・予想データ(predictions.json)の両方で共通のチーム識別子として使う(内部識別子としてのみ使用)。
/// 画面表示にはFullNameを使い、Abbreviation(新聞の順位表で使われる一文字略称)はColorHex/TextColorHexの
/// バッジ表示にのみ使う。
/// </summary>
public sealed record Team(
    string Code,
    string FullName,
    string Abbreviation,
    string ColorHex,
    string TextColorHex,
    League League);
