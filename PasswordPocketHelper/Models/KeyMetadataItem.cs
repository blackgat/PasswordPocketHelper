using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;

namespace PasswordPocketHelper.Models
{
    [Serializable]
    internal class KeyMetadataItem
    {
        public string name { get; set; }
        public Uri[]? uris { get; set; }
        public string? username { get; set; }
        public string? password { get; set; }

        public KeyMetadataItem(string name)
        {
            this.name = name;
        }
    }

    internal static class KeyMetadataItemHelper
    {
        public static KeyMetadataItem ToKeyMetadataItem(this BitwardenExportDataItem item)
        {
            Debug.Assert(item != null);
            Debug.Assert(!string.IsNullOrEmpty(item.name));

            var keyMetadataItem = new KeyMetadataItem(item.name)
            {
                username = item.login.username,
                password = item.login.password
            };

            try
            {
                var uriList = new List<Uri>();
                if (item.login.uris != null)
                {
                    foreach (var loginUri in item.login.uris)
                    {
                        uriList.Add(loginUri.uri);
                    }
                }

                keyMetadataItem.uris = uriList.ToArray();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return keyMetadataItem;
        }

        public static KeyMetadataItem ToKeyMetadataItem(this ChromeCsvExportData item)
        {
            Debug.Assert(item != null);
            Debug.Assert(!string.IsNullOrEmpty(item.name));

            var uriList = new List<Uri>();
            if (!string.IsNullOrEmpty(item.url))
            {
                item.url = HttpUtility.UrlDecode(item.url); // Now it will be "xxx,xxx,xxx"
                var urls = item.url.Split(
                    new[] { "," },
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (var url in urls)
                {
                    var normalizedUrl = url;

                    // Don't known why have following urls.
                    if (url.ToLower().StartsWith("https//"))
                    {
                        normalizedUrl = url.ToLower().Replace("https//", "https://");
                    }
                    else if (url.ToLower().StartsWith("http//"))
                    {
                        normalizedUrl = url.ToLower().Replace("http//", "http://");
                    }

                    try
                    {
                        uriList.Add(new Uri(normalizedUrl));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Unable to convert {url} to Uri object. {e.Message}");
                    }
                }
            }

            var keyMetadataItem = new KeyMetadataItem(item.name)
            {
                username = item.username,
                password = item.password,
                uris = uriList.ToArray()
            };

            return keyMetadataItem;
        }
    }
}