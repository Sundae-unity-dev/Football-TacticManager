using FootballTactician.World;
using Microsoft.JSInterop;

namespace FT.App.Services;

/// <summary>
/// 브라우저 저장소 접근. IndexedDB 에 gzip + base64 로 넣고, 막혀 있으면 localStorage 로 떨어진다.
///
/// localStorage 는 약 5MB 가 한도다. P1 에서는 318KB 라 충분했지만, 이적 기록과 구단 역사가
/// 시즌마다 쌓이므로 한도가 없는 쪽으로 옮긴다.
/// </summary>
public sealed class SaveStore(IJSRuntime js) : IAsyncDisposable
{
    const string SlotKey = "slot1";
    const string MetaKey = "slot1.meta";

    IJSObjectReference? _module;

    async Task<IJSObjectReference?> ModuleAsync()
    {
        if (_module != null) return _module;
        try
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");
            return _module;
        }
        catch
        {
            return null;    // 모듈을 못 읽으면 저장 기능만 꺼진다
        }
    }

    /// <summary>실제로 쓰이는 저장소 이름. 화면에 표시한다.</summary>
    public async Task<string> BackendAsync()
    {
        var m = await ModuleAsync();
        if (m == null) return "없음";
        try { return await m.InvokeAsync<string>("backend"); }
        catch { return "없음"; }
    }

    public async Task<bool> HasSaveAsync()
        => !string.IsNullOrEmpty(await GetAsync(MetaKey));

    public async Task<string?> MetaAsync() => await GetAsync(MetaKey);

    public async Task SaveAsync(GameWorld world, string label)
    {
        string payload = SaveGame.ToBase64(world, label);
        await PutAsync(SlotKey, payload);

        var club = world.MyClub;
        string meta = $"{club?.name ?? "미선택"} · {world.SeasonYear} 시즌 · {world.Date} · " +
                      $"{DateTime.Now:MM/dd HH:mm} · {payload.Length / 1024}KB";
        await PutAsync(MetaKey, meta);
    }

    public async Task<GameWorld?> LoadAsync()
    {
        string? payload = await GetAsync(SlotKey);
        if (string.IsNullOrEmpty(payload)) return null;
        return SaveGame.FromBase64(payload).world;
    }

    public async Task ClearAsync()
    {
        var m = await ModuleAsync();
        if (m == null) return;
        try
        {
            await m.InvokeVoidAsync("remove", SlotKey);
            await m.InvokeVoidAsync("remove", MetaKey);
        }
        catch { /* 저장소가 막혀 있으면 지울 것도 없다 */ }
    }

    async Task PutAsync(string key, string value)
    {
        var m = await ModuleAsync();
        if (m == null) return;
        try { await m.InvokeAsync<string>("put", key, value); }
        catch { /* 용량 초과나 시크릿 창 — 저장만 실패하고 게임은 계속된다 */ }
    }

    async Task<string?> GetAsync(string key)
    {
        var m = await ModuleAsync();
        if (m == null) return null;
        try { return await m.InvokeAsync<string?>("get", key); }
        catch { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module == null) return;
        try { await _module.DisposeAsync(); }
        catch { /* 페이지가 이미 내려갔을 수 있다 */ }
    }
}
