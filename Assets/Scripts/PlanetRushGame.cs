using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlanetRushGame : MonoBehaviour
{
    const int Width = 13;
    const int Height = 180;
    const float Cell = 0.66f;
    const float BlockHP = 4f;

    class Block
    {
        public GameObject go;
        public SpriteRenderer face;
        public SpriteRenderer shine;
        public float hp;
        public int x, y;
        public BlockType type;
    }

    enum BlockType { Normal, Gold, Energy, Explosive }

    readonly List<Block> blocks = new();
    readonly List<GameObject> fxObjects = new();

    Camera cam;
    Canvas canvas;
    Transform ship;
    SpriteRenderer shipBody;
    SpriteRenderer drill;
    SpriteRenderer engineGlow;
    Text progressText;
    Text planetText;
    Text resourceText;
    Text weaponText;
    GameObject panelOverlay;

    Sprite squareSprite;
    Sprite circleSprite;
    Sprite shipSprite;
    Sprite drillSprite;

    float miningTimer;
    float particleTimer;
    float weaponTimer;
    float bounceCooldown;
    float shake;
    float comboTimer;
    float planetBannerTimer;
    int combo;
    int mined;
    int total;
    int resources;
    int planet = 1;
    int weaponIndex;
    int miningUpgrade;
    int speedUpgrade;
    int engineUpgrade;
    readonly int[] weaponCosts = { 0, 100, 250, 500 };
    bool overlayOpen;

    Vector2 velocity = new(0.8f, -2.8f);
    int targetX = Width / 2;
    int targetY = 1;
    bool planetCleared;

    readonly string[] weapons = { "DRILL LANCE", "PULSE CANNON", "ARC BEAM", "BURST MORTAR" };

    void Awake()
    {
        Application.targetFrameRate = 60;
        squareSprite = MakeSquareSprite();
        circleSprite = MakeCircleSprite();
        shipSprite = MakeShipSprite();
        drillSprite = MakeDrillSprite();
        BuildCamera();
        BuildPlanet();
        BuildShip();
        BuildHUD();
    }

    void Update()
    {
        if (planetCleared)
        {
            planetBannerTimer -= Time.deltaTime;
            if (planetBannerTimer <= 0f) NewPlanet();
            return;
        }

        UpdateTarget();
        MoveShip();
        MineTarget();
        FireWeapon();
        UpdateCombo();
        UpdateCamera();
        UpdateHUD();
        UpdateFX();

        if (mined >= total)
        {
            planetCleared = true;
            planetBannerTimer = 1.25f;
            ShowPlanetClear();
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
        cam.orthographicSize = 5.65f;
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 2.5f, -20);
    }

    void BuildPlanet()
    {
        foreach (var b in blocks) if (b.go != null) Destroy(b.go);
        blocks.Clear();

        total = Width * Height;
        float specialBoost = Mathf.Min(0.06f, (planet - 1) * 0.008f);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var go = new GameObject("Block_" + x + "_" + y);
                go.transform.position = GridPos(x, y);
                go.transform.localScale = Vector3.one * (Cell * 0.96f);

                var face = go.AddComponent<SpriteRenderer>();
                face.sprite = squareSprite;

                int roll = Mathf.Abs((x * 92821 + y * 68917 + x * y * 31 + planet * 173) % 1000);
                BlockType type = roll < 12 + specialBoost * 100 ? BlockType.Explosive :
                                 roll < 32 + specialBoost * 100 ? BlockType.Gold :
                                 roll < 62 + specialBoost * 100 ? BlockType.Energy :
                                 BlockType.Normal;
                face.color = BlockColor(type, x, y);
                face.sortingOrder = 0;

                // A recessed inner face makes the grid read as physical blocks rather than flat pixels.
                var inset = new GameObject("Inset");
                inset.transform.SetParent(go.transform, false);
                inset.transform.localScale = Vector3.one * 0.78f;
                var insetRenderer = inset.AddComponent<SpriteRenderer>();
                insetRenderer.sprite = squareSprite;
                insetRenderer.color = Darker(face.color, 0.58f);
                insetRenderer.sortingOrder = 1;

                var shine = new GameObject("Shine").AddComponent<SpriteRenderer>();
                shine.transform.SetParent(go.transform, false);
                shine.transform.localPosition = new Vector3(-0.05f, 0.05f, 0);
                shine.transform.localScale = new Vector3(0.48f, 0.08f, 1);
                shine.sprite = squareSprite;
                shine.color = new Color(1, 1, 1, type == BlockType.Normal ? 0.10f : 0.22f);
                shine.sortingOrder = 2;

                blocks.Add(new Block { go = go, face = face, shine = shine, hp = BlockHP, x = x, y = y, type = type });
            }
        }
    }

    Color BlockColor(BlockType type, int x, int y)
    {
        if (type == BlockType.Gold) return new Color(1f, 0.62f, 0.08f);
        if (type == BlockType.Energy) return new Color(0.12f, 0.86f, 0.92f);
        if (type == BlockType.Explosive) return new Color(1f, 0.20f, 0.27f);

        float variation = ((x * 17 + y * 11) % 5) * 0.012f;
        return new Color(0.075f + variation, 0.32f + variation, 0.53f + variation);
    }

    Color Darker(Color c, float factor)
    {
        return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }

    void BuildShip()
    {
        var go = new GameObject("Autonomous Miner");
        ship = go.transform;
        ship.position = GridPos(Width / 2, 1);
        ship.localScale = Vector3.one * 1.12f;

        var glow = new GameObject("Engine Glow");
        glow.transform.SetParent(ship, false);
        glow.transform.localPosition = new Vector3(0, -0.55f, 0);
        glow.transform.localScale = new Vector3(0.9f, 1.5f, 1);
        engineGlow = glow.AddComponent<SpriteRenderer>();
        engineGlow.sprite = circleSprite;
        engineGlow.color = new Color(0.05f, 0.8f, 1f, 0.28f);
        engineGlow.sortingOrder = 15;

        shipBody = go.AddComponent<SpriteRenderer>();
        shipBody.sprite = shipSprite;
        shipBody.sortingOrder = 25;

        var drillGo = new GameObject("Drill");
        drillGo.transform.SetParent(ship, false);
        drillGo.transform.localPosition = new Vector3(0, -0.72f, 0);
        drillGo.transform.localScale = Vector3.one * 0.9f;
        drill = drillGo.AddComponent<SpriteRenderer>();
        drill.sprite = drillSprite;
        drill.sortingOrder = 26;
    }

    void BuildHUD()
    {
        var cgo = new GameObject("HUD");
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        cgo.AddComponent<GraphicRaycaster>();

        var top = MakePanel("TopBar", new Vector2(0.5f, 0.965f), new Vector2(940, 220), new Color(0.008f, 0.012f, 0.028f, 0.96f));
        planetText = MakeText(top.transform, "SECTOR 01   PLANET 01", 48, TextAnchor.UpperLeft, new Vector2(880, 65), new Vector2(-18, -10));
        progressText = MakeText(top.transform, "MINED 0 / 2340", 36, TextAnchor.MiddleLeft, new Vector2(590, 65), new Vector2(-18, -82));
        resourceText = MakeText(top.transform, "◆ 0", 38, TextAnchor.MiddleRight, new Vector2(260, 65), new Vector2(-18, -82));

        weaponText = MakeText(canvas.transform, "DRILL LANCE", 26, TextAnchor.MiddleCenter, new Vector2(420, 58), new Vector2(0, -810));
        weaponText.color = new Color(0.35f, 0.9f, 1f, 0.9f);

        MakeButton("GARAGE", new Vector2(0.22f, 0.075f), new Vector2(250, 92), () => ToggleOverlay("GARAGE"));
        MakeButton("ARMOURY", new Vector2(0.50f, 0.075f), new Vector2(250, 92), () => ToggleOverlay("ARMOURY"));
        MakeButton("UPGRADES", new Vector2(0.78f, 0.075f), new Vector2(250, 92), () => ToggleOverlay("UPGRADES"));

        var hint = MakeText(canvas.transform, "AUTONOMOUS MINER  •  LIVE", 22, TextAnchor.MiddleCenter, new Vector2(700, 45), new Vector2(0, -890));
        hint.color = new Color(0.35f, 0.65f, 0.8f, 0.65f);
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = dims;
        rt.anchoredPosition = pos;
        return t;
    }

    void MakeButton(string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = MakePanel(label, anchor, size, new Color(0.025f, 0.055f, 0.09f, 0.94f));
        var button = go.AddComponent<Button>();
        button.onClick.AddListener(action);
        var txt = MakeText(go.transform, label, 25, TextAnchor.MiddleCenter, size, Vector2.zero);
        txt.color = new Color(0.65f, 0.9f, 1f);
    }

    void ToggleOverlay(string title)
    {
        if (panelOverlay != null) Destroy(panelOverlay);

        panelOverlay = MakePanel("Overlay", new Vector2(0.5f, 0.53f), new Vector2(860, 920), new Color(0.008f, 0.015f, 0.035f, 0.985f));
        overlayOpen = true;

        MakeText(panelOverlay.transform, title, 46, TextAnchor.MiddleCenter, new Vector2(760, 70), new Vector2(0, 370));

        string body = title == "GARAGE" ?
            "HULL     MK I\n\nENGINE   MK I\n\nREACTOR  MK I\n\nEach module increases the miner's performance." :
            title == "ARMOURY" ?
            "WEAPONS\n\nDRILL LANCE     EQUIPPED\nPULSE CANNON    LOCKED\nARC BEAM        LOCKED\nBURST MORTAR    LOCKED" :
            "PERMANENT UPGRADES\n\nMINING POWER     +0\nMINING SPEED     +0\nENGINE POWER     +0\nRESOURCE BONUS   +0";

        var info = MakeText(panelOverlay.transform, body, 28, TextAnchor.UpperLeft, new Vector2(700, 580), new Vector2(0, 30));
        info.lineSpacing = 1.15f;
        info.color = new Color(0.75f, 0.88f, 0.96f);

        var close = MakePanel("Close", new Vector2(0.5f, 0.14f), new Vector2(300, 76), new Color(0.05f, 0.2f, 0.28f, 1f));
        close.transform.SetParent(panelOverlay.transform, false);
        var cb = close.AddComponent<Button>();
        cb.onClick.AddListener(() => { Destroy(panelOverlay); panelOverlay = null; overlayOpen = false; });
        MakeText(close.transform, "CLOSE", 25, TextAnchor.MiddleCenter, new Vector2(300, 76), Vector2.zero);
    }

    void MakeMenuButton(Transform parent, string label, float y, bool interactable, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("MenuButton");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = interactable ? new Color(0.04f, 0.14f, 0.19f, 1f) : new Color(0.025f, 0.045f, 0.065f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(690, 72);
        rt.anchoredPosition = new Vector2(0, y);
        var button = go.AddComponent<Button>();
        button.interactable = interactable;
        button.onClick.AddListener(action);
        var txt = MakeText(go.transform, label, 23, TextAnchor.MiddleCenter, new Vector2(680, 70), Vector2.zero);
        txt.color = interactable ? new Color(0.7f, 0.94f, 1f) : new Color(0.45f, 0.55f, 0.6f);
    }

    void BuyWeapon(int index)
    {
        if (index <= weaponIndex) { weaponIndex = index; return; }
        int cost = weaponCosts[index];
        if (resources < cost) return;
        resources -= cost;
        weaponIndex = index;
        CloseOverlay();
    }

    void BuyUpgrade(int type)
    {
        int cost = type == 0 ? 75 : 100;
        if (resources < cost) return;
        resources -= cost;
        if (type == 0) miningUpgrade++;
        else if (type == 1) speedUpgrade++;
        else engineUpgrade++;
        CloseOverlay();
    }

    void CloseOverlay()
    {
        if (panelOverlay != null) Destroy(panelOverlay);
        panelOverlay = null;
        overlayOpen = false;
    }

    void UpdateTarget()
    {
        int currentY = Mathf.Clamp(Mathf.RoundToInt((2.5f - ship.position.y) / Cell), 0, Height - 1);
        int desiredY = Mathf.Min(Height - 1, currentY + 1);
        Block best = null;
        float bestScore = float.MaxValue;

        foreach (var b in blocks)
        {
            if (b.go == null || b.y < desiredY || b.y > desiredY + 6) continue;
            float dy = b.y - desiredY;
            float dx = Mathf.Abs(b.x - Mathf.RoundToInt(ship.position.x / Cell + Width * 0.5f));
            float special = b.type == BlockType.Gold || b.type == BlockType.Energy ? -0.35f : 0;
            float score = dy * 1.5f + dx + special;
            if (score < bestScore) { bestScore = score; best = b; }
        }

        if (best != null) { targetX = best.x; targetY = best.y; }
    }

    void MoveShip()
    {
        Vector3 target = GridPos(targetX, targetY);
        Vector2 delta = target - ship.position;
        float horizontal = Mathf.Clamp(delta.x * 3.5f, -3.5f, 3.5f);

        velocity.x = Mathf.Lerp(velocity.x, horizontal, Time.deltaTime * 4.5f);
        velocity.y = Mathf.Lerp(velocity.y, -(3.15f + engineUpgrade * 0.18f), Time.deltaTime * 2.5f);
        ship.position += (Vector3)(velocity * Time.deltaTime);

        float left = -Width * Cell * 0.5f + Cell * 0.52f;
        float right = Width * Cell * 0.5f - Cell * 0.52f;

        if (ship.position.x < left)
        {
            ship.position = new Vector3(left, ship.position.y, ship.position.z);
            velocity.x = Mathf.Abs(velocity.x) * 1.16f;
            SpawnImpact(ship.position, new Color(0.2f, 0.8f, 1f));
        }
        else if (ship.position.x > right)
        {
            ship.position = new Vector3(right, ship.position.y, ship.position.z);
            velocity.x = -Mathf.Abs(velocity.x) * 1.16f;
            SpawnImpact(ship.position, new Color(0.2f, 0.8f, 1f));
        }

        ship.rotation = Quaternion.Euler(0, 0, Mathf.Clamp(-velocity.x * 6f, -20f, 20f));
        drill.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 22f) * 7f);
        engineGlow.transform.localScale = new Vector3(0.8f + Mathf.Abs(velocity.y) * 0.10f, 1.0f + Mathf.Abs(velocity.y) * 0.14f, 1);
    }

    void MineTarget()
    {
        miningTimer -= Time.deltaTime;
        bounceCooldown -= Time.deltaTime;
        if (miningTimer > 0f) return;
        miningTimer = Mathf.Max(0.07f, 0.15f - speedUpgrade * 0.012f);

        Block best = null;
        float distance = 999f;
        foreach (var b in blocks)
        {
            if (b.go == null) continue;
            float d = Vector2.Distance(ship.position, b.go.transform.position);
            if (d < distance && d < 0.86f) { distance = d; best = b; }
        }
        if (best == null) return;

        best.hp -= 1f;
        float ratio = Mathf.Clamp01(best.hp / BlockHP);
        best.go.transform.localScale = Vector3.one * Cell * (0.72f + ratio * 0.24f);
        best.face.color = Color.Lerp(Color.white, BlockColor(best.type, best.x, best.y), ratio);
        best.shine.color = new Color(1, 1, 1, 0.35f * (1f - ratio));
        SpawnBurst(best.go.transform.position, BlockColor(best.type, best.x, best.y), 5);
        shake = Mathf.Max(shake, 0.035f);

        if (best.hp <= 0f) DestroyBlock(best, true);
    }

    void DestroyBlock(Block b, bool reward)
    {
        combo++;
        comboTimer = 1.25f;

        if (reward)
        {
            int value = (b.type == BlockType.Gold ? 7 : b.type == BlockType.Energy ? 3 : 1) + miningUpgrade;
            if (b.y % 7 == 0) value += 2;
            resources += value;
        }

        mined++;
        Vector3 pos = b.go.transform.position;
        Color c = BlockColor(b.type, b.x, b.y);
        bool explosive = b.type == BlockType.Explosive;

        SpawnBurst(pos, c, explosive ? 18 : 8);
        Destroy(b.go);
        b.go = null;

        if (explosive) TriggerExplosion(b, pos, c);

        if (bounceCooldown <= 0f)
        {
            velocity.y = Mathf.Abs(velocity.y) * (explosive ? 1.05f : 0.84f);
            velocity.x += Random.Range(-0.75f, 0.75f);
            bounceCooldown = 0.11f;
        }
    }

    void TriggerExplosion(Block center, Vector3 pos, Color color)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (b.go == null || b == center) continue;
            if (Mathf.Abs(b.x - center.x) <= 1 && Mathf.Abs(b.y - center.y) <= 1)
            {
                SpawnBurst(b.go.transform.position, color, 7);
                resources += b.type == BlockType.Gold ? 6 : b.type == BlockType.Energy ? 3 : 1;
                mined++;
                Destroy(b.go);
                b.go = null;
            }
        }
        shake = Mathf.Max(shake, 0.18f);
    }

    void FireWeapon()
    {
        weaponTimer -= Time.deltaTime;
        if (weaponTimer > 0f) return;
        weaponTimer = weaponIndex == 0 ? 0.72f : 0.9f;

        Block target = null;
        float nearest = 2.2f;
        foreach (var b in blocks)
        {
            if (b.go == null) continue;
            float d = Vector2.Distance(ship.position, b.go.transform.position);
            if (d < nearest) { nearest = d; target = b; }
        }
        if (target == null) return;

        Vector3 p = target.go.transform.position;
        SpawnBeam(ship.position, p, weaponIndex);
        target.hp -= weaponIndex == 0 ? 1f : 0.6f;
        if (target.hp <= 0f) DestroyBlock(target, true);
    }

    void SpawnBeam(Vector3 a, Vector3 b, int mode)
    {
        var go = new GameObject("Weapon FX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.sortingOrder = 22;
        float length = Vector2.Distance(a, b);
        go.transform.position = (a + b) * 0.5f + Vector3.back * 0.02f;
        go.transform.localScale = new Vector3(length, mode == 0 ? 0.07f : 0.12f, 1);
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        sr.color = mode == 0 ? new Color(0.2f, 0.95f, 1f, 0.85f) : new Color(0.85f, 0.35f, 1f, 0.85f);
        fxObjects.Add(go);
        Destroy(go, 0.08f);
    }

    void UpdateCombo()
    {
        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f) combo = 0;
    }

    void UpdateCamera()
    {
        Vector3 p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, ship.position.x, Time.deltaTime * 7f);
        p.y = Mathf.Lerp(p.y, ship.position.y + 1.15f, Time.deltaTime * 7f);

        if (shake > 0f)
        {
            p += (Vector3)(Random.insideUnitCircle * shake);
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.8f);
        }

        cam.transform.position = new Vector3(p.x, p.y, -20);
    }

    void UpdateHUD()
    {
        progressText.text = "MINED " + mined + " / " + total;
        planetText.text = "SECTOR " + planet.ToString("00") + "   PLANET " + planet.ToString("00");
        resourceText.text = "◆ " + resources + (combo > 1 ? "   x" + combo : "");
        weaponText.text = weapons[weaponIndex] + "   •   AUTO FIRE";
    }

    void UpdateFX()
    {
        particleTimer -= Time.deltaTime;
        if (particleTimer <= 0f)
        {
            particleTimer = 0.06f;
            SpawnTrailParticle();
        }
    }

    void SpawnBurst(Vector3 pos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.10f);
            go.transform.localScale = Vector3.one * Random.Range(0.035f, 0.105f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.color = color;
            sr.sortingOrder = 30;
            var fx = go.AddComponent<FadeParticle>();
            fx.velocity = Random.insideUnitCircle.normalized * Random.Range(1.2f, 3.0f);
            fx.life = Random.Range(0.16f, 0.42f);
        }
    }

    void SpawnImpact(Vector3 pos, Color color)
    {
        SpawnBurst(pos, color, 7);
        shake = Mathf.Max(shake, 0.06f);
    }

    void SpawnTrailParticle()
    {
        var go = new GameObject("Engine Trail");
        go.transform.position = ship.position + new Vector3(Random.Range(-0.12f, 0.12f), 0.45f, 0.05f);
        go.transform.localScale = Vector3.one * Random.Range(0.04f, 0.09f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = new Color(0.15f, 0.82f, 1f, 0.55f);
        sr.sortingOrder = 20;
        var fx = go.AddComponent<FadeParticle>();
        fx.velocity = new Vector2(-velocity.x * 0.15f, 0.9f);
        fx.life = 0.24f;
    }

    void ShowPlanetClear()
    {
        var banner = MakePanel("ClearBanner", new Vector2(0.5f, 0.52f), new Vector2(780, 250), new Color(0.01f, 0.04f, 0.08f, 0.96f));
        MakeText(banner.transform, "PLANET CLEARED", 54, TextAnchor.MiddleCenter, new Vector2(740, 80), new Vector2(0, 45));
        var sub = MakeText(banner.transform, "NEXT PLANET INCOMING", 24, TextAnchor.MiddleCenter, new Vector2(740, 60), new Vector2(0, -35));
        sub.color = new Color(0.35f, 0.9f, 1f);
        Destroy(banner, 1.2f);
    }

    void NewPlanet()
    {
        planet++;
        mined = 0;
        resources += 50;
        planetCleared = false;
        targetX = Width / 2;
        targetY = 1;
        velocity = new Vector2(Random.Range(-1.2f, 1.2f), -3f);
        ship.position = GridPos(Width / 2, 1);
        BuildPlanet();
    }

    Vector3 GridPos(int x, int y)
    {
        return new Vector3((x - (Width - 1) * 0.5f) * Cell, 2.5f - y * Cell, 0);
    }

    Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f), 8);
    }

    Sprite MakeCircleSprite()
    {
        var tex = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++)
        {
            float dx = x - 11.5f, dy = y - 11.5f;
            tex.SetPixel(x, y, dx * dx + dy * dy < 132 ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(.5f, .5f), 24);
    }

    Sprite MakeShipSprite()
    {
        var tex = new Texture2D(48, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 48; x++)
        {
            float dx = (x - 23.5f) / 17f, dy = (y - 31f) / 27f;
            bool body = dx * dx + dy * dy < 1f;
            bool wing = y > 18 && y < 42 && Mathf.Abs(x - 23.5f) > 10 && Mathf.Abs(x - 23.5f) < 22;
            bool nose = y > 47 && Mathf.Abs(x - 23.5f) < (64 - y) * 0.42f;
            tex.SetPixel(x, y, (body || wing || nose) ? new Color(0.78f, 0.92f, 0.98f) : Color.clear);
        }
        for (int y = 24; y < 40; y++) for (int x = 15; x < 33; x++)
            tex.SetPixel(x, y, new Color(0.04f, 0.58f, 0.76f));
        for (int y = 27; y < 34; y++) for (int x = 18; x < 30; x++)
            tex.SetPixel(x, y, new Color(0.08f, 0.95f, 1f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 48, 64), new Vector2(.5f, .5f), 32);
    }

    Sprite MakeDrillSprite()
    {
        var tex = new Texture2D(18, 30, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 30; y++) for (int x = 0; x < 18; x++)
        {
            float width = 2f + y * 0.20f;
            bool on = Mathf.Abs(x - 8.5f) < width;
            tex.SetPixel(x, y, on ? new Color(0.55f, 0.9f, 1f) : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 18, 30), new Vector2(.5f, .9f), 20);
    }
}

public class FadeParticle : MonoBehaviour
{
    public Vector2 velocity;
    public float life = 0.3f;
    float age;
    SpriteRenderer sr;

    void Start() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        if (sr != null)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Clamp01(1f - age / life));
        if (age >= life) Destroy(gameObject);
    }
}
