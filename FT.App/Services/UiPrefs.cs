using Microsoft.JSInterop;

namespace FT.App.Services;

/// <summary>
/// 화면 설정. 글씨 크기처럼 **세계가 아니라 이 브라우저**에 속한 값을 들고 있다.
///
/// 세이브에 넣지 않는다. 같은 세이브를 큰 모니터와 노트북에서 번갈아 열 때
/// 글씨 크기가 따라다니면 오히려 불편하다.
/// </summary>
public sealed class UiPrefs(IJSRuntime js) : IAsyncDisposable
{
    public const string Base = "base";
    public const string Large = "lg";
    public const string ExtraLarge = "xl";

    IJSObjectReference? _module;

    /// <summary>지금 고른 값. 처음 읽기 전에는 기본값이다.</summary>
    public string Scale { get; private set; } = Base;

    public event Action? Changed;

    async Task<IJSObjectReference?> ModuleAsync()
    {
        if (_module != null) return _module;
        try { return _module = await js.InvokeAsync<IJSObjectReference>("import", "./js/ui.js"); }
        catch { return null; }      // 모듈을 못 읽으면 기본 크기로 돈다
    }

    public async Task LoadAsync()
    {
        var m = await ModuleAsync();
        if (m == null) return;
        try { Scale = await m.InvokeAsync<string>("get"); }
        catch { Scale = Base; }
        Changed?.Invoke();
    }

    public async Task SetScaleAsync(string scale)
    {
        if (Scale == scale) return;
        Scale = scale;
        Changed?.Invoke();           // 화면은 즉시 바꾸고, 저장은 뒤따라간다

        var m = await ModuleAsync();
        if (m == null) return;
        try { await m.InvokeVoidAsync("set", scale); }
        catch { /* 저장만 실패한다. 이번 판에는 적용된 채로 둔다 */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module == null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
