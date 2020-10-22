using Godot;

public class Sign : GDKnyttBaseObject
{
    public int junis = 0;

    public override void _Ready()
    {
        string letter = "";
        switch (ObjectID.y)
        {
            case 17: letter = "A"; break;
            case 18: letter = "B"; break;
            case 19: letter = "C"; break;
        }

        string text = GDArea.Area.getExtraData($"Sign({letter})");
        if (text == null) { text = "<SIGN TEXT MISSING>"; }
        GetNode<Label>("SignRect/Label").Text = text;
    }

    public void _on_Area2D_body_entered(Node body)
    {
        if (!(body is Juni)) { return; }
        junis++;
        GetNode<AnimationPlayer>("AnimationPlayer").Play("FadeIn");
    }

    public void _on_Area2D_body_exited(Node body)
    {
        if (!(body is Juni)) { return; }
        junis--;
        if (junis > 0) { return; }
        GetNode<AnimationPlayer>("AnimationPlayer").PlayBackwards("FadeIn");
        junis = 0;
    }
}
