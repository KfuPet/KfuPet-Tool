using System.Windows;
using KfuPet_Tool.Helpers;
using KfuPet_Tool.ViewModels;

namespace KfuPet_Tool
{
    public partial class LogWindow : Window
    {
        private readonly LogViewModel _viewModel;

        public LogWindow()
        {
            InitializeComponent();
            DarkTitleBar.Apply(this);
            _viewModel = new LogViewModel();
            DataContext = _viewModel;
            Closed += (_, _) => _viewModel.Shutdown();
        }
    }
}
