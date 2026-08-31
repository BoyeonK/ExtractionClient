using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class HTTPManager {
    private static readonly HttpClient _httpClient = new HttpClient {
        BaseAddress = new Uri(Gitignores.baseUrl),
        Timeout = TimeSpan.FromSeconds(5)
    };

    public enum LoginState {
        None,
        Login,
        Guest,
    }

    private LoginState _authState = LoginState.None;

    // 새 세션이 열리는 순간 만료 통보 래치를 다시 무장한다. 대입 지점이 넷(계정 생성·로그인·
    // 게스트·초기화)이라 각자 풀게 하면 새 인증 경로가 생길 때 반드시 하나가 빠진다
    public LoginState AuthState {
        get => _authState;
        private set {
            _authState = value;
            if (value != LoginState.None) _sessionExpiredNotified = false;
        }
    }

    public bool IsMatching { get; private set; } = false;
    public string SessionId { get; private set; } = null;
    public int Uid { get; private set; } = 0;
    public string GuestId { get; private set; } = null;
    public string TicketId { get; private set; } = null;
    public InventoryItem[] Inventory { get; private set; } = null;
    public ShopItem[] ShopItems { get; private set; } = null;
    public int Money { get; private set; } = 0;
    public int MapId { get; private set; } = 0;
    private string _token = null;

    // 버전 검사의 클라 쪽 유일한 출처. 서버의 latestVersion과 손으로 맞추는 값이며
    // 어긋나면 아무도 로그인 화면에 못 간다(표시가 틀어지는 정도가 아니라 접속이 막힌다)
    // TEMP: 서버가 보내는 값은 "alpha-1"이다. 불일치 갈래를 실측하려고 일부러 틀리게 둔 값이며,
    //       확인이 끝나면 "alpha-1"로 맞출 것. 그전까지 버전 검사는 항상 불일치로 떨어진다
    public const string version = "alphaTest";
    private const string _versionUrl = "api/version";
    private const string _signupUrl = "api/signup";
    private const string _loginUrl = "api/login";
    private const string _guestLoginUrl = "api/guest";
    private const string _sessionResumeUrl = "api/session/resume";
    private const string _inventoryUrl = "api/items/inventory";
    private const string _purchaseUrl = "api/items/purchase";

    private const string _matchStartUrl = "api/game/match/start";
    private const string _matchStatusUrl = "api/game/match/status";
    private const string _matchCancelUrl = "api/game/match/cancel";
    private const string _connectUrl = "api/game/match/connect";

    #region 유효성 검사 헬퍼
    private bool IsValidId(string id) {
        if (string.IsNullOrEmpty(id)) return false;
        return Regex.IsMatch(id, @"^[a-zA-Z0-9]{4,16}$");
    }

    private bool IsValidPassword(string password) {
        if (string.IsNullOrEmpty(password)) return false;
        return Regex.IsMatch(password, @"^(?=.*[a-zA-Z])(?=.*[0-9])[a-zA-Z0-9!@#$%^&*()]{4,16}$");
    }
    #endregion

    #region 통신 공통 헬퍼 함수
    // 상태 코드까지 필요한 호출부를 위한 반환 타입.
    // HasResponse가 false면 서버 응답 자체가 없었던 것(전송 실패·취소)이라 상태 코드가 의미 없다
    private struct HttpCallResult {
        public string Body;
        public int StatusCode;
        public bool HasResponse;
    }

    // 인증이 필요한 요청이 401을 받았을 때 발화한다. 401은 어느 요청에서 왔든 결론이
    // '세션이 죽었으니 재로그인'으로 같아 호출자가 분기할 여지가 없다 — 그래서 호출부마다
    // 반환 타입을 늘리지 않고 감지를 SendRequestWithStatusAsync 한 곳에 모은다
    public event Action OnSessionExpired;

    // 통보는 세션당 1회다. 해제는 AuthState가 None을 벗어날 때뿐이다
    private bool _sessionExpiredNotified = false;

    // 세션 만료 통보. 구독자가 UI를 건드리므로 메인 스레드로 넘긴다
    private void NotifySessionExpired() {
        if (_sessionExpiredNotified) return;
        _sessionExpiredNotified = true;

        Managers.ExecuteAtMainThread(() => {
            Util.LogWarning("세션이 만료되었습니다. 재로그인이 필요합니다.");
            OnSessionExpired?.Invoke();
        });
    }

    // 상태 코드 401 또는 200으로 감싸 온 본문의 code == 401 (로그인·세션 유지와 같은 이유).
    // 본문이 비었거나 JSON이 아닐 수 있으므로 파싱 실패는 '만료 아님'으로 흘린다 —
    // 여기서 예외가 새면 모든 인증 요청의 응답 처리가 함께 죽는다
    private bool IsUnauthorized(int statusCode, string body) {
        if (statusCode == HTTP_UNAUTHORIZED) return true;
        if (string.IsNullOrEmpty(body)) return false;

        try {
            BaseResponse resData = JsonUtility.FromJson<BaseResponse>(body);
            return resData != null && resData.code == HTTP_UNAUTHORIZED;
        }
        catch {
            return false;
        }
    }

    // requireAuth가 true면 세션이 필요한 요청이므로 헤더에 세션 아이디를 넣어서 보냄 (로그아웃 등).
    // notifySessionExpiry는 세션 수명을 스스로 다루는 호출(세션 유지·로그아웃)만 false로 준다
    private async Task<string> SendRequestAsync(HttpMethod method, string url, string jsonBody = null, bool requireAuth = false, CancellationToken cancelToken = default, bool notifySessionExpiry = true) {
        HttpCallResult result = await SendRequestWithStatusAsync(method, url, jsonBody, requireAuth, cancelToken, notifySessionExpiry);
        return result.Body;
    }

    private async Task<HttpCallResult> SendRequestWithStatusAsync(HttpMethod method, string url, string jsonBody = null, bool requireAuth = false, CancellationToken cancelToken = default, bool notifySessionExpiry = true) {
        try {
            using (HttpRequestMessage request = new HttpRequestMessage(method, url)) {
                // 인증 헤더 추가 (로그아웃 등)
                if (requireAuth && AuthState != LoginState.None && !string.IsNullOrEmpty(SessionId)) {
                    request.Headers.Add("x-session-id", SessionId);
                }

                // 본문(Body) 추가: 데이터가 있으면 넣고, 없는데 POST면 빈 JSON 객체("{}") 전송
                if (!string.IsNullOrEmpty(jsonBody)) {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }
                else if (method == HttpMethod.Post) {
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request, cancelToken);
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) {
                    Managers.ExecuteAtMainThread(() => Util.LogWarning($"[{url}] 상태 코드 에러: {response.StatusCode}"));
                }

                if (requireAuth && notifySessionExpiry && IsUnauthorized((int)response.StatusCode, responseText)) {
                    NotifySessionExpired();
                }

                return new HttpCallResult {
                    Body = responseText,
                    StatusCode = (int)response.StatusCode,
                    HasResponse = true,
                };
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning($"[{url}] 네트워크 요청이 사용자에 의해 안전하게 취소되었습니다."));
            return default;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => Util.LogError($"[{url}] 네트워크 에러 발생: {e.Message}"));
            return default;
        }
    }
    #endregion


    // ---------- Version Call ----------
    private bool _isRequesting = false;

    // 버전 확인 결과. bool로 뭉뚱그리지 않는 이유는 안내가 셋으로 갈리기 때문이다 —
    // 점검은 기다리는 것 외에 할 일이 없고, 버전 불일치는 클라를 새로 받아야 한다.
    // Failed는 통신 실패·파싱 실패·서버의 success=false를 함께 덮는다(전부 '다시 누른다'가 답이다)
    public enum VersionResult {
        Success,
        Maintenance,
        VersionMismatch,
        Failed,
    }

    public async Task<VersionResult> GetVersionCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return VersionResult.Failed;
        if (IsMatching) return VersionResult.Failed;
        _isRequesting = true;
        try {
            string responseText = await SendRequestAsync(HttpMethod.Get, _versionUrl, null, false, cancelToken);
            if (string.IsNullOrEmpty(responseText)) {
                return VersionResult.Failed;
            }

            // JSON이 아닌 본문(프록시 에러 페이지 등)이 JsonUtility에 들어가면 예외가
            // async void 호출부로 빠져나가 팝업 없이 시작 버튼이 굳는다
            if (!responseText.Trim().StartsWith("{")) {
                Managers.ExecuteAtMainThread(() => Util.LogError("서버 응답이 JSON 형식이 아닙니다."));
                return VersionResult.Failed;
            }

            VersionResponse resData = JsonUtility.FromJson<VersionResponse>(responseText);
            if (resData == null || !resData.success || resData.data == null) {
                Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
                return VersionResult.Failed;
            }

            // 점검을 버전보다 먼저 본다 — 둘 다 참일 수 있는데, 점검 중에는 최신 클라를 받아도
            // 못 들어가므로 버전 안내를 먼저 하면 사용자를 헛수고시킨다
            if (resData.data.isMaintenance) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("서버가 점검 중입니다."));
                return VersionResult.Maintenance;
            }

            // latestVersion이 비어 오면 불일치로 본다(확정) — 서버가 그렇게 보낼 일이 없다는 전제다.
            // '비었으면 통과'로 완화하지 말 것: 검사를 켜 둔 의미가 사라진다
            if (resData.data.latestVersion != version) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning(
                    $"클라이언트 버전 불일치 (클라: {version} / 서버: {resData.data.latestVersion})"));
                return VersionResult.VersionMismatch;
            }

            return VersionResult.Success;
        }
        finally {
            _isRequesting = false;
        }
    }

    // ---------- Auth Calls (Signup, Login, Guest Login, Logout) ----------

    public async Task<bool> PostCreateAccountCall(string id, string password, CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (IsMatching) return false;

        // OPTION: 메세지 팝업 UI를 만들고 Util.Log를 팝업으로 변경
        if (AuthState != LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다. 로그아웃 후 이용해주세요."));
            return false;
        }
        if (!IsValidId(id)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("아이디는 영문과 숫자를 조합하여 4~16자리로 입력해주세요."));
            return false;
        }
        if (!IsValidPassword(password)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("비밀번호는 영문, 숫자, 특수문자(!@#$%^&*())를 모두 포함하여 4~16자리로 입력해주세요."));
            return false;
        }

        _isRequesting = true;

        try { 
            Managers.ExecuteAtMainThread(() => Util.Log("계정 생성 요청을 보냅니다..."));

            string jsonString = JsonUtility.ToJson(new AuthRequest { id = id, password = password });
            string responseText = await SendRequestAsync(HttpMethod.Post, _signupUrl, jsonString, false, cancelToken);
            if (responseText == null) {
                return false;
            }

            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
            if (resData != null && resData.success) {
                SessionId = resData.data.sessionId;
                Uid = resData.data.uid;
                Money = resData.data.money;
                Inventory = resData.data.inventory;
                ShopItems = resData.data.shopItems;
                AuthState = LoginState.Login;

                // TODO: 인벤토리 UI 새로고침 등 필요한 작업 실행
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"계정 생성 성공! [Session: {resData.data.sessionId} ]");
                });
                return true;
            }

            Managers.ExecuteAtMainThread(() => {
                Util.LogError("서버에서 실패 응답을 보냈습니다.");
            });
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    // 로그인 결과. 실패를 둘로 나누는 이유는 사용자에게 할 안내가 다르기 때문이다 —
    // Failed는 아이디·비밀번호를 다시 입력하면 되지만,
    // AlreadyInGame은 입력과 무관해 매치가 끝나기 전에는 몇 번을 다시 눌러도 결과가 같다
    public enum LoginResult {
        Success,
        Failed,
        AlreadyInGame,
    }

    private const int HTTP_CONFLICT = 409;

    public async Task<LoginResult> PostLoginCall(string id, string password, CancellationToken cancelToken = default) {
        if (_isRequesting) return LoginResult.Failed;
        if (IsMatching) return LoginResult.Failed;

        if (AuthState != LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return LoginResult.Failed;
        }
        if (!IsValidId(id) || string.IsNullOrEmpty(password)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("아이디 또는 비밀번호의 형식이 올바르지 않습니다."));
            return LoginResult.Failed;
        }

        _isRequesting = true;

        try {
            string jsonString = JsonUtility.ToJson(new AuthRequest { id = id, password = password });
            HttpCallResult call = await SendRequestWithStatusAsync(HttpMethod.Post, _loginUrl, jsonString, false, cancelToken);
            if (!call.HasResponse) return LoginResult.Failed;

            // 본문을 파싱하기 전에 상태 코드로 가른다 — 409 본문 형식이 명세에 없어 비어 올 수 있다
            if (call.StatusCode == HTTP_CONFLICT) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("진행 중인 매치가 있어 로그인할 수 없습니다."));
                return LoginResult.AlreadyInGame;
            }
            if (string.IsNullOrEmpty(call.Body)) return LoginResult.Failed;

            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(call.Body);
            if (resData != null && resData.success) {
                SessionId = resData.data.sessionId;
                Uid = resData.data.uid;
                Money = resData.data.money;
                Inventory = resData.data.inventory;
                ShopItems = resData.data.shopItems;
                AuthState = LoginState.Login;

                // TODO: 인벤토리 UI 새로고침 등 필요한 작업 실행
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그인 성공! [Session: {resData.data.sessionId} ]");
                });
                return LoginResult.Success;
            }

            // 200으로 감싸 보내는 실패 응답도 있으므로 본문의 code로 한 번 더 가려낸다 (세션 유지와 같은 이유)
            if (resData != null && resData.code == HTTP_CONFLICT) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("진행 중인 매치가 있어 로그인할 수 없습니다."));
                return LoginResult.AlreadyInGame;
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
            return LoginResult.Failed;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> PostGuestLoginCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (IsMatching) return false;

        if (AuthState != LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return false;
        }

        _isRequesting = true;

        try {
            Managers.ExecuteAtMainThread(() => Util.Log("게스트 로그인 요청을 보냅니다..."));

            string responseText = await SendRequestAsync(HttpMethod.Post, _guestLoginUrl, null, false, cancelToken);
            if (responseText == null) return false;

            GuestAuthResponse resData = JsonUtility.FromJson<GuestAuthResponse>(responseText);
            if (resData != null && resData.success) {
                SessionId = resData.data.sessionId;
                Uid = resData.data.uid;
                GuestId = resData.data.guestId;
                AuthState = LoginState.Guest;
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"게스트 로그인 성공! [Session: {resData.data.sessionId} | GuestID: {resData.data.guestId}]");
                });
                return true;
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> PostLogoutCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (IsMatching) return false;

        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그아웃 되어 있거나 세션이 없습니다."));
            return false;
        }

        _isRequesting = true;
        try {
            Managers.ExecuteAtMainThread(() => Util.Log("로그아웃 요청을 보냅니다..."));

            HttpCallResult call = await SendRequestWithStatusAsync(
                HttpMethod.Post, "api/logout", null, true, cancelToken, notifySessionExpiry: false);
            if (!call.HasResponse) return false;

            // 401은 서버에 세션이 이미 없다는 뜻이라 로그아웃의 목적은 달성된 것이다.
            // 만료 통보로 보내지 않는 이유도 같다 — 사용자가 스스로 로그아웃을 눌렀는데
            // "세션이 만료되었습니다" 안내가 뜨는 꼴이 된다.
            // 본문 파싱보다 앞에 두는 것은 로그인 409와 같은 이유다(빈 본문이 JsonUtility에
            // 들어가면 예외가 async void 호출부로 빠져나가 팝업 없이 버튼만 굳는다)
            if (IsUnauthorized(call.StatusCode, call.Body)) {
                ClearAuthStateLocal();
                Managers.ExecuteAtMainThread(() => Util.LogWarning("세션이 이미 만료되어 로그아웃으로 처리합니다."));
                return true;
            }
            if (string.IsNullOrEmpty(call.Body)) return false;

            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(call.Body);
            if (resData != null && resData.success) {
                ClearAuthStateLocal();
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그아웃 성공: {call.Body}");
                });
                return true;
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("로그아웃 처리에 실패했습니다."));
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    // 서버 호출 없이 로컬 인증 상태만 초기화한다.
    // 세션이 서버에서 이미 만료된 경우(세션 유지 실패 등)에는 로그아웃 API를 부를 수 없으므로 분리한다
    public void ClearAuthStateLocal() {
        SessionId = null;
        GuestId = null;
        Uid = 0;
        Money = 0;
        TicketId = null;
        // IsMatching은 TicketId와 한 쌍이다(StartMatchCall이 함께 세우고 CancelMatchCall이 함께 지운다).
        // 남겨두면 PostLoginCall의 첫 가드에 걸려 재로그인이 통째로 막힌다
        IsMatching = false;
        Inventory = null;
        ShopItems = null;
        _token = null;
        AuthState = LoginState.None;
    }

    // 세션 유지 결과. 실패를 둘로 나누는 이유는 후속 처리가 다르기 때문이다 —
    // Expired는 서버에 세션이 없다는 확정이라 재로그인 외에 방법이 없고,
    // Unreachable은 세션이 아직 살아 있을 수 있어 같은 요청을 다시 보낼 여지가 있다
    public enum ResumeResult {
        Success,
        Expired,
        Unreachable,
    }

    private const int HTTP_UNAUTHORIZED = 401;

    // 매치 종료 후 로비 복귀 시 기존 세션 유효성 확인 + 매치 결과가 반영된 최신 로비 데이터 재조회.
    // Success면 호출자가 Login 과정을 건너뛴다. Expired면 ClearAuthStateLocal() 후 일반 로그인으로 폴백하고,
    // Unreachable이면 재시도할지 폴백할지는 호출자가 정한다
    public async Task<ResumeResult> PostResumeSessionCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return ResumeResult.Unreachable;
        if (IsMatching) return ResumeResult.Unreachable;

        if (AuthState == LoginState.None || string.IsNullOrEmpty(SessionId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이어받을 세션이 없습니다."));
            return ResumeResult.Expired;
        }

        _isRequesting = true;
        try {
            // 만료 통보를 태우지 않는다 — 이 호출은 401을 ResumeResult.Expired로 직접 돌려주고
            // 호출자가 자기 폴백 UI를 돌린다. 통보까지 나가면 팝업과 전이가 두 번씩 돈다
            HttpCallResult call = await SendRequestWithStatusAsync(
                HttpMethod.Post, _sessionResumeUrl, null, true, cancelToken, notifySessionExpiry: false);
            if (!call.HasResponse || string.IsNullOrEmpty(call.Body)) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("세션 유지 요청이 서버에 닿지 않았습니다."));
                return ResumeResult.Unreachable;
            }

            if (call.StatusCode == HTTP_UNAUTHORIZED) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("세션이 만료되었습니다. 재로그인이 필요합니다."));
                return ResumeResult.Expired;
            }

            SessionResumeResponse resData = JsonUtility.FromJson<SessionResumeResponse>(call.Body);
            if (resData != null && resData.success && resData.data != null) {
                Uid = resData.data.uid;
                Money = resData.data.money;
                Inventory = resData.data.inventory;
                // ShopItems는 로그인 때 받은 캐시를 유지한다 (세션 중 불변이라 재전송하지 않는 스펙)
                Managers.ExecuteAtMainThread(() => Util.Log($"세션 유지 성공 [Session: {SessionId}]"));
                return ResumeResult.Success;
            }

            // 200으로 감싸 보내는 실패 응답도 있으므로 본문의 code로 한 번 더 만료를 가려낸다
            if (resData != null && resData.code == HTTP_UNAUTHORIZED) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("세션이 만료되었습니다. 재로그인이 필요합니다."));
                return ResumeResult.Expired;
            }

            Managers.ExecuteAtMainThread(() => Util.LogWarning($"세션 유지에 실패했습니다. [status: {call.StatusCode}]"));
            return ResumeResult.Unreachable;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> GetInventoryCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (IsMatching) return false;
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        _isRequesting = true;
        try {
            string responseText = await SendRequestAsync(HttpMethod.Get, _inventoryUrl, null, true, cancelToken);
            if (responseText == null) return false;

            InventoryResponse resData = JsonUtility.FromJson<InventoryResponse>(responseText);
            if (resData != null && resData.success) {
                Inventory = resData.data.inventory;
                // TODO: 인벤토리 UI 새로고침 등 필요한 작업 실행
                return true;
            }
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> PostPurchaseCall(
        int itemId, int slotIndex, int quantity,
        InventoryItem[] inventorySnapshot,
        CancellationToken cancelToken = default)
    {
        if (_isRequesting) return false;
        if (IsMatching) return false;
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        _isRequesting = true;
        try {
            PurchaseRequest reqData = new PurchaseRequest {
                item_id    = itemId,
                slot_index = slotIndex,
                quantity   = quantity,
                inventory  = inventorySnapshot,
            };
            string jsonString = JsonUtility.ToJson(reqData);
            string responseText = await SendRequestAsync(
                HttpMethod.Post, _purchaseUrl, jsonString, true, cancelToken);
            if (responseText == null) return false;

            PurchaseResponse resData = JsonUtility.FromJson<PurchaseResponse>(responseText);
            if (resData != null && resData.success) {
                Money     = resData.data.money;
                Inventory = resData.data.inventory;
                return true;
            }
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    // ---------- Match Calls (Start Match, Check Status, Cancel Match, Connect) ----------

    public async Task<bool> StartMatchCall(int mapId, int characterType, string loadoutType, InventoryItem[] inventory, CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (IsMatching) return false;
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        if (loadoutType == "CUSTOM" && (inventory == null || inventory.Length == 0)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("CUSTOM 모드에서는 인벤토리 스냅샷이 필수입니다."));
            return false;
        }

        _isRequesting = true;
        try {
            Managers.ExecuteAtMainThread(() => Util.Log("매치메이킹 큐 진입을 요청합니다..."));

            // JSON으로 보낼 데이터 조립
            MatchStartRequest reqData = new MatchStartRequest {
                mapId = mapId,
                characterType = characterType,
                loadoutType = loadoutType,
                inventory = inventory
            };
            string jsonString = JsonUtility.ToJson(reqData);

            // requireAuth 플래그를 true로 주어 헤더에 x-session-id를 자동으로 포함시킵니다!
            string responseText = await SendRequestAsync(HttpMethod.Post, _matchStartUrl, jsonString, true, cancelToken);
            if (responseText == null) return false;

            Managers.ExecuteAtMainThread(() => Util.Log($"[매칭 시작 응답 원본] {responseText}"));
            if (!responseText.Trim().StartsWith("{")) {
                Managers.ExecuteAtMainThread(() => Util.LogError("서버 응답이 JSON 형식이 아닙니다."));
                return false;
            }

            // 응답 데이터 파싱
            MatchStartResponse resData = JsonUtility.FromJson<MatchStartResponse>(responseText);

            if (resData != null) {
                if (resData.success) {
                    TicketId = resData.data.ticketId;
                    IsMatching = true;
                    Managers.ExecuteAtMainThread(() => {
                        Util.Log($"매칭 큐 진입 성공! [Ticket ID: {TicketId}]");
                    });
                    return true;
                }
                else {
                    IsMatching = false;
                    string errorCode = resData.error?.code ?? "";
                    Managers.ExecuteAtMainThread(() => {
                        Util.LogError($"매칭 큐 진입 실패: {errorCode}");
                    });
                    return false;
                }
            }
            IsMatching = false;
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> CancelMatchCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        if (string.IsNullOrEmpty(TicketId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("취소할 매칭 티켓이 없습니다."));
            return false;
        }

        _isRequesting = true;
        try {
            Managers.ExecuteAtMainThread(() => Util.Log("매치메이킹 취소를 요청합니다..."));

            // JSON 데이터 조립
            MatchCancelRequest reqData = new MatchCancelRequest {
                ticketId = TicketId
            };
            string jsonString = JsonUtility.ToJson(reqData);

            // API 호출 (POST, requireAuth = true)
            string responseText = await SendRequestAsync(HttpMethod.Post, _matchCancelUrl, jsonString, true, cancelToken);
            if (responseText == null) return false;

            // 응답 파싱
            BaseResponse resData = JsonUtility.FromJson<BaseResponse>(responseText);

            if (resData != null) {
                if (resData.success) {
                    TicketId = null;
                    IsMatching = false;
                    Managers.ExecuteAtMainThread(() => {
                        Util.Log("매칭 취소 완료!");
                    });
                    return true;
                }
                else {
                    Managers.ExecuteAtMainThread(() => {
                        Util.LogError($"매칭 취소 실패: {resData.error?.message}");
                    });
                    return false;
                }
            }
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> CheckMatchStatusCall(CancellationToken cancelToken = default) {
        if (_isRequesting) return false;
        if (AuthState == LoginState.None || string.IsNullOrEmpty(TicketId)) {
            return false;
        }

        _isRequesting = true;
        try {
            string url = $"{_matchStatusUrl}?ticketId={TicketId}";
            string responseText = await SendRequestAsync(HttpMethod.Get, url, null, true, cancelToken);

            if (string.IsNullOrEmpty(responseText)) return false;

            MatchStatusResponse resData = JsonUtility.FromJson<MatchStatusResponse>(responseText);

            if (resData != null) {
                if (resData.success) {
                    if (resData.data.status == "WAITING") {
                        Managers.ExecuteAtMainThread(() => Util.Log("매칭 대기 중... (서버 응답 확인)"));
                        return false;
                    }
                    else if (resData.data.status == "SUCCESS") {
                        _token = resData.data.roomToken;
                        MapId = resData.data.mapId;
                        Managers.ExecuteAtMainThread(() => {
                            Util.Log($"Token: {resData.data.roomToken}, MapId: {resData.data.mapId}");
                        });

                        bool connectSuccess = await TryConnectCall(cancelToken);

                        return connectSuccess;
                    }
                }
                else {
                    // 서버 로직 실패 (티켓 만료 등)
                    TicketId = null;
                    IsMatching = false;
                    Managers.ExecuteAtMainThread(() => {
                        Util.LogError($"매칭 상태 확인 실패");
                    });
                    return false;
                }
            }
            return false;
        }
        finally {
            _isRequesting = false;
        }
    }

    public async Task<bool> TryConnectCall(CancellationToken cancelToken = default) {
        try {
            if (AuthState == LoginState.None || string.IsNullOrEmpty(_token)) {
                Managers.ExecuteAtMainThread(() => Util.LogWarning("세션 또는 룸 토큰이 유효하지 않습니다."));
                return false;
            }

            ConnectRequest reqData = new ConnectRequest {
                roomToken = _token
            };
            string jsonString = JsonUtility.ToJson(reqData);

            string responseText = await SendRequestAsync(HttpMethod.Post, _connectUrl, jsonString, true, cancelToken);
            if (string.IsNullOrEmpty(responseText)) return false;

            ConnectResponse resData = JsonUtility.FromJson<ConnectResponse>(responseText);

            if (resData != null) {
                if (resData.success && resData.data != null) {
                    Managers.ExecuteAtMainThread(() => {
                        Util.Log($"[IP: {resData.data.ip}, Port: {resData.data.port}], SID: {resData.data.ingameSessionId}, sKey: {resData.data.securityKey}");
                        Managers.Network.udpManager.RegisterEndPointAndStart(resData.data.ip, resData.data.port);
                        Managers.Network.udpManager.Handler.SetSessionVariable((ushort)resData.data.ingameSessionId, Convert.ToUInt32(resData.data.securityKey));
                    });

                    try {
                        await Task.Delay(1500, cancelToken);
                    }
                    catch (TaskCanceledException) {
                        return false;
                    }

                    Managers.ExecuteAtMainThread(() => {
                        Managers.Network.udpManager.SendChannelOpenPkt();
                        BaseScene scene = Managers.Scene.CurrentScene;
                        if (scene is LobbyScene lobbyScene) {
                            Managers.Scene.LoadSceneWithLoadingScene(Define.Scene.TestIngameScene, Define.Scene.LoadingScene);
                        }
                    });

                    return true;
                }
                else {
                    Managers.ExecuteAtMainThread(() => {
                        Util.LogError($"인게임 서버 접속 정보 획득 실패: {resData.error?.message}");
                        //_token = null;
                    });
                    return false;
                }
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("응답 데이터 파싱에 실패했습니다."));
            return false;
        }
        finally {
            IsMatching = false;
        }
    }

    public async void StartMatchPolling(Action onSuccess, CancellationToken cancelToken = default) {
        while (IsMatching) {
            try {
                await Task.Delay(3000, cancelToken);
            }
            catch (OperationCanceledException) {
                break;
            }

            if (!IsMatching) break;

            bool isSuccess = await CheckMatchStatusCall(cancelToken);
            if (isSuccess) {
                Managers.ExecuteAtMainThread(onSuccess);
                break;
            }
        }
    }
}