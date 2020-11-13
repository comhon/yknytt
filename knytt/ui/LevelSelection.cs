using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using YKnyttLib;

public class LevelSelection : CanvasLayer
{
    KnyttWorldManager<Texture> Manager { get; }
    PackedScene info_scene;

    [Export] public int loadThreads = 4;

    string filter_category;
    string filter_difficulty;
    string filter_size;

    bool halt_consumers = false;
    bool discovery_over = false;
    List<Task> consumers;

    GameButton top_button;

    ConcurrentQueue<KnyttWorldManager<Texture>.WorldEntry> finished_entries;
    ConcurrentQueue<Action> load_hopper;

    public LevelSelection()
    {
        Manager = new KnyttWorldManager<Texture>();
        finished_entries = new ConcurrentQueue<KnyttWorldManager<Texture>.WorldEntry>();
        load_hopper = new ConcurrentQueue<Action>();
        consumers = new List<Task>();
    }

    public override void _Ready()
    {
        this.info_scene = ResourceLoader.Load<PackedScene>("res://knytt/ui/InfoScreen.tscn");
        var sys = OS.GetName();
        //if (sys.Equals("Android") || sys.Equals("HTML5") || sys.Equals("iOS")) { singleThreadedLoad(); }
        //else { multiThreadedLoad(); }
        singleThreadedLoad();
    }

    private void singleThreadedLoad()
    {
        loadDefaultWorlds(true);
        discoverWorlds("./worlds", true);
    }

    private void multiThreadedLoad()
    {
        startHopperConsumers();

        Task.Run(() => this.loadDefaultWorlds(false));
        Task.Run(() => this.discoverWorlds("./worlds", false));
    }

    private void startHopperConsumers()
    {
        Action consumer = () =>
        {
            while (true)
            {
                Action action;
                if (load_hopper.Count > 0 && load_hopper.TryDequeue(out action))
                {
                    var t = Task.Run(action);
                    t.Wait();
                }
                else
                {
                    var t = Task.Delay(5);
                    t.Wait();
                    if (discovery_over) { break; }
                }

                if (halt_consumers) { break; }
            }
        };

        for (int i = 0; i < loadThreads; i++)
        {
            consumers.Add(Task.Run(consumer));
        }
    }

    public void killConsumers()
    {
        halt_consumers = true;
        Task.WaitAll(consumers.ToArray());
    }

    public override void _PhysicsProcess(float delta)
    {
        // Process the queue
        if (finished_entries.Count == 0) { return; }
        KnyttWorldManager<Texture>.WorldEntry entry;
        if (!finished_entries.TryDequeue(out entry)) { return; }
        if (Manager.addWorld(entry))
        {
            var game_container = GetNode<GameContainer>("MainContainer/ScrollContainer/GameContainer");
            game_container.addWorld((GDKnyttWorldImpl)entry.world, entry.extra_data, game_container.Count == 0);
        }
    }

    private void loadDefaultWorlds(bool single_threaded)
    {
        startBinLoad("res://knytt/worlds/Nifflas - The Machine.knytt.bin", single_threaded);
        startBinLoad("res://knytt/worlds/Nifflas - Gustav's Daughter.knytt.bin", single_threaded);
        startBinLoad("res://knytt/worlds/Nifflas - Sky Flowerz.knytt.bin", single_threaded);
        startBinLoad("res://knytt/worlds/Nifflas - Tutorial.knytt.bin", single_threaded);
    }

    // Search the given directory for worlds
    // TODO: This should do a very different process for bin files
    private void discoverWorlds(string path, bool single_threaded)
    {
        var wd = new Directory();
        //if (!wd.DirExists(path)) { discovery_over = true; return; }
        var error = wd.Open(path);
        if (error != Error.Ok) { discovery_over = true; return; }

        wd.ListDirBegin();
        while(true)
        {
            string name = wd.GetNext();
            if (name.Length == 0) { break; }
            
            if (wd.CurrentIsDir())
            {
                if (!verifyDirWorld(wd, name)) { continue; }
                startDirectoryLoad(wd.GetCurrentDir() + "/" + name, name, single_threaded);
            }
            else
            {
                if (!name.EndsWith(".knytt.bin")) { continue; }
                startBinLoad(wd.GetCurrentDir() + "/" + name, single_threaded);
            }
        }
        wd.ListDirEnd();
        discovery_over = true;
    }

    private void startDirectoryLoad(string world_dir, string name, bool single_threaded)
    {
        if (single_threaded) { directoryLoad(world_dir, name); return; }

        Action action = () => { directoryLoad(world_dir, name); };
        load_hopper.Enqueue(action);
    }

    private void directoryLoad(string world_dir, string name)
    {
        var entry = generateDirectoryWorld(world_dir, name);
        finished_entries.Enqueue(entry);
    }

    private void startBinLoad(string world_dir, bool single_threaded)
    {
        if (single_threaded) { binLoad(world_dir); return; }

        Action action = () => { binLoad(world_dir); };
        load_hopper.Enqueue(action);
    }

    private void binLoad(string world_dir)
    {
        var entry = generateBinWorld(world_dir);
        finished_entries.Enqueue(entry);
    }

    object file_lock = new object();

    private KnyttWorldManager<Texture>.WorldEntry generateDirectoryWorld(string world_dir, string name)
    {
        Texture icon;
        string txt;
        lock(file_lock) {icon = GDKnyttAssetManager.loadExternalTexture(world_dir + "/Icon.png");}
        lock(file_lock) {txt = GDKnyttAssetManager.loadTextFile(world_dir + "/World.ini");}
        GDKnyttWorldImpl world = new GDKnyttWorldImpl();
        world.setDirectory(world_dir, name);
        world.loadWorldConfig(txt);
        return new KnyttWorldManager<Texture>.WorldEntry(world, icon);
    }

    private KnyttWorldManager<Texture>.WorldEntry generateBinWorld(string world_dir)
    {
        Texture icon;
        string txt;
        KnyttBinWorldLoader binloader;
        lock(file_lock) {binloader = new KnyttBinWorldLoader(GDKnyttAssetManager.loadFile(world_dir));}
        lock(file_lock) {icon = GDKnyttAssetManager.loadTexture(binloader.GetFile("Icon.png"));}
        lock(file_lock) {txt = GDKnyttAssetManager.loadTextFile(binloader.GetFile("World.ini"));}
        GDKnyttWorldImpl world = new GDKnyttWorldImpl();
        world.setDirectory(world_dir, binloader.RootDirectory);
        world.loadWorldConfig(txt);
        world.setBinMode(null);
        return new KnyttWorldManager<Texture>.WorldEntry(world, icon);
    }

    private bool verifyDirWorld(Directory dir, string name)
    {
        if (name.Equals(".") || name.Equals("..")) { return false; }

        // TODO: Check for file signatures
        return true;
    }

    private void listWorlds()
    {
        // Should take filter settings into account
        Manager.setFilter(filter_category, filter_difficulty, filter_size);
        var worlds = Manager.Filtered;
        var game_container = GetNode<GameContainer>("MainContainer/ScrollContainer/GameContainer");
        game_container.clearWorlds();

        foreach(var world_entry in worlds)
        {
            game_container.addWorld((GDKnyttWorldImpl)world_entry.world, world_entry.extra_data, false);
        }
    }

    public void _on_BackButton_pressed()
    {
        killConsumers();
        ClickPlayer.Play();
        this.QueueFree();
    }

    public void _on_GamePressed(GameButton button)
    {
        ClickPlayer.Play();
        var info = info_scene.Instance() as InfoScreen;
        info.initialize(button.KWorld);
        this.GetParent().AddChild(info);
    }

    public void _on_CategoryDropdown_item_selected(int index)
    {
        var dropdown = GetNode<OptionButton>("MainContainer/FilterContainer/Category/CategoryDropdown");
        filter_category = dropdown.Text.Equals("[All]") ? null : dropdown.Text;
        this.listWorlds();
    }

    public void _on_DifficultyDropdown_item_selected(int index)
    {
        var dropdown = GetNode<OptionButton>("MainContainer/FilterContainer/Difficulty/DifficultyDropdown");
        filter_difficulty = dropdown.Text.Equals("[All]") ? null : dropdown.Text;
        this.listWorlds();
    }

    public void _on_SizeDropdown_item_selected(int index)
    {
        var dropdown = GetNode<OptionButton>("MainContainer/FilterContainer/Size/SizeDropdown");
        filter_size = dropdown.Text.Equals("[All]") ? null : dropdown.Text;
        this.listWorlds();
    }
}
