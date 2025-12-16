using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SseClient
{
    public class ServerSentEventsClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private string _currentUrl;
        private bool _autoReconnect;
        private int _baseReconnectDelayMs;
        private int _maxReconnectDelayMs = 300000; // 최대 5분
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
            TimeSpan? timeout = null,
            bool autoReconnect = false,
            int baseReconnectDelayMs = 5000,
            int maxReconnectAttempts = int.MaxValue)
        {
            _httpClient = new HttpClient
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(100)
            };

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
            }
            catch (Exception ex)
            {
                ProcessInfo = $"Process Info Error: {ex.Message}";
            }
        }

        public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
        {
            _currentUrl = url;
            await ConnectInternalAsync(url, cancellationToken);
        }

        private async Task ConnectInternalAsync(string url, CancellationToken cancellationToken = default)
        {
            if (_cts != null)
            {
                var error = new InvalidOperationException("Already connected");
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

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                }
                catch (HttpRequestException ex)
                {
                    disconnectReason = DisconnectReason.ConnectionFailed;
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.ConnectionError, $"서버 연결 실패: {url}", ex));
                    shouldReconnect = _autoReconnect;
                    throw;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    disconnectReason = DisconnectReason.Timeout;
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.Timeout, "연결 시간 초과", ex));
                    shouldReconnect = _autoReconnect;
                    throw;
                }

                if (!response.IsSuccessStatusCode)
                {
                    disconnectReason = DisconnectReason.ServerError;
                    var statusError = new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.InvalidUrl, $"잘못된 응답 코드: {(int)response.StatusCode}", statusError));
                    shouldReconnect = _autoReconnect;
                    throw statusError;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType != "text/event-stream")
                {
                    disconnectReason = DisconnectReason.InvalidContentType;
                    var error = new InvalidOperationException($"잘못된 Content-Type: {contentType ?? "null"} (예상: text/event-stream)");
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.InvalidResponse,
                        $"잘못된 Content-Type: {contentType ?? "null"}", error));
                    shouldReconnect = _autoReconnect;
                    throw error;
                }

                Connected?.Invoke(this, EventArgs.Empty);
                _reconnectAttemptCount = 0; // 성공적 연결 시 카운터 리셋

                await ReadStreamAsync(response, _cts.Token);

                disconnectReason = DisconnectReason.StreamEnded;
                shouldReconnect = _autoReconnect;
            }
            catch (OperationCanceledException)
            {
                disconnectReason = DisconnectReason.Cancelled;
                shouldReconnect = false;
            }
            catch (Exception ex)
            {
                if (disconnectReason == DisconnectReason.Unknown)
                {
                    disconnectReason = DisconnectReason.UnexpectedError;
                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.UnknownError, $"예상치 못한 오류: {ex.Message}", ex));
                    shouldReconnect = _autoReconnect;
                }
            }
            finally
            {
                Disconnected?.Invoke(this, new DisconnectEventArgs(disconnectReason));
                _cts?.Dispose();
                _cts = null;

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
                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.ConnectionError,
                    $"최대 재연결 시도 횟수({_maxReconnectAttempts})에 도달했습니다.", null));
                return;
            }

            _reconnectAttemptCount++;

            // Exponential Backoff with Cap (최대 5분)
            int delayMs = _baseReconnectDelayMs * (int)Math.Pow(2, Math.Min(_reconnectAttemptCount - 1, 6)); // 최대 2^6 = 64배
            delayMs = Math.Min(delayMs, _maxReconnectDelayMs);

            Reconnecting?.Invoke(this, new ReconnectEventArgs(_reconnectAttemptCount, _maxReconnectAttempts, delayMs));

            try
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return; // 취소 시 재연결 중단
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
        }

        public void DisableAutoReconnect()
        {
            _autoReconnect = false;
        }

        public async Task ReconnectAsync()
        {
            Disconnect();
            await Task.Delay(1000);
            _reconnectAttemptCount = 0;
            await ConnectAsync(_currentUrl);
        }

        private async Task ReadStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
#if NET48
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
#else
                await using (var stream = await response.Content.ReadAsStreamAsync())
                await using (var reader = new StreamReader(stream, Encoding.UTF8))
#endif
                {
                    string eventType = null;
                    string id = null;
                    var dataBuilder = new StringBuilder();
                    int lineNumber = 0;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line;
                        try
                        {
#if NET48
                            line = await reader.ReadLineAsync();
#else
                            line = await reader.ReadLineAsync();
#endif
                            lineNumber++;
                        }
                        catch (Exception ex)
                        {
                            ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.StreamReadError,
                                $"스트림 읽기 오류 (라인 {lineNumber})", ex));
                            throw;
                        }

                        if (line == null) break; // 스트림 종료

                        if (string.IsNullOrEmpty(line))
                        {
                            if (dataBuilder.Length > 0)
                            {
                                try
                                {
                                    var data = dataBuilder.ToString().TrimEnd('\n');
                                    MessageReceived?.Invoke(this, new SseMessageEventArgs
                                    {
                                        EventType = eventType ?? "message",
                                        Data = data,
                                        Id = id
                                    });
                                }
                                catch (Exception ex)
                                {
                                    ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.DataParseError,
                                        $"메시지 처리 중 오류 (라인 {lineNumber})", ex));
                                }

                                eventType = null;
                                id = null;
                                dataBuilder.Clear();
                            }
                            continue;
                        }

                        if (line.StartsWith(":")) continue; // 주석 무시

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
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                ErrorOccurred?.Invoke(this, new SseErrorEventArgs(SseErrorType.StreamError, "스트림 처리 중 오류 발생", ex));
                throw;
            }
        }

        public void Disconnect()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _httpClient?.Dispose();
            _cts?.Dispose();
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
        ConnectionError, InvalidUrl, InvalidResponse, Timeout,
        StreamError, StreamReadError, DataParseError, UnknownError
    }

    public enum DisconnectReason
    {
        Unknown, Cancelled, ConnectionFailed, ServerError,
        InvalidContentType, StreamEnded, UnexpectedError
    }

    // 사용 예제
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var client = new ServerSentEventsClient(autoReconnect: true, baseReconnectDelayMs: 5000))
            {
                Console.WriteLine("=== Process Information ===");
                Console.WriteLine(client.ProcessInfo);
                Console.WriteLine();

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("Shutdown signal received...");
                    cts.Cancel();
                };

                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    Console.WriteLine("Process exiting...");
                    cts.Cancel();
                };

                var url = args.Length > 0 ? args[0] : "https://your-sse-server.com/events";
                
                Console.WriteLine($"Connecting to {url}...");
                Console.WriteLine("Auto-reconnect enabled. Process will run until terminated.");
                Console.WriteLine();

                client.Connected += (s, e) => 
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ✓ Connected");
                    Console.ResetColor();
                };
                
                client.Disconnected += (s, e) => 
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ✗ Disconnected: {e.Reason}");
                    Console.ResetColor();
                };

                client.Reconnecting += (s, e) =>
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ⟳ Reconnecting... (Attempt {e.AttemptNumber}, waiting {e.DelayMs}ms)");
                    Console.ResetColor();
                };
                
                client.MessageReceived += (s, e) =>
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{e.EventType}] {e.Data}");
                };
                
                client.ErrorOccurred += (s, e) => 
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{e.ErrorType}] {e.Message}");
                    if (e.Exception != null)
                        Console.WriteLine($"  {e.Exception.GetType().Name}: {e.Exception.Message}");
                    Console.ResetColor();
                };

                try
                {
                    await client.ConnectAsync(url, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Connection cancelled.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fatal error: {ex.Message}");
                }
            }

            Console.WriteLine("Process terminated.");
        }
    }
}