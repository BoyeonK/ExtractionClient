using System;
using System.Net.Http;
using UnityEngine;

public class NetworkManager {
    private static readonly HttpClient _httpClient = new HttpClient();

    public async void TestCall() {
        Managers.ExecuteAtMainThread(() => {
            Debug.Log("NetworkManager.TestCall()이 메인 스레드에서 실행되었습니다!");
        });

        string url = "http://localhost:3000/api/version";

        try {
            // 요청 (응답이 올 때까지 백그라운드 스레드에서 비동기로 대기)
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            // HTTP 상태 코드가 200(OK)번대가 아니면 즉시 catch 문으로 빠짐
            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();
            Managers.ExecuteAtMainThread(() => {
                Debug.Log($"서버 응답 성공: {responseText}");
            });
        }
        catch (Exception e) {
            Managers.ExecuteAtMainThread(() => {
                Debug.LogError($"네트워크 에러 발생: {e.Message}");
            });
        }
    }
}
