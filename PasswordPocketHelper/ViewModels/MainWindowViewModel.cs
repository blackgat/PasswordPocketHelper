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
        public const int NameMaxLength = 64;
        public const int UrlMaxLength = 128;
        public const int AccountMaxLength = 255;
        public const int PasswordMaxLength = 239;

        private ICommand? _uiButtonReadBitwardenCommand;
        public ICommand UiButtonReadBitwardenCommand => _uiButtonReadBitwardenCommand = _uiButtonReadBitwardenCommand ?? new RelayCommand(OnUiButtonReadBitwardenCommand);

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
                    var pendingEmptyUriItems = new List<BitwardenExportDataItem>();
                    var pendingMultiUriItems = new List<BitwardenExportDataItem>();
                    var pendingEmptyStringItems = new List<BitwardenExportDataItem>();
                    var pendingLengthTooLongItems = new List<BitwardenExportDataItem>();

                    void BasicClassification(BitwardenExportDataItem item)
                    {
                        if (item.login.uris == null)
                        {
                            pendingEmptyUriItems.Add(item);
                        }
                        else if (item.login.uris is { Length: > 1 })
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

                            var uri = item.login.uris.First();
                            chromeCsvExportData.url = uri.uri.IsAbsoluteUri ? uri.uri.Host : uri.uri.OriginalString;

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
                            //if (string.IsNullOrEmpty(chromeCsvExportData.name) ||
                            //    string.IsNullOrEmpty(chromeCsvExportData.username) ||
                            //    string.IsNullOrEmpty(chromeCsvExportData.password) ||
                            //    string.IsNullOrEmpty(chromeCsvExportData.url))
                            //{
                            //    pendingEmptyStringItems.Add(item);
                            //}
                            //else
                            //{
                            //    if (!string.IsNullOrEmpty(chromeCsvExportData.name) && Encoding.ASCII.GetBytes(chromeCsvExportData.name).Length > NameMaxLength ||
                            //        !string.IsNullOrEmpty(chromeCsvExportData.username) && Encoding.ASCII.GetBytes(chromeCsvExportData.username).Length > AccountMaxLength ||
                            //        !string.IsNullOrEmpty(chromeCsvExportData.password) && Encoding.ASCII.GetBytes(chromeCsvExportData.password).Length > PasswordMaxLength ||
                            //        !string.IsNullOrEmpty(chromeCsvExportData.url) && Encoding.ASCII.GetBytes(chromeCsvExportData.url).Length > UrlMaxLength)
                            //    {
                            //        pendingLengthTooLongItems.Add(item);
                            //    }
                            //    else
                            //    {
                            //        chromeCsvExportDataList.Add(chromeCsvExportData);
                            //    }
                            //}
                        }
                    }

                    foreach (var bitwardenExportDataItem in bitWardenData.items)
                    {
                        BasicClassification(bitwardenExportDataItem);
                    }

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

                    var emptyUriItems = pendingEmptyUriItems.ToList();
                    pendingEmptyUriItems.Clear();
                    foreach (var bitwardenExportDataItem in emptyUriItems)
                    {
                        bitwardenExportDataItem.login.uris = new[] { new BitwardenExportDataItemLoginUri { uri = new Uri("http://dummy.idv") } };
                        BasicClassification(bitwardenExportDataItem);
                    }

                    var emptyStringItems = pendingEmptyStringItems.ToList();
                    pendingEmptyStringItems.Clear();
                    foreach (var bitwardenExportDataItem in emptyStringItems)
                    {
                        var chromeCsvExportData = new ChromeCsvExportData
                        {
                            name = bitwardenExportDataItem.name,
                            username = bitwardenExportDataItem.login.username,
                            password = bitwardenExportDataItem.login.password
                        };

                        var uri = bitwardenExportDataItem.login.uris?.First();
                        if (uri != null)
                        {
                            chromeCsvExportData.url = uri.uri.IsAbsoluteUri ? uri.uri.Host : uri.uri.OriginalString;
                        }

                        if (string.IsNullOrEmpty(chromeCsvExportData.url))
                        {
                            chromeCsvExportData.url = "dummy.idv";
                        }

                        chromeCsvExportDataList.Add(chromeCsvExportData);
                    }

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

        public ICommand UiButtonReadChromeCommand => _uiButtonReadChromeCommand = _uiButtonReadChromeCommand ?? new RelayCommand(OnUiButtonReadChromeCommand);

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
                    var list = new List<ChromeCsvExportData>();
                    using (var reader = new StreamReader(targetFileName))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<ChromeCsvExportData>();
                        list.AddRange(records);
                    }

                    foreach (var chromeCsvExportData in list)
                    {
                        chromeCsvExportData.url = HttpUtility.UrlDecode(chromeCsvExportData.url);
                    }
                }
            }
        }
    }



}
