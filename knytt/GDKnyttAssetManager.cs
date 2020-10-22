using System.Collections.Generic;
using System.Text;
using Godot;
using YKnyttLib;
using YUtil.Collections;

public class GDKnyttAssetManager
{
    public const int TILESET_WIDTH = 16;
    public const int TILESET_HEIGHT = 8;
    public const int TILE_WIDTH = 24;
    public const int TILE_HEIGHT = 24;

    public GDKnyttWorld GDWorld { get; }
    private Dictionary<string, string> Directories { get; }

    ObjectCache<int, TileSet> TileSetCache;
    ObjectCache<int, Texture> GradientCache;
    ObjectCache<int, AudioStream> SongCache;
    ObjectCache<int, AudioStream> AmbianceCache;
    ObjectCache<KnyttPoint, GDKnyttObjectBundle> ObjectCache;
    
    public GDKnyttAssetManager(GDKnyttWorld gdworld, int tile_cache, int gradient_cache, int song_cache, int ambiance_cache, int object_cache)
    {
        this.GDWorld = gdworld;
        Directories = new Dictionary<string, string>();

        TileSetCache = new ObjectCache<int, TileSet>(tile_cache);
        TileSetCache.OnCreate = (int num) => buildTileSet(num);

        GradientCache = new ObjectCache<int, Texture>(gradient_cache);
        GradientCache.OnCreate = (int num) => buildGradient(num);

        SongCache = new ObjectCache<int, AudioStream>(song_cache);
        SongCache.OnCreate = (int num) => buildSong(num);

        AmbianceCache = new ObjectCache<int, AudioStream>(ambiance_cache);
        AmbianceCache.OnCreate = (int num) => buildAmbiance(num);

        ObjectCache = new ObjectCache<KnyttPoint, GDKnyttObjectBundle>(object_cache);
        ObjectCache.OnCreate = (KnyttPoint id) => GDKnyttObjectFactory.buildKnyttObject(id);
    }

    public TileSet getTileSet(int num) { return TileSetCache.IncObject(num); }
    public void returnTileSet(int num) { TileSetCache.DecObject(num); }

    public Texture getGradient(int num) { return GradientCache.IncObject(num); }
    public void returnGradient(int num) { GradientCache.DecObject(num); }

    public AudioStream getSong(int num) { return SongCache.IncObject(num); }
    public void returnSong(int num) { SongCache.DecObject(num); }

    public AudioStream getAmbiance(int num) { return AmbianceCache.IncObject(num); }
    public void returnAmbiance(int num) { AmbianceCache.DecObject(num); }

    public GDKnyttObjectBundle GetObject(KnyttPoint object_id) { return ObjectCache.IncObject(object_id); }
    public void returnObject(KnyttPoint object_id) { ObjectCache.DecObject(object_id); }

    private TileSet buildTileSet(int num)
    {
        var texture = GDWorld.KWorld.getWorldTexture($"Tilesets/Tileset{num}.png");
        return makeTileset((Texture)texture, true);
    }

    private Texture buildGradient(int num)
    {
        return (Texture)GDWorld.KWorld.getWorldTexture($"Gradients/Gradient{num}.png");
    }

    public AudioStream buildSong(int num)
    {
        if (num == 0) { return null; }
        return (AudioStream)GDWorld.KWorld.getWorldSound($"Music/Song{num}.ogg");
    }

    public AudioStream buildAmbiance(int num)
    {
        if (num == 0) { return null; }
        return (AudioStream)GDWorld.KWorld.getWorldSound($"Ambiance/Ambi{num}.ogg");
    }

    public static Texture loadExternalTexture(string path)
    {
        var image = new Image();
        image.Load(path);
        return image2Texture(image);
    }

    public static Texture loadInternalTexture(string path)
    {
        return ResourceLoader.Load<Texture>(path);
    }

    public static Texture loadTexture(byte[] buffer)
    {
        var image = new Image();
        image.LoadPngFromBuffer(buffer);
        return image2Texture(image);
    }

    public static AudioStream loadSound(string path)
    {
        return ResourceLoader.Load<AudioStream>(path);
    }

    private static Texture image2Texture(Image image)
    {
        var texture = new ImageTexture();
        texture.CreateFromImage(image, (int)Texture.FlagsEnum.Repeat);
        return texture;
    }

    public static string loadTextFile(byte[] buffer)
    {
        ASCIIEncoding encoder = new ASCIIEncoding();
        return encoder.GetString(buffer, 0, buffer.Length);
    }

    public static string loadTextFile(string path)
    {
        var f = new File();
        f.Open(path, File.ModeFlags.Read);
        string txt = f.GetAsText();
        f.Close();
        return txt;
    }

    public static AudioStream loadRaw(byte[] buffer, int sample_rate=11025)
    {
        var sample = new AudioStreamSample();
        sample.Format = AudioStreamSample.FormatEnum.Format8Bits;
        sample.MixRate = sample_rate;
        sample.Stereo = false;
        sample.Data = buffer;
        //sample.LoopMode = loop ? AudioStreamSample.LoopModeEnum.PingPong : AudioStreamSample.LoopModeEnum.Disabled;
        return sample;
    }

    public static AudioStream loadRaw(string path, int sample_rate=11025)
    {
        var sample = loadRaw(loadFile(path), sample_rate);
        return sample;
    }

    public static byte[] loadFile(string path)
    {
        var f = new File();
        f.Open(path, File.ModeFlags.Read);
        var buffer = f.GetBuffer((int)f.GetLen());
        f.Close();
        return buffer;
    }

    public static AudioStream loadOGG(byte[] buffer, bool loop=false)
    {
        var stream = new AudioStreamOGGVorbis();
        stream.Data = buffer;
        stream.Loop = loop;

        return stream;
    }

    public static TileSet makeTileset(Texture texture, bool collisions)
    {
        var ts = new TileSet();
        
        int i = 0;
        for (int y = 0; y < TILESET_HEIGHT; y++)
        {
            for (int x = 0; x < TILESET_WIDTH; x++)
            {
                ts.CreateTile(i);
                ts.TileSetTexture(i, texture);
                var region = new Rect2(x*TILE_WIDTH, y*TILE_HEIGHT, TILE_WIDTH, TILE_HEIGHT);
                ts.TileSetRegion(i, region);

                if (collisions)
                {
                    var image = texture.GetData();
                    var bitmap = new BitMap();
                    bitmap.CreateFromImageAlpha(image, .01f);

                    var polygons = bitmap.OpaqueToPolygons(region, 2);
                    List<Vector2> plist = new List<Vector2>();
                    for (int j = 0; j < polygons.Count; j++)
                    {
                        Vector2[] v = (Vector2[])polygons[j];
                        for (int k = 0; k < v.Length; k++)
                        {
                            // I have no idea why it's adding y*48 to y coordinates...
                            Vector2 mv = new Vector2(v[k].x, v[k].y-(y*TILE_HEIGHT*2));
                            plist.Add(mv);
                        }
                    }

                    // Point cloud must be at least 3
                    if (plist.Count >= 3)
                    {
                        var collision = new ConvexPolygonShape2D();
                        collision.SetPointCloud(plist.ToArray());
                        ts.TileSetShape(i, 0, collision);
                    }
                }
                i++;
            }
        }
        return ts;
    }
}
