using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YKnyttLib;

public class InfoScreen : CanvasLayer
{
    [Export] string complainURL;

    public GDKnyttWorldImpl KWorld { get; private set; }

    public void initialize(string path)
    {
        KWorld = new GDKnyttWorldImpl();
        if (new Directory().DirExists(path))
        {
            KWorld.setDirectory(path, GDKnyttAssetManager.extractFilename(path));
        }
        else
        {
            var loader = new KnyttBinWorldLoader(GDKnyttAssetManager.loadFile(path));
            KWorld.setBinMode(loader);
            KWorld.setDirectory(path, loader.RootDirectory);
        }
        string ini = GDKnyttAssetManager.loadTextFile(KWorld.getWorldData("World.ini"));
        KWorld.loadWorldConfig(ini);

        Texture info = (KWorld.worldFileExists("Info+.png") ? KWorld.getWorldTexture("Info+.png") :
                        KWorld.worldFileExists("Info.png") ? KWorld.getWorldTexture("Info.png") : null) as Texture;
        if (info != null)
        {
            info.Flags |= (uint)Texture.FlagsEnum.Filter;
            GetNode<TextureRect>("InfoRect").Texture = info;
        }

        GetNode<SlotButton>("InfoRect/Slot1Button").BaseFile = "user://Saves/" + KWorld.WorldDirectoryName;
        GetNode<SlotButton>("InfoRect/Slot2Button").BaseFile = "user://Saves/" + KWorld.WorldDirectoryName;
        GetNode<SlotButton>("InfoRect/Slot3Button").BaseFile = "user://Saves/" + KWorld.WorldDirectoryName;
        GetNode<Button>("InfoRect/RatePanel/VBoxContainer/UninstallButton").Disabled = KWorld.WorldDirectory.StartsWith("res://");
    }

    public void _on_BackButton_pressed()
    {
        ClickPlayer.Play();
        KWorld.purgeBinFile();
        this.QueueFree();
    }

    public void closeOtherSlots(int slot)
    {
        closeSlotIfNot(GetNode<SlotButton>("InfoRect/Slot1Button"), slot);
        closeSlotIfNot(GetNode<SlotButton>("InfoRect/Slot2Button"), slot);
        closeSlotIfNot(GetNode<SlotButton>("InfoRect/Slot3Button"), slot);
    }

    private void closeSlotIfNot(SlotButton sb, int slot)
    {
        if (sb.slot != slot) { sb.close(); }
    }

    public void _on_SlotButton_StartGame(bool new_save, string filename, int slot)
    {
        string cache_dir = GDKnyttAssetManager.extractFilename(KWorld.WorldDirectory);
        GDKnyttAssetManager.ensureDirExists($"user://Cache/{cache_dir}");
        var f = new File();
        f.Open($"user://Cache/{cache_dir}/LastPlayed.flag", File.ModeFlags.Write);
        f.Close();

        KnyttSave save = new KnyttSave(KWorld,
                         new_save ? GDKnyttAssetManager.loadTextFile(KWorld.getWorldData("DefaultSavegame.ini")) :
                                    GDKnyttAssetManager.loadTextFile(filename),
                                    slot);

        KWorld.CurrentSave = save;
        GDKnyttDataStore.KWorld = KWorld;
        GDKnyttDataStore.startGame(new_save);
        this.QueueFree();
    }

    private void _on_StatHTTPRequest_ready()
    {
        string serverURL = GDKnyttSettings.ServerURL;
        GetNode<HTTPRequest>("StatHTTPRequest").Request(
            $"{serverURL}/rating/?name={Uri.EscapeDataString(KWorld.Info.Name)}&author={Uri.EscapeDataString(KWorld.Info.Author)}");
    }

    private void _on_StatHTTPRequest_request_completed(int result, int response_code, string[] headers, byte[] body)
    {
        if (result == (int)HTTPRequest.Result.Success && response_code == 200) {; } else { return; }
        var response = Encoding.UTF8.GetString(body, 0, body.Length);
        var json = JSON.Parse(response);
        if (json.Error != Error.Ok) { return; }

        var my_powers = new HashSet<int>();
        var my_cutscenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var my_endings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int slot = 1; slot <= 3; slot++)
        {
            string savename = $"user://Saves/{KWorld.WorldDirectoryName} {slot}.ini";
            if (new File().FileExists(savename))
            {
                KnyttSave save = new KnyttSave(KWorld, GDKnyttAssetManager.loadTextFile(savename), slot);
                for (int i = 0; i < 13; i++) { if (save.getPower(i)) { my_powers.Add(i); } }
                my_cutscenes.UnionWith(save.Cutscenes);
                my_endings.UnionWith(save.Endings);
            }
        }

        upvotes = HTTPUtil.jsonInt(json.Result, "upvotes");
        downvotes = HTTPUtil.jsonInt(json.Result, "downvotes");
        complains = HTTPUtil.jsonInt(json.Result, "complains");
        updateRates();

        var stat_panel = GetNode<StatPanel>("InfoRect/StatPanel");
        int[] powers_count = new int[13];
        for (int i = 0; i < 13; i++)
        {
            powers_count[i] = HTTPUtil.jsonInt(json.Result, $"power{i}");
        }

        if (powers_count.Any(c => c > 0))
        {
            stat_panel.addLabel("Powers:");
            for (int i = 0; i < 13; i++)
            {
                if (powers_count[i] > 0)
                {
                    stat_panel.addPower(i, powers_count[i], my_powers.Contains(i));
                }
            }
        }

        List<string> cutscenes = new List<string>();
        List<int> cutscenes_count = new List<int>();
        List<string> endings = new List<string>();
        List<int> endings_count = new List<int>();

        var cutscene_infos = HTTPUtil.jsonValue<Godot.Collections.Array>(json.Result, "cutscenes");
        foreach (Dictionary record in cutscene_infos)
        {
            bool is_ending = HTTPUtil.jsonBool(record, "ending");
            (is_ending ? endings : cutscenes).Add(HTTPUtil.jsonValue<string>(record, "name"));
            (is_ending ? endings_count : cutscenes_count).Add(HTTPUtil.jsonInt(record, "counter"));
        }

        if (endings.Count > 0)
        {
            stat_panel.addLabel("Endings:");
            foreach (var p in endings.Zip(endings_count, (a, b) => new { Name = a, Count = b }))
            {
                stat_panel.addEnding(p.Name, p.Count, my_endings.Contains(p.Name));
            }
        }

        if (cutscenes.Count > 0)
        {
            stat_panel.addLabel("Cutscenes:");
            foreach (var p in cutscenes.Zip(cutscenes_count, (a, b) => new { Name = a, Count = b }))
            {
                stat_panel.addCutscene(p.Name, p.Count, my_cutscenes.Contains(p.Name));
            }
        }

        if (!(powers_count.Any(c => c > 0) || endings.Count > 0 || cutscenes.Count > 0))
        {
            stat_panel.addLabel("No achievements found");
        }
    }

    private void _on_StatsButton_pressed()
    {
        var panel = GetNode<Panel>("InfoRect/StatPanel");
        panel.Visible = !panel.Visible;
    }

    private int upvotes;
    private int downvotes;
    private int complains;

    public WorldEntry worldEntry
    {
        set
        {
            upvotes = value.Upvotes;
            downvotes = value.Downvotes;
            complains = value.Complains;
            updateRates();
        }
    }

    private void _on_UpvoteButton_pressed()
    {
        sendRating((int)RateHTTPRequest.Action.Upvote);
    }

    private void _on_DownvoteButton_pressed()
    {
        sendRating((int)RateHTTPRequest.Action.Downvote);
    }

    private void _on_ComplainButton_pressed()
    {
        sendRating((int)RateHTTPRequest.Action.Complain);
        GetNode<Control>("InfoRect/RatePanel/VBoxContainer/Control/VisitButton").Visible = true;
    }

    private void sendRating(int action)
    {
        GetNode<RateHTTPRequest>("RateHTTPRequest").send(KWorld.Info.Name, KWorld.Info.Author, action);
    }

    private void _on_RateHTTPRequest_RateAdded(int action)
    {
        if (action == (int)RateHTTPRequest.Action.Upvote) { upvotes++; }
        if (action == (int)RateHTTPRequest.Action.Downvote) { downvotes++; }
        if (action == (int)RateHTTPRequest.Action.Complain) { complains++; }
        updateRates();
    }

    public void updateRates()
    {
        var rate_root = GetNode<Control>("InfoRect/RatePanel/VBoxContainer/Rates");
        rate_root.GetNode<Label>("UpvoteLabel").Text = $"+{upvotes}";
        rate_root.GetNode<Label>("DownvoteLabel").Text = $"-{downvotes}";
        GetNode<Label>("InfoRect/RatePanel/VBoxContainer/Control2/Label").Text = $"({complains})";
    }

    private void _on_OptimizeButton_pressed()
    {
        Task.Run(optimize);
        GetNode<Timer>("HintTimer").Start();
    }

    private void optimize()
    {
        string[] nodes_to_disable = { "InfoRect/BackButton", 
            "InfoRect/Slot1Button", "InfoRect/Slot2Button", "InfoRect/Slot3Button", 
            "InfoRect/RatePanel/VBoxContainer/OptimizeButton", "InfoRect/RatePanel/VBoxContainer/UninstallButton", 
            "InfoRect/RatePanel/VBoxContainer/Control3/ConfirmUninstallButton" };
        foreach (string node in nodes_to_disable) { GetNode<Button>(node).Disabled = true; }
        closeOtherSlots(-1);

        if (KWorld.BinMode) { KWorld.unpackWorld(); }
        GDKnyttAssetManager.compileInternalTileset(KWorld, recompile: true);

        GetNode<Timer>("HintTimer").Stop();
        GDKnyttDataStore.ProgressHint = "Level was unpacked and compiled.";
        _on_HintTimer_timeout();
        foreach (string node in nodes_to_disable) { GetNode<Button>(node).Disabled = false; }
    }

    private void _on_HintTimer_timeout()
    {
        GetNode<Label>("InfoRect/HintLabel").Text = GDKnyttDataStore.ProgressHint;
    }

    private void _on_VisitButton_pressed()
    {
        OS.ShellOpen(complainURL);
    }

    private void _on_UninstallButton_pressed()
    {
        var button = GetNode<Control>("InfoRect/RatePanel/VBoxContainer/Control3/ConfirmUninstallButton");
        button.Visible = !button.Visible;
    }

    private void _on_ConfirmUninstallButton_pressed()
    {
        ClickPlayer.Play();
        KWorld.uninstallWorld();
        GetTree().ChangeScene("res://knytt/ui/MainMenu.tscn");
    }
}
