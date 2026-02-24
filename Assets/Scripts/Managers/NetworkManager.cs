using System;
using System.Net.Http;
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
}
