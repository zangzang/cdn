using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SseClient
{
    public class ServerSentEventsClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
        private readonly ILogger _logger;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private string _currentUrl;
        private bool _autoReconnect;
        private int _baseReconnectDelayMs;
        private readonly int _maxReconnectDelayMs = 300000; // 최대 5분
        private int _maxReconnectAttempts;
        private int _reconnectAttemptCount;

        public event EventHandler<SseMessageEventArgs> MessageReceived;
        public event EventHandler<SseErrorEventArgs> ErrorOccurred;
        public event EventHandler Connected;
        public event EventHandler<DisconnectEventArgs> Disconnected;
        public event EventHandler<ReconnectEventArgs> Reconnecting;

        public bool IsConnected => _cts != null && !_cts.IsCancellationRequested;
        public string ProcessInfo { get; private set; }

        public ServerSentEventsClient(
            ILogger logger = null,
            HttpClient httpClient = null,
            bool autoReconnect = false,
            int baseReconnectDelayMs = 5000,
            int maxReconnectAttempts = int.MaxValue,
            TimeSpan? timeout = null)
        {
            _logger = logger ?? NullLogger.Instance;

            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeHttpClient = false;
                _logger.LogDebug("Using injected HttpClient");
            }
            else
            {
                // SSE는 무한정 연결을 유지해야 하므로 Timeout.InfiniteTimeSpan 사용
                var httpTimeout = timeout ?? System.Threading.Timeout.InfiniteTimeSpan;
                _httpClient = new HttpClient
                {
                    Timeout = httpTimeout
                };
                _disposeHttpClient = true;
                _logger.LogDebug("Created new HttpClient with timeout: {Timeout}", 
                    httpTimeout == System.Threading.Timeout.InfiniteTimeSpan ? "Infinite" : httpTimeout.ToString());
            }

            _autoReconnect = autoReconnect;
            _baseReconnectDelayMs = baseReconnectDelayMs;
            _maxReconnectAttempts = maxReconnectAttempts;
            _reconnectAttemptCount = 0;

            InitializeProcessInfo();
        }

        private void InitializeProcessInfo()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                ProcessInfo = $"Process: {process.ProcessName} (PID: {process.Id}) | " +
                              $"Path: {process.MainModule.FileName} | " +
                              $"Started: {process.StartTime:yyyy-MM-dd HH:mm:ss}";
                _logger.LogInformation("Process info initialized: {ProcessInfo}", ProcessInfo);
            }
            catch (Exception ex)
            {
                ProcessInfo = $"Process Info Error: {ex.Message}";
                _logger.LogWarning(ex, "Failed to get process information");
            }
        }

        public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url), "URL cannot be null or empty");
            }

            _currentUrl = url;
            _logger.LogInformation("Starting connection to {Url}", url);
            await ConnectInternalAsync(url, cancellationToken);
        }

        private async Task ConnectInternalAsync(string url, CancellationToken cancellationToken = default)
        {
            if (_cts != null)
            {
                var error = new InvalidOperationException("Already connected");
                _logger.LogError(error, "Connection attempt while already connected");
                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.ConnectionError, "이미 연결되어 있습니다.", error));
                throw error;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var disconnectReason = DisconnectReason.Unknown;
            var shouldReconnect = false;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                _logger.LogDebug("Sending HTTP request to {Url}", url);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                }
                catch (HttpRequestException ex)
                {
                    disconnectReason = DisconnectReason.ConnectionFailed;
                    _logger.LogError(ex, "Connection failed to {Url}", url);
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.ConnectionError, $"서버 연결 실패: {url}", ex));
                    shouldReconnect = _autoReconnect;
                    throw;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    disconnectReason = DisconnectReason.Timeout;
                    _logger.LogError(ex, "Connection timeout to {Url}", url);
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.Timeout, "연결 시간 초과", ex));
                    shouldReconnect = _autoReconnect;
                    throw;
                }

                if (!response.IsSuccessStatusCode)
                {
                    disconnectReason = DisconnectReason.ServerError;
                    var statusError = new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    _logger.LogError("Server returned error status code: {StatusCode} {ReasonPhrase}", 
                        (int)response.StatusCode, response.ReasonPhrase);
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.InvalidUrl, 
                        $"잘못된 응답 코드: {(int)response.StatusCode} ({response.ReasonPhrase})", statusError));
                    shouldReconnect = _autoReconnect;
                    response.Dispose();
                    throw statusError;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType != "text/event-stream")
                {
                    disconnectReason = DisconnectReason.InvalidContentType;
                    var error = new InvalidOperationException($"잘못된 Content-Type: {contentType ?? "null"}");
                    _logger.LogError("Invalid Content-Type: {ContentType}, expected: text/event-stream", contentType ?? "null");
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.InvalidResponse,
                        $"잘못된 Content-Type: {contentType ?? "null"} (예상: text/event-stream)", error));
                    shouldReconnect = _autoReconnect;
                    response.Dispose();
                    throw error;
                }

                _logger.LogInformation("Successfully connected to SSE server");
                Connected?.Invoke(this, EventArgs.Empty);
                _reconnectAttemptCount = 0;

                await ReadStreamAsync(response, _cts.Token);

                disconnectReason = DisconnectReason.StreamEnded;
                shouldReconnect = _autoReconnect;
                _logger.LogInformation("Stream ended normally");
            }
            catch (OperationCanceledException)
            {
                disconnectReason = DisconnectReason.Cancelled;
                shouldReconnect = false;
                _logger.LogInformation("Connection cancelled");
            }
            catch (Exception ex)
            {
                if (disconnectReason == DisconnectReason.Unknown)
                {
                    disconnectReason = DisconnectReason.UnexpectedError;
                    _logger.LogError(ex, "Unexpected error occurred");
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.UnknownError, 
                        $"예상치 못한 오류: {ex.Message}", ex));
                    shouldReconnect = _autoReconnect;
                }
            }
            finally
            {
                _logger.LogInformation("Disconnected with reason: {Reason}", disconnectReason);
                Disconnected?.Invoke(this, new DisconnectEventArgs(disconnectReason));
                
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }

                if (shouldReconnect && !cancellationToken.IsCancellationRequested)
                {
                    await TryReconnectAsync(cancellationToken);
                }
            }
        }

        private async Task TryReconnectAsync(CancellationToken cancellationToken)
        {
            if (_maxReconnectAttempts > 0 && _reconnectAttemptCount >= _maxReconnectAttempts)
            {
                _logger.LogWarning("Max reconnect attempts ({MaxAttempts}) reached", _maxReconnectAttempts);
                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.ConnectionError,
                    $"최대 재연결 시도 횟수({_maxReconnectAttempts})에 도달했습니다.", null));
                return;
            }

            _reconnectAttemptCount++;

            // Exponential Backoff with Cap (최대 2^6 = 64배, 최대 5분)
            int multiplier = (int)Math.Pow(2, Math.Min(_reconnectAttemptCount - 1, 6));
            int delayMs = _baseReconnectDelayMs * multiplier;
            delayMs = Math.Min(delayMs, _maxReconnectDelayMs);

            _logger.LogInformation("Reconnecting in {DelayMs}ms (Attempt {AttemptNumber}/{MaxAttempts})", 
                delayMs, _reconnectAttemptCount, _maxReconnectAttempts > 0 ? _maxReconnectAttempts.ToString() : "unlimited");
            Reconnecting?.Invoke(this, new ReconnectEventArgs(_reconnectAttemptCount, _maxReconnectAttempts, delayMs));

            try
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Reconnect cancelled during delay");
                return;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await ConnectInternalAsync(_currentUrl, cancellationToken);
            }
        }

        public void EnableAutoReconnect(int baseDelayMs = 5000, int maxAttempts = int.MaxValue)
        {
            _autoReconnect = true;
            _baseReconnectDelayMs = baseDelayMs;
            _maxReconnectAttempts = maxAttempts;
            _logger.LogInformation("Auto-reconnect enabled: baseDelay={BaseDelayMs}ms, maxAttempts={MaxAttempts}", 
                baseDelayMs, maxAttempts > 0 ? maxAttempts.ToString() : "unlimited");
        }

        public void DisableAutoReconnect()
        {
            _autoReconnect = false;
            _logger.LogInformation("Auto-reconnect disabled");
        }

        public async Task ReconnectAsync()
        {
            if (string.IsNullOrEmpty(_currentUrl))
            {
                throw new InvalidOperationException("No URL to reconnect to. Call ConnectAsync first.");
            }

            _logger.LogInformation("Manual reconnect requested");
            Disconnect();
            await Task.Delay(1000);
            _reconnectAttemptCount = 0;
            await ConnectAsync(_currentUrl);
        }

        private async Task ReadStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            Stream stream = null;
            StreamReader reader = null;

            try
            {
#if NET48 || NETSTANDARD2_0
                stream = await response.Content.ReadAsStreamAsync();
#else
                stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#endif
                reader = new StreamReader(stream, Encoding.UTF8);

                string eventType = null;
                string id = null;
                var dataBuilder = new StringBuilder();
                int lineNumber = 0;

                _logger.LogDebug("Started reading SSE stream");

                while (!cancellationToken.IsCancellationRequested)
                {
                    string line;
                    try
                    {
                        line = await reader.ReadLineAsync();
                        lineNumber++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Stream read error at line {LineNumber}", lineNumber);
                        ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.StreamReadError,
                            $"스트림 읽기 오류 (라인 {lineNumber})", ex));
                        throw;
                    }

                    if (line == null)
                    {
                        _logger.LogDebug("Stream ended (null line received)");
                        break;
                    }

                    if (string.IsNullOrEmpty(line))
                    {
                        if (dataBuilder.Length > 0)
                        {
                            try
                            {
                                var data = dataBuilder.ToString().TrimEnd('\n');
                                var eventArgs = new SseMessageEventArgs
                                {
                                    EventType = eventType ?? "message",
                                    Data = data,
                                    Id = id
                                };

                                _logger.LogTrace("Received SSE message: EventType={EventType}, DataLength={DataLength}, Id={Id}", 
                                    eventArgs.EventType, data.Length, id ?? "null");
                                MessageReceived?.Invoke(this, eventArgs);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message at line {LineNumber}", lineNumber);
                                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.DataParseError,
                                    $"메시지 처리 중 오류 (라인 {lineNumber})", ex));
                            }

                            eventType = null;
                            id = null;
                            dataBuilder.Clear();
                        }
                        continue;
                    }

                    if (line.StartsWith(":"))
                    {
                        _logger.LogTrace("Comment line ignored: {Line}", line);
                        continue;
                    }

                    var colonIndex = line.IndexOf(':');
                    if (colonIndex == -1) continue;

                    var field = line.Substring(0, colonIndex);
                    var value = colonIndex + 1 < line.Length && line[colonIndex + 1] == ' '
                        ? line.Substring(colonIndex + 2)
                        : line.Substring(colonIndex + 1);

                    switch (field)
                    {
                        case "event":
                            eventType = value;
                            break;
                        case "data":
                            dataBuilder.AppendLine(value);
                            break;
                        case "id":
                            id = value;
                            break;
                        case "retry":
                            if (int.TryParse(value, out var retryMs) && retryMs > 0)
                            {
                                _baseReconnectDelayMs = retryMs;
                                _logger.LogInformation("Server requested retry delay: {RetryMs}ms", retryMs);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Stream processing error");
                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.StreamError, 
                    "스트림 처리 중 오류 발생", ex));
                throw;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Dispose();
                    _logger.LogTrace("StreamReader disposed");
                }
                if (stream != null)
                {
                    stream.Dispose();
                    _logger.LogTrace("Stream disposed");
                }
            }
        }

        public void Disconnect()
        {
            _logger.LogInformation("Disconnect requested");
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing SSE client");
            Disconnect();
            
            if (_disposeHttpClient && _httpClient != null)
            {
                _httpClient.Dispose();
                _logger.LogTrace("HttpClient disposed");
            }
            
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            
            _disposed = true;
        }
    }

    public class SseMessageEventArgs : EventArgs
    {
        public string EventType { get; set; }
        public string Data { get; set; }
        public string Id { get; set; }
    }

    public class SseErrorEventArgs : EventArgs
    {
        public SseErrorType ErrorType { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }

        public SseErrorEventArgs(SseErrorType errorType, string message, Exception exception)
        {
            ErrorType = errorType;
            Message = message;
            Exception = exception;
        }
    }

    public class DisconnectEventArgs : EventArgs
    {
        public DisconnectReason Reason { get; set; }
        public DisconnectEventArgs(DisconnectReason reason) => Reason = reason;
    }

    public class ReconnectEventArgs : EventArgs
    {
        public int AttemptNumber { get; set; }
        public int MaxAttempts { get; set; }
        public int DelayMs { get; set; }

        public ReconnectEventArgs(int attemptNumber, int maxAttempts, int delayMs)
        {
            AttemptNumber = attemptNumber;
            MaxAttempts = maxAttempts;
            DelayMs = delayMs;
        }
    }

    public enum SseErrorType
    {
        ConnectionError,
        InvalidUrl,
        InvalidResponse,
        Timeout,
        StreamError,
        StreamReadError,
        DataParseError,
        UnknownError
    }

    public enum DisconnectReason
    {
        Unknown,
        Cancelled,
        ConnectionFailed,
        ServerError,
        InvalidContentType,
        StreamEnded,
        UnexpectedError,
        Timeout
    }

    // 사용 예제
    class Program
    {
        static async Task Main(string[] args)
        {
            // ILogger 설정
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<ServerSentEventsClient>();

            // HttpClient 생성 (선택적, DI에서 주입받을 수도 있음)
            // SSE용이므로 Timeout을 무한으로 설정
            var httpClient = new HttpClient
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            using (var client = new ServerSentEventsClient(
                logger: logger,
                httpClient: httpClient,
                autoReconnect: true,
                baseReconnectDelayMs: 5000))
            {
                Console.WriteLine("=== Process Information ===");
                Console.WriteLine(client.ProcessInfo);
                Console.WriteLine();

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    logger.LogInformation("Shutdown signal received");
                    cts.Cancel();
                };

                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    logger.LogInformation("Process exiting");
                    cts.Cancel();
                };

                var url = args.Length > 0 ? args[0] : "https://your-sse-server.com/events";

                // 이벤트 구독
                client.Connected += (s, e) => logger.LogInformation("✓ Connected to SSE server");
                client.Disconnected += (s, e) => logger.LogInformation("✗ Disconnected: {Reason}", e.Reason);
                client.Reconnecting += (s, e) => logger.LogInformation("⟳ Reconnecting (Attempt {Attempt})", e.AttemptNumber);
                client.MessageReceived += (s, e) => logger.LogInformation("📨 [{EventType}] {Data}", e.EventType, e.Data);
                client.ErrorOccurred += (s, e) => logger.LogError(e.Exception, "❌ [{ErrorType}] {Message}", e.ErrorType, e.Message);

                try
                {
                    await client.ConnectAsync(url, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Connection cancelled");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Fatal error");
                }
            }

            httpClient.Dispose();
            Console.WriteLine("Process terminated.");
        }
    }
}