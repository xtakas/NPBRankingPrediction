using NpbRankingPrediction.Core.DataFiles;

namespace NpbRankingPrediction.Core.DataAccess;

/// <summary>
/// 永続化の抽象。現状はJSONファイル実装(<see cref="JsonFileDataStore"/>)のみだが、
/// 将来Azure Table/Blob Storage実装に差し替える際もScraper/Webはこのインターフェースだけに依存させる。
/// </summary>
public interface IDataStore
{
    Task<GamesFile> LoadGamesAsync(int season, CancellationToken ct = default);

    Task SaveGamesAsync(GamesFile games, CancellationToken ct = default);

    Task<StandingsFile> LoadStandingsAsync(int season, CancellationToken ct = default);

    Task SaveStandingsAsync(StandingsFile standings, CancellationToken ct = default);

    /// <summary>予想データは事前に手動配置される想定のため、未作成なら null を返す。</summary>
    Task<PredictionsFile?> LoadPredictionsAsync(int season, CancellationToken ct = default);

    Task<ScoresFile> LoadScoresAsync(int season, CancellationToken ct = default);

    Task SaveScoresAsync(ScoresFile scores, CancellationToken ct = default);

    Task<SeasonsFile> LoadSeasonsAsync(CancellationToken ct = default);

    Task SaveSeasonsAsync(SeasonsFile seasons, CancellationToken ct = default);
}
