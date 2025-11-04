# Simulation Settings UI Controls

## Overview
The **Simulation Settings Panel** appears on the right side of the screen during the simulation. It allows you to adjust key parameters in real-time without restarting.

## Controls

### 1. Dig Depth (meters)
- **Range**: 0.02m to 0.20m
- **Default**: 0.08m
- **Effect**: Controls how much terrain is lowered per dig operation
- **Use Cases**:
  - **Increase** (0.15-0.20m): Faster terrain flattening, more dramatic visual effect, robots fill up faster
  - **Decrease** (0.02-0.05m): Slower, more gradual terrain changes, finer control
- **Step**: 0.01m increments

### 2. Robot Speed Multiplier
- **Range**: 0.5x to 3.0x
- **Default**: 1.0x
- **Effect**: Multiplies the base robot speed (0.6 m/s)
- **Use Cases**:
  - **0.5x**: Slow, methodical movement for observation and debugging
  - **1.0x**: Default speed - balanced performance
  - **1.5-2.0x**: Faster simulation for quicker terrain completion
  - **3.0x**: Maximum speed - very fast simulation
- **Step**: 0.1x increments

## How to Use

1. **Start the simulation** - The settings panel appears automatically on the right side
2. **Locate the "SIMULATION SETTINGS"** panel with two sliders
3. **Drag the sliders** to adjust values:
   - Dig Depth slider: Adjust how much terrain is removed per dig
   - Robot Speed slider: Adjust how fast robots move
4. **Watch the current values** update in real-time below each slider
5. **See the effects immediately** - robots will dig faster/slower, move faster/slower

## Real-Time Adjustment

- All changes take effect **immediately** without restarting
- Robot behavior changes are applied to all robots
- Dig operations use the new depth setting right away
- Speed changes affect robot movement instantly

## Performance Tips

- **If simulation is slow**: Increase robot speed to 1.5-2.0x to complete tasks faster
- **If robots are getting stuck**: Increase dig depth to clear terrain more aggressively
- **For detailed observation**: Reduce speed to 0.5-0.7x for better visibility
- **For quick testing**: Increase both speed and dig depth to 2.0x and 0.15m

## Console Output

When you adjust settings, you'll see confirmations in the Godot console:
```
[Settings] Dig depth set to 0.10m
[Settings] Robot speed set to 1.5x
```

---

**Note**: These settings only affect the current simulation session. To change default values permanently, edit:
- `SimpleDigLogic.cs` - default `DIG_AMOUNT` value
- `VehicleAgent3D.cs` - `SpeedMps` export property
