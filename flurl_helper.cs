using Flurl.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YourNamespace.Helpers
{
    /// <summary>
    /// Flurl.Http를 사용한 HTTP 요청 Helper 클래스
    /// </summary>
    public class FlurlHttpHelper
    {
        private readonly string _baseUrl;
        private readonly int _defaultTimeout;

        public FlurlHttpHelper(string baseUrl, int defaultTimeoutSeconds = 30)
        {
            _baseUrl = baseUrl;
            _defaultTimeout = defaultTimeoutSeconds;
        }

        #region POST 메소드 (파일 없음)

        /// <summary>
        /// 기본 POST 요청 (JSON)
        /// </summary>
        public async Task<T> PostAsync<T>(
            string endpoint,
            object data,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);
            return await request.PostJsonAsync(data, cancellationToken).ReceiveJson<T>();
        }

        /// <summary>
        /// POST 요청 (폼 데이터)
        /// </summary>
        public async Task<T> PostFormAsync<T>(
            string endpoint,
            Dictionary<string, string> formData,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);
            return await request.PostUrlEncodedAsync(formData, cancellationToken).ReceiveJson<T>();
        }

        /// <summary>
        /// POST 요청 (응답 문자열)
        /// </summary>
        public async Task<string> PostAsStringAsync(
            string endpoint,
            object data,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);
            return await request.PostJsonAsync(data, cancellationToken).ReceiveString();
        }

        #endregion

        #region POST 메소드 (단일 파일)

        /// <summary>
        /// 단일 파일 업로드 (Stream)
        /// </summary>
        public async Task<T> PostWithFileAsync<T>(
            string endpoint,
            Stream fileStream,
            string fileName,
            string fieldName = "file",
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);

            return await request.PostMultipartAsync(mp =>
            {
                mp.AddFile(fieldName, fileStream, fileName);

                if (formData != null)
                {
                    foreach (var kvp in formData)
                    {
                        mp.AddString(kvp.Key, kvp.Value);
                    }
                }
            }, cancellationToken).ReceiveJson<T>();
        }

        /// <summary>
        /// 단일 파일 업로드 (파일 경로)
        /// </summary>
        public async Task<T> PostWithFileAsync<T>(
            string endpoint,
            string filePath,
            string fieldName = "file",
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

            using var fileStream = File.OpenRead(filePath);
            var fileName = Path.GetFileName(filePath);

            return await PostWithFileAsync<T>(
                endpoint, fileStream, fileName, fieldName,
                formData, headers, queryParams, timeoutSeconds, cancellationToken);
        }

        /// <summary>
        /// 단일 파일 업로드 (byte 배열)
        /// </summary>
        public async Task<T> PostWithFileAsync<T>(
            string endpoint,
            byte[] fileBytes,
            string fileName,
            string fieldName = "file",
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(fileBytes);
            return await PostWithFileAsync<T>(
                endpoint, stream, fileName, fieldName,
                formData, headers, queryParams, timeoutSeconds, cancellationToken);
        }

        #endregion

        #region POST 메소드 (다중 파일)

        /// <summary>
        /// 다중 파일 업로드 (파일 경로 목록)
        /// </summary>
        public async Task<T> PostWithFilesAsync<T>(
            string endpoint,
            List<string> filePaths,
            string fieldName = "files",
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);

            var streams = new List<Stream>();

            try
            {
                return await request.PostMultipartAsync(mp =>
                {
                    foreach (var filePath in filePaths)
                    {
                        if (!File.Exists(filePath))
                            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

                        var fileStream = File.OpenRead(filePath);
                        streams.Add(fileStream);
                        var fileName = Path.GetFileName(filePath);
                        mp.AddFile(fieldName, fileStream, fileName);
                    }

                    if (formData != null)
                    {
                        foreach (var kvp in formData)
                        {
                            mp.AddString(kvp.Key, kvp.Value);
                        }
                    }
                }, cancellationToken).ReceiveJson<T>();
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream?.Dispose();
                }
            }
        }

        /// <summary>
        /// 다중 파일 업로드 (서로 다른 필드명)
        /// </summary>
        public async Task<T> PostWithMultipleFilesAsync<T>(
            string endpoint,
            Dictionary<string, string> filePathsWithFieldNames,
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);

            var streams = new List<Stream>();

            try
            {
                return await request.PostMultipartAsync(mp =>
                {
                    foreach (var kvp in filePathsWithFieldNames)
                    {
                        var fieldName = kvp.Key;
                        var filePath = kvp.Value;

                        if (!File.Exists(filePath))
                            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

                        var fileStream = File.OpenRead(filePath);
                        streams.Add(fileStream);
                        var fileName = Path.GetFileName(filePath);
                        mp.AddFile(fieldName, fileStream, fileName);
                    }

                    if (formData != null)
                    {
                        foreach (var kvp in formData)
                        {
                            mp.AddString(kvp.Key, kvp.Value);
                        }
                    }
                }, cancellationToken).ReceiveJson<T>();
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream?.Dispose();
                }
            }
        }

        /// <summary>
        /// 다중 파일 업로드 (FileUploadInfo 객체 사용)
        /// </summary>
        public async Task<T> PostWithFilesAsync<T>(
            string endpoint,
            List<FileUploadInfo> files,
            Dictionary<string, string> formData = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);

            return await request.PostMultipartAsync(mp =>
            {
                foreach (var file in files)
                {
                    mp.AddFile(file.FieldName, file.Stream, file.FileName);
                }

                if (formData != null)
                {
                    foreach (var kvp in formData)
                    {
                        mp.AddString(kvp.Key, kvp.Value);
                    }
                }
            }, cancellationToken).ReceiveJson<T>();
        }

        #endregion

        #region GET 메소드

        /// <summary>
        /// GET 요청
        /// </summary>
        public async Task<T> GetAsync<T>(
            string endpoint,
            Dictionary<string, string> headers = null,
            Dictionary<string, object> queryParams = null,
            int? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(endpoint, headers, queryParams, timeoutSeconds);
            return await request.GetJsonAsync<T>(cancellationToken);
        }

        #endregion

        #region Private Helper 메소드

        /// <summary>
        /// IFlurlRequest 빌드
        /// </summary>
        private IFlurlRequest BuildRequest(
            string endpoint,
            Dictionary<string, string> headers,
            Dictionary<string, object> queryParams,
            int? timeoutSeconds)
        {
            var request = _baseUrl
                .AppendPathSegment(endpoint)
                .WithTimeout(timeoutSeconds ?? _defaultTimeout);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request = request.WithHeader(header.Key, header.Value);
                }
            }

            if (queryParams != null)
            {
                request = request.SetQueryParams(queryParams);
            }

            return request;
        }

        #endregion
    }

    /// <summary>
    /// 파일 업로드 정보 클래스
    /// </summary>
    public class FileUploadInfo : IDisposable
    {
        public string FieldName { get; set; }
        public Stream Stream { get; set; }
        public string FileName { get; set; }

        public FileUploadInfo(string fieldName, Stream stream, string fileName)
        {
            FieldName = fieldName;
            Stream = stream;
            FileName = fileName;
        }

        public FileUploadInfo(string fieldName, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

            FieldName = fieldName;
            Stream = File.OpenRead(filePath);
            FileName = Path.GetFileName(filePath);
        }

        public void Dispose()
        {
            Stream?.Dispose();
        }
    }

    /// <summary>
    /// API 응답 표준 모델
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public int StatusCode { get; set; }
    }
}