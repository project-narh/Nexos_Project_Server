using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Packet;
using static S_PlayerList;
using UDP;
using System.Buffers;
using ZstdSharp.Unsafe;


namespace Server.Web
{
    public class Startup
    {
        //Dictionary<WebSocket, int> LoginSocket = new Dictionary<WebSocket, int>();
        Dictionary<WebSocket, (bool isLogin, int uid)> SocketList = new Dictionary<WebSocket, (bool, int)>();
        Dictionary<int, WebSocket> _Socket = new Dictionary<int, WebSocket>(); // 빠른 조회를 위해서 접속된 클라
        public static Func<int, string, Task> _SendToClient;
        public static Func<int, string, bool, Task> _LoginSendToClient;
        private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

        int temp_id = 0;

        public void ConfigureServices(IServiceCollection services)
        {
            // DI Container
            services.Configure<WebSocketOptions>(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(120);
            });
            services.AddCors();

        }

        //미들 웨어 설정
        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            _SendToClient = SendToClient;
            _LoginSendToClient = SendToClient;
            WebPacketHandler._AddUser += ADD_User;
            WebPacketHandler._CheckUserAsync += CheckLogin;
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // 개발자 페이지
            }
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/image/item"))
                {
                    Console.WriteLine("이미지 요청 감지:");
                    Console.WriteLine($"User-Agent: {context.Request.Headers["User-Agent"]}");
                    Console.WriteLine($"Referer: {context.Request.Headers["Referer"]}");
                    Console.WriteLine($"IP: {context.Connection.RemoteIpAddress}");
                }

                await next();
            });
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Content-Type", "application/json");
                await next();
            });
            app.UseCors(builder =>
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader());

            app.UseStaticFiles();
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/metamask-auth", HttpHandler.MetaMaskLoginHandler);
            });
            
            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var WebSockets = await context.WebSockets.AcceptWebSocketAsync();
                    int temp = ++temp_id;
                    lock (SocketList)
                    {
                        SocketList[WebSockets] = (false, temp);
                    }
                    Console.WriteLine($"{temp}에 임시 등록");

                    string connetion = JsonSerializer.Serialize(new { tempid = temp }, Startup.jsonOptions);
                    await SendToClient(WebSockets, connetion);
                    await HandleWebSocket(WebSockets);
                }
                else
                {
                    await next();
                }
            });
        }


        private async Task<bool> CheckLogin(int uid)
        {
            if (_Socket.TryGetValue(uid, out var socket) && socket != null && socket.State == WebSocketState.Open)
            {
                Console.WriteLine($"[WebSocket] UID {uid}는 연결되어 있음");
                return false;
                // socket 사용 가능
            }
            else
            {
                Console.WriteLine($"[WebSocket] UID {uid}에 대한 유효한 연결이 없음");
                return true;
            }
        }

        private async Task HandleWebSocket(WebSocket webSocket)
        {

            //var buffer = new byte[1024 * 4]; // 일단 4KB로 설정 (후에 테스트 하고 확정)
            var buffer = _bufferPool.Rent(1024 * 4);
            List<byte> messageBuffer = new List<byte>();
            WebSocketReceiveResult result;

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        //messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
                        var segment = new ArraySegment<byte>(buffer, 0, result.Count);
                        messageBuffer.AddRange(segment);
                        //messageBuffer.AddRange(buffer[..result.Count]);
                    } while (!result.EndOfMessage);

                    string reciveMessage = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();

                    int id = -1;

                    if(SocketList.TryGetValue(webSocket, out var data))
                    {
                        id = data.uid;
                        Console.WriteLine($"[WebSocket] 수신 : {id} = {reciveMessage}");

                        string response = await Process(id, reciveMessage);
                        if (!string.IsNullOrEmpty(response))
                        {
                            var responseBuffer = Encoding.UTF8.GetBytes(response);
                            await webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WebSocket] {webSocket} 에 대한 데이터가 존재하지 않습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 예외 발생 : {ex.Message}");
            }
            finally
            {
                lock(SocketList)
                {
                    if(SocketList.TryGetValue(webSocket, out var data))
                    {
                        if(data.isLogin)
                        {

                            lock (_Socket)
                            {
                                _Socket.Remove(data.uid);
                            }
                        }
                        SocketList.Remove(webSocket);
                    }
                }
                try
                {
                    if (webSocket.State == WebSocketState.Open ||
                        webSocket.State == WebSocketState.CloseReceived ||
                        webSocket.State == WebSocketState.CloseSent)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebSocket] 연결 종료 중 오류 발생: {ex.Message}");
                }
                finally
                {
                    webSocket.Dispose();
                    _bufferPool.Return(buffer);
                }
            }

        }
        private async Task<string> Process(int id, string massage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(massage)) return JsonSerializer.Serialize(new { error = "빈 패킷" }, jsonOptions);

                using var json = JsonDocument.Parse(massage);
                var root = json.RootElement;

                if (!root.EnumerateObject().Any())
                {
                    return JsonSerializer.Serialize(new { error = "[WebSocket] 비어있는 요청" }, jsonOptions);
                }

                //foreach (var property in root.EnumerateObject())
                //{
                //    return await PacketManager.Instance.OnRecvPacketWeb(id, property.Name, property.Value);
                //}
                var property = root.EnumerateObject().First();
                return await PacketManager.Instance.OnRecvPacketWeb(id, property.Name, property.Value);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 에러: {ex.Message}");
                return JsonSerializer.Serialize(new { error = "유효하지 않은 패킷" }, jsonOptions);

            }
            return JsonSerializer.Serialize(new { error = "유효하지 않은 요청" }, jsonOptions);
        }

        public async Task SendToClient(WebSocket socket, string message)
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                Console.WriteLine("[전송] " + message + "값 확인");
                var buffer = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                Console.WriteLine($"[WebSocket] 클라이언트에게 메시지를 보낼 수 없음 (연결 안됨)");
            }
        }

        public async Task SendToClient(int playerId, string message)
        {
            WebSocket socket = _Socket[playerId];
            if (socket != null && socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                Console.WriteLine($"[WebSocket] 클라이언트 {playerId}에게 메시지를 보낼 수 없음 (연결 없음)");
            }
        }

        public async Task SendToClient(int playerId, string message ,bool isLogin)
        {
            WebSocket? socket = SocketList.FirstOrDefault(kvp => kvp.Value.isLogin && kvp.Value.uid == playerId).Key;
            if (socket != null && socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                Console.WriteLine($"[WebSocket] 로그인 클라이언트 {playerId}에게 메시지를 보낼 수 없음 (연결 없음)");
            }
        }

        public void ADD_User(int tempid, int uid)
        {
            WebSocket? socket = SocketList.FirstOrDefault(kvp => !kvp.Value.isLogin && kvp.Value.uid == tempid).Key;
            
            if(socket != null)
            {
                lock(SocketList)
                {
                    SocketList[socket] = (true, uid);
                }
                lock (_Socket)
                {
                    _Socket[uid] = socket;
                }
            }
        }

        public static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 🔹 한글 깨짐 방지
            WriteIndented = false // JSON 들여쓰기 (가독성 옵션, 필요 시 `true` 설정 가능)
        };
    }
}
