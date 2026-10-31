using FT.App.Services;
using Microsoft.AspNetCore.Components;

namespace FT.App.Components;

/// <summary>
/// 세계 상태가 바뀌면 다시 그리는 페이지.
///
/// 레이아웃만 구독하면 페이지는 갱신되지 않는다. 상단 바에서 한 주를 넘기거나
/// 경기를 치러도 보고 있던 화면이 그대로 남아, 감독이 옛 정보를 보고 판단하게 된다.
/// 상태를 읽는 페이지는 전부 이 클래스를 상속한다.
/// </summary>
public abstract class SessionPage : ComponentBase, IDisposable
{
    [Inject] protected GameSession Session { get; set; } = default!;

    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    void OnSessionChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => Session.Changed -= OnSessionChanged;
}
