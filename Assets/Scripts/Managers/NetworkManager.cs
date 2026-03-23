using NUnit.Framework.Interfaces;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;

public class NetworkManager {
    private static readonly HttpClient _httpClient = new HttpClient {
        BaseAddress = new Uri(Gitignores.baseUrl)
    };

    public string sessionId = null;
    public string guestId = null;
    public string ticketId = null;
    public int uid = 0;

    public string version = "alphaTest";
    private const string _versionUrl = "api/version";
    private const string _signupUrl = "api/signup";
    private const string _loginUrl = "api/login";
    private const string _guestLoginUrl = "api/guest";

    private const string _matchStartUrl = "api/game/match/start";
    private const string _matchStatusUrl = "api/game/match/status";
    private const string _matchCancelUrl = "api/game/match/cancel";

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
                if (requireAuth && !string.IsNullOrEmpty(sessionId)) {
                    request.Headers.Add("x-session-id", sessionId);
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

    public async Task<bool> TestCall(CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => Util.Log("서버로 요청을 보냅니다..."));

        string responseText = await SendRequestAsync(HttpMethod.Get, _versionUrl, null, false, cancelToken);
        if (responseText == null) return false;

        Managers.ExecuteAtMainThread(() => Util.Log($"서버 응답 성공: {responseText}"));
        return true;
    }

    public async Task<bool> TestCreateAccountCall(string id, string password, CancellationToken cancelToken = default) {
        // TODO : 추후 메세지 팝업 UI를 만들고 Util.Log를 팝업으로 변경하기
        if (!string.IsNullOrEmpty(sessionId)) {
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

        Managers.ExecuteAtMainThread(() => Util.Log("계정 생성 요청을 보냅니다..."));

        string jsonString = JsonUtility.ToJson(new CreateAccountRequestData { id = id, password = password });
        string responseText = await SendRequestAsync(HttpMethod.Post, _signupUrl, jsonString, false, cancelToken);
        if (responseText == null) return false;

        AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
        if (resData != null && resData.success) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log($"계정 생성 성공! [Session: {resData.data.sessionId} ]");
                sessionId = resData.data.sessionId;
                uid = resData.data.uid;
            });
            return true;
        }

        Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
        return false;
    }

    public async Task<bool> TestLoginCall(string id, string password, CancellationToken cancelToken = default) {
        if (!string.IsNullOrEmpty(sessionId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return false;
        }
        if (!IsValidId(id) || !IsValidPassword(password)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("아이디 또는 비밀번호의 형식이 올바르지 않습니다."));
            return false;
        }

        Managers.ExecuteAtMainThread(() => Util.Log("로그인 요청을 보냅니다..."));

        string jsonString = JsonUtility.ToJson(new LoginRequestData { id = id, password = password });
        string responseText = await SendRequestAsync(HttpMethod.Post, _loginUrl, jsonString, false, cancelToken);
        if (responseText == null) return false;

        AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
        if (resData != null && resData.success) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log($"로그인 성공! [Session: {resData.data.sessionId} ]");
                sessionId = resData.data.sessionId;
                uid = resData.data.uid;
            });
            return true;
        }

        Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
        return false;
    }

    public async Task<bool> GuestLoginCall(CancellationToken cancelToken = default) {
        if (!string.IsNullOrEmpty(sessionId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그인된 상태입니다."));
            return false;
        }

        Managers.ExecuteAtMainThread(() => Util.Log("게스트 로그인 요청을 보냅니다..."));

        string responseText = await SendRequestAsync(HttpMethod.Post, _guestLoginUrl, null, false, cancelToken);
        if (responseText == null) return false;

        AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
        if (resData != null && resData.success) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log($"게스트 로그인 성공! [Session: {resData.data.sessionId} | GuestID: {resData.data.guestId}]");
                sessionId = resData.data.sessionId;
                uid = resData.data.uid;
                guestId = resData.data.guestId;
            });
            return true;
        }

        Managers.ExecuteAtMainThread(() => Util.LogError("서버에서 실패 응답을 보냈습니다."));
        return false;
    }

    public async Task<bool> TestLogoutCall(CancellationToken cancelToken = default) {
        if (string.IsNullOrEmpty(sessionId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("이미 로그아웃 되어 있거나 세션이 없습니다."));
            return false;
        }

        Managers.ExecuteAtMainThread(() => Util.Log("로그아웃 요청을 보냅니다..."));

        string responseText = await SendRequestAsync(HttpMethod.Post, "api/logout", null, true, cancelToken);
        if (responseText == null) return false;

        AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);
        if (resData != null && resData.success) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log($"로그아웃 성공: {responseText}");
                // 로컬 상태 초기화
                sessionId = null;
                guestId = null;
                uid = 0;
            });
            return true;
        }

        Managers.ExecuteAtMainThread(() => Util.LogError("로그아웃 처리에 실패했습니다."));
        return false;
    }

    public async Task<bool> StartMatchCall(int mapId, string loadoutType, EquippedItem[] equippedItems, CancellationToken cancelToken = default) {
        if (string.IsNullOrEmpty(sessionId)) {
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
        if (!responseText.Trim().StartsWith("{"))
        {
            Managers.ExecuteAtMainThread(() => Util.LogError("서버 응답이 JSON 형식이 아닙니다."));
            return false;
        }



        // 응답 데이터 파싱
        MatchStartResponse resData = JsonUtility.FromJson<MatchStartResponse>(responseText);

        if (resData != null) {
            if (resData.success) {
                Managers.ExecuteAtMainThread(() => {
                    ticketId = resData.data.ticketId;
                    Util.Log($"매칭 큐 진입 성공! [Ticket ID: {ticketId}]");
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
        if (string.IsNullOrEmpty(sessionId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("로그인이 필요한 기능입니다."));
            return false;
        }

        if (string.IsNullOrEmpty(ticketId)) {
            Managers.ExecuteAtMainThread(() => Util.LogWarning("취소할 매칭 티켓이 없습니다."));
            return false;
        }

        Managers.ExecuteAtMainThread(() => Util.Log("매치메이킹 취소를 요청합니다..."));

        // JSON 데이터 조립
        MatchTicket reqData = new MatchTicket {
            ticketId = ticketId
        };
        string jsonString = JsonUtility.ToJson(reqData);

        // API 호출 (POST, requireAuth = true)
        string responseText = await SendRequestAsync(HttpMethod.Post, _matchCancelUrl, jsonString, true, cancelToken);
        if (responseText == null) return false;

        // 응답 파싱
        BaseResponse resData = JsonUtility.FromJson<BaseResponse>(responseText);

        if (resData != null) {
            if (resData.success) {
                Managers.ExecuteAtMainThread(() => {
                    Util.Log("매칭 취소 완료!");
                    ticketId = null;
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
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(ticketId)) {
            return false;
        }

        string url = $"{_matchStatusUrl}?ticketId={ticketId}";
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
                    Managers.ExecuteAtMainThread(() => {
                        Util.Log($"매칭 성공! [방 접속 정보 - IP: {resData.data.udpServerIp}, Port: {resData.data.udpServerPort}]");
                    });

                    Managers.ExecuteAtMainThread(() => {
                        // TODO : 받아온 정보를 토대로 게임 서버에 접속하는 로직 추가 (UDP 세션 생성 등)
                    });
                    return true;
                }
            }
            else {
                // 서버 로직 실패 (티켓 만료 등)
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError($"매칭 상태 확인 실패");
                    ticketId = null;
                });
                return false;
            }
        }
        return false;
    }
}
