using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sounder_APP.Models;

// 在裁剪模式下，System.Text.Json 需要源码生成上下文
// 相关上下文定义见 Models/JsonContext.cs

namespace Sounder_APP.Services
{
    /// <summary>
    /// API 客户端 - 访问 https://stand.homes/ 后端
    /// </summary>
    public class ApiService
    {
        private const string BaseUrl = "https://stand.homes/";
        private readonly HttpClient _httpClient;

        // 裁剪模式下必须使用源码生成上下文，而非反射 + JsonSerializerOptions
        private static readonly AppJsonContext JsonContext = AppJsonContext.Default;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        /// <summary>
        /// 获取资源列表（分页）
        /// </summary>
        public async Task<ApiListResponse<RemoteResource>> GetResourceListAsync(int page = 1, int limit = 10)
        {
            var url = $"api/sounders?page={page}&limit={limit}";
            Debug.WriteLine($"[API] GET {url}");
            try
            {
                var response = await _httpClient.GetAsync(url);
                Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API] Response body: {body[..Math.Min(body.Length, 500)]}");

                var result = JsonSerializer.Deserialize(body, JsonContext.ApiListResponseRemoteResource);
                if (result?.Data?.Items == null)
                {
                    Debug.WriteLine("[API] WARN: Response data is null");
                    return new ApiListResponse<RemoteResource> { Message = "响应解析失败" };
                }
                Debug.WriteLine($"[API] Success: Data.Items.Count={result.Data.Items.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                return new ApiListResponse<RemoteResource>
                {
                    Message = $"请求失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取资源详情
        /// </summary>
        public async Task<ApiDetailResponse<RemoteResource>> GetResourceDetailAsync(string id)
        {
            var url = $"api/sounders/{id}";
            Debug.WriteLine($"[API] GET {url}");
            try
            {
                var response = await _httpClient.GetAsync(url);
                Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API] Response body: {body[..Math.Min(body.Length, 500)]}");

                var result = JsonSerializer.Deserialize(body, JsonContext.ApiDetailResponseRemoteResource);
                if (result?.Data == null)
                {
                    Debug.WriteLine("[API] WARN: Detail data is null");
                    return new ApiDetailResponse<RemoteResource> { Message = "响应解析失败" };
                }
                Debug.WriteLine($"[API] Success: Data.Name={result.Data.Name}");
                foreach (var a in result.Data.AudioList)
                    Debug.WriteLine($"[API]   Audio: id='{a.Id}' name='{a.Name}' src='{a.Src}' duration={a.Duration}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                return new ApiDetailResponse<RemoteResource>
                {
                    Message = $"请求失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 搜索资源
        /// </summary>
        public async Task<ApiListResponse<RemoteResource>> SearchResourcesAsync(string keyword, int page = 1, int limit = 10)
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"api/sounders?keyword={encodedKeyword}&page={page}&limit={limit}";
            Debug.WriteLine($"[API] GET {url}");
            try
            {
                var response = await _httpClient.GetAsync(url);
                Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API] Response body: {body[..Math.Min(body.Length, 500)]}");

                var result = JsonSerializer.Deserialize(body, JsonContext.ApiListResponseRemoteResource);
                if (result?.Data?.Items == null)
                {
                    Debug.WriteLine("[API] WARN: Response data is null");
                    return new ApiListResponse<RemoteResource> { Message = "搜索结果为空" };
                }
                Debug.WriteLine($"[API] Success: Data.Items.Count={result.Data.Items.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                return new ApiListResponse<RemoteResource>
                {
                    Message = $"搜索失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取投稿列表（分页 + 状态筛选 + 搜索）
        /// </summary>
        public async Task<SubmissionListResponse> GetSubmissionsAsync(
            int page = 1, int size = 10, string? status = null, string? keyword = null)
        {
            var url = $"api/submissions?page={page}&size={size}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={status}";
            if (!string.IsNullOrEmpty(keyword))
                url += $"&keyword={Uri.EscapeDataString(keyword)}";

            Debug.WriteLine($"[API] GET {url}");
            try
            {
                var response = await _httpClient.GetAsync(url);
                Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API] Response body: {body[..Math.Min(body.Length, 500)]}");

                var result = JsonSerializer.Deserialize(body, JsonContext.SubmissionListResponse);
                if (result?.Data?.Submissions == null)
                {
                    Debug.WriteLine("[API] WARN: Submissions response data is null");
                    return new SubmissionListResponse { Message = "响应解析失败" };
                }
                Debug.WriteLine($"[API] Submissions Success: Count={result.Data.Submissions.Count}, Total={result.Data.Total}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                return new SubmissionListResponse
                {
                    Message = $"请求失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 创建投稿
        /// </summary>
        [UnconditionalSuppressMessage("ILLink", "IL2026", Justification = "data 为匿名类型，无法使用 source gen")]
        public async Task<SubmissionDetailResponse> CreateSubmissionAsync(object data)
        {
            var url = "api/submissions";
            Debug.WriteLine($"[API] POST {url}");
            try
            {
                var json = JsonSerializer.Serialize(data, JsonContext.Options);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API] Response body: {body[..Math.Min(body.Length, 500)]}");

                response.EnsureSuccessStatusCode();

                var result = JsonSerializer.Deserialize(body, JsonContext.SubmissionDetailResponse);
                if (result?.Data == null)
                {
                    Debug.WriteLine("[API] WARN: Create submission response data is null");
                    return new SubmissionDetailResponse { Message = "响应解析失败" };
                }
                Debug.WriteLine($"[API] CreateSubmission Success: Id={result.Data.Id}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                return new SubmissionDetailResponse
                {
                    Message = $"提交失败: {ex.Message}"
                };
            }
        }
    }
}
