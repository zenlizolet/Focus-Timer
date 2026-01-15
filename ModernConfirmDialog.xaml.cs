using System.Windows;

namespace StudyTimer
{
    public partial class ModernConfirmDialog : Window
    {
        public ModernConfirmDialog(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static bool ShowConfirm(string title, string message)
        {
            var dialog = new ModernConfirmDialog(title, message);
            return dialog.ShowDialog() == true;
        }
    }
}

