using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlanetRushGame : MonoBehaviour
{
    const int Width = 13;
    const int Height = 180;
    const float Cell = 0.62f;
    const float BlockHP = 4f;

    class Block
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float hp;
        public int x, y;
    }

    readonly List<Block> blocks = new();
    Transform ship;
    SpriteRenderer shipRenderer;
    Camera cam;
    Canvas canvas;
    Text progressText;
    Text planetText;
    Text resourceText;
    float miningTimer;
    float particleTimer;
    float shipSpeed = 2.7f;
    int mined;
    int total;
    int resources;
    int targetY = 1;
    int targetX = Width / 2;
    Vector2 velocity = new(0.8f, -2.4f);
    bool planetCleared;

    void Awake()
    {
        Application.targetFrameRate = 60;
        BuildCamera();
        BuildPlanet();
        BuildShip();
        BuildHUD();
    }

    void Update()
    {
        if (planetCleared) return;

        UpdateTarget();
        MoveShip();
        MineTarget();
        UpdateCamera();
        UpdateHUD();

        particleTimer -= Time.deltaTime;
        if (particleTimer <= 0f)
        {
            particleTimer = 0.055f;
            SpawnTrailParticle();
        }

        if (mined >= total)
        {
            planetCleared = true;
            Invoke(nameof(NewPlanet), 1.0f);
        }
    }

    void BuildCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 5.2f;
        cam.transform.position = new Vector3(0, 2.5f, -20);
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    void BuildPlanet()
    {
        total = Width * Height;
        var palette = new[]
        {
            new Color(0.10f,0.75f,1f),
            new Color(0.20f,0.95f,0.85f),
            new Color(0.55f,0.25f,1f),
            new Color(1f,0.25f,0.72f),
            new Color(0.95f,0.55f,0.12f),
            new Color(0.95f,0.9f,0.2f)
        };

        Sprite baseSprite = MakeSquareSprite();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var go = new GameObject("Block_" + x + "_" + y);
                go.transform.position = GridPos(x, y);
                go.transform.localScale = Vector3.one * Cell;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = baseSprite;

                float r = Mathf.PerlinNoise(x * 0.37f, y * 0.17f);
                Color c = palette[Mathf.Clamp(Mathf.FloorToInt(r * palette.Length), 0, palette.Length - 1)];
                c *= 0.72f + 0.28f * Mathf.PerlinNoise(x * 0.9f, y * 0.31f);
                sr.color = c;
                sr.sortingOrder = 0;

                var b = new Block { go=go, sr=sr, hp=BlockHP, x=x, y=y };
                blocks.Add(b);
            }
        }
    }

    void BuildShip()
    {
        var go = new GameObject("Autonomous Miner");
        ship = go.transform;
        ship.position = GridPos(Width / 2, 1) + Vector3.back * 0.1f;

        shipRenderer = go.AddComponent<SpriteRenderer>();
        shipRenderer.sprite = MakeShipSprite();
        shipRenderer.color = Color.white;
        shipRenderer.sortingOrder = 20;

        // Soft neon halo made from simple layered sprites, avoiding external assets.
        var glow = new GameObject("Ship Glow");
        glow.transform.SetParent(ship);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localScale = Vector3.one * 1.65f;
        var gr = glow.AddComponent<SpriteRenderer>();
        gr.sprite = MakeSquareSprite();
        gr.color = new Color(0.05f,0.9f,1f,0.16f);
        gr.sortingOrder = 19;
    }

    void BuildHUD()
    {
        var cgo = new GameObject("HUD");
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cgo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080,1920);
        cgo.AddComponent<GraphicRaycaster>();

        var top = MakePanel("TopBar", new Vector2(0.5f,0.97f), new Vector2(920,190), new Color(0.01f,0.015f,0.035f,0.92f));
        planetText = MakeText(top.transform, "PLANET 01", 54, TextAnchor.UpperLeft, new Vector2(800,70), new Vector2(-20,-15));
        progressText = MakeText(top.transform, "MINED 0 / 2340", 44, TextAnchor.MiddleLeft, new Vector2(800,70), new Vector2(-20,-82));
        resourceText = MakeText(top.transform, "◆ 0", 44, TextAnchor.MiddleRight, new Vector2(220,70), new Vector2(-20,-82));

        var hint = MakeText(canvas.transform, "AUTONOMOUS MINER", 28, TextAnchor.MiddleCenter, new Vector2(600,55), new Vector2(0,-875));
        hint.color = new Color(0.45f,0.8f,1f,0.65f);
    }

    GameObject MakePanel(string name, Vector2 anchor, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    Text MakeText(Transform parent, string value, int size, TextAnchor align, Vector2 dims, Vector2 pos)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = value;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.sizeDelta = dims;
        rt.anchoredPosition = pos;
        return t;
    }

    void UpdateTarget()
    {
        int currentY = Mathf.Clamp(Mathf.RoundToInt((2.5f - ship.position.y) / Cell), 0, Height-1);
        int desiredY = Mathf.Min(Height-1, currentY + 1);
        Block best = null;
        float bestScore = float.MaxValue;
        foreach (var b in blocks)
        {
            if (b.go == null || b.y < desiredY) continue;
            float dy = b.y - desiredY;
            float dx = Mathf.Abs(b.x - Mathf.RoundToInt((ship.position.x / Cell) + Width * 0.5f));
            if (dy > 5f) continue;
            float score = dy * 1.8f + dx;
            if (score < bestScore) { bestScore = score; best = b; }
        }
        if (best != null) { targetX = best.x; targetY = best.y; }
    }

    void MoveShip()
    {
        Vector3 target = GridPos(targetX, targetY);
        Vector2 delta = target - ship.position;
        float horizontal = Mathf.Clamp(delta.x * 1.8f, -1.9f, 1.9f);

        velocity.x = Mathf.Lerp(velocity.x, horizontal, Time.deltaTime * 4.5f);
        velocity.y = Mathf.Lerp(velocity.y, -2.2f, Time.deltaTime * 2.2f);

        ship.position += (Vector3)(velocity * Time.deltaTime);

        if (ship.position.x < -Width*Cell*0.5f + Cell*0.55f)
        {
            ship.position = new Vector3(-Width*Cell*0.5f + Cell*0.55f, ship.position.y, ship.position.z);
            velocity.x = Mathf.Abs(velocity.x);
        }
        if (ship.position.x > Width*Cell*0.5f - Cell*0.55f)
        {
            ship.position = new Vector3(Width*Cell*0.5f - Cell*0.55f, ship.position.y, ship.position.z);
            velocity.x = -Mathf.Abs(velocity.x);
        }

        float angle = Mathf.Clamp(-velocity.x * 7f, -18f, 18f);
        ship.rotation = Quaternion.Euler(0,0,angle);
    }

    void MineTarget()
    {
        miningTimer -= Time.deltaTime;
        if (miningTimer > 0f) return;
        miningTimer = 0.18f;

        Block best = null;
        float d = 999f;
        foreach (var b in blocks)
        {
            if (b.go == null) continue;
            float dist = Vector2.Distance(ship.position, b.go.transform.position);
            if (dist < d && dist < 0.85f) { d = dist; best = b; }
        }
        if (best == null) return;

        best.hp -= 1f;
        best.sr.transform.localScale = Vector3.one * Cell * (0.88f + 0.12f * (best.hp / BlockHP));
        best.sr.color = Color.Lerp(best.sr.color, Color.white, 0.25f);

        SpawnBurst(best.go.transform.position, best.sr.color);

        if (best.hp <= 0f)
        {
            resources += 1 + (best.y % 7 == 0 ? 2 : 0);
            mined++;
            Destroy(best.go);
            best.go = null;
        }
    }

    void UpdateCamera()
    {
        Vector3 p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, ship.position.x, Time.deltaTime * 5f);
        p.y = Mathf.Lerp(p.y, ship.position.y + 1.1f, Time.deltaTime * 5f);
        cam.transform.position = new Vector3(p.x,p.y,-20);
    }

    void UpdateHUD()
    {
        progressText.text = "MINED " + mined + " / " + total;
        resourceText.text = "◆ " + resources;
    }

    void SpawnBurst(Vector3 pos, Color color)
    {
        for (int i=0;i<4;i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.12f);
            go.transform.localScale = Vector3.one * Random.Range(0.07f,0.14f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite();
            sr.color = new Color(color.r,color.g,color.b,1);
            sr.sortingOrder = 15;
            var fx = go.AddComponent<FadeParticle>();
            fx.velocity = Random.insideUnitCircle.normalized * Random.Range(1.1f,2.4f);
            fx.life = Random.Range(0.18f,0.38f);
        }
    }

    void SpawnTrailParticle()
    {
        var go = new GameObject("Trail");
        go.transform.position = ship.position + Vector3.back * 0.05f;
        go.transform.localScale = Vector3.one * Random.Range(0.05f,0.11f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquareSprite();
        sr.color = new Color(0.1f,0.85f,1f,0.45f);
        sr.sortingOrder = 18;
        var fx = go.AddComponent<FadeParticle>();
        fx.velocity = new Vector2(-velocity.x*0.12f,0.6f);
        fx.life = 0.22f;
    }

    void NewPlanet()
    {
        foreach (var b in blocks) if (b.go != null) Destroy(b.go);
        blocks.Clear();
        mined = 0;
        resources += 50;
        total = Width * Height;
        planetCleared = false;
        targetX = Width/2;
        targetY = 1;
        ship.position = GridPos(Width/2,1);
        BuildPlanet();
    }

    Vector3 GridPos(int x, int y)
    {
        return new Vector3((x - (Width-1)*0.5f)*Cell, 2.5f - y*Cell, 0);
    }

    Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(8,8,TextureFormat.RGBA32,false);
        tex.filterMode = FilterMode.Point;
        for(int y=0;y<8;y++) for(int x=0;x<8;x++) tex.SetPixel(x,y,Color.white);
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,8,8),new Vector2(.5f,.5f),8);
    }

    Sprite MakeShipSprite()
    {
        var tex = new Texture2D(32,48,TextureFormat.RGBA32,false);
        tex.filterMode = FilterMode.Point;
        for(int y=0;y<48;y++) for(int x=0;x<32;x++)
        {
            float dx=(x-15.5f)/13f, dy=(y-23.5f)/22f;
            bool body=dx*dx+dy*dy<1f;
            bool nose=y>33 && Mathf.Abs(x-15.5f)<(47-y)*0.28f;
            bool wing=(y>12&&y<30&&Mathf.Abs(x-15.5f)>8&&Mathf.Abs(x-15.5f)<15);
            tex.SetPixel(x,y,(body||nose||wing)?new Color(0.8f,0.95f,1f):Color.clear);
        }
        for(int y=18;y<29;y++) for(int x=10;x<22;x++) tex.SetPixel(x,y,new Color(0.05f,0.85f,1f));
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,32,48),new Vector2(.5f,.5f),32);
    }
}

public class FadeParticle : MonoBehaviour
{
    public Vector2 velocity;
    public float life = 0.3f;
    float age;
    SpriteRenderer sr;
    void Start(){ sr=GetComponent<SpriteRenderer>(); }
    void Update()
    {
        age += Time.deltaTime;
        transform.position += (Vector3)(velocity*Time.deltaTime);
        if(sr!=null) sr.color = new Color(sr.color.r,sr.color.g,sr.color.b,Mathf.Clamp01(1f-age/life));
        if(age>=life) Destroy(gameObject);
    }
}
