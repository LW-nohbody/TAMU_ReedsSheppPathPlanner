# DigSim3D Premium UI Architecture

## 🏗️ Component Hierarchy

```
SimulationDirector (Node3D)
├── CanvasLayer (Layer=100)
│   └── DigSimUIv3_Premium (Control)
│       ├── Left Panel (PremiumUIPanel)
│       │   ├── Title Bar (draggable)
│       │   ├── Overall Progress Label (AnimatedValueLabel)
│       │   ├── Progress Bar
│       │   ├── Remaining Dirt Label (AnimatedValueLabel)
│       │   ├── Robot Status Entries (PremiumRobotStatusEntry)
│       │   │   ├── Robot Name + Color
│       │   │   ├── Payload Indicator
│       │   │   ├── Mini Chart (performance history)
│       │   │   └── Position/Status Info
│       │   └── Terrain Height Map Thumbnail
│       │
│       └── Right Panel (PremiumUIPanel)
│           ├── Speed Control Section
│           │   ├── Preset Buttons (PresetButtonGroup)
│           │   └── Speed Slider (PremiumSlider)
│           ├── Dig Depth Control Section
│           │   ├── Preset Buttons
│           │   └── Depth Slider (PremiumSlider)
│           └── Dig Radius Control Section
│               └── Radius Slider (PremiumSlider)
```

## 🔗 Data Flow

### Speed Control Flow
```
User adjusts slider
    ↓
PremiumSlider.ValueChanged event
    ↓
DigSimUIv3_Premium.OnSpeedChanged(value)
    ↓
For each vehicle in _vehicles:
    vehicle.SpeedMps = value
    ↓
VehicleVisualizer uses SpeedMps in _Process()
    ↓
Robot moves at new speed
```

### Dig Parameter Flow
```
User adjusts depth/radius slider
    ↓
PremiumSlider.ValueChanged event
    ↓
DigSimUIv3_Premium.OnDigDepthChanged(value) or OnDigRadiusChanged(value)
    ↓
Updates _digConfig.DigDepth or _digConfig.DigRadius
    ↓
DigService uses updated config for next dig operation
```

### Mouse Interaction Flow
```
User clicks on screen
    ↓
SimulationDirector._Process() checks IsMouseOverUI()
    ↓
If over UI:
    - Input.MouseMode = Visible
    - UI elements receive input
    - Camera rotation disabled
If NOT over UI:
    - Input.MouseMode = Captured (when right-clicking)
    - Camera rotation enabled
    - UI cannot be clicked
```

## 📊 Component Details

### Core UI Components

#### **DigSimUIv3_Premium**
- **Purpose**: Main UI coordinator
- **Location**: `Scripts/UI/DigSimUIv3_Premium.cs`
- **Key Methods**:
  - `AddRobot()`: Register robot for status display
  - `UpdateRobotPayload()`: Update robot performance data
  - `UpdateTerrainProgress()`: Update overall progress
  - `SetVehicles()`: Link to vehicle objects for control
  - `SetDigConfig()`: Link to dig configuration
  - `SetTerrain()`: Link to terrain for thumbnail
  - `OnSpeedChanged()`: Handle speed control changes
  - `OnDigDepthChanged()`: Handle depth control changes
  - `OnDigRadiusChanged()`: Handle radius control changes

#### **PremiumSlider**
- **Purpose**: Custom slider with color-coding and glow
- **Location**: `Scripts/UI/PremiumSlider.cs`
- **Features**:
  - Color changes based on value (green → yellow → red)
  - Glowing effect
  - Real-time value display
  - ValueChanged event

#### **AnimatedValueLabel**
- **Purpose**: Label with smooth value transitions
- **Location**: `Scripts/UI/AnimatedValueLabel.cs`
- **Features**:
  - Smooth numeric transitions
  - Configurable format strings
  - Interpolation speed control

#### **PremiumRobotStatusEntry**
- **Purpose**: Individual robot status display
- **Location**: `Scripts/UI/PremiumRobotStatusEntry.cs`
- **Features**:
  - Color-coded robot identification
  - Payload indicator
  - Mini performance chart
  - Position and status text

#### **MiniChart**
- **Purpose**: Small performance visualization
- **Location**: `Scripts/UI/MiniChart.cs`
- **Features**:
  - Historical data tracking
  - Line graph rendering
  - Auto-scaling

#### **TerrainHeightMapThumbnail**
- **Purpose**: Miniature terrain visualization
- **Location**: `Scripts/UI/TerrainHeightMapThumbnail.cs`
- **Features**:
  - Height map rendering
  - Progress overlay
  - Real-time updates

#### **PremiumUIPanel**
- **Purpose**: Glassmorphism panel container
- **Location**: `Scripts/UI/PremiumUIPanel.cs`
- **Features**:
  - Blur effect background
  - Glowing borders
  - Title bar
  - Content area

#### **PresetButtonGroup**
- **Purpose**: Quick value selection buttons
- **Location**: `Scripts/UI/PresetButtonGroup.cs`
- **Features**:
  - Multiple preset buttons
  - Visual feedback
  - PresetSelected event

## 🎨 Visual Design

### Color Scheme
- **Background**: Dark blue-gray with transparency (0.08, 0.08, 0.12, 0.85)
- **Borders**: Glowing blue (0.4, 0.6, 1.0, 0.8)
- **Accents**: Cyan-blue gradients
- **Text**: White with slight tint (0.7, 0.9, 1.0)

### Slider Color Coding
```
Value Range    Color
0% - 33%      Green (safe)
34% - 66%     Yellow (moderate)
67% - 100%    Red (high)
```

### Typography
- **Titles**: 18pt, bold, colored
- **Labels**: 14pt, regular
- **Values**: 12pt, monospace (for numbers)

## 🔧 Integration Points

### SimulationDirector Integration
```csharp
// In _Ready()
_digSimUI = new DigSimUIv3_Premium();
uiLayer.AddChild(_digSimUI);

// Setup
_digSimUI.SetDigConfig(_digConfig);
_digSimUI.SetVehicles(_vehicles);
_digSimUI.SetTerrain(_terrain);

// In _Process()
_digSimUI.UpdateRobotPayload(i, payloadPercent, position, status);
_digSimUI.UpdateTerrainProgress(remaining, initial);
```

### VehicleVisualizer Integration
```csharp
public float SpeedMps = 0.6f;  // Directly controlled by UI

// In _Process()
var nextXZ = curXZ + dir * SpeedMps * dt;  // Uses UI-controlled speed
```

### DigConfig Integration
```csharp
public class DigConfig
{
    public float DigDepth;   // Controlled by UI slider
    public float DigRadius;  // Controlled by UI slider
    public float MaxPayload;
}
```

## 🐛 Debugging Tips

### Enable Debug Output
All UI components log to console:
```
[DigSimUIv3_Premium] Initializing premium UI...
[Settings] ⚡ Robot speed changed to X.XX m/s
[Settings] ⛏️ Dig depth changed to X.XX m
[Settings] 📊 Dig radius changed to X.XX m
```

### Check UI Visibility
```csharp
// In Godot remote scene tree
CanvasLayer
└── DigSimUIv3_Premium [Visible: true]
    ├── Left Panel [Visible: true]
    └── Right Panel [Visible: true]
```

### Verify Mouse Detection
Add temporary debug:
```csharp
bool mouseOverUI = IsMouseOverUI();
if (mouseOverUI != _lastMouseOverUI)
{
    GD.Print($"Mouse over UI: {mouseOverUI}");
    _lastMouseOverUI = mouseOverUI;
}
```

### Monitor Value Changes
Watch for these events:
- `PremiumSlider.ValueChanged`
- `PresetButtonGroup.PresetSelected`
- `DigSimUIv3_Premium.OnSpeedChanged`

## 📈 Performance Considerations

### Optimization Points
1. **UI Updates**: Throttled to 60 FPS via _Process()
2. **Chart History**: Limited to last 100 samples
3. **Terrain Thumbnail**: Updates only when needed
4. **Animation**: Lightweight interpolation only

### Memory Usage
- Each robot entry: ~1KB
- Chart data: ~400 bytes per robot
- Terrain thumbnail: Depends on resolution
- Total: ~10-20KB for 8 robots

## 🚀 Future Enhancements

### Possible Additions
1. **Collapsible panels** - Hide/show sections
2. **Custom themes** - User-selectable color schemes
3. **Profile system** - Save/load control settings
4. **Hotkeys** - Keyboard shortcuts for presets
5. **Multi-language** - Internationalization support
6. **Accessibility** - Screen reader support
7. **Mobile support** - Touch-friendly controls

### Performance Improvements
1. **Object pooling** - Reuse UI elements
2. **LOD for charts** - Reduce detail when small
3. **Lazy updates** - Only update visible elements
4. **Batch rendering** - Group similar draw calls

## ✅ Status Summary

**All components**: ✅ Implemented and tested
**Build status**: ✅ 0 errors, 0 warnings
**Mouse handling**: ✅ Fixed and working
**Real-time control**: ✅ Functional
**Visual polish**: ✅ Complete

Ready for production use! 🎉
