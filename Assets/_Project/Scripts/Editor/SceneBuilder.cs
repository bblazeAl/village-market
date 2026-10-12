using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace IdleArcade.EditorTools
{
    /// <summary>
    /// Builds the Game and Boot scenes from code. Layout: customers live on the south ("front of house"),
    /// the player works on the north side of every stand; production (orchard, bakery) is further north,
    /// the pie terrace is an east expansion behind a gate.
    /// </summary>
    public static class SceneBuilder
    {
        public const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        public const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        static AssetBuilder.Result A;
        static Transform world, decor, stations, pads, ground;
        static readonly List<PayPad> padList = new List<PayPad>();
        static readonly Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
        static int padOrder;

        [MenuItem("IdleArcade/Build/Everything (assets + scenes)")]
        public static void BuildEverything()
        {
            ProjectSetup.Configure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            A = AssetBuilder.BuildAll();
            BuildGameScene();
            BuildBootScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GameScenePath);
            Debug.Log("[Build] Everything built");
        }

        [MenuItem("IdleArcade/Build/Game Scene Only")]
        public static void BuildGameSceneOnly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            A = AssetBuilder.BuildAll();
            BuildGameScene();
            EditorSceneManager.OpenScene(GameScenePath);
        }

        // ================================================================== helpers
        static GameObject Node(string name, Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static Transform Zone(GameObject owner, string name, Vector3 worldPos, float radius, float yaw = 0f, Station station = null, bool marker = true)
        {
            var z = Node(name, owner.transform, Vector3.zero).transform;
            z.position = worldPos;
            z.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (marker)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(A.zonePrefab, owner.transform);
                m.transform.position = worldPos + Vector3.up * 0.02f;
                m.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                m.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
                var zm = m.GetComponent<ZoneMarker>();
                zm.station = station;
                zm.zoneOverride = z;
                zm.radiusOverride = radius;
            }
            return z;
        }

        static void Obstacle(GameObject root, Vector3 size, Vector3 center)
        {
            var o = root.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box;
            o.size = size;
            o.center = center;
            o.carving = true;
            o.carveOnlyStationary = false;
        }

        static void ObstacleFromBounds(GameObject root, float margin = 0.45f)
        {
            var b = SyntyLib.RenderBounds(root);
            var local = root.transform.InverseTransformPoint(b.center);
            var size = root.transform.InverseTransformVector(b.size);
            size = new Vector3(Mathf.Abs(size.x) + margin, Mathf.Min(Mathf.Abs(size.y), 2f), Mathf.Abs(size.z) + margin);
            Obstacle(root, size, new Vector3(local.x, size.y * 0.5f, local.z));
        }

        static ItemContainer Container(GameObject parent, string name, Vector3 localPos, ItemType accepts, int max, int cols, int rows, float sx, float sz, float layer)
        {
            var go = Node(name, parent.transform, localPos);
            var c = go.AddComponent<ItemContainer>();
            c.accepts = accepts; c.max = max; c.cols = cols; c.rows = rows; c.spacingX = sx; c.spacingZ = sz; c.layerHeight = layer;
            return c;
        }

        static Image ProgressRing(GameObject parent, Vector3 localPos, out GameObject rootGo)
        {
            var holder = Node("Progress", parent.transform, localPos);
            holder.AddComponent<Billboard>();
            var canvas = AssetBuilder.WorldCanvas(holder.transform, "Canvas", new Vector2(100, 100), 0.01f);
            AssetBuilder.Img(canvas.transform, "Bg", A.ui["circle"], new Color(0.1f, 0.07f, 0.05f, 0.65f), Vector2.zero, new Vector2(96, 96));
            var fill = AssetBuilder.Img(canvas.transform, "Fill", A.ui["circle"], new Color32(255, 206, 64, 255), Vector2.zero, new Vector2(80, 80));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillAmount = 0.3f;
            AssetBuilder.Img(canvas.transform, "Ring", A.ui["ring_thin"], Color.white, Vector2.zero, new Vector2(100, 100));
            rootGo = holder;
            return fill;
        }

        static PayPad Pad(string id, string titleKey, Vector3 pos, int cost, Sprite icon, string[] requires, GameObject[] reveals, int served = 0,
            PadAction action = PadAction.Build, HelperRole role = HelperRole.None, Transform post = null, GameObject[] hides = null, bool focus = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(A.padPrefab, pads);
            go.name = "Pad_" + id;
            go.transform.position = pos;
            var p = go.GetComponent<PayPad>();
            p.id = id;
            p.titleKey = titleKey;
            p.cost = cost;
            p.icon = icon;
            p.requires = requires;
            p.requiresServed = served;
            p.reveals = reveals ?? new GameObject[0];
            p.hides = hides ?? new GameObject[0];
            p.action = action;
            p.hireRole = role;
            p.hirePost = post;
            p.focusOnReveal = focus;
            p.order = padOrder++;
            if (p.iconImage) p.iconImage.sprite = icon;
            if (p.titleText) p.titleText.text = Loc.T(titleKey);
            if (p.priceText) p.priceText.text = cost.ToString();
            padList.Add(p);
            return p;
        }

        static GameObject Deco(string name, Vector3 pos, float yaw = 0f, float scale = 1f, bool colliders = true)
            => SyntyLib.Place(name, decor, pos, yaw, scale, colliders);

        /// <summary>Places a prefab so that its render-bounds centre (xz) lands on 'centre'.</summary>
        static GameObject DecoCentered(string name, Vector3 centre, float yaw = 0f, float scale = 1f)
        {
            var go = Deco(name, Vector3.zero, yaw, scale);
            if (go == null) return null;
            var b = SyntyLib.RenderBounds(go);
            go.transform.position += new Vector3(centre.x - b.center.x, centre.y - b.min.y, centre.z - b.center.z);
            return go;
        }

        static Mesh RoundedRectMesh(string name, Vector2 size, float radius, float tile, int seg = 8)
        {
            var pts = new List<Vector2>();
            Vector2 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, h.y));
            Vector2[] centres = { new Vector2(h.x - radius, h.y - radius), new Vector2(-h.x + radius, h.y - radius), new Vector2(-h.x + radius, -h.y + radius), new Vector2(h.x - radius, -h.y + radius) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { Vector2.zero };
            foreach (var p in pts) { verts.Add(new Vector3(p.x, 0f, p.y)); uvs.Add(p / tile); }
            var tris = new List<int>();
            for (int i = 0; i < pts.Count; i++) { tris.Add(0); tris.Add(1 + (i + 1) % pts.Count); tris.Add(1 + i); }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            string path = $"{AssetBuilder.MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Surface(string name, Vector3 centre, Vector2 size, float radius, Material mat, float tile, float yaw = 0f)
        {
            var go = Node(name, ground, centre, yaw);
            go.AddComponent<MeshFilter>().sharedMesh = RoundedRectMesh("Surf_" + name, size, radius, tile);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ================================================================== game scene
        static void BuildGameScene()
        {
            padList.Clear();
            roots.Clear();
            padOrder = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            world = new GameObject("World").transform;
            ground = Node("Ground", world, Vector3.zero).transform;
            decor = Node("Decor", world, Vector3.zero).transform;
            stations = Node("Stations", world, Vector3.zero).transform;
            pads = Node("Pads", null, Vector3.zero).transform;

            BuildLighting();
            BuildGround();
            BuildDecor();

            var cfg = A.config;

            // ----------------------------------------------------------------- checkout
            var checkoutRoot = Node("Checkout", stations, new Vector3(-5f, 0f, 0.5f));
            var counter = SyntyLib.Place("SM_Prop_Table_01_TableCloth_01", checkoutRoot.transform, Vector3.zero);
            var cashBox = SyntyLib.Place("SM_Prop_Chest_01", checkoutRoot.transform, new Vector3(0.75f, 0.79f, 0.1f), 180f, 0.7f, false);
            SyntyLib.Place("SM_Prop_Lantern_01", checkoutRoot.transform, new Vector3(-0.85f, 0.79f, 0.15f), 0f, 1f, false);
            SyntyLib.Place("SM_Prop_Sign_03", checkoutRoot.transform, new Vector3(0f, 0f, -0.75f), 0f, 0.9f, false);
            ObstacleFromBounds(counter);
            var checkout = checkoutRoot.AddComponent<Checkout>();
            checkout.body = counter.transform;
            checkout.radius = 0.95f;
            checkout.zone = Zone(checkoutRoot, "CashierZone", new Vector3(-5f, 0f, 1.85f), 0.95f, 180f, checkout);
            var qp = new[] { new Vector3(-5f, 0, -0.85f), new Vector3(-5f, 0, -1.75f), new Vector3(-5.8f, 0, -2.45f), new Vector3(-6.6f, 0, -3.1f), new Vector3(-7.4f, 0, -3.75f) };
            checkout.queueSpots = qp.Select((p, i) => { var t = Node("Queue" + i, checkoutRoot.transform, Vector3.zero).transform; t.position = p; t.rotation = Quaternion.Euler(0, 0, 0); return t; }).ToArray();
            checkout.bagPoint = Node("BagPoint", checkoutRoot.transform, new Vector3(0f, 0.95f, 0f)).transform;
            var pileGo = Node("MoneyPile", checkoutRoot.transform, Vector3.zero);
            pileGo.transform.position = new Vector3(-3.15f, 0.02f, 1.85f);
            var pile = pileGo.AddComponent<MoneyPile>();
            pile.zone = pileGo.transform;
            pile.radius = 0.95f;
            checkout.pile = pile;
            var ring = ProgressRing(checkoutRoot, new Vector3(0f, 2.1f, 0.6f), out var ringRoot);
            checkout.progress = ring;
            checkout.progressRoot = ringRoot;
            roots["counter"] = checkoutRoot;

            // ----------------------------------------------------------------- shelves
            Shelf MakeShelf(string id, string table, Vector3 pos, ItemType type, string[] deco)
            {
                var root = Node("Shelf_" + id, stations, pos);
                var t = SyntyLib.Place(table, root.transform, Vector3.zero);
                ObstacleFromBounds(t);
                foreach (var d in deco)
                {
                    var parts = d.Split('@');
                    var xyz = parts[1].Split(',').Select(float.Parse).ToArray();
                    SyntyLib.Place(parts[0], root.transform, new Vector3(xyz[0], xyz[1], xyz[2]), xyz.Length > 3 ? xyz[3] : 0f, xyz.Length > 4 ? xyz[4] : 1f, false);
                }
                var sh = root.AddComponent<Shelf>();
                sh.type = type;
                sh.body = t.transform;
                sh.radius = 0.95f;
                sh.container = Container(root, "Display", new Vector3(0f, 0.8f, 0.02f), type, 12, 6, 2, 0.3f, 0.4f, type.stackHeight * 0.95f);
                sh.zone = Zone(root, "PlayerZone", pos + new Vector3(0f, 0f, 1.3f), 0.95f, 180f, sh);
                sh.customerSpots = new[]
                {
                    Node("Spot0", root.transform, new Vector3(-0.6f, 0f, -1.25f)).transform,
                    Node("Spot1", root.transform, new Vector3(0.6f, 0f, -1.25f)).transform,
                };
                roots[id] = root;
                return sh;
            }

            var appleStand = MakeShelf("apple_stand", "SM_Prop_Table_01", new Vector3(-0.5f, 0f, 0.5f), cfg.apple,
                new[] { "SM_Prop_Basket_03@-1.45,0,0.25,30,0.8", "SM_Prop_Sign_05@1.3,0,-0.45,-15,0.7" });
            var breadShelf = MakeShelf("bread_shelf", "SM_Prop_Table_01_TableCloth_02", new Vector3(3.2f, 0f, 0.5f), cfg.bread,
                new[] { "SM_Prop_Sack_02@1.45,0,0.3,0,0.8", "SM_Prop_Food_Flour_01@1.3,0,-0.4,0,1" });
            var pieShelf = MakeShelf("pie_shelf", "SM_Prop_Table_01_TableCloth_01", new Vector3(10.4f, 0f, 0.5f), cfg.pie,
                new[] { "SM_Prop_Cake_Stand_01@1.4,0,0.25,0,1.2" });

            // ----------------------------------------------------------------- orchard
            AppleTree MakeTree(string id, Vector3 pos, string model, float yaw)
            {
                var root = Node("Tree_" + id, stations, pos);
                var tree = SyntyLib.Place(model, root.transform, Vector3.zero, yaw, 0.7f, false);
                // remove canopy collider from Synty and use a trunk-only one
                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.radius = 0.35f; trunk.height = 2.5f; trunk.center = new Vector3(0, 1.25f, 0);
                Obstacle(root, new Vector3(0.9f, 2f, 0.9f), new Vector3(0, 1f, 0));
                var at = root.AddComponent<AppleTree>();
                at.item = cfg.apple;
                at.shakeTarget = tree.transform;
                at.radius = 1.1f;
                at.zone = Zone(root, "PickZone", pos + new Vector3(0f, 0f, -1.55f), 1.0f, 0f, at);
                // apples hang on the camera-facing half of the canopy
                var b = SyntyLib.RenderBounds(tree);
                float cy = b.center.y + b.extents.y * 0.12f;
                var slots = new List<Transform>();
                int n = 7;
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(-70f, 70f, i / (float)(n - 1)) * Mathf.Deg2Rad;
                    float yOff = (i % 2 == 0 ? -0.35f : 0.25f) + (i == n / 2 ? 0.4f : 0f);
                    var p = new Vector3(Mathf.Sin(a) * b.extents.x * 0.78f, 0f, -Mathf.Cos(a) * b.extents.z * 0.78f);
                    var s = Node("Slot" + i, root.transform, Vector3.zero).transform;
                    s.position = new Vector3(b.center.x + p.x, cy + yOff, b.center.z + p.z);
                    slots.Add(s);
                }
                at.slots = slots.ToArray();
                roots[id] = root;
                return at;
            }

            var tree1 = MakeTree("tree1", new Vector3(0.6f, 0f, 6.4f), "SM_Env_Tree_Fruit_02", 0f);
            MakeTree("tree2", new Vector3(4.0f, 0f, 6.4f), "SM_Env_Tree_Fruit_01", 40f);
            MakeTree("tree3", new Vector3(0.6f, 0f, 9.9f), "SM_Env_Tree_Fruit_03", 120f);
            MakeTree("tree4", new Vector3(4.0f, 0f, 9.9f), "SM_Env_Tree_Fruit_02", 200f);

            // ----------------------------------------------------------------- bakery
            Oven MakeOven(string id, Vector3 pos)
            {
                var root = Node("Oven_" + id, stations, pos);
                var body = SyntyLib.Place("SM_Prop_Oven_02", root.transform, new Vector3(0f, 0f, 1.25f), 180f);
                var table = SyntyLib.Place("SM_Prop_Table_03", root.transform, Vector3.zero);
                SyntyLib.Place("SM_Prop_Sack_01", root.transform, new Vector3(1.25f, 0f, 1.0f), 25f, 0.9f, false);
                ObstacleFromBounds(body);
                ObstacleFromBounds(table);
                var oven = root.AddComponent<Oven>();
                oven.output = cfg.bread;
                oven.body = body.transform;
                oven.mouth = Node("Mouth", root.transform, new Vector3(0f, 0.75f, 0.5f)).transform;
                oven.tray = Container(root, "Tray", new Vector3(0f, 0.78f, 0f), cfg.bread, 8, 2, 2, 0.42f, 0.42f, 0.2f);
                oven.radius = 0.95f;
                oven.zone = Zone(root, "TakeZone", pos + new Vector3(0f, 0f, -1.15f), 0.95f, 180f, oven);
                oven.progress = ProgressRing(root, new Vector3(0f, 2.2f, 1.2f), out _);
                roots[id] = root;
                return oven;
            }
            MakeOven("oven", new Vector3(-5.6f, 0f, 5.3f));
            MakeOven("oven2", new Vector3(-8.8f, 0f, 5.3f));

            // ----------------------------------------------------------------- boards
            GameObject Board(string id, string model, Vector3 pos, PanelKind kind, Sprite badge)
            {
                var root = Node("Board_" + id, stations, pos, 90f);
                var b = SyntyLib.Place(model, root.transform, Vector3.zero);
                ObstacleFromBounds(b);
                var z = root.AddComponent<PanelZone>();
                z.panel = kind;
                z.body = b.transform;
                z.radius = 0.95f;
                z.zone = Zone(root, "Mat", pos + new Vector3(1.35f, 0f, 0f), 0.95f, 90f);
                var sign = Node("Badge", root.transform, new Vector3(0f, 2.4f, 0f));
                sign.AddComponent<Billboard>();
                var c = AssetBuilder.WorldCanvas(sign.transform, "Canvas", new Vector2(120, 120), 0.01f);
                AssetBuilder.Img(c.transform, "Bg", A.ui["circle"], Color.white, Vector2.zero, new Vector2(116, 116));
                AssetBuilder.Img(c.transform, "Icon", badge, Color.white, Vector2.zero, new Vector2(84, 84));
                roots[id] = root;
                return root;
            }
            Board("upgrades", "SM_Prop_Quest_Board_01", new Vector3(-9.6f, 0f, 1.2f), PanelKind.Upgrades, A.ui["upgrade_board"]);
            Board("helpers", "SM_Prop_Quest_Board_02", new Vector3(-9.6f, 0f, -1.9f), PanelKind.Helpers, A.ui["helper"]);

            // ----------------------------------------------------------------- terrace
            var terraceDeco = Node("TerraceDeco", stations, Vector3.zero);
            SyntyLib.Place("SM_Prop_Bunting_01", terraceDeco.transform, new Vector3(17f, 3.1f, 8.6f), 0f, 1f, false);
            SyntyLib.Place("SM_Prop_Lantern_02", terraceDeco.transform, new Vector3(16.6f, 2.6f, -4.8f), 0f, 1f, false);
            SyntyLib.Place("SM_Prop_Post_01", terraceDeco.transform, new Vector3(16.9f, 0f, -4.9f), 0f, 1f);
            SyntyLib.Place("SM_Prop_Planter_02", terraceDeco.transform, new Vector3(16.5f, 0f, 7.5f), 0f, 1f);
            SyntyLib.Place("SM_Prop_Planter_02", terraceDeco.transform, new Vector3(7.8f, 0f, 7.8f), 0f, 1f);
            roots["terrace"] = terraceDeco;

            // pie oven (converter): input table left, oven back, output table right
            var pieRoot = Node("PieOven", stations, new Vector3(12.2f, 0f, 6.2f));
            var pieBody = SyntyLib.Place("SM_Prop_Oven_03", pieRoot.transform, new Vector3(0f, 0f, 1.35f), 180f);
            var inTable = SyntyLib.Place("SM_Prop_Table_03", pieRoot.transform, new Vector3(-1.5f, 0f, 0f));
            var outTable = SyntyLib.Place("SM_Prop_Table_03", pieRoot.transform, new Vector3(1.5f, 0f, 0f));
            ObstacleFromBounds(pieBody); ObstacleFromBounds(inTable); ObstacleFromBounds(outTable);
            var conv = pieRoot.AddComponent<Converter>();
            conv.input = cfg.apple;
            conv.output = cfg.pie;
            conv.body = pieBody.transform;
            conv.mouth = Node("Mouth", pieRoot.transform, new Vector3(0f, 0.8f, 0.6f)).transform;
            conv.inTray = Container(pieRoot, "InTray", new Vector3(-1.5f, 0.78f, 0f), cfg.apple, 8, 2, 2, 0.32f, 0.32f, 0.28f);
            conv.outTray = Container(pieRoot, "OutTray", new Vector3(1.5f, 0.78f, 0f), cfg.pie, 6, 2, 2, 0.42f, 0.42f, 0.16f);
            conv.radius = 0.9f;
            conv.zone = Zone(pieRoot, "InZone", pieRoot.transform.position + new Vector3(-1.5f, 0f, -1.15f), 0.9f, 180f, conv);
            conv.outRadius = 0.9f;
            conv.outZone = Zone(pieRoot, "OutZone", pieRoot.transform.position + new Vector3(1.5f, 0f, -1.15f), 0.9f, 180f, null);
            conv.progress = ProgressRing(pieRoot, new Vector3(0f, 2.4f, 1.3f), out var pieRing);
            conv.progressRoot = pieRing;
            roots["pie_oven"] = pieRoot;

            // picnic tables
            PicnicTable MakeTable(string id, Vector3 pos)
            {
                var root = Node("Table_" + id, stations, pos);
                var t = SyntyLib.Place("SM_Prop_Picnic_Table_01_TableCloth_01", root.transform, Vector3.zero);
                SyntyLib.Place("SM_Prop_Picnic_Bench_01", root.transform, new Vector3(0f, 0f, 0.78f));
                SyntyLib.Place("SM_Prop_Picnic_Bench_01", root.transform, new Vector3(0f, 0f, -0.78f), 180f);
                Obstacle(root, new Vector3(2.4f, 1f, 2.1f), new Vector3(0f, 0.5f, 0f));
                var pt = root.AddComponent<PicnicTable>();
                pt.seats = new[]
                {
                    Node("Seat0", root.transform, new Vector3(-0.5f, 0.02f, -0.72f), 0f).transform,
                    Node("Seat1", root.transform, new Vector3(0.5f, 0.02f, -0.72f), 0f).transform,
                    Node("Seat2", root.transform, new Vector3(-0.5f, 0.02f, 0.72f), 180f).transform,
                    Node("Seat3", root.transform, new Vector3(0.5f, 0.02f, 0.72f), 180f).transform,
                };
                pt.plateSpots = new[]
                {
                    Node("Plate0", root.transform, new Vector3(-0.5f, 0.75f, -0.2f)).transform,
                    Node("Plate1", root.transform, new Vector3(0.5f, 0.75f, -0.2f)).transform,
                    Node("Plate2", root.transform, new Vector3(-0.5f, 0.75f, 0.2f)).transform,
                    Node("Plate3", root.transform, new Vector3(0.5f, 0.75f, 0.2f)).transform,
                };
                var tip = Node("Tips", root.transform, new Vector3(1.55f, 0.02f, 0f));
                pt.tipPile = tip.AddComponent<MoneyPile>();
                pt.tipPile.zone = tip.transform;
                pt.tipPile.radius = 0.8f;
                pt.tipPile.cols = 2; pt.tipPile.rows = 2;
                pt.radius = 1.0f;
                pt.zone = Zone(root, "CleanZone", pos + new Vector3(-1.7f, 0f, 0f), 1.0f, 90f, pt);
                roots[id] = root;
                return pt;
            }
            MakeTable("table1", new Vector3(14.3f, 0f, 1.2f));
            MakeTable("table2", new Vector3(14.3f, 0f, -2.6f));

            var washRoot = Node("WashTub", stations, new Vector3(16.3f, 0f, 4.6f));
            var tub = SyntyLib.Place("SM_Prop_Barrel_Half_01", washRoot.transform, Vector3.zero, 0f, 1.35f);
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.name = "Water";
            water.transform.SetParent(washRoot.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            water.transform.localScale = new Vector3(0.95f, 0.01f, 0.95f);
            water.GetComponent<MeshRenderer>().sharedMaterial = AssetBuilder.LitMat("M_Water", null, new Color(0.45f, 0.75f, 0.95f), 1f, 0.85f);
            SyntyLib.Place("SM_Prop_Bucket_02", washRoot.transform, new Vector3(0.9f, 0f, 0.3f), 20f, 1f, false);
            ObstacleFromBounds(tub);
            var wash = washRoot.AddComponent<WashTub>();
            wash.body = tub.transform;
            wash.mouth = Node("Mouth", washRoot.transform, new Vector3(0f, 0.7f, 0f)).transform;
            wash.radius = 0.95f;
            wash.zone = Zone(washRoot, "DropZone", washRoot.transform.position + new Vector3(-1.35f, 0f, 0f), 0.95f, 90f, wash);
            roots["wash"] = washRoot;

            // compost bin for unwanted items (east edge of the square, away from every other zone)
            var binRoot = Node("CompostBin", stations, new Vector3(6.1f, 0f, -2.0f));
            var binBody = SyntyLib.Place("SM_Prop_Barrel_01", binRoot.transform, Vector3.zero, 0f, 1.1f);
            SyntyLib.Place("SM_Prop_Food_Leaf_01", binRoot.transform, new Vector3(0f, 1.0f, 0f), 30f, 2.5f, false);
            ObstacleFromBounds(binBody, 0.3f);
            var bin = binRoot.AddComponent<TrashBin>();
            bin.body = binBody.transform;
            bin.mouth = Node("Mouth", binRoot.transform, new Vector3(0f, 1.0f, 0f)).transform;
            bin.radius = 0.75f;
            bin.zone = Zone(binRoot, "BinZone", binRoot.transform.position + new Vector3(-1.15f, 0f, 0f), 0.75f, 90f, bin);
            var binBadge = Node("Badge", binRoot.transform, new Vector3(0f, 2.0f, 0f));
            binBadge.AddComponent<Billboard>();
            var binCanvas = AssetBuilder.WorldCanvas(binBadge.transform, "Canvas", new Vector2(110, 110), 0.01f);
            AssetBuilder.Img(binCanvas.transform, "Bg", A.ui["circle"], Color.white, Vector2.zero, new Vector2(106, 106));
            AssetBuilder.Img(binCanvas.transform, "Icon", A.ui["trash"], Color.white, Vector2.zero, new Vector2(76, 76));
            roots["bin"] = binRoot;

            // terrace gate (removed by the terrace pad)
            var gate = SyntyLib.Place("SM_Bld_Fence_Wood_Gate_01", world, new Vector3(7.0f, 0f, 2.2f), 90f, 1f);
            gate.name = "TerraceGate";
            var gateObs = gate.AddComponent<NavMeshObstacle>();
            gateObs.shape = NavMeshObstacleShape.Box;
            var gb = SyntyLib.RenderBounds(gate);
            gateObs.center = gate.transform.InverseTransformPoint(gb.center);
            gateObs.size = new Vector3(2.5f, 1.8f, 0.4f);
            gateObs.carving = true;

            // ----------------------------------------------------------------- pads (play order)
            var icons = BuildPadIcons();
            Pad("counter", "pad.counter", checkout.zone.position, 10, icons["counter"], new[] { "coins" }, new[] { checkoutRoot });
            Pad("apple_stand", "pad.apple_stand", appleStand.zone.position, 15, icons["apple_stand"], new[] { "counter" }, new[] { roots["apple_stand"] });
            Pad("tree1", "pad.tree", tree1.zone.position, 15, icons["tree"], new[] { "apple_stand" }, new[] { roots["tree1"] });
            Pad("tree2", "pad.tree", ((AppleTree)roots["tree2"].GetComponent<AppleTree>()).zone.position, 30, icons["tree"], new[] { "tree1" }, new[] { roots["tree2"] }, 1);
            Pad("tree3", "pad.tree", roots["tree3"].GetComponent<AppleTree>().zone.position, 45, icons["tree"], new[] { "tree2" }, new[] { roots["tree3"] });
            Pad("hire_cashier", "pad.hire_cashier", new Vector3(-2.4f, 0f, 3.6f), 90, A.ui["cashier"], new[] { "tree2" }, null, 4,
                PadAction.Hire, HelperRole.Cashier, checkout.zone);
            Pad("oven", "pad.oven", roots["oven"].GetComponent<Oven>().zone.position, 60, icons["oven"], new[] { "tree3" }, new[] { roots["oven"] });
            Pad("bread_shelf", "pad.bread_shelf", breadShelf.zone.position, 45, icons["bread_shelf"], new[] { "oven" }, new[] { roots["bread_shelf"], roots["bin"] });
            Pad("upgrades", "pad.upgrades", roots["upgrades"].GetComponent<PanelZone>().zone.position, 50, A.ui["upgrade_board"], new[] { "bread_shelf" }, new[] { roots["upgrades"] });
            Pad("tree4", "pad.tree", roots["tree4"].GetComponent<AppleTree>().zone.position, 75, icons["tree"], new[] { "hire_cashier", "tree3" }, new[] { roots["tree4"] });
            Pad("hire_picker", "pad.hire_picker", new Vector3(6.3f, 0f, 4.6f), 150, A.ui["helper"], new[] { "upgrades" }, null, 0,
                PadAction.Hire, HelperRole.ApplePicker, tree1.zone);
            Pad("helpers", "pad.helpers", roots["helpers"].GetComponent<PanelZone>().zone.position, 70, A.ui["helper"], new[] { "hire_picker" }, new[] { roots["helpers"] });
            Pad("terrace", "pad.terrace", new Vector3(5.6f, 0f, 2.2f), 180, icons["terrace"], new[] { "hire_picker", "bread_shelf" }, new[] { roots["terrace"] },
                0, PadAction.Build, HelperRole.None, null, new[] { gate });
            Pad("pie_oven", "pad.pie_oven", conv.zone.position, 100, icons["pie_oven"], new[] { "terrace" }, new[] { roots["pie_oven"] });
            Pad("pie_shelf", "pad.pie_shelf", pieShelf.zone.position, 80, icons["pie_shelf"], new[] { "pie_oven" }, new[] { roots["pie_shelf"] });
            Pad("table1", "pad.table", roots["table1"].GetComponent<PicnicTable>().zone.position, 60, icons["table"], new[] { "pie_shelf" }, new[] { roots["table1"] });
            Pad("wash", "pad.wash", wash.zone.position, 50, icons["wash"], new[] { "table1" }, new[] { roots["wash"] });
            Pad("table2", "pad.table", roots["table2"].GetComponent<PicnicTable>().zone.position, 70, icons["table"], new[] { "wash" }, new[] { roots["table2"] });
            Pad("hire_baker", "pad.hire_baker", new Vector3(-3.2f, 0f, 4.4f), 170, A.ui["helper"], new[] { "terrace", "helpers" }, null, 0,
                PadAction.Hire, HelperRole.Baker, roots["oven"].GetComponent<Oven>().zone);
            Pad("hire_pie_helper", "pad.hire_waiter", new Vector3(8.6f, 0f, 3.9f), 200, A.ui["helper"], new[] { "table2" }, null, 0,
                PadAction.Hire, HelperRole.PieHelper, conv.zone);
            Pad("oven2", "pad.oven2", roots["oven2"].GetComponent<Oven>().zone.position, 180, icons["oven"], new[] { "hire_baker" }, new[] { roots["oven2"] });

            // ----------------------------------------------------------------- start coins
            var coinsGo = Node("StartCoins", world, new Vector3(-2.4f, 0.02f, -2.0f));
            var startCoins = coinsGo.AddComponent<MoneyPile>();
            startCoins.zone = coinsGo.transform;
            startCoins.radius = 1.0f;
            startCoins.cols = 4; startCoins.rows = 3;

            // ----------------------------------------------------------------- player
            var spawn = Node("PlayerSpawn", world, new Vector3(0f, 0f, -3.6f), 180f);
            var player = BuildPlayer(spawn.transform.position);

            // ----------------------------------------------------------------- managers
            var gameRoot = new GameObject("Game");
            var game = gameRoot.AddComponent<Game>();
            game.startCoins = startCoins;
            game.playerSpawn = spawn.transform;
            game.player = player;
            game.checkout = checkout;
            var pm = gameRoot.AddComponent<ProgressionManager>();
            pm.pads = padList.ToArray();
            var cm = gameRoot.AddComponent<CustomerManager>();
            cm.customerBase = A.customerBase;
            cm.spawnPoints = new[]
            {
                Node("CustomerSpawnA", world, new Vector3(-25f, 0f, -4.6f), 90f).transform,
                Node("CustomerSpawnB", world, new Vector3(-25f, 0f, -3.2f), 90f).transform,
            };
            cm.spawnPoint = cm.spawnPoints[0];
            cm.exitPoint = Node("CustomerExit", world, new Vector3(-26f, 0f, -4f)).transform;
            cm.checkout = checkout;
            var hm = gameRoot.AddComponent<HelperManager>();
            hm.helperBase = A.helperBase;
            hm.entryPoint = Node("HelperEntry", world, new Vector3(-17f, 0f, -4f)).transform;
            var tut = gameRoot.AddComponent<Tutorial>();
            tut.startCoins = startCoins;
            tut.checkout = checkout;
            tut.firstTree = tree1;
            tut.appleStand = appleStand;
            var audioGo = new GameObject("Audio");
            audioGo.AddComponent<AudioManager>();

            // guide
            var guideGo = new GameObject("Guide");
            var guide = guideGo.AddComponent<Guide>();
            var arrow = (GameObject)PrefabUtility.InstantiatePrefab(A.config.guideArrowPrefab, guideGo.transform);
            arrow.transform.localScale = Vector3.one * 2.1f;
            guide.arrow = arrow.transform;
            var lineGo = new GameObject("PathLine");
            lineGo.transform.SetParent(guideGo.transform, false);
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.sharedMaterial = A.config.guideLineMaterial;
            lr.widthMultiplier = 0.32f;
            lr.textureMode = LineTextureMode.Tile;
            lr.alignment = LineAlignment.TransformZ;
            lineGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 0;
            guide.line = lr;

            // camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 140f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            var rig = camGo.AddComponent<CameraRig>();
            rig.target = player.transform;
            camGo.transform.position = player.transform.position + new Vector3(0, 19f, -15f);
            camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

            // UI
            var ui = new GameObject("UI");
            ui.AddComponent<Hud>();

            // world bounds for the player
            BuildBounds();

            // navmesh: bake with every unlockable hidden so their footprints stay walkable until built
            var toggles = new List<GameObject>(roots.Values) { gate };
            foreach (var g in toggles) g.SetActive(false);
            var surf = world.gameObject.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = ~0;
            surf.BuildNavMesh();
            string navPath = "Assets/_Project/Scenes/Game_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surf.navMeshData, navPath);
            surf.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            foreach (var g in toggles) g.SetActive(true);

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[Build] Game scene saved with {padList.Count} pads");
        }

        static Dictionary<string, Sprite> BuildPadIcons()
        {
            var d = new Dictionary<string, Sprite>();
            Sprite R(string key, string prefab, Vector3 euler)
            {
                var pf = SyntyLib.Find(prefab);
                var s = IconRenderer.Render(pf, "icon_pad_" + key, euler);
                d[key] = s;
                return s;
            }
            R("counter", "SM_Prop_Table_01_TableCloth_01", new Vector3(35, -35, 0));
            R("apple_stand", "SM_Prop_Basket_03", new Vector3(35, -35, 0));
            R("tree", "SM_Env_Tree_Fruit_02", new Vector3(20, -30, 0));
            R("oven", "SM_Prop_Oven_02", new Vector3(30, 150, 0));
            R("bread_shelf", "SM_Prop_Food_Bread_Loaf_02", new Vector3(35, -40, 0));
            R("terrace", "SM_Prop_Picnic_Umbrella_01", new Vector3(25, -35, 0));
            R("pie_oven", "SM_Prop_Oven_03", new Vector3(30, 150, 0));
            R("pie_shelf", "SM_Prop_Food_Pie_02", new Vector3(40, -30, 0));
            R("table", "SM_Prop_Picnic_Table_01_TableCloth_01", new Vector3(35, -35, 0));
            R("wash", "SM_Prop_Barrel_Half_01", new Vector3(40, -30, 0));
            return d;
        }

        static PlayerController BuildPlayer(Vector3 pos)
        {
            var root = new GameObject("Player");
            root.transform.position = pos;
            root.tag = "Player";
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.38f;
            cc.height = 1.4f;
            cc.center = new Vector3(0f, 0.72f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(A.config.playerModel, root.transform);
            model.name = "Model";
            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = A.config.animator;
            animator.applyRootMotion = false;
            var hands = new GameObject("Hands").transform;
            hands.SetParent(root.transform, false);
            hands.localPosition = new Vector3(0f, 0.68f, 0.4f);
            var stack = root.AddComponent<ItemStack>();
            stack.anchor = hands;
            var carrier = root.AddComponent<Carrier>();
            carrier.kind = CarrierKind.Player;
            carrier.stack = stack;
            carrier.transferInterval = A.config.balance.playerTransferInterval;
            var anim = root.AddComponent<CharacterAnim>();
            anim.animator = animator;
            anim.stack = stack;
            anim.runSpeed = 4.6f;
            var pc = root.AddComponent<PlayerController>();
            pc.carrier = carrier;
            pc.stack = stack;
            pc.anim = anim;
            // soft contact shadow so the hero reads on any floor
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.name = "BlobShadow";
            sh.transform.SetParent(root.transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = Vector3.one * 1.2f;
            var shr = sh.GetComponent<MeshRenderer>();
            shr.sharedMaterial = A.shadowMat;
            shr.shadowCastingMode = ShadowCastingMode.Off;
            return pc;
        }

        static void BuildBounds()
        {
            var b = Node("PlayerBounds", world, Vector3.zero);
            void Wall(Vector3 c, Vector3 s)
            {
                var w = Node("Wall", b.transform, c);
                var col = w.AddComponent<BoxCollider>();
                col.size = s;
                w.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
            // the bounds are not part of the navmesh bake (they sit outside every walk route)
            Wall(new Vector3(4f, 1f, 13.2f), new Vector3(34f, 2f, 0.5f));     // north
            Wall(new Vector3(4f, 1f, -7.6f), new Vector3(34f, 2f, 0.5f));     // south
            Wall(new Vector3(18.6f, 1f, 2.8f), new Vector3(0.5f, 2f, 21f));   // east
            Wall(new Vector3(-12.6f, 1f, 5.5f), new Vector3(0.5f, 2f, 15f));  // west (road gap below)
            Wall(new Vector3(-12.6f, 1f, -6.9f), new Vector3(0.5f, 2f, 1.4f));
        }

        // ================================================================== environment
        static void BuildLighting()
        {
            var lightGo = new GameObject("Sun");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.intensity = 1.35f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.62f;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.3f;
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            RenderSettings.sun = l;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.78f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.48f, 0.43f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.95f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 110f;
            RenderSettings.skybox = null;

            var vol = new GameObject("PostProcess");
            var v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            string profPath = "Assets/_Project/Generated/PostProfile.asset";
            AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, profPath);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            var tone = prof.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(14f);
            ca.contrast.Override(10f);
            ca.postExposure.Override(0.15f);
            var vig = prof.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.5f);
            var wb = prof.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            foreach (var c in prof.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, prof); }
            EditorUtility.SetDirty(prof);
            v.sharedProfile = prof;
        }

        static void BuildGround()
        {
            // base grass plane with collider (navmesh + player floor)
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Grass";
            plane.transform.SetParent(ground, false);
            plane.transform.localScale = new Vector3(16f, 1f, 16f);
            plane.transform.position = new Vector3(0f, 0f, 0f);
            var gm = new Material(A.groundGrass);
            plane.GetComponent<MeshRenderer>().sharedMaterial = AssetBuilder.LitMat("M_Ground_Grass_Big", A.groundGrass.GetTexture("_BaseMap") as Texture2D, A.groundGrass.GetColor("_BaseColor"), 26f);
            Object.DestroyImmediate(gm);

            Surface("Plaza", new Vector3(-1.2f, 0.01f, -0.9f), new Vector2(23f, 12.5f), 2.5f, A.groundPlaza, 3.2f);
            Surface("Road", new Vector3(-19f, 0.008f, -4.1f), new Vector2(18f, 3.6f), 1.6f, A.groundPath, 4.5f);
            Surface("Orchard", new Vector3(2.3f, 0.012f, 8.2f), new Vector2(7.4f, 7.6f), 1.8f, A.groundDirt, 5f);
            Surface("Bakery", new Vector3(-7.2f, 0.012f, 5.6f), new Vector2(7.6f, 4.6f), 1.4f, A.groundPlaza, 3.2f);
            Surface("Terrace", new Vector3(12.6f, 0.014f, 1.8f), new Vector2(10.6f, 14f), 2.2f, A.groundDeck, 3.6f);
            Surface("BoardsPad", new Vector3(-9.2f, 0.012f, -0.3f), new Vector2(3.4f, 5.6f), 1.2f, A.groundPlaza, 3.2f);
        }

        static void BuildDecor()
        {
            // village houses frame the market
            DecoCentered("Bld_Preset_House_01", new Vector3(-17f, 0f, 16f), 180f, 0.8f);
            DecoCentered("Bld_Preset_House_02", new Vector3(-5f, 0f, 18.5f), 180f, 0.8f);
            DecoCentered("Bld_Preset_House_03", new Vector3(8f, 0f, 18.5f), 180f, 0.8f);
            DecoCentered("Bld_Preset_Tavern_01", new Vector3(27f, 0f, 9f), -90f, 0.7f);
            DecoCentered("Bld_Preset_Shed_01", new Vector3(-17f, 0f, 5.5f), 90f, 0.85f);
            DecoCentered("Bld_Preset_Windmill_01", new Vector3(22f, 0f, 24f), 200f, 0.8f);
            DecoCentered("Bld_Preset_House_04", new Vector3(-27f, 0f, 14f), 160f, 0.8f);

            // big trees and hedges around the edges (nothing tall south of the square: the camera looks from there)
            var trees = new (string, Vector3, float)[]
            {
                ("SM_Env_Tree_04", new Vector3(-14.5f, 0, 9.5f), 1.0f), ("SM_Env_Tree_Meadow_02", new Vector3(-21f, 0, 2f), 0.75f),
                ("SM_Env_Tree_04", new Vector3(-16f, 0, -9.5f), 0.9f), ("SM_Env_Tree_Birch_01", new Vector3(-19f, 0, -12f), 0.55f),
                ("SM_Env_Tree_Meadow_02", new Vector3(22f, 0, -4f), 0.7f), ("SM_Env_Tree_04", new Vector3(21.5f, 0, 4.5f), 1.0f),
                ("SM_Env_Tree_Meadow_01", new Vector3(14f, 0, 15f), 0.45f), ("SM_Env_Tree_Birch_02", new Vector3(-11f, 0, 15f), 0.55f),
                ("SM_Env_Tree_04", new Vector3(24f, 0, 13f), 0.9f), ("SM_Env_Tree_Meadow_02", new Vector3(-27f, 0, 9f), 0.75f),
                ("SM_Env_Tree_04", new Vector3(19.5f, 0, -12.5f), 0.8f), ("SM_Env_Tree_Meadow_01", new Vector3(-29f, 0, -6f), 0.4f),
                ("SM_Env_Tree_Birch_01", new Vector3(27f, 0, -1f), 0.5f), ("SM_Env_Tree_04", new Vector3(1f, 0, 16f), 0.9f),
            };
            foreach (var (n, p, s) in trees)
            {
                var t = Deco(n, p, Random.Range(0f, 360f), s);
                if (t != null) foreach (var lg in t.GetComponentsInChildren<LODGroup>()) { lg.ForceLOD(-1); }
            }
            Deco("SM_Env_Hedge_01", new Vector3(-12.5f, 0, 11f), 0f, 0.6f);
            Deco("SM_Env_Hedge_02", new Vector3(19f, 0, 10f), 30f, 0.6f);
            Deco("SM_Env_Hedge_03", new Vector3(19.5f, 0, -8f), 70f, 0.6f);
            foreach (var bp in new[] { new Vector3(-13.6f, 0, -7.4f), new Vector3(18.8f, 0, -6.4f), new Vector3(-13.8f, 0, 12.2f), new Vector3(18.6f, 0, 12.6f), new Vector3(9f, 0, 13.4f) })
                Deco("SM_Env_Bush_02", bp, Random.Range(0f, 360f), 0.45f);

            // fences: behind production and along the terrace edge (gate gap at z≈2)
            for (int i = 0; i < 8; i++) Deco("SM_Bld_Fence_Wood_01", new Vector3(-11f + i * 2.6f, 0, 12.6f), 0f);
            for (int i = 0; i < 2; i++) Deco("SM_Bld_Fence_Wood_01", new Vector3(7.0f, 0, 6.2f + i * 2.6f), 90f);
            Deco("SM_Bld_Fence_Wood_01", new Vector3(7.0f, 0, 3.4f), 90f);
            Deco("SM_Bld_Fence_Wood_01", new Vector3(7.0f, 0, -0.4f), 90f);
            Deco("SM_Bld_Fence_Wood_01", new Vector3(7.0f, 0, -3.0f), 90f);

            // neighbouring merchants frame the square: colourful stalls (decor only) with a few idle vendors
            var stalls = new (Vector3 pos, float yaw, int v)[]
            {
                (new Vector3(-16.6f, 0, 0.6f), 90f, 2), (new Vector3(-19.5f, 0, -9.2f), 45f, 5),
                (new Vector3(-7.2f, 0, -10.4f), 180f, 3), (new Vector3(1.6f, 0, -10.6f), 180f, 6), (new Vector3(10.2f, 0, -10.4f), 180f, 1),
                (new Vector3(21.2f, 0, -0.6f), -90f, 9), (new Vector3(21.4f, 0, 7.4f), -90f, 7),
            };
            foreach (var st in stalls) DecoCentered("SM_Prop_Market_Stall_0" + st.v, st.pos, st.yaw);
            Deco("SM_Veh_TraderWagon_01", new Vector3(-17.2f, 0f, -2.2f), 70f, 0.9f);
            var vendors = new (string model, Vector3 pos, float yaw, float mood)[]
            {
                ("SM_Chr_Bunny_Female_01", new Vector3(-14.9f, 0f, 2.4f), 110f, 2f),
                ("SM_Chr_Pig_Male_01", new Vector3(-15.4f, 0f, -7.1f), 40f, 0f),
                ("SM_Chr_Frog_Male_01", new Vector3(19.6f, 0f, 1.4f), -100f, 2f),
            };
            foreach (var vd in vendors)
            {
                var npc = Deco(vd.model, vd.pos, vd.yaw, 1f, false);
                if (npc == null) continue;
                var an = npc.GetComponentInChildren<Animator>();
                an.runtimeAnimatorController = A.config.animator;
                an.applyRootMotion = false;
                var amb = npc.AddComponent<AmbientNpc>();
                amb.animator = an;
                amb.mood = vd.mood;
            }

            // market entrance arch + bunting over the road
            Deco("SM_Prop_Archway_01", new Vector3(-12.2f, 0f, -4.1f), 90f, 1f, false);
            Deco("SM_Prop_Bunting_01", new Vector3(-1f, 2.6f, 12.3f), 0f, 1f, false);
            Deco("SM_Prop_Bunting_02", new Vector3(-11f, 2.6f, 12.3f), 0f, 1f, false);
            Deco("SM_Prop_Bunting_01", new Vector3(-13.2f, 3.2f, -6.4f), 90f, 0.45f, false);

            // fountain anchors the south of the square
            Deco("SM_Env_Wild_Flower_Grp_04", new Vector3(8.5f, 0f, -9.0f), 0f, 0.9f, false);
            Deco("SM_Env_Wild_Flower_Grp_01", new Vector3(-4.5f, 0f, -9.2f), 40f, 0.9f, false);
            Deco("SM_Prop_Well_01", new Vector3(-9.8f, 0f, 9.4f), 30f, 0.85f);

            // bakery & orchard props
            Deco("SM_Prop_Wood_Pile_01", new Vector3(-11f, 0f, 7.6f), 90f);
            Deco("SM_Prop_Barrel_01", new Vector3(-3.6f, 0f, 7.6f), 0f);
            Deco("SM_Prop_Barrel_01", new Vector3(-3.0f, 0f, 8.3f), 40f);
            Deco("SM_Prop_Crate_02", new Vector3(-4.2f, 0f, 8.6f), 15f);
            Deco("SM_Prop_Hay_Bale_01", new Vector3(6.4f, 0f, 9.6f), 80f);
            Deco("SM_Prop_Wheelbarrow_01", new Vector3(6.0f, 0f, 11.4f), -30f);
            Deco("SM_Prop_Basket_01", new Vector3(5.8f, 0f, 7.3f), 10f, 0.8f);
            Deco("SM_Prop_ScareCrow_01", new Vector3(-1.4f, 0f, 11.6f), 160f, 0.9f);
            Deco("SM_Prop_Bee_Hive_02", new Vector3(-1.7f, 0f, 8.2f), 20f, 0.9f);
            Deco("SM_Prop_Bench_01", new Vector3(-9.8f, 0f, -5.4f), 0f, 0.9f);
            Deco("SM_Prop_Lamp_02", new Vector3(-12.2f, 0f, -7.2f), 0f, 1f);
            Deco("SM_Prop_Lamp_02", new Vector3(10.2f, 0f, -7.0f), 0f, 1f);
            Deco("SM_Prop_Lamp_02", new Vector3(-11.4f, 0f, 3.2f), 0f, 1f);
            Deco("SM_Prop_Planter_01", new Vector3(6.2f, 0f, -5.2f), 0f, 1f);
            Deco("SM_Prop_Planter_03", new Vector3(-11.6f, 0f, -1.0f), 0f, 1f);

            // flowers and grass tufts on the green, never on walkways
            var rnd = new System.Random(7);
            string[] flowers = { "SM_Env_Wild_Flower_Grp_01", "SM_Env_Wild_Flower_Grp_02", "SM_Env_Wild_Flower_Grp_03", "SM_Env_Wild_Flower_Grp_04", "SM_Env_Grass_01", "SM_Env_Grass_02", "SM_Env_Flowers_01", "SM_Env_Grass_03" };
            int placed = 0;
            for (int i = 0; i < 400 && placed < 110; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 64 - 30), 0f, (float)(rnd.NextDouble() * 44 - 18));
                if (p.x > -13.5f && p.x < 19f && p.z > -7.8f && p.z < 13f) continue;   // play area
                if (p.x < -12f && p.z > -6.5f && p.z < -1.8f) continue;                // road
                var n = flowers[rnd.Next(flowers.Length)];
                Deco(n, p, (float)(rnd.NextDouble() * 360), 0.8f + (float)rnd.NextDouble() * 0.5f, false);
                placed++;
            }
            // a few flower beds inside the market for colour
            Deco("SM_Env_Wild_Flower_Grp_02", new Vector3(7.8f, 0f, -6.6f), 0f, 0.8f, false);
            Deco("SM_Env_Wild_Flower_Grp_03", new Vector3(-12f, 0f, 8.5f), 0f, 0.8f, false);
            Deco("SM_Env_Rose_Plane_01", new Vector3(-0.5f, 0f, 12.2f), 0f, 1f, false);
        }

        // ================================================================== boot scene
        static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(120, 196, 255, 255);
            camGo.AddComponent<AudioListener>();
            new GameObject("Boot").AddComponent<Boot>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }
    }

}
