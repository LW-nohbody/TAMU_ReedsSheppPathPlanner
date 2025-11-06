using Godot;
using System.Collections.Generic;
using System.Linq;

namespace DigSim3D.App
{
    /// <summary>
    /// Displays real-time robot statistics and progress
    /// Positioned below payload UI on the left side
    /// </summary>
    public partial class RobotStatsUI : Control
    {
        private Label _statsLabel = null!;
        private Label _progressLabel = null!;
        private ProgressBar _overallProgress = null!;
        private ScrollContainer _scrollContainer = null!;
        
        private readonly List<RobotInfo> _robots = new();
        private float _totalDirtDug = 0f;
        private float _initialTerrainVolume = 0f;

        public override void _Ready()
        {
            // Create main panel - positioned BELOW payload UI (left side, stacked)
            // Positioned at a lower position to avoid overlap with RobotPayloadUI
            var panel = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                CustomMinimumSize = new Vector2(340, 220),
                Position = new Vector2(10, GetViewport().GetVisibleRect().Size.Y - 480)
            };
            AddChild(panel);

            // Create scroll container
            _scrollContainer = new ScrollContainer
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 8,
                OffsetTop = 8,
                OffsetRight = -8,
                OffsetBottom = -8
            };
            panel.AddChild(_scrollContainer);

            // Create VBox for content
            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            vbox.AddThemeConstantOverride("separation", 6);
            _scrollContainer.AddChild(vbox);

            // Title
            var title = new Label
            {
                Text = "📊 ROBOT STATISTICS",
                CustomMinimumSize = new Vector2(0, 20)
            };
            title.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(title);

            // Overall progress bar
            _progressLabel = new Label
            {
                Text = "Overall Progress: 0%",
                CustomMinimumSize = new Vector2(0, 16)
            };
            _progressLabel.AddThemeFontSizeOverride("font_size", 9);
            vbox.AddChild(_progressLabel);
            
            _overallProgress = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                CustomMinimumSize = new Vector2(0, 18)
            };
            vbox.AddChild(_overallProgress);

            // Stats display
            _statsLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(320, 200),
                ClipText = true
            };
            _statsLabel.AddThemeFontSizeOverride("font_size", 8);
            vbox.AddChild(_statsLabel);

            // Update timer
            var timer = new Godot.Timer { WaitTime = 0.5 };
            timer.Timeout += UpdateDisplay;
            AddChild(timer);
            timer.Start();

            GD.Print("[RobotStatsUI] ✅ Initialized below payload UI");
        }

        public void RegisterRobot(int id, string name)
        {
            _robots.Add(new RobotInfo
            {
                Id = id,
                Name = name,
                TotalDug = 0f,
                CurrentPayload = 0f,
                DigsCompleted = 0,
                CurrentTarget = Vector3.Zero,
                Status = "Idle"
            });
        }

        public void UpdateRobotStats(int id, float payload, int digs, Vector3 target, string status)
        {
            var robot = _robots.FirstOrDefault(r => r.Id == id);
            if (robot != null)
            {
                robot.CurrentPayload = payload;
                robot.DigsCompleted = digs;
                robot.CurrentTarget = target;
                robot.Status = status;
            }
        }

        public void RecordDig(int id, float amount)
        {
            var robot = _robots.FirstOrDefault(r => r.Id == id);
            if (robot != null)
            {
                robot.TotalDug += amount;
                _totalDirtDug += amount;
            }
        }

        public void SetInitialVolume(float volume)
        {
            _initialTerrainVolume = volume;
        }

        private void UpdateDisplay()
        {
            if (_robots.Count == 0) return;

            var text = new System.Text.StringBuilder();
            text.AppendLine($"Total Excavated: {_totalDirtDug:F2} m³\n");

            // Calculate progress
            float progress = _initialTerrainVolume > 0 
                ? (_totalDirtDug / _initialTerrainVolume) * 100f 
                : 0f;
            progress = Mathf.Clamp(progress, 0f, 100f);
            
            _progressLabel.Text = $"Overall Progress: {progress:F1}%";
            _overallProgress.Value = progress;

            // Per-robot stats
            text.AppendLine("Per-Robot Stats:");
            text.AppendLine("─────────────────────");

            foreach (var robot in _robots)
            {
                string payloadBar = CreateBar(robot.CurrentPayload, 0.5f, 8);
                text.AppendLine($"{robot.Name}:");
                text.AppendLine($"  Status: {robot.Status}");
                text.AppendLine($"  Payload: [{payloadBar}]");
                text.AppendLine($"  Total Dug: {robot.TotalDug:F2} m³");
                text.AppendLine($"  Digs: {robot.DigsCompleted}");
                text.AppendLine("");
            }

            _statsLabel.Text = text.ToString();
        }

        private string CreateBar(float value, float max, int width)
        {
            int filled = Mathf.RoundToInt((value / max) * width);
            filled = Mathf.Clamp(filled, 0, width);
            return new string('█', filled) + new string('░', width - filled);
        }

        private class RobotInfo
        {
            public int Id { get; set; } = 0;
            public string Name { get; set; } = "";
            public float TotalDug { get; set; } = 0f;
            public float CurrentPayload { get; set; } = 0f;
            public int DigsCompleted { get; set; } = 0;
            public Vector3 CurrentTarget { get; set; } = Vector3.Zero;
            public string Status { get; set; } = "";
        }
    }
}
