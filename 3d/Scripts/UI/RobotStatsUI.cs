using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Displays real-time robot statistics and progress
/// </summary>
public partial class RobotStatsUI : Control
{
    private Label _statsLabel;
    private Label _progressLabel;
    private ProgressBar _overallProgress;
    
    private readonly List<RobotInfo> _robots = new();
    private float _totalDirtDug = 0f;
    private float _initialTerrainVolume = 0f;

    public override void _Ready()
    {
        // Create UI elements
        var vbox = new VBoxContainer
        {
            Position = new Vector2(10, 10),
            CustomMinimumSize = new Vector2(400, 0)
        };
        AddChild(vbox);

        // Title
        var title = new Label
        {
            Text = "🤖 Robot Dig Statistics"
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        // Overall progress bar
        _progressLabel = new Label { Text = "Overall Progress: 0%" };
        vbox.AddChild(_progressLabel);
        
        _overallProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            CustomMinimumSize = new Vector2(0, 30)
        };
        vbox.AddChild(_overallProgress);

        // Stats display
        _statsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            CustomMinimumSize = new Vector2(400, 300)
        };
        vbox.AddChild(_statsLabel);

        // Update timer
        var timer = new Timer { WaitTime = 0.5 };
        timer.Timeout += UpdateDisplay;
        AddChild(timer);
        timer.Start();
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
        text.AppendLine($"\n📊 Total Dirt Excavated: {_totalDirtDug:F2} m³\n");

        // Calculate progress
        float progress = _initialTerrainVolume > 0 
            ? (_totalDirtDug / _initialTerrainVolume) * 100f 
            : 0f;
        progress = Mathf.Clamp(progress, 0f, 100f);
        
        _progressLabel.Text = $"Overall Progress: {progress:F1}%";
        _overallProgress.Value = progress;

        // Per-robot stats
        text.AppendLine("Per-Robot Statistics:");
        text.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var robot in _robots)
        {
            string payloadBar = CreateBar(robot.CurrentPayload, 0.5f, 10);
            text.AppendLine($"\n🤖 {robot.Name}:");
            text.AppendLine($"   Status: {robot.Status}");
            text.AppendLine($"   Payload: [{payloadBar}] {robot.CurrentPayload:F2}/0.50 m³");
            text.AppendLine($"   Total Dug: {robot.TotalDug:F2} m³");
            text.AppendLine($"   Digs: {robot.DigsCompleted}");
            text.AppendLine($"   Target: ({robot.CurrentTarget.X:F1}, {robot.CurrentTarget.Z:F1})");
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
        public int Id { get; set; }
        public string Name { get; set; }
        public float TotalDug { get; set; }
        public float CurrentPayload { get; set; }
        public int DigsCompleted { get; set; }
        public Vector3 CurrentTarget { get; set; }
        public string Status { get; set; }
    }
}
