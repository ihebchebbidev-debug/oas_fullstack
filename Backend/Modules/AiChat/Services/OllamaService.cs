using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MyApi.Modules.AiChat.DTOs;
using System.Diagnostics;

namespace MyApi.Modules.AiChat.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OllamaService> _logger;
        private readonly string _ollamaBaseUrl;
        private readonly string _defaultModel;

        // Concurrency control — prevents multiple LLM requests from overloading the VPS CPU
        private static readonly SemaphoreSlim _llmSemaphore = new(1, 1);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public OllamaService(
            IHttpClientFactory httpClientFactory,
            ILogger<OllamaService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                             ?? configuration.GetValue<string>("Ollama:BaseUrl")
                             ?? "http://localhost:11434";
            _defaultModel = Environment.GetEnvironmentVariable("OLLAMA_DEFAULT_MODEL")
                            ?? configuration.GetValue<string>("Ollama:DefaultModel")
                            ?? "mistral";

            _logger.LogInformation("🤖 OllamaService initialized — BaseUrl={BaseUrl}, DefaultModel={Model}", _ollamaBaseUrl, _defaultModel);
        }

        public async Task<GenerateWishResponseDto> GenerateAsync(GenerateWishRequestDto request, string userId)
        {
            var model = string.IsNullOrWhiteSpace(request.Model) ? _defaultModel : request.Model;
            var hasMessages = request.Messages != null && request.Messages.Count > 0;
            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "📨 [Generate] START — user={UserId}, model={Model}, mode={Mode}, msgCount={MsgCount}, temp={Temp}, maxTokens={MaxTokens}",
                userId, model, hasMessages ? "chat" : "generate",
                hasMessages ? request.Messages!.Count : 0,
                request.Temperature ?? 0.7f,
                request.MaxTokens ?? 1024);

            // Acquire semaphore — only 1 LLM request at a time to avoid CPU overload
            _logger.LogDebug("🔒 [Generate] Waiting for semaphore — user={UserId}", userId);
            await _llmSemaphore.WaitAsync();
            _logger.LogDebug("🔓 [Generate] Semaphore acquired — user={UserId}, waitMs={Ms}", userId, sw.ElapsedMilliseconds);

            try
            {
                var client = _httpClientFactory.CreateClient("Ollama");
                client.BaseAddress = new Uri(_ollamaBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(300);

                _logger.LogDebug("🔗 [Generate] HttpClient created — baseAddress={BaseAddress}, timeout={Timeout}s",
                    _ollamaBaseUrl, 300);

                HttpResponseMessage httpResponse;

                if (hasMessages)
                {
                    var chatPayload = new OllamaChatRequest
                    {
                        Model = model,
                        Messages = request.Messages!.Select(m => new OllamaChatMessage
                        {
                            Role = m.Role.ToLower(),
                            Content = m.Content
                        }).ToList(),
                        Stream = false,
                        Options = new OllamaChatOptions
                        {
                            Temperature = request.Temperature ?? 0.7f,
                            Num_predict = request.MaxTokens ?? 1024
                        }
                    };

                    var payloadJson = JsonSerializer.Serialize(chatPayload, _jsonOptions);
                    _logger.LogInformation("🔄 [Generate] POST /api/chat — model={Model}, messages={Count}, payloadLen={PayloadLen}",
                        model, chatPayload.Messages.Count, payloadJson.Length);
                    _logger.LogDebug("📋 [Generate] Payload: {Payload}", payloadJson.Substring(0, Math.Min(payloadJson.Length, 500)));

                    httpResponse = await client.PostAsJsonAsync("/api/chat", chatPayload, _jsonOptions);

                    _logger.LogInformation("📡 [Generate] Ollama responded — statusCode={StatusCode}, elapsed={Ms}ms",
                        (int)httpResponse.StatusCode, sw.ElapsedMilliseconds);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogError("❌ [Generate] Ollama /api/chat error — status={StatusCode}, body={Body}, elapsed={Ms}ms",
                            (int)httpResponse.StatusCode, errorBody, sw.ElapsedMilliseconds);
                        return new GenerateWishResponseDto
                        {
                            Success = false, Model = model,
                            Error = $"LLM returned HTTP {(int)httpResponse.StatusCode}: {errorBody}"
                        };
                    }

                    var rawResponse = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug("📄 [Generate] Raw response (first 300 chars): {Raw}", rawResponse.Substring(0, Math.Min(rawResponse.Length, 300)));

                    var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(rawResponse, _jsonOptions);
                    if (chatResponse?.Message == null || string.IsNullOrEmpty(chatResponse.Message.Content))
                    {
                        _logger.LogWarning("⚠️ [Generate] Empty response from Ollama chat — user={UserId}, elapsed={Ms}ms, rawLen={RawLen}",
                            userId, sw.ElapsedMilliseconds, rawResponse.Length);
                        return new GenerateWishResponseDto { Success = false, Model = model, Error = "Empty response from LLM" };
                    }

                    sw.Stop();
                    _logger.LogInformation("✅ [Generate] OK — user={UserId}, model={Model}, ollamaDuration={OllamaMs}ms, totalElapsed={TotalMs}ms, responseLen={Len}, responsePreview=\"{Preview}\"",
                        userId, chatResponse.Model,
                        chatResponse.Total_duration / 1_000_000, sw.ElapsedMilliseconds,
                        chatResponse.Message.Content.Length,
                        chatResponse.Message.Content.Substring(0, Math.Min(chatResponse.Message.Content.Length, 150)));

                    return new GenerateWishResponseDto
                    {
                        Success = true,
                        Response = chatResponse.Message.Content.Trim(),
                        Model = chatResponse.Model,
                        TotalDurationNs = chatResponse.Total_duration,
                        ConversationId = request.ConversationId
                    };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(request.Prompt))
                    {
                        _logger.LogWarning("⚠️ [Generate] No prompt provided in generate mode — user={UserId}", userId);
                        return new GenerateWishResponseDto { Success = false, Model = model, Error = "Either Prompt or Messages must be provided" };
                    }

                    var generatePayload = new OllamaGenerateRequest
                    {
                        Model = model,
                        Prompt = request.Prompt!,
                        Stream = false
                    };

                    _logger.LogInformation("🔄 [Generate] POST /api/generate — model={Model}, promptLen={PromptLen}",
                        model, request.Prompt!.Length);
                    _logger.LogDebug("📋 [Generate] Prompt preview: \"{Preview}\"",
                        request.Prompt!.Substring(0, Math.Min(request.Prompt.Length, 200)));

                    httpResponse = await client.PostAsJsonAsync("/api/generate", generatePayload, _jsonOptions);

                    _logger.LogInformation("📡 [Generate] Ollama responded — statusCode={StatusCode}, elapsed={Ms}ms",
                        (int)httpResponse.StatusCode, sw.ElapsedMilliseconds);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogError("❌ [Generate] Ollama /api/generate error — status={StatusCode}, body={Body}",
                            (int)httpResponse.StatusCode, errorBody);
                        return new GenerateWishResponseDto
                        {
                            Success = false, Model = model,
                            Error = $"LLM returned HTTP {(int)httpResponse.StatusCode}: {errorBody}"
                        };
                    }

                    var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaGenerateResponse>(_jsonOptions);
                    if (ollamaResponse == null || string.IsNullOrEmpty(ollamaResponse.Response))
                    {
                        _logger.LogWarning("⚠️ [Generate] Empty response from Ollama generate — user={UserId}", userId);
                        return new GenerateWishResponseDto { Success = false, Model = model, Error = "Empty response from LLM" };
                    }

                    sw.Stop();
                    _logger.LogInformation("✅ [Generate] OK — user={UserId}, model={Model}, ollamaDuration={OllamaMs}ms, totalElapsed={TotalMs}ms, responseLen={Len}",
                        userId, ollamaResponse.Model, ollamaResponse.Total_duration / 1_000_000, sw.ElapsedMilliseconds, ollamaResponse.Response.Length);

                    return new GenerateWishResponseDto
                    {
                        Success = true,
                        Response = ollamaResponse.Response.Trim(),
                        Model = ollamaResponse.Model,
                        TotalDurationNs = ollamaResponse.Total_duration,
                        ConversationId = request.ConversationId
                    };
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("⏱️ [Generate] TIMEOUT after {Ms}ms — user={UserId}, model={Model}", sw.ElapsedMilliseconds, userId, model);
                return new GenerateWishResponseDto { Success = false, Model = model, Error = "Request timed out — the LLM server took too long to respond" };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "🔌 [Generate] Cannot reach Ollama at {Url} — user={UserId}, elapsed={Ms}ms, innerEx={Inner}",
                    _ollamaBaseUrl, userId, sw.ElapsedMilliseconds, ex.InnerException?.Message ?? "(none)");
                return new GenerateWishResponseDto { Success = false, Model = model, Error = $"Cannot reach LLM server at {_ollamaBaseUrl}: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [Generate] Unexpected error — user={UserId}, elapsed={Ms}ms, exType={ExType}, message={Msg}",
                    userId, sw.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
                return new GenerateWishResponseDto { Success = false, Model = model, Error = "An unexpected error occurred while generating the response" };
            }
            finally
            {
                _llmSemaphore.Release();
                _logger.LogDebug("🔓 [Generate] Semaphore released — user={UserId}", userId);
            }
        }

        /// <summary>
        /// Streams chat response as SSE events — fully async I/O.
        /// </summary>
        public async Task StreamChatAsync(GenerateWishRequestDto request, string userId, Stream responseStream, CancellationToken cancellationToken)
        {
            var model = string.IsNullOrWhiteSpace(request.Model) ? _defaultModel : request.Model;
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("🔄 [StreamChat] START — user={UserId}, model={Model}, msgCount={MsgCount}, temp={Temp}, maxTokens={MaxTokens}",
                userId, model,
                request.Messages?.Count ?? 0,
                request.Temperature ?? 0.7f,
                request.MaxTokens ?? 1024);

            // Acquire semaphore — only 1 LLM request at a time
            _logger.LogDebug("🔒 [StreamChat] Waiting for semaphore — user={UserId}", userId);
            await _llmSemaphore.WaitAsync(cancellationToken);
            _logger.LogDebug("🔓 [StreamChat] Semaphore acquired — user={UserId}", userId);

            try
            {
                var client = _httpClientFactory.CreateClient("Ollama");
                client.BaseAddress = new Uri(_ollamaBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(300);

                var messages = (request.Messages ?? new List<ChatMessageDto>())
                    .Select(m => new OllamaChatMessage { Role = m.Role.ToLower(), Content = m.Content })
                    .ToList();

                if (messages.Count == 0 && !string.IsNullOrWhiteSpace(request.Prompt))
                {
                    messages.Add(new OllamaChatMessage { Role = "user", Content = request.Prompt! });
                    _logger.LogDebug("📝 [StreamChat] Converted prompt to user message — promptLen={Len}", request.Prompt!.Length);
                }

                // Log all messages being sent
                for (int i = 0; i < messages.Count; i++)
                {
                    _logger.LogDebug("📝 [StreamChat] Message[{Index}] role={Role}, contentLen={Len}, preview=\"{Preview}\"",
                        i, messages[i].Role, messages[i].Content.Length,
                        messages[i].Content.Substring(0, Math.Min(messages[i].Content.Length, 120)));
                }

                var chatPayload = new OllamaChatRequest
                {
                    Model = model,
                    Messages = messages,
                    Stream = true,
                    Options = new OllamaChatOptions
                    {
                        Temperature = request.Temperature ?? 0.7f,
                        Num_predict = request.MaxTokens ?? 1024
                    }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(chatPayload, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = jsonContent };

                // Send SSE keep-alive comment immediately so proxies don't timeout
                await WriteSseLineAsync(responseStream, ": keepalive", cancellationToken);
                _logger.LogDebug("💓 [StreamChat] Sent initial keepalive — elapsed={Ms}ms", sw.ElapsedMilliseconds);

                _logger.LogInformation("📡 [StreamChat] Sending request to Ollama — url={Url}/api/chat", _ollamaBaseUrl);
                var httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                _logger.LogInformation("📡 [StreamChat] Ollama headers received — statusCode={StatusCode}, elapsed={Ms}ms",
                    (int)httpResponse.StatusCode, sw.ElapsedMilliseconds);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("❌ [StreamChat] Ollama error — status={StatusCode}, body={Body}, elapsed={Ms}ms",
                        (int)httpResponse.StatusCode, errorBody, sw.ElapsedMilliseconds);
                    var errorEvent = JsonSerializer.Serialize(new { error = $"LLM error {(int)httpResponse.StatusCode}: {errorBody}" });
                    await WriteSseLineAsync(responseStream, $"data: {errorEvent}", cancellationToken);
                    await WriteSseLineAsync(responseStream, "data: [DONE]", cancellationToken);
                    return;
                }

                _logger.LogInformation("📥 [StreamChat] Connected — first byte at {Ms}ms, starting chunk processing", sw.ElapsedMilliseconds);

                using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096);
                int chunkCount = 0;
                int totalContentLength = 0;
                var lastActivity = Stopwatch.StartNew();
                var firstChunkLogged = false;

                // Start a background keep-alive task to prevent proxy timeouts
                using var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var keepAliveTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!keepAliveCts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(15_000, keepAliveCts.Token);
                            if (lastActivity.ElapsedMilliseconds > 10_000)
                            {
                                _logger.LogDebug("💓 [StreamChat] Sending keepalive — lastActivity={Ms}ms ago", lastActivity.ElapsedMilliseconds);
                                await WriteSseLineAsync(responseStream, ": keepalive", keepAliveCts.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }, keepAliveCts.Token);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    lastActivity.Restart();

                    try
                    {
                        var chunk = JsonSerializer.Deserialize<OllamaChatStreamChunk>(line, _jsonOptions);
                        if (chunk?.Message?.Content != null)
                        {
                            chunkCount++;
                            totalContentLength += chunk.Message.Content.Length;

                            if (!firstChunkLogged)
                            {
                                _logger.LogInformation("⚡ [StreamChat] First content chunk — user={UserId}, elapsed={Ms}ms, content=\"{Content}\"",
                                    userId, sw.ElapsedMilliseconds, chunk.Message.Content);
                                firstChunkLogged = true;
                            }

                            var sseData = JsonSerializer.Serialize(new
                            {
                                choices = new[]
                                {
                                    new
                                    {
                                        delta = new { content = chunk.Message.Content },
                                        index = 0
                                    }
                                },
                                model = chunk.Model
                            });
                            await WriteSseLineAsync(responseStream, $"data: {sseData}", cancellationToken);
                        }

                        if (chunk?.Done == true)
                        {
                            sw.Stop();
                            _logger.LogInformation("✅ [StreamChat] DONE — user={UserId}, model={Model}, chunks={ChunkCount}, totalContentLen={ContentLen}, elapsed={Ms}ms, ollamaDuration={OllamaMs}ms",
                                userId, chunk.Model, chunkCount, totalContentLength, sw.ElapsedMilliseconds, chunk.Total_duration / 1_000_000);
                            await WriteSseLineAsync(responseStream, "data: [DONE]", cancellationToken);
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("⚠️ [StreamChat] Malformed chunk — error={Error}, rawLine=\"{Line}\"",
                            ex.Message, line.Substring(0, Math.Min(line.Length, 200)));
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("🛑 [StreamChat] Client disconnected — user={UserId}, chunks={ChunkCount}, elapsed={Ms}ms", userId, chunkCount, sw.ElapsedMilliseconds);
                }

                keepAliveCts.Cancel();
                try { await keepAliveTask; } catch { }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("⏱️ [StreamChat] TIMEOUT after {Ms}ms — user={UserId}, model={Model}", sw.ElapsedMilliseconds, userId, model);
                try
                {
                    var errorEvent = JsonSerializer.Serialize(new { error = "Stream request timed out" });
                    await WriteSseLineAsync(responseStream, $"data: {errorEvent}", CancellationToken.None);
                    await WriteSseLineAsync(responseStream, "data: [DONE]", CancellationToken.None);
                }
                catch { }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "🔌 [StreamChat] Cannot reach Ollama at {Url} — user={UserId}, elapsed={Ms}ms, innerEx={Inner}",
                    _ollamaBaseUrl, userId, sw.ElapsedMilliseconds, ex.InnerException?.Message ?? "(none)");
                var errorEvent = JsonSerializer.Serialize(new { error = $"Cannot reach LLM: {ex.Message}" });
                await WriteSseLineAsync(responseStream, $"data: {errorEvent}", cancellationToken);
                await WriteSseLineAsync(responseStream, "data: [DONE]", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [StreamChat] Unexpected error — user={UserId}, elapsed={Ms}ms, exType={ExType}, message={Msg}",
                    userId, sw.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
                var errorEvent = JsonSerializer.Serialize(new { error = ex.Message });
                await WriteSseLineAsync(responseStream, $"data: {errorEvent}", cancellationToken);
                await WriteSseLineAsync(responseStream, "data: [DONE]", cancellationToken);
            }
            finally
            {
                _llmSemaphore.Release();
                _logger.LogDebug("🔓 [StreamChat] Semaphore released — user={UserId}", userId);
            }
        }

        /// <summary>
        /// Write a single SSE line — fully async, no synchronous IO.
        /// </summary>
        private static async Task WriteSseLineAsync(Stream stream, string line, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n\n");
            await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct);
            await stream.FlushAsync(ct);
        }
    }
}
