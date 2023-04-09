using System.Text.Json;

namespace PasswordPocketHelper.Utility
{
    internal class ObjectHelper
    {
        public static T? DeepCopy<T>(T source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
