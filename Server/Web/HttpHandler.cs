using System.Text.Json;

namespace Server.Web
{
    public static class HttpHandler
    {
        public static async Task MetaMaskLoginHandler(HttpContext context)
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            Console.WriteLine($"[Inventory Sync] 요청 수신: {body}");
            try
            {
                var data = JsonSerializer.Deserialize<MetaMaskLoginResult>(body);
                Console.WriteLine($"[MetaMask] address: {data.address}, signature: {data.signature}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetaMask] JSON 파싱 실패: {ex.Message}");
            }
            context.Response.StatusCode = 200;
        }

    }
    public class MetaMaskLoginResult
    {
        public string address { get; set; }
        public string signature { get; set; }
        public string message { get; set; }
    }
}
