using CommunityToolkit.Mvvm.ComponentModel;

namespace KfuPet_Tool.Models
{
    public partial class AttachmentInfo : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _boneId = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _resourcePath = string.Empty;

        [ObservableProperty]
        private double _offsetX;

        [ObservableProperty]
        private double _offsetY;

        [ObservableProperty]
        private double _pivotX = 0.5;

        [ObservableProperty]
        private double _pivotY = 0.5;

        [ObservableProperty]
        private int _zOrder;

        [ObservableProperty]
        private bool _visible = true;

        [ObservableProperty]
        private double _scaleX = 1.0;

        [ObservableProperty]
        private double _scaleY = 1.0;
    }
}
