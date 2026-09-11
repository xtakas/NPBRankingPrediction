using System.Net.Http.Json;
using NpbRankingPrediction.Core.DataFiles;

namespace NpbRankingPrediction.Web.Services;

/// <summary>
/// wwwroot/data配下に静的ホスティングされたJSONファイルを読み込む。
/// Azure移行時はこのサービスの内部実装をStorage APIに差し替える想定。
/// </summary>
public sealed class SeasonDataService(HttpClient httpClient)
{
    public async Task<List<SeasonInfo>> LoadSeasonsAsync(CancellationToken ct = default)
    {
        var file = await httpClient.GetFromJsonAsync<SeasonsFile>("data/seasons.json", ct);
        return file?.Seasons ?? [];
    }

    public Task<StandingsFile?> LoadStandingsAsync(int season, CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<StandingsFile>($"data/{season}/standings.json", ct);

    public Task<PredictionsFile?> LoadPredictionsAsync(int season, CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<PredictionsFile>($"data/{season}/predictions.json", ct);

    /// <summary>
    /// predictor-names.json はGitHubにコミットしないローカル専用ファイルなので、
    /// 公開環境(GitHub Pages)では404になるのが正常。その場合は実名なし(null)として扱う。
    /// </summary>
    public async Task<Dictionary<string, string>> LoadPredictorNamesAsync(int season, CancellationToken ct = default)
    {
        try
        {
            var file = await httpClient.GetFromJsonAsync<PredictorNamesFile>($"data/{season}/predictor-names.json", ct);
            return file?.Names ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public Task<ScoresFile?> LoadScoresAsync(int season, CancellationToken ct = default)
        => httpClient.GetFromJsonAsync<ScoresFile>($"data/{season}/scores.json", ct);

    /// <summary>
    /// data/config.json はサイト全体の任意設定なので、未配置(404)やパース失敗時は
    /// 既定値(両方null = 上限なし)にフォールバックする。
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await httpClient.GetFromJsonAsync<AppConfig>("data/config.json", ct);
            return config ?? new AppConfig(null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return new AppConfig(null, null);
        }
    }
}
