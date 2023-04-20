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
using System.Diagnostics;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Threading.Tasks;
using PasswordPocketHelper.Utility;
using System.Threading;

namespace PasswordPocketHelper.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        // The following data are guessed values based on official browser extensions
        public const int NameMaxLength = 64;
        public const int UrlMaxLength = 128;
        public const int AccountMaxLength = 255;
        public const int PasswordMaxLength = 239;

        private readonly List<KeyMetadataItem> _keyMetadataItemList = new();

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
                    var jsonSrcString = File.ReadAllText(targetFileName);
                    var bitWardenData = JsonSerializer.Deserialize<BitwardenExportData>(jsonSrcString)!;
                    if (bitWardenData is { encrypted: true })
                    {
                        MessageBox.Show("Not support encrypted export data.");
                        return;
                    }

                    ResetUiInfo();

                    if (bitWardenData.items != null)
                    {
                        foreach (var bitwardenExportDataItem in bitWardenData.items)
                        {
                            _keyMetadataItemList.Add(bitwardenExportDataItem.ToKeyMetadataItem());
                        }
                    }

                    UiTotalRecordsRead = _keyMetadataItemList.Count;
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

                    ResetUiInfo();

                    foreach (var chromeCsvExportData in chromeExportDataList)
                    {
                        _keyMetadataItemList.Add(chromeCsvExportData.ToKeyMetadataItem());
                    }

                    UiTotalRecordsRead = _keyMetadataItemList.Count;
                }
            }
        }

        private ICommand? _uiButtonExecuteCommand;
        public ICommand UiButtonExecuteCommand => _uiButtonExecuteCommand ??= new RelayCommand(OnUiButtonExecuteCommand);

        private async void OnUiButtonExecuteCommand()
        {
            if (!_keyMetadataItemList.Any())
            {
                MessageBox.Show("No item found!", "Notice", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            var progress = new Progress<int>(OnReportProgress);
            var cancellation = new CancellationTokenSource();
            var chromeCsvExportDataList = new List<ChromeCsvExportData>();
            var args = new object[] { progress, cancellation.Token, _keyMetadataItemList.AsReadOnly(), chromeCsvExportDataList };
            try
            {
                await Task.Run(() => DoWork(args), cancellation.Token);

                _keyMetadataItemList.Clear();

                var timeStamp = $"{DateTime.Now:yyyyMMddHHmmss}";
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Save result as Chrome export file...",
                    InitialDirectory = KnownFolders.GetPath(KnownFolder.Downloads),
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    AddExtension = true,
                    DefaultExt = "csv",
                    CheckPathExists = true,
                    OverwritePrompt = true,
                    ValidateNames = true,
                    FileName = $"PasswordPocketHelper_NormalizedData_{timeStamp}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var exportFileName = saveFileDialog.FileName;
                    try
                    {
                        await using var writer = new StreamWriter(exportFileName);
                        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                        await csv.WriteRecordsAsync(chromeCsvExportDataList, cancellation.Token);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private ICommand? _uiButtonResetCommand;
        public ICommand UiButtonResetCommand => _uiButtonResetCommand ??= new RelayCommand(OnUiButtonResetCommand);

        private void OnUiButtonResetCommand()
        {
            _keyMetadataItemList.Clear();
            ResetUiInfo();
        }

        private void OnReportProgress(int progress)
        {
        }

        private void DoWork(object[] args)
        {
            // TODO: Support progress
            var progress = (IProgress<int>)args[0];
            var cancellationToken = (CancellationToken)args[1];
            var keyMetadataItemList = (IEnumerable<KeyMetadataItem>)args[2];
            var chromeCsvExportDataList = (List<ChromeCsvExportData>)args[3];
            
            var pendingMultiUriItems = new List<KeyMetadataItem>();
            var pendingInfoLengthTooLongItems = new List<KeyMetadataItem>();

            void BasicClassification(KeyMetadataItem item)
            {
                if (item.uris is { Length: > 1 })
                {
                    pendingMultiUriItems.Add(item);
                }
                else
                {
                    var chromeCsvExportData = new ChromeCsvExportData { name = item.name, username = item.username, password = item.password };

                    var uri = item.uris?.FirstOrDefault();
                    if (uri != null)
                        chromeCsvExportData.url = uri is { IsAbsoluteUri: true }
                            ? uri.Host
                            : uri.OriginalString;

                    if (!string.IsNullOrEmpty(chromeCsvExportData.name) && Encoding.UTF8.GetBytes(chromeCsvExportData.name).Length > NameMaxLength ||
                        !string.IsNullOrEmpty(chromeCsvExportData.username) && Encoding.UTF8.GetBytes(chromeCsvExportData.username).Length > AccountMaxLength ||
                        !string.IsNullOrEmpty(chromeCsvExportData.password) && Encoding.UTF8.GetBytes(chromeCsvExportData.password).Length > PasswordMaxLength ||
                        !string.IsNullOrEmpty(chromeCsvExportData.url) && Encoding.UTF8.GetBytes(chromeCsvExportData.url).Length > UrlMaxLength)
                    {
                        pendingInfoLengthTooLongItems.Add(item);
                    }
                    else
                    {
                        chromeCsvExportDataList.Add(chromeCsvExportData);
                    }
                }
            }

            foreach (var bitwardenExportDataItem in keyMetadataItemList)
            {
                if (cancellationToken.IsCancellationRequested) return;

                BasicClassification(bitwardenExportDataItem);
            }

            UiNumberOfMultipleUrlRecords = pendingMultiUriItems.Count;
            UiNumberOfRecordsWithFieldTextLengthTooLong = pendingInfoLengthTooLongItems.Count;

            void MultipleUrlItemClassification(KeyMetadataItem item)
            {
                var uris = item.uris!;
                var cnt = 1;
                foreach (var uri in uris)
                {
                    var newItem = ObjectHelper.DeepCopy(item)!;
                    newItem.name = !string.IsNullOrEmpty(item.username)
                        // uri + account
                        ? $"{uri.Host} - {item.username}"
                        // name + uri
                        : $"{newItem.name}({cnt++})({uri.Host})";
                    newItem.uris = new[] { uri };
                    BasicClassification(newItem);
                }
            }

            var multiUriItems = pendingMultiUriItems.ToList();
            pendingMultiUriItems.Clear();
            foreach (var bitwardenExportDataItem in multiUriItems)
            {
                if (cancellationToken.IsCancellationRequested) return;

                MultipleUrlItemClassification(bitwardenExportDataItem);
            }

            void InfoLengthTooLongItemClassification(KeyMetadataItem item)
            {
                if (!string.IsNullOrEmpty(item.name) && Encoding.UTF8.GetBytes(item.name).Length > NameMaxLength)
                {
                    item.name = item.name.ReduceStringSize(NameMaxLength, Encoding.UTF8);
                }
                if (!string.IsNullOrEmpty(item.username) && Encoding.ASCII.GetBytes(item.username).Length > AccountMaxLength)
                {
                    // Do nothing, this is important information.
                    //item.username = item.username.ReduceStringSize(AccountMaxLength, Encoding.ASCII);
                }
                if (!string.IsNullOrEmpty(item.password) && Encoding.ASCII.GetBytes(item.password).Length > PasswordMaxLength)
                {
                    // Do nothing, this is important information.
                    //item.password = item.password.ReduceStringSize(PasswordMaxLength, Encoding.ASCII);
                }

                BasicClassification(item);
            }

            var infoLengthTooLongItems = pendingInfoLengthTooLongItems.ToList();
            pendingInfoLengthTooLongItems.Clear();
            foreach (var infoLengthTooLongItem in infoLengthTooLongItems)
            {
                InfoLengthTooLongItemClassification(infoLengthTooLongItem);
            }

            UiNumberOfRecordsAvailableForPasswordPocket = chromeCsvExportDataList.Count;
        }
    }
}
