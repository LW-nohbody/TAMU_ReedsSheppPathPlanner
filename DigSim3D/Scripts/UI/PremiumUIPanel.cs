using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigSim3D.UI
{
    /// <summary>
    /// Premium draggable UI panel with glassmorphism effect
    /// </summary>
    public partial class PremiumUIPanel : Control
    {
        private Panel _backgroundPanel = null!;
        private Control _contentContainer = null!;
        private Label _titleLabel = null!;
        private bool _isDragging = false;
        private Vector2 _dragOffset;
        
        // Glassmorphism colors
        private Color _glassColor = new Color(0.1f, 0.1f, 0.15f, 0.75f);
        private Color _borderColor = new Color(0.4f, 0.6f, 0.9f, 0.8f);
        private Color _glowColor = new Color(0.4f, 0.6f, 1.0f, 0.3f);
        
        public string Title { get; set; } = "Panel";
        public bool IsDraggable { get; set; } = true;
        
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Pass;
            
            // Create glassmorphic background panel
            _backgroundPanel = new Panel
            {
                MouseFilter = MouseFilterEnum.Stop
            };
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = _glassColor;
            styleBox.BorderColor = _borderColor;
            styleBox.SetBorderWidthAll(2);
            styleBox.SetCornerRadiusAll(12);
            
            // Add subtle shadow/glow effect
            styleBox.ShadowColor = _glowColor;
            styleBox.ShadowSize = 8;
            styleBox.ShadowOffset = new Vector2(0, 2);
            
            _backgroundPanel.AddThemeStyleboxOverride("panel", styleBox);
            AddChild(_backgroundPanel);
            
            // Resize background to match container
            _backgroundPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            
            // Create title bar for dragging
            CreateTitleBar();
            
            // Create content container with padding
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 15);
            margin.AddThemeConstantOverride("margin_right", 15);
            margin.AddThemeConstantOverride("margin_top", 45); // Space for title bar
            margin.AddThemeConstantOverride("margin_bottom", 15);
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            margin.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(margin);
            
            _contentContainer = new VBoxContainer
            {
                MouseFilter = MouseFilterEnum.Pass
            };
            _contentContainer.AddThemeConstantOverride("separation", 10);
            margin.AddChild(_contentContainer);
        }
        
        private void CreateTitleBar()
        {
            var titleBar = new Control
            {
                CustomMinimumSize = new Vector2(0, 40)
            };
            titleBar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            titleBar.OffsetBottom = 40;
            titleBar.MouseFilter = MouseFilterEnum.Stop;
            AddChild(titleBar);
            
            // Title label
            _titleLabel = new Label
            {
                Text = Title,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = Colors.White
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 16);
            _titleLabel.AddThemeColorOverride("font_color", Colors.White);
            _titleLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
            titleBar.AddChild(_titleLabel);
            
            // Connect drag events to title bar
            titleBar.GuiInput += OnTitleBarInput;
        }
        
        private void OnTitleBarInput(InputEvent @event)
        {
            if (!IsDraggable) return;
            
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    if (mouseButton.Pressed)
                    {
                        _isDragging = true;
                        _dragOffset = mouseButton.Position;
                    }
                    else
                    {
                        _isDragging = false;
                    }
                }
            }
        }
        
        public override void _Process(double delta)
        {
            if (_isDragging)
            {
                var mousePos = GetViewport().GetMousePosition();
                Position = mousePos - _dragOffset;
                
                // Keep panel within viewport bounds
                var viewportSize = GetViewportRect().Size;
                Position = new Vector2(
                    Mathf.Clamp(Position.X, 0, viewportSize.X - Size.X),
                    Mathf.Clamp(Position.Y, 0, viewportSize.Y - Size.Y)
                );
            }
        }
        
        public Control GetContentContainer() => _contentContainer;
        
        public void AddContent(Control child)
        {
            _contentContainer.AddChild(child);
        }
        
        // Animate panel entrance
        public async void AnimateIn()
        {
            Modulate = new Color(1, 1, 1, 0);
            Scale = new Vector2(0.9f, 0.9f);
            
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "modulate:a", 1.0f, 0.3);
            tween.TweenProperty(this, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            await ToSignal(tween, Tween.SignalName.Finished);
        }
    }
}
