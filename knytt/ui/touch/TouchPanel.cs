using Godot;
using System;
using System.Linq;

public class TouchPanel : Panel
{
    private StyleBox normalStylebox;
    private StyleBox pressedStylebox;

    private Control leftUpPanel, rightUpPanel, leftPanel, rightPanel, downPanel,
        infoPanel, resetPanel, pausePanel, umbrellaPanel, walkPanel,
        jumpPanel, down2Panel, arrowsMainPanel, jumpMainPanel;

    private Rect2 leftRect, rightRect, upRect, downRect,
        infoRect, resetRect, pauseRect, umbrellaRect, walkRect,
        jumpRect, down2Rect;

    private Rect2[] allRectangles;

    private String[] actionNames = {
        "left", "right", "up", "down",
        "show_info", "debug_die", "pause", "umbrella", "walk", "jump", "down"
    };

    private Control[] jumpPanels;
    private String[] jumpActionNames;

    // Excess space for buttons
    private const int X_EXCESS = 40;
    private const int TOP_EXCESS = 50;
    private const int BOTTOM_EXCESS = 40;

    // Left/right prediction settings
    private const float SPEED_TOO_FAST = 80;
    private const float SPEED_TOO_SLOW = 30;


    public override void _Ready()
    {
        normalStylebox = ResourceLoader.Load("res://knytt/ui/touch/NormalStyleBox.tres") as StyleBox;
        pressedStylebox = ResourceLoader.Load("res://knytt/ui/touch/PressedStyleBox.tres") as StyleBox;

        leftUpPanel = GetNode<Control>("ArrowsPanel/LeftUpPanel");
        rightUpPanel = GetNode<Control>("ArrowsPanel/RightUpPanel");
        leftPanel = GetNode<Control>("ArrowsPanel/LeftPanel");
        rightPanel = GetNode<Control>("ArrowsPanel/RightPanel");
        downPanel = GetNode<Control>("ArrowsPanel/DownPanel");
        infoPanel = GetNode<Control>("JumpPanel/InfoPanel");
        resetPanel = GetNode<Control>("JumpPanel/ResetPanel");
        pausePanel = GetNode<Control>("JumpPanel/PausePanel");
        umbrellaPanel = GetNode<Control>("JumpPanel/UmbrellaPanel");
        walkPanel = GetNode<Control>("JumpPanel/WalkPanel");
        jumpPanel = GetNode<Control>("JumpPanel/JumpPanel");
        down2Panel = GetNode<Control>("JumpPanel/DownPanel");
        arrowsMainPanel = GetNode<Control>("ArrowsPanel");
        jumpMainPanel = GetNode<Control>("JumpPanel");

        jumpPanels = new Control[] {
            infoPanel, resetPanel, pausePanel, umbrellaPanel, walkPanel, jumpPanel, down2Panel
        };
        jumpActionNames = actionNames.Skip(4).ToArray();

        GetTree().Root.Connect("size_changed", this, nameof(_on_viewport_size_changed));
        GetNode("/root/Console").Connect("ConsoleOpen", this, nameof(OnConsoleOpen));
        GetNode("/root/Console").Connect("ConsoleClosed", this, nameof(OnConsoleClosed));

        Configure();
    }

    // Manipulates anchors and margins to apply program settings
    public void Configure()
    {
        Visible = TouchSettings.EnablePanel;
        SetProcessInput(TouchSettings.EnablePanel);
        var curtain = GetTree().Root.FindNode("Curtain", owned: false) as Control;
        curtain.Visible = Visible;
        if (!Visible) return;
        
        Modulate = new Color(Modulate.r, Modulate.g, Modulate.b, TouchSettings.Opacity);
        
        RectSize = new Vector2(TouchSettings.ScreenWidth + 4, RectSize.y);
    
        var anchor_top = TouchSettings.PanelAnchor;
        var height = arrowsMainPanel.RectSize.y - 2; // correction to hide the border at the edge

        AnchorTop = AnchorBottom = 1 - anchor_top;
        MarginTop = (1 - anchor_top) * -height;
        MarginBottom = anchor_top * height;

        int jump_width_excess = (int)(120 * (TouchSettings.JumpScale - 1));
        jumpMainPanel.MarginLeft = -240 - jump_width_excess;
        down2Panel.MarginRight = jumpPanel.MarginRight = 240 + jump_width_excess;
        jumpPanel.GetNode<Control>("Label").RectPivotOffset = jumpPanel.RectSize / 2;
        down2Panel.GetNode<Control>("Label").RectPivotOffset = down2Panel.RectSize / 2;

        arrowsMainPanel.RectPivotOffset = new Vector2(0, (1 - anchor_top) * height);
        jumpMainPanel.RectPivotOffset = new Vector2(jumpMainPanel.RectSize.x, (1 - anchor_top) * height);
        
        var swap = TouchSettings.SwapHands;
        arrowsMainPanel.AnchorLeft = arrowsMainPanel.AnchorRight = swap ? 1f : 0f;
        jumpMainPanel.AnchorLeft = jumpMainPanel.AnchorRight = swap ? 0f : 1f;
        foreach (var p in jumpPanels)
        {
            p.GetNode<Control>("Label").RectScale = new Vector2(swap ? -1f : 1f, 1f);
        }

        _on_viewport_size_changed();
    }

    private float getScale()
    {
        return Mathf.Min(OS.GetScreenDpi() * TouchSettings.Scale * GetViewport().GetVisibleRect().Size.x / (GetViewport().Size.x * 100), 1.4f / TouchSettings.Viewport);
    }

    // Returns rectangle for the button with excess space
    private Rect2 getPressRect(Control c, bool grow_left = false, bool grow_top = false,
                               bool grow_right = false, bool grow_bottom = false,
                               bool flip_left = false)
    {
        // "Scale" doesn't affect RectSize, needs to calculate it manually
        // Also "flip_left" is a workaround when scale < 0
        var scale = getScale();
        float x = c.RectGlobalPosition.x - (grow_left ? X_EXCESS * scale : 0);
        float y = c.RectGlobalPosition.y - (grow_top ? TOP_EXCESS * scale : 0);
        float x_size = c.RectSize.x * scale + (grow_left ? X_EXCESS * scale : 0)
                                            + (grow_right ? X_EXCESS * scale : 0);
        float y_size = c.RectSize.y * scale + (grow_top ? TOP_EXCESS * scale : 0)
                                            + (grow_bottom ? BOTTOM_EXCESS * scale : 0);
        return new Rect2(x - (flip_left ? x_size : 0), y, x_size, y_size);
    }

    private void _on_viewport_size_changed()
    {
        if (!TouchSettings.EnablePanel || GetViewport() == null) { return; }

        var scale = getScale();
        var swap_hands = TouchSettings.SwapHands;

        // Finish button placements (they dynamically depends on scale)
        arrowsMainPanel.MarginLeft = swap_hands ? -120 * scale : 0;
        arrowsMainPanel.MarginRight = swap_hands ? 0 : 120 * scale;
        arrowsMainPanel.RectScale = new Vector2(scale, scale);
        jumpMainPanel.RectScale = new Vector2(swap_hands ? -scale : scale, scale);
        
        // Calculate rects for all the buttons
        leftRect = getPressRect(leftUpPanel, grow_left: true, grow_top: true)
                        .GrowIndividual(0, 0, 0, leftPanel.RectSize.y * scale);
        rightRect = getPressRect(rightUpPanel, grow_right: true, grow_top: true)
                        .GrowIndividual(0, 0, 0, rightPanel.RectSize.y * scale);
        upRect = getPressRect(leftUpPanel, grow_left: true, grow_right: true, grow_top: true)
                        .GrowIndividual(0, 0, rightUpPanel.RectSize.y * scale, 0);
        downRect = getPressRect(downPanel, grow_left: true, grow_right: true, grow_bottom: true);

        infoRect = getPressRect(infoPanel, flip_left: swap_hands);
        resetRect = getPressRect(resetPanel, flip_left: swap_hands);
        pauseRect = getPressRect(pausePanel, flip_left: swap_hands);
        umbrellaRect = getPressRect(umbrellaPanel, grow_top: true, flip_left: swap_hands);
        walkRect = getPressRect(walkPanel, flip_left: swap_hands);
        jumpRect = getPressRect(jumpPanel, grow_top: true, flip_left: swap_hands);
        down2Rect = getPressRect(down2Panel, grow_bottom: true, flip_left: swap_hands);

        allRectangles = new Rect2[] {
            leftRect, rightRect, upRect, downRect,
            infoRect, resetRect, pauseRect, umbrellaRect, walkRect,
            jumpRect, down2Rect
        };

        // Some magic formula to stick visible area to the top, or bottom, or center
        var camera = GetTree().Root.FindNode("GKnyttCamera", owned: false) as Camera2D;
        camera.Offset = new Vector2(-TouchSettings.ScreenWidth / 2, (TouchSettings.AreaAnchor - 1) * (GetViewport().GetVisibleRect().Size.y - 240) - 120);

        var curtain = GetTree().Root.FindNode("Curtain", owned: false);
        var horizontalCurtain = curtain.GetNode<ColorRect>("HorizontalRect");
        var leftCurtain = curtain.GetNode<ColorRect>("LeftRect");
        var rightCurtain = curtain.GetNode<ColorRect>("RightRect");
        float curtainHeight = GetViewport().GetVisibleRect().Size.y - 240;
        horizontalCurtain.RectSize = new Vector2(TouchSettings.ScreenWidth, curtainHeight);
        horizontalCurtain.RectPosition = new Vector2(0, TouchSettings.AreaAnchor * 240);
        leftCurtain.RectSize = rightCurtain.RectSize = 
            new Vector2((TouchSettings.ScreenWidth - 600) / 2, GetViewport().GetVisibleRect().Size.y);
        rightCurtain.RectPosition = new Vector2(TouchSettings.ScreenWidth - rightCurtain.RectSize.x, 0);
    }

    private void ChangeOpacity(Control c, bool pressed)
    {
        c.AddStyleboxOverride("panel", pressed ? pressedStylebox : normalStylebox);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventScreenTouch || @event is InputEventScreenDrag)
        {
            Vector2 position = @event is InputEventScreenTouch ?
                (@event as InputEventScreenTouch).Position :
                (@event as InputEventScreenDrag).Position;

            bool released = @event is InputEventScreenTouch && !(@event as InputEventScreenTouch).Pressed;

            if (released)
            {
                foreach (var p in allRectangles.Zip(actionNames, (a, b) => new { Rect = a, Name = b }))
                {
                    if (p.Rect.HasPoint(position)) { Input.ActionRelease(p.Name); }
                }

                if (leftRect.HasPoint(position)) { Input.ActionRelease("right"); }
                if (rightRect.HasPoint(position)) { Input.ActionRelease("left"); }
            }
            else if (@event is InputEventScreenDrag drag_event)
            {
                var speed = drag_event.Speed;
                var rel = drag_event.Relative;
                var old_position = drag_event.Position - drag_event.Relative;

                // Release action if user lefts the button
                if ((leftRect.HasPoint(old_position) && !leftRect.HasPoint(position)) ||
                    (rightRect.HasPoint(old_position) && !rightRect.HasPoint(position)))
                {
                    Input.ActionRelease(Input.IsActionPressed("left") ? "left" : "right");
                }

                foreach (var p in allRectangles.Zip(actionNames, (a, b) => new { Rect = a, Name = b }))
                {
                    if (p.Rect.HasPoint(old_position) && !p.Rect.HasPoint(position)) { Input.ActionRelease(p.Name); }
                }

                float adj_speed = speed.x / getScale();
                if (TouchSettings.Swipe)
                {
                    // If user swipes left/right too fast, left/right action can be pressed in advance
                    if (leftRect.HasPoint(position) || rightRect.HasPoint(position))
                    {
                        if (adj_speed > SPEED_TOO_FAST)
                        {
                            Input.ActionRelease("left");
                            Input.ActionPress("right");
                        }
                        if (adj_speed < -SPEED_TOO_FAST)
                        {
                            Input.ActionRelease("right");
                            Input.ActionPress("left");
                        }
                    }
                }

                // If user swipes back after this, original direction should be restored
                // Swiping up/down (too slow on X) should not affect this
                if (leftRect.HasPoint(position) && !(Input.IsActionPressed("right") && adj_speed >= -SPEED_TOO_SLOW ))
                {
                    Input.ActionRelease("right");
                    Input.ActionPress("left");
                }
                if (rightRect.HasPoint(position) && !(Input.IsActionPressed("left") && adj_speed <= SPEED_TOO_SLOW))
                {
                    Input.ActionRelease("left");
                    Input.ActionPress("right");
                }

                if (downRect.HasPoint(position)) { Input.ActionPress("down"); }
                if (upRect.HasPoint(position)) { Input.ActionPress("up"); }
            }
            else
            {
                foreach (var p in allRectangles.Zip(actionNames, (a, b) => new { Rect = a, Name = b }))
                {
                    if (p.Rect.HasPoint(position)) { Input.ActionPress(p.Name); }
                }
                
                if (Input.IsActionPressed("show_info") && Input.IsActionPressed("jump") && Input.IsActionPressed("down"))
                {
                    Input.ActionRelease("show_info");
                    Input.ActionRelease("jump");
                    Input.ActionRelease("down");
                    GetNode<Console>("/root/Console").toggleConsole();
                }
            }

            ChangeOpacity(leftUpPanel, Input.IsActionPressed("left") && Input.IsActionPressed("up"));
            ChangeOpacity(rightUpPanel, Input.IsActionPressed("right") && Input.IsActionPressed("up"));
            ChangeOpacity(leftPanel, Input.IsActionPressed("left") && !Input.IsActionPressed("up"));
            ChangeOpacity(rightPanel, Input.IsActionPressed("right") && !Input.IsActionPressed("up"));
            ChangeOpacity(downPanel, Input.IsActionPressed("down"));
            foreach (var p in jumpPanels.Zip(jumpActionNames, (a, b) => new { Pan = a, Name = b }))
            {
                if (p.Name == "pause" || p.Name == "map") continue;
                ChangeOpacity(p.Pan, Input.IsActionPressed(p.Name));
            }
        }
    }

    // TODO: temporary solution. Maybe take some space from "down" button?
    public void InstallMap()
    {
        actionNames[8] = "map";
        jumpActionNames[4] = "map";
        walkPanel.GetNode<Label>("Label").Text = "Map";
    }

    public void OnConsoleOpen()
    {
        SetProcessInput(false);
    }

    public void OnConsoleClosed()
    {
        SetProcessInput(TouchSettings.EnablePanel);
    }
}
