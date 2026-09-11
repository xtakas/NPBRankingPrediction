namespace NpbRankingPrediction.Core.Models;

public static class TeamCatalog
{
    // ColorHex/TextColorHexは各球団のコーポレートカラーに近い色の概算値。
    // Abbreviationはスポーツ紙の順位表で使われる一文字略称。
    public static readonly Team Giants = new("巨人", "読売ジャイアンツ", "巨", "#F97709", "#FFFFFF", League.Central);
    public static readonly Team Tigers = new("阪神", "阪神タイガース", "神", "#FFE100", "#000000", League.Central);
    public static readonly Team DeNA = new("DeNA", "横浜DeNAベイスターズ", "デ", "#0054A6", "#FFFFFF", League.Central);
    public static readonly Team Carp = new("広島", "広島東洋カープ", "広", "#E4002B", "#FFFFFF", League.Central);
    public static readonly Team Swallows = new("ヤクルト", "東京ヤクルトスワローズ", "ヤ", "#00A65E", "#FFFFFF", League.Central);
    public static readonly Team Dragons = new("中日", "中日ドラゴンズ", "中", "#003DA5", "#FFFFFF", League.Central);

    public static readonly Team Hawks = new("ソフトバンク", "福岡ソフトバンクホークス", "ソ", "#FED500", "#000000", League.Pacific);
    public static readonly Team Fighters = new("日本ハム", "北海道日本ハムファイターズ", "日", "#002F6C", "#FFFFFF", League.Pacific);
    public static readonly Team Buffaloes = new("オリックス", "オリックス・バファローズ", "オ", "#001E62", "#FFFFFF", League.Pacific);
    public static readonly Team Marines = new("ロッテ", "千葉ロッテマリーンズ", "ロ", "#000000", "#FFFFFF", League.Pacific);
    public static readonly Team Eagles = new("楽天", "東北楽天ゴールデンイーグルス", "楽", "#870021", "#FFFFFF", League.Pacific);
    public static readonly Team Lions = new("西武", "埼玉西武ライオンズ", "西", "#00479D", "#FFFFFF", League.Pacific);

    public static readonly IReadOnlyList<Team> All =
    [
        Giants, Tigers, DeNA, Carp, Swallows, Dragons,
        Hawks, Fighters, Buffaloes, Marines, Eagles, Lions,
    ];

    public static readonly IReadOnlyList<Team> CentralTeams = All.Where(t => t.League == League.Central).ToList();
    public static readonly IReadOnlyList<Team> PacificTeams = All.Where(t => t.League == League.Pacific).ToList();

    public static Team? FindByCode(string code) => All.FirstOrDefault(t => t.Code == code);

    public static Team? FindByFullName(string fullName) => All.FirstOrDefault(t => t.FullName == fullName);

    public static IReadOnlyList<Team> TeamsIn(League league) => league == League.Central ? CentralTeams : PacificTeams;
}
