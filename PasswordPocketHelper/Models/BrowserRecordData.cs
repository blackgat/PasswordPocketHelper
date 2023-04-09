using System;

namespace PasswordPocketHelper.Models
{
    [Serializable]
    internal class BrowserRecordData
    {
        public string name { get; set; }
        public Uri[] uris { get; set; }
        public string username { get; set; }
        public string password { get; set; }
    }
}