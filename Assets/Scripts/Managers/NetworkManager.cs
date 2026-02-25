using NUnit.Framework.Interfaces;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager {
    private static readonly HttpClient _httpClient = new HttpClient {
        BaseAddress = new Uri(GitIgnores.baseUrl)
    };
    public string sessionId = null;
    public string guestId = null;
    public int uid = 0;

    public string version = "alphaTest";
    private const string _versionUrl = "api/version";
    private const string _signupUrl = "api/signup";
    private const string _loginUrl = "api/login";
    private const string _guestLoginUrl = "api/guest";

    public async Task<bool> TestCall(CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => {
            Util.Log("서버로 요청을 보냅니다...");
        });

        try {
            //cts가 취소되면, 즉 통신을 보낸 객체가 파괴되서 OnDestroy가 호출되면, OperationCanceledException이 발생함.
            HttpResponseMessage response = await _httpClient.GetAsync(_versionUrl, cancelToken);

            response.EnsureSuccessStatusCode();
            string responseText = await response.Content.ReadAsStringAsync();
            
            Managers.ExecuteAtMainThread(() => {
                Util.Log($"서버 응답 성공: {responseText}");
            });
            return true;
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log("네트워크 요청이 사용자에 의해 안전하게 취소되었습니다.");
            });
            return false;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogError($"네트워크 에러 발생: {e.Message}");
            });
            return false;
        }
    }

    public async Task<bool> TestCreateAccountCall(string id, string password, CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => {
            Util.Log("계정 생성 요청을 보냅니다...");
        });
        try {
            CreateAccountRequestData reqData = new CreateAccountRequestData { id = id, password = password };
            string jsonString = JsonUtility.ToJson(reqData);

            StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_signupUrl, content, cancelToken);
            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();
            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);

            if (resData != null && resData.success) {
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"계정 생성 성공! [Session: {resData.data.sessionId} ]");

                    sessionId = resData.data.sessionId;
                    uid = resData.data.uid;
                });
                return true;
            }
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError("서버에서 실패 응답을 보냈습니다.");
                });
                return false;
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log("계정 생성 요청이 사용자에 의해 안전하게 취소되었습니다.");
            });
            return false;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogError($"계정 생성 네트워크 에러 발생: {e.Message}");
            });
            return false;
        }
    }

    public async Task<bool> TestLoginCall(string id, string password, CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => {
            Util.Log("로그인 요청을 보냅니다...");
        });
        try {
            LoginRequestData reqData = new LoginRequestData { id = id, password = password };
            string jsonString = JsonUtility.ToJson(reqData);

            StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_loginUrl, content, cancelToken);
            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();
            AuthResponse resData = JsonUtility.FromJson<AuthResponse>(responseText);

            if (resData != null && resData.success) {
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그인 성공! [Session: {resData.data.sessionId} ]");

                    sessionId = resData.data.sessionId;
                    uid = resData.data.uid;
                });
                return true;
            }
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError("서버에서 실패 응답을 보냈습니다.");
                });
                return false;
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log("로그인 요청이 사용자에 의해 안전하게 취소되었습니다.");
            });
            return false;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogError($"로그인 네트워크 에러 발생: {e.Message}");
            });
            return false;
        }
    }

    public async Task<bool> GuestLoginCall(CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => {
            Util.Log("게스트 로그인 요청을 보냅니다...");
        });

        try {
            StringContent emptyContent = new StringContent("{}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_guestLoginUrl, emptyContent, cancelToken);
            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();
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
            else {
                Managers.ExecuteAtMainThread(() => {
                    Util.LogError("서버에서 실패 응답을 보냈습니다.");
                });
                return false;
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogWarning("게스트 로그인 요청이 안전하게 취소되었습니다.");
            });
            return false;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogError($"로그인 네트워크 에러 발생: {e.Message}");
            });
            return false;
        }
    }

    public async Task<bool> TestLogoutCall(CancellationToken cancelToken = default) {
        if (string.IsNullOrEmpty(sessionId)) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogWarning("이미 로그아웃 되어 있거나 세션이 없습니다.");
            });
            return false;
        }

        Managers.ExecuteAtMainThread(() => {
            Util.Log("로그아웃 요청을 보냅니다...");
        });
        try {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/logout")) {
                // 헤더 키('x-session-id')에 값을 넣어줍니다.
                request.Headers.Add("x-session-id", sessionId);
                request.Content = new StringContent("", Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request, cancelToken);
                response.EnsureSuccessStatusCode();
                string responseText = await response.Content.ReadAsStringAsync();

                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"로그아웃 성공: {responseText}");

                    sessionId = null;
                    guestId = null;
                    uid = 0;
                });
                return true;
            }
        }
        catch (OperationCanceledException) {
            Managers.ExecuteAtMainThread(() => {
                Util.Log("로그아웃 요청이 사용자에 의해 안전하게 취소되었습니다.");
            });
            return false;
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Util.LogError($"로그아웃 네트워크 에러 발생: {e.Message}");
            });
            return false;
        }
    }
}
