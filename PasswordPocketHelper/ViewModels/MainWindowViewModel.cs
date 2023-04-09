using System;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows;
using PasswordPocketHelper.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Web;
using PasswordPocketHelper.Utility;

namespace PasswordPocketHelper.ViewModels
{
    public enum NormalizationType
    {
        Bitwarden,
        Chrome,
        Edge
    }

    public class NormalizationWorker : BackgroundWorker
    {
        public NormalizationType Type { get; }

        public NormalizationWorker(NormalizationType type)
        {
            Type = type;
        }
    }

    public class MainWindowViewModel : ObservableObject
    {
        // The following data are guessed values based on official browser extensions
        public const int NameMaxLength = 64;
        public const int UrlMaxLength = 128;
        public const int AccountMaxLength = 255;
        public const int PasswordMaxLength = 239;

        private int _uiTotalRecordsRead;

        public int UiTotalRecordsRead
        {
            get => _uiTotalRecordsRead;
            set => SetProperty(ref _uiTotalRecordsRead, value);
        }

        private int _uiNumberOfMultipleUrlRecords;

        public int UiNumberOfMultipleUrlRecords
        {
            get => _uiNumberOfMultipleUrlRecords;
            set => SetProperty(ref _uiNumberOfMultipleUrlRecords, value);
        }

        private int _uiNumberOfRecordsWithFieldTextLengthTooLong;

        public int UiNumberOfRecordsWithFieldTextLengthTooLong
        {
            get => _uiNumberOfRecordsWithFieldTextLengthTooLong;
            set => SetProperty(ref _uiNumberOfRecordsWithFieldTextLengthTooLong, value);
        }

        private int _uiNumberOfRecordsAvailableForPasswordPocket;

        public int UiNumberOfRecordsAvailableForPasswordPocket
        {
            get => _uiNumberOfRecordsAvailableForPasswordPocket;
            set => SetProperty(ref _uiNumberOfRecordsAvailableForPasswordPocket, value);
        }

        private ICommand? _uiButtonReadBitwardenCommand;
        public ICommand UiButtonReadBitwardenCommand => _uiButtonReadBitwardenCommand ??= new RelayCommand(OnUiButtonReadBitwardenCommand);

        private void OnUiButtonReadBitwardenCommand()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var targetFileName = openFileDialog.FileName;
                if (File.Exists(targetFileName))
                {
                    UiTotalRecordsRead = 0;
                    UiNumberOfMultipleUrlRecords = 0;
                    UiNumberOfRecordsWithFieldTextLengthTooLong = 0;

                    var jsonSrcString = File.ReadAllText(targetFileName);
                    var bitWardenData = JsonSerializer.Deserialize<BitwardenExportData>(jsonSrcString)!;
                    if (bitWardenData is { encrypted: true })
                    {
                        MessageBox.Show("Not support encrypted export data.");
                        return;
                    }

                    //var backgroundWorker = new BackgroundWorker();
                    //backgroundWorker.WorkerReportsProgress = true;
                    //backgroundWorker.WorkerSupportsCancellation = true;

                    var chromeCsvExportDataList = new List<ChromeCsvExportData>();
                    var pendingMultiUriItems = new List<BitwardenExportDataItem>();
                    var pendingLengthTooLongItems = new List<BitwardenExportDataItem>();

                    void BasicClassification(BitwardenExportDataItem item)
                    {
                        if (item.login.uris is { Length: > 1 })
                        {
                            pendingMultiUriItems.Add(item);
                        }
                        else
                        {
                            var chromeCsvExportData = new ChromeCsvExportData
                            {
                                name = item.name,
                                username = item.login.username,
                                password = item.login.password
                            };

                            var uri = item.login.uris?.First();
                            if (uri != null)
                                chromeCsvExportData.url = uri is { uri.IsAbsoluteUri: true }
                                    ? uri.uri.Host
                                    : uri.uri.OriginalString;

                            if (!string.IsNullOrEmpty(chromeCsvExportData.name) && Encoding.ASCII.GetBytes(chromeCsvExportData.name).Length > NameMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.username) && Encoding.ASCII.GetBytes(chromeCsvExportData.username).Length > AccountMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.password) && Encoding.ASCII.GetBytes(chromeCsvExportData.password).Length > PasswordMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.url) && Encoding.ASCII.GetBytes(chromeCsvExportData.url).Length > UrlMaxLength)
                            {
                                pendingLengthTooLongItems.Add(item);
                            }
                            else
                            {
                                chromeCsvExportDataList.Add(chromeCsvExportData);
                            }
                        }
                    }

                    foreach (var bitwardenExportDataItem in bitWardenData.items)
                    {
                        BasicClassification(bitwardenExportDataItem);
                    }

                    UiTotalRecordsRead = bitWardenData.items.Length;
                    UiNumberOfMultipleUrlRecords = pendingMultiUriItems.Count;
                    UiNumberOfRecordsWithFieldTextLengthTooLong = pendingLengthTooLongItems.Count;

                    void MultipleUrlItemClassification(BitwardenExportDataItem bitwardenExportDataItem1)
                    {
                        var uris = bitwardenExportDataItem1.login.uris!;
                        var cnt = 1;
                        foreach (var uri in uris)
                        {
                            var newItem = ObjectHelper.DeepCopy(bitwardenExportDataItem1)!;
                            newItem.name = $"{newItem.name} - {cnt++}";
                            newItem.login.uris = new[] { uri };
                            BasicClassification(newItem);
                        }
                    }

                    var multiUriItems = pendingMultiUriItems.ToList();
                    pendingMultiUriItems.Clear();
                    foreach (var bitwardenExportDataItem in multiUriItems)
                    {
                        MultipleUrlItemClassification(bitwardenExportDataItem);
                    }

                    UiNumberOfRecordsAvailableForPasswordPocket = chromeCsvExportDataList.Count;

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                        InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads)
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        var exportFileName = saveFileDialog.FileName;
                        using var writer = new StreamWriter(exportFileName);
                        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                        csv.WriteRecords(chromeCsvExportDataList);
                    }
                }
            }
        }

        private ICommand? _uiButtonReadChromeCommand;

        public ICommand UiButtonReadChromeCommand => _uiButtonReadChromeCommand ??= new RelayCommand(OnUiButtonReadChromeCommand);

        private void OnUiButtonReadChromeCommand()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var targetFileName = openFileDialog.FileName;
                if (File.Exists(targetFileName))
                {
                    var chromeExportDataList = new List<ChromeCsvExportData>();
                    {
                        using var reader = new StreamReader(targetFileName);
                        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                        var records = csv.GetRecords<ChromeCsvExportData>();
                        chromeExportDataList.AddRange(records);
                    }
                    var browserRecordDataList = new List<BrowserRecordData>();
                    foreach (var chromeCsvExportData in chromeExportDataList)
                    {
                        chromeCsvExportData.url = HttpUtility.UrlDecode(chromeCsvExportData.url); // Now it will be "xxx,xxx,xxx"
                        var urls = chromeCsvExportData.url.Split(
                            new [] { "," },
                            StringSplitOptions.RemoveEmptyEntries);
                        var uriList = new List<Uri>();
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
                                Debug.WriteLine($"Unable to convert {url} to Uri object.");
                            }
                        }

                        var data = new BrowserRecordData
                        {
                            name = chromeCsvExportData.name,
                            username = chromeCsvExportData.username,
                            password = chromeCsvExportData.password,
                            uris = uriList.ToArray()
                        };

                        browserRecordDataList.Add(data);
                    }

                    var chromeCsvExportDataList = new List<ChromeCsvExportData>();
                    var pendingMultiUriItems = new List<BrowserRecordData>();
                    var pendingLengthTooLongItems = new List<BrowserRecordData>();

                    void BasicClassification(BrowserRecordData item)
                    {
                        if (item.uris is { Length: > 1 })
                        {
                            pendingMultiUriItems.Add(item);
                        }
                        else
                        {
                            var chromeCsvExportData = new ChromeCsvExportData
                            {
                                name = item.name,
                                username = item.username,
                                password = item.password
                            };

                            var uri = item.uris?.First();
                            if (uri != null)
                                chromeCsvExportData.url = uri is { IsAbsoluteUri: true }
                                    ? uri.Host
                                    : uri.OriginalString;

                            if (!string.IsNullOrEmpty(chromeCsvExportData.name) && Encoding.ASCII.GetBytes(chromeCsvExportData.name).Length > NameMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.username) && Encoding.ASCII.GetBytes(chromeCsvExportData.username).Length > AccountMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.password) && Encoding.ASCII.GetBytes(chromeCsvExportData.password).Length > PasswordMaxLength ||
                                !string.IsNullOrEmpty(chromeCsvExportData.url) && Encoding.ASCII.GetBytes(chromeCsvExportData.url).Length > UrlMaxLength)
                            {
                                pendingLengthTooLongItems.Add(item);
                            }
                            else
                            {
                                chromeCsvExportDataList.Add(chromeCsvExportData);
                            }
                        }
                    }

                    foreach (var bitwardenExportDataItem in browserRecordDataList)
                    {
                        BasicClassification(bitwardenExportDataItem);
                    }

                    UiTotalRecordsRead = browserRecordDataList.Count;
                    UiNumberOfMultipleUrlRecords = pendingMultiUriItems.Count;
                    UiNumberOfRecordsWithFieldTextLengthTooLong = pendingLengthTooLongItems.Count;

                    void MultipleUrlItemClassification(BrowserRecordData item)
                    {
                        var uris = item.uris!;
                        var cnt = 1;
                        foreach (var uri in uris)
                        {
                            var newItem = ObjectHelper.DeepCopy(item)!;
                            newItem.name = $"{newItem.name} - {cnt++}";
                            newItem.uris = new[] { uri };
                            BasicClassification(newItem);
                        }
                    }

                    var multiUriItems = pendingMultiUriItems.ToList();
                    pendingMultiUriItems.Clear();
                    foreach (var bitwardenExportDataItem in multiUriItems)
                    {
                        MultipleUrlItemClassification(bitwardenExportDataItem);
                    }

                    UiNumberOfRecordsAvailableForPasswordPocket = chromeCsvExportDataList.Count;

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                        InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads)
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        var exportFileName = saveFileDialog.FileName;
                        {
                            using var writer = new StreamWriter(exportFileName);
                            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                            csv.WriteRecords(chromeCsvExportDataList);
                        }
                    }
                }
            }
        }

        private ICommand? _uiButtonExecuteCommand;

        public ICommand UiButtonExecuteCommand => _uiButtonExecuteCommand ??= new RelayCommand(OnUiButtonExecuteCommand);

        private void OnUiButtonExecuteCommand()
        {
        }
    }
}
