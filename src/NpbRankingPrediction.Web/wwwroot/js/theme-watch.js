// FluentUIのテーマサービスは、ダーク/ライトが解決されるたびに(SetThemeAsyncによる明示指定でも、
// 「自動」状態でOS設定が変わった場合でも)document.body に "themeChanged" カスタムイベントを
// dispatchする(detail.isDark に解決結果が入る)。ThemeToggle.razor はこれを購読することで、
// ユーザーが未選択のままOSのダーク/ライトが切り替わった場合でもトグルの表示とグラフの配色を
// 追従させる。
export function watchThemeChanged(dotNetRef) {
    const handler = (e) => {
        dotNetRef.invokeMethodAsync("OnBodyThemeChanged", e.detail.isDark);
    };
    document.body.addEventListener("themeChanged", handler);
    return {
        dispose: () => document.body.removeEventListener("themeChanged", handler),
    };
}
