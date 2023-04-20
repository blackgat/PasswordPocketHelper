using System.Text;

namespace PasswordPocketHelper.Utility
{
    internal static class StringExt
    {
        public static string ReduceStringSize(this string text, int maxDataLength, Encoding encoding)
        {
            var data = encoding.GetBytes(text);
            if (data.Length > maxDataLength)
            {
                return text.Substring(0, text.Length - 1).ReduceStringSize(maxDataLength, encoding);
            }

            return text;
        }

    }
}
