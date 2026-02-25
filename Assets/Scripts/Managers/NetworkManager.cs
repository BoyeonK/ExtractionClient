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


    public string version = "alphaTest";
    private const string _versionUrl = "api/version";
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

    public async Task<bool> GuestLoginCall(CancellationToken cancelToken = default) {
        Managers.ExecuteAtMainThread(() => {
            Util.Log("게스트 로그인 요청을 보냅니다...");
        });

        try {
            // POST 요청은 'Body(본문)'가 필요합니다. 
            StringContent emptyContent = new StringContent("{}", Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(_guestLoginUrl, emptyContent, cancelToken);

            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();

            GuestLoginResponse resData = JsonUtility.FromJson<GuestLoginResponse>(responseText);

            if (resData != null && resData.success) {
                Managers.ExecuteAtMainThread(() => {
                    Util.Log($"게스트 로그인 성공! [Session: {resData.data.sessionId} | GuestID: {resData.data.guestId}]");

                    // TODO: 여기서 발급받은 sessionId를 Managers.Data.SessionId 같은 곳에 저장해서
                    // 이후 다른 API 통신 헤더(Header)에 넣어서 쓸 수 있게 보관해야 합니다!
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
}
