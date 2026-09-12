namespace NpbRankingPrediction.Web.Services;

/// <summary>
/// FluentUIの<see cref="Microsoft.FluentUI.AspNetCore.Components.IThemeService"/>には現在のダーク/ライト状態を
/// 通知するイベントが無いため、<see cref="Components.ThemeToggle"/>で切り替えた結果をこのサービス経由で
/// Dashboardなど他コンポーネントに伝搬させる(ApexChartの文字色をテーマに合わせて再構築するために使う)。
/// </summary>
public class ThemeState
{
    public bool IsDark { get; private set; }

    public event Action? Changed;

    public void SetDark(bool isDark)
    {
        if (IsDark == isDark)
        {
            return;
        }

        IsDark = isDark;
        Changed?.Invoke();
    }
}
