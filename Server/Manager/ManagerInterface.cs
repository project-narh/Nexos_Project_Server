using System.Reflection;

namespace Server.Manager
{
    public interface ManagerInterface
    {
        public void Init();
    }

    public static class ManagerInitializer
    {
        public static void InitializeAll()
        {
            var managerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    typeof(ManagerInterface).IsAssignableFrom(t) &&
                    !t.IsInterface && !t.IsAbstract);

            foreach (var type in managerTypes)
            {
                try
                {
                    // 📌 Instance 프로퍼티 강제 호출
                    var instanceProp = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                    if (instanceProp?.GetValue(null) is ManagerInterface manager)
                    {
                        manager.Init();
                        Console.WriteLine($"[Manager] {type.Name} 초기화 완료");
                    }
                    else
                    {
                        Console.WriteLine($"[Manager] {type.Name} → Instance 프로퍼티가 없거나 Init 실패");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Manager] {type.Name} 초기화 에러: {ex.Message}");
                }
            }
        }
    }
}
