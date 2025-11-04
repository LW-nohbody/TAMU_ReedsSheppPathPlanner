using Godot;
using System;

namespace SimCore.UI
{
    /// <summary>
    /// UI panel for adjusting simulation parameters in real-time
    /// </summary>
    public partial class SimulationSettingsUI : Control
    {
        private VBoxContainer _container = null!;
        private HSlider _digDepthSlider = null!;
        private HSlider _robotSpeedSlider = null!;
        private Label _digDepthLabel = null!;
        private Label _robotSpeedLabel = null!;

        public override void _Ready()
        {
            // Create main panel
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(320, 0)
            };
            AddChild(panel);

            // Create container
            _container = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            panel.AddChild(_container);

            // Title
            var titleLabel = new Label
            {
                Text = "SIMULATION SETTINGS",
                CustomMinimumSize = new Vector2(300, 30)
            };
            _container.AddChild(titleLabel);

            _container.AddChild(new HSeparator());

            // Dig Depth Control
            var digLabel = new Label { Text = "Dig Depth (m):" };
            _container.AddChild(digLabel);

            _digDepthSlider = new HSlider
            {
                MinValue = 0.02f,
                MaxValue = 0.20f,
                Value = SimCore.Core.SimpleDigLogic.DIG_AMOUNT,
                Step = 0.01f,
                CustomMinimumSize = new Vector2(300, 30)
            };
            _digDepthSlider.ValueChanged += OnDigDepthChanged;
            _container.AddChild(_digDepthSlider);

            _digDepthLabel = new Label
            {
                Text = $"Current: {SimCore.Core.SimpleDigLogic.DIG_AMOUNT:F3} m"
            };
            _container.AddChild(_digDepthLabel);

            _container.AddChild(new HSeparator());

            // Robot Speed Control
            var speedLabel = new Label { Text = "Robot Speed:" };
            _container.AddChild(speedLabel);

            _robotSpeedSlider = new HSlider
            {
                MinValue = 0.5f,
                MaxValue = 3.0f,
                Value = VehicleAgent3D.GlobalSpeedMultiplier,
                Step = 0.1f,
                CustomMinimumSize = new Vector2(300, 30)
            };
            _robotSpeedSlider.ValueChanged += OnRobotSpeedChanged;
            _container.AddChild(_robotSpeedSlider);

            _robotSpeedLabel = new Label
            {
                Text = $"Current: {VehicleAgent3D.GlobalSpeedMultiplier:F1}x"
            };
            _container.AddChild(_robotSpeedLabel);

            _container.AddChild(new HSeparator());

            // Info label
            var infoLabel = new Label
            {
                Text = "Adjust these values to tune\nthe simulation performance\nand behavior",
                CustomMinimumSize = new Vector2(300, 60)
            };
            _container.AddChild(infoLabel);
        }

        private void OnDigDepthChanged(double value)
        {
            float digDepth = (float)value;
            SimCore.Core.SimpleDigLogic.DIG_AMOUNT = digDepth;
            _digDepthLabel.Text = $"Current: {digDepth:F3} m";
            GD.Print($"[Settings] Dig depth set to {digDepth:F3}m");
        }

        private void OnRobotSpeedChanged(double value)
        {
            float speedMultiplier = (float)value;
            VehicleAgent3D.GlobalSpeedMultiplier = speedMultiplier;
            _robotSpeedLabel.Text = $"Current: {speedMultiplier:F1}x";
            GD.Print($"[Settings] Robot speed set to {speedMultiplier:F1}x");
        }
    }
}
