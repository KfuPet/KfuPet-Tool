using System.Windows;
using KfuPet_Tool.Helpers;

namespace KfuPet_Tool
{
    /// <summary>
    /// 启动选择窗口：让用户勾选本次要启动的功能（支持多选），确认后由 App 启动对应窗口。
    /// </summary>
    public partial class LauncherWindow : Window
    {
        public bool LaunchMainProgram { get; private set; }

        public bool LaunchServerLog { get; private set; }

        public LauncherWindow()
        {
            InitializeComponent();
            DarkTitleBar.Apply(this);
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            LaunchMainProgram = MainProgramCheck.IsChecked == true;
            LaunchServerLog = ServerLogCheck.IsChecked == true;

            if (!LaunchMainProgram && !LaunchServerLog)
            {
                MessageBox.Show("请至少选择一项要启动的功能。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
