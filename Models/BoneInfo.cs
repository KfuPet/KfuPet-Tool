using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KfuPet_Tool.Models
{
    public partial class BoneInfo : ObservableObject
    {
        [ObservableProperty]
        private string _boneId = string.Empty;

        [ObservableProperty]
        private string _boneName = string.Empty;

        [ObservableProperty]
        private string? _parentBoneId;

        [ObservableProperty]
        private double _positionX;

        [ObservableProperty]
        private double _positionY;

        [ObservableProperty]
        private double _positionDeltaX;

        [ObservableProperty]
        private double _positionDeltaY;

        [ObservableProperty]
        private double _rotation;

        [ObservableProperty]
        private double _rotationDelta;

        [ObservableProperty]
        private double _scaleX = 1.0;

        [ObservableProperty]
        private double _scaleY = 1.0;

        [ObservableProperty]
        private double _scaleDeltaX;

        [ObservableProperty]
        private double _scaleDeltaY;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private double _worldX;

        [ObservableProperty]
        private double _worldY;

        public ObservableCollection<BoneInfo> Children { get; } = new();

        public ObservableCollection<AttachmentInfo> Attachments { get; } = new();

        public bool HasChildren => Children.Count > 0;
    }
}
