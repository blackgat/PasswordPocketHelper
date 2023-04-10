namespace PasswordPocketHelper.ViewModels
{
    public partial class MainWindowViewModel
    {
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

        private void ResetUiInfo()
        {
            UiTotalRecordsRead = 0;
            UiNumberOfMultipleUrlRecords = 0;
            UiNumberOfRecordsWithFieldTextLengthTooLong = 0;
            UiNumberOfRecordsAvailableForPasswordPocket = 0;
        }
    }
}