using NUnit.Framework.Interfaces;
using System;
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

    public LoginState AuthState { get; private set; } = LoginState.None;
    public string SessionId { get; private set; } = null;
    public int Uid { get; private set; } = 0;
    public string GuestId { get; private set; } = null;
    public string TicketId { get; private set; } = null;
    public InventoryItem[] Inventory { get; private set; } = null;
    private string _token = null;

    public string version = "alphaTest";
    private const string _versionUrl = "api/version";
    private const string _signupUrl = "api/signup";
    private const string _loginUrl = "api/login";
    private const string _guestLoginUrl = "api/guest";
    private const string _inventoryUrl = "api/inventory";

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
    // requireAuth가 true면 세션이 필요한 요청이므로 헤더에 세션 아이디를 넣어서 보냄 (로그아웃 등)
    private async Task<string> SendRequestAsync(HttpMethod method, string url, string jsonBody = null, bool requireAuth = false, CancellationToken cancelToken = default) {
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

                return responseText;
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning($"[{url}] 네트워크 요청이 사용자에 의해 안전하게 취소되었습니다."));
            return null;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => Util.LogError($"[{url}] 네트워크 에러 발생: {e.Message}"));
            return null;
        }
    }
    #endregion


    // ---------- Version Call ----------
    private bool _tryingVersionCall = false;

    public async Task<bool> GetVersionCall(CancellationToken cancelToken = default) {
        if (_tryingVersionCall == true) return false; 
        _tryingVersionCall = true;
        try {
            string responseText = await SendRequestAsync(HttpMethod.Get, _versionUrl, null, false, cancelToken);
            if (string.IsNullOrEmpty(responseText)) {
                return false; 
            }

            VersionResponse resData = JsonUtility.FromJson<VersionResponse>(responseText);
            if (resData != null && resData.success && resData.data != null) {
                //TODO : resData.data를 보고 분기처리, 결과에 따라선 true를 반환하지 못할 수도 있음
                return true;
            }
            return false;
        }
        finally {
            _tryingVersionCall = false;
        }
    }

    // ---------- Auth Calls (Signup, Login, Guest Login, Logout) ----------
    private bool _tryingAuthCall = false;

    public async Task<bool> PostCreateAccountCall(string id, string password, CancellationToken cancelToken = default) {
        if (_tryingAuthCall) return false;

        // TODO : 추후 메세지 팝업 UI를 만들고 Util.Log를 팝업으로 변경하기
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

        _tryingAuthCall = true;

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
                Inventory = resData.data.inventory;
                AuthState = LoginState.Login;

                // TODO : 인벤토리 UI 새로고침 등 필요한 작업 실행하기
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
            _tryingAuthCall = false;
        }
    }

    public async Task<bool> PostLoginCall(string id, string password, CancellationToken cancelToken = default) {
        if (_tryingAuthCall) return false;

        if (AuthState != LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return false;
        }
        if (!IsValidId(id) || string.IsNullOrEmpty(password)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("아이디 또는 비밀번호의 형식이 올바르지 않습니다."));
            return false;
        }

        _tryingAuthCall = true;

        try {
            string jsonString = JsonUtility.ToJson(new AuthRequest { id = id, password = password });
            string responseText = await SendRequestAsync(HttpMethod.Post, _loginUrl, jsonString, false, cancelToken);
            if (responseText == null) return false;

            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
            if (resData != null && resData.success) {
                SessionId = resData.data.sessionId;
                Uid = resData.data.uid;
                Inventory = resData.data.inventory;
                AuthState = LoginState.Login;

                // TODO : 인벤토리 UI 새로고침 등 필요한 작업 실행하기
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그인 성공! [Session: {resData.data.sessionId} ]");
                });
                return true;
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
            return false;
        }
        finally {
            _tryingAuthCall = false;
        }
    }

    public async Task<bool> PostGuestLoginCall(CancellationToken cancelToken = default) {
        if (_tryingAuthCall) return false;

        if (AuthState != LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return false;
        }

        _tryingAuthCall = true;

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
            _tryingAuthCall = false;
        }
    }

    public async Task<bool> PostLogoutCall(CancellationToken cancelToken = default) {
        if (_tryingAuthCall) return false;

        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그아웃 되어 있거나 세션이 없습니다."));
            return false;
        }

        _tryingAuthCall = true;
        try {
            Managers.ExecuteAtMainThread(() => Util.Log("로그아웃 요청을 보냅니다..."));

            string responseText = await SendRequestAsync(HttpMethod.Post, "api/logout", null, true, cancelToken);
            if (responseText == null) return false;

            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
            if (resData != null && resData.success) {
                SessionId = null;
                GuestId = null;
                Uid = 0;
                TicketId = null;
                Inventory = null;
                _token = null;
                AuthState = LoginState.None;
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그아웃 성공: {responseText}");
                });
                return true;
            }

            Managers.ExecuteAtMainThread(() => Util.LogError("로그아웃 처리에 실패했습니다."));
            return false;
        }
        finally {
            _tryingAuthCall = false;
        }
    }

    public async Task<bool> GetInventoryCall(CancellationToken cancelToken = default) {
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        string responseText = await SendRequestAsync(HttpMethod.Get, _inventoryUrl, null, true, cancelToken);
        if (responseText == null) return false;

        InventoryResponse resData = JsonUtility.FromJson<InventoryResponse>(responseText);
        if (resData != null && resData.success) {
            Inventory = resData.data.inventory;
            // TODO : 인벤토리 UI 새로고침 등 필요한 작업 실행하기
            return true;
        }
        return false;
    }

    // ---------- Match Calls (Start Match, Check Status, Cancel Match, Connect) ----------

    public async Task<bool> StartMatchCall(int mapId, string loadoutType, EquippedItem[] equippedItems, CancellationToken cancelToken = default) {
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        if (loadoutType == "CUSTOM" && (equippedItems == null || equippedItems.Length == 0)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("CUSTOM 모드에서는 장착한 아이템 정보가 필수입니다."));
            return false;
        }

        Managers.ExecuteAtMainThread(() => Util.Log("매치메이킹 큐 진입을 요청합니다..."));

        // JSON으로 보낼 데이터 조립
        MatchStartRequest reqData = new MatchStartRequest {
            mapId = mapId,
            loadoutType = loadoutType,
            equippedItems = equippedItems ?? new EquippedItem[0] // null 방어
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
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"매칭 큐 진입 성공! [Ticket ID: {TicketId}]");
                });
                return true;
            }
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError($"매칭 큐 진입 실패");
                });
                return false;
            }
        }
        return false;
    }

    public async Task<bool> CancelMatchCall(CancellationToken cancelToken = default) {
        if (AuthState == LoginState.None) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        if (string.IsNullOrEmpty(TicketId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("취소할 매칭 티켓이 없습니다."));
            return false;
        }

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
                Managers.ExecuteAtMainThread(() => {
                    Util.Log("매칭 취소 완료!");
                });
                return true;
            }
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError($"매칭 취소 실패: {resData.message}");
                });
                return false;
            }
        }
        return false;
    }

    public async Task<bool> CheckMatchStatusCall(CancellationToken cancelToken = default) {
        if (AuthState == LoginState.None || string.IsNullOrEmpty(TicketId)) {
            return false;
        }

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
                    Managers.ExecuteAtMainThread(() => {
                        Util.Log($"Token: {resData.data.roomToken}");
                    });

                    bool connectSuccess = await TryConnectCall(cancelToken);

                    return connectSuccess;
                }
            }
            else {
                // 서버 로직 실패 (티켓 만료 등)
                TicketId = null;
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError($"매칭 상태 확인 실패");
                });
                return false;
            }
        }
        return false;
    }

    public async Task<bool> TryConnectCall(CancellationToken cancelToken = default) {
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
                    Managers.Network.udpManager.RequestSessionIdAndSecurityKey();
                });

                return true;
            }
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError($"인게임 서버 접속 정보 획득 실패: {resData.message}");
                    //_token = null;
                });
                return false;
            }
        }

        Managers.ExecuteAtMainThread(() => Util.LogError("응답 데이터 파싱에 실패했습니다."));
        return false;
    }
}