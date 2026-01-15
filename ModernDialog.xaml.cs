using System.Windows;

namespace StudyTimer
{
    public partial class ModernDialog : Window
    {
        public ModernDialog(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public static void Show(string title, string message)
        {
            var dialog = new ModernDialog(title, message);
            dialog.ShowDialog();
        }
    }
}

