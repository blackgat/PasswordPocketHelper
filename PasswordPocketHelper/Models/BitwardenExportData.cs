using System;

namespace PasswordPocketHelper.Models
{
    [Serializable]
    internal class BitwardenExportDataFolder
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    [Serializable]
    internal class BitwardenExportDataItemLoginUri
    {
        public string? match { get; set; }

        public Uri? uri { get; set; }
    }

    [Serializable]
    internal class BitwardenExportDataItemLogin
    {
        public BitwardenExportDataItemLoginUri[]? uris { get; set; }
        public string? username { get; set; }
        public string? password { get; set; }
        public string? totp { get; set; }
    }

    [Serializable]
    internal class BitwardenExportDataItem
    {
        public string? id { get; set; }
        public string? organizationId { get; set; }
        public string? folderId { get; set; }
        public int type { get; set; }
        public int repromopt { get; set; }
        public string? name { get; set; }
        public string? notes { get; set; }
        public bool favorite { get; set; }
        public BitwardenExportDataItemLogin? login { get; set; }
        public string? collectionIds { get; set; }
    }

    [Serializable]
    internal class BitwardenExportData
    {
        public bool encrypted { get; set; }
        public BitwardenExportDataFolder[]? folders { get; set; }
        public BitwardenExportDataItem[]? items { get; set; }
    }
}