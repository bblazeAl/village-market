using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace IdleArcade.EditorTools
{
    /// <summary>Creates every generated asset the game needs and writes Resources/GameConfig.asset.</summary>
    public static class AssetBuilder
    {
        public const string Gen = "Assets/_Project/Generated";
        public const string PrefabDir = Gen + "/Prefabs";
        public const string MatDir = Gen + "/Materials";
        public const string DataDir = Gen + "/Data";
        public const string MeshDir = Gen + "/Meshes";

        public static readonly string[] CustomerModels =
        {
            "SM_Chr_Bird_Female_01", "SM_Chr_Bird_Male_01", "SM_Chr_Bunny_Female_01", "SM_Chr_Bunny_Male_01",
            "SM_Chr_Cat_Female_01", "SM_Chr_Frog_Female_01", "SM_Chr_Frog_Male_01", "SM_Chr_Mouse_Female_01",
            "SM_Chr_Pig_Female_01", "SM_Chr_Racoon_Male_01", "SM_Chr_Fox_Male_01"
        };
        // index = (int)HelperRole % length: None, Cashier, ApplePicker, Baker, PieHelper
        public static readonly string[] HelperModels = { "SM_Chr_Mouse_Male_01", "SM_Chr_Mouse_Male_01", "SM_Chr_Racoon_Female_01", "SM_Chr_Pig_Male_01", "SM_Chr_Cat_Male_01" };
        public const string PlayerModel = "SM_Chr_Fox_Female_01";

        public class Result
        {
            public GameConfig config;
            public Dictionary<string, Sprite> ui;
            public Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
            public Material zoneMat, groundGrass, groundPlaza, groundPath, groundDirt, groundDeck, arrowMat, shadowMat;
            public GameObject customerBase, helperBase, padPrefab, zonePrefab;
            public RuntimeAnimatorController animator;
        }

        static void Dirs()
        {
            foreach (var d in new[] { Gen, PrefabDir, MatDir, DataDir, MeshDir, "Assets/_Project/Resources" }) Directory.CreateDirectory(d);
        }

        // ------------------------------------------------------------------ materials
        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");

        public static Material SaveMat(Material m, string name)
        {
            string p = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        public static Material LitMat(string name, Texture2D tex, Color tint, float tiling = 1f, float smooth = 0.05f)
        {
            var m = new Material(Lit);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return SaveMat(m, name);
        }

        public static Material UnlitTransparent(string name, Texture2D tex, Color tint)
        {
            var m = new Material(Unlit);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            return SaveMat(m, name);
        }

        static Texture2D SaveTex(string name, SpriteFactory.Painter p, TextureWrapMode wrap)
        {
            string path = $"{Gen}/UI/{name}.png";
            var t = p.ToTexture();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = wrap;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ meshes
        static Mesh ArrowMesh()
        {
            // chunky arrow pointing down (-Y), extruded along Z, centred
            var outline = new[]
            {
                new Vector2(0f, -0.55f), new Vector2(0.42f, -0.08f), new Vector2(0.17f, -0.08f), new Vector2(0.17f, 0.45f),
                new Vector2(-0.17f, 0.45f), new Vector2(-0.17f, -0.08f), new Vector2(-0.42f, -0.08f)
            };
            float d = 0.13f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var norms = new List<Vector3>();
            // caps (fan from a centre point works because the outline is star-shaped from (0,0))
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -d : d;
                int c = verts.Count;
                verts.Add(new Vector3(0, 0, z));
                norms.Add(new Vector3(0, 0, side == 0 ? -1 : 1));
                foreach (var v in outline) { verts.Add(new Vector3(v.x, v.y, z)); norms.Add(new Vector3(0, 0, side == 0 ? -1 : 1)); }
                for (int i = 0; i < outline.Length; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % outline.Length;
                    if (side == 0) { tris.Add(c); tris.Add(b); tris.Add(a); }
                    else { tris.Add(c); tris.Add(a); tris.Add(b); }
                }
            }
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                var n = new Vector3(b.y - a.y, -(b.x - a.x), 0).normalized;
                n = -n;
                int s = verts.Count;
                verts.Add(new Vector3(a.x, a.y, -d)); verts.Add(new Vector3(b.x, b.y, -d));
                verts.Add(new Vector3(b.x, b.y, d)); verts.Add(new Vector3(a.x, a.y, d));
                for (int k = 0; k < 4; k++) norms.Add(n);
                tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            var mesh = new Mesh { name = "GuideArrow" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            string p = $"{MeshDir}/GuideArrow.asset";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(mesh, p);
            return mesh;
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject SavePrefab(GameObject go, string name)
        {
            string p = $"{PrefabDir}/{name}.prefab";
            var pf = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return pf;
        }

        static void NoShadowReceive(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.receiveShadows = true;
        }

        static GameObject ItemPrefab(string synty, float size, string name, System.Action<GameObject> extra = null)
        {
            var w = SyntyLib.Normalised(synty, size, name);
            extra?.Invoke(w);
            w.AddComponent<ItemVisual>();
            foreach (var r in w.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return SavePrefab(w, name);
        }

        static ItemType ItemAsset(string name, ItemKind kind, string key, GameObject prefab, int price, float height, Sprite icon, bool sellable = true)
        {
            string p = $"{DataDir}/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<ItemType>(p);
            if (t == null) { t = ScriptableObject.CreateInstance<ItemType>(); AssetDatabase.CreateAsset(t, p); }
            t.kind = kind; t.nameKey = key; t.prefab = prefab; t.price = price; t.stackHeight = height; t.icon = icon; t.sellable = sellable;
            EditorUtility.SetDirty(t);
            return t;
        }

        // ------------------------------------------------------------------ world-space UI pieces
        public static Canvas WorldCanvas(Transform parent, string name, Vector2 size, float scale = 0.01f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            return c;
        }

        public static Image Img(Transform parent, string name, Sprite s, Color c, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var i = go.GetComponent<Image>();
            i.sprite = s;
            i.color = c;
            i.raycastTarget = false;
            return i;
        }

        public static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, Material mat, float size, Color c, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static GameObject BuildPadPrefab(Result r, TMP_FontAsset font, Material worldMat)
        {
            var root = new GameObject("PayPad");
            var pad = root.AddComponent<PayPad>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            pad.visualRoot = visual;
            var canvas = WorldCanvas(visual, "Canvas", new Vector2(240, 240), 0.01f);
            canvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvas.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var ct = canvas.transform;
            Img(ct, "Shadow", r.ui["shadow"], new Color(0, 0, 0, 0.35f), new Vector2(0, -6), new Vector2(250, 250));
            Img(ct, "Disc", r.ui["circle"], new Color(0.12f, 0.1f, 0.08f, 0.55f), Vector2.zero, new Vector2(214, 214));
            var fill = Img(ct, "Fill", r.ui["circle"], new Color32(255, 206, 64, 230), Vector2.zero, new Vector2(214, 214));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            pad.fill = fill;
            Img(ct, "Ring", r.ui["ring"], Color.white, Vector2.zero, new Vector2(236, 236));
            // icon + price stand slightly up so they read at the camera angle
            var bob = new GameObject("Bob", typeof(RectTransform));
            bob.layer = 5;
            bob.transform.SetParent(ct, false);
            ((RectTransform)bob.transform).sizeDelta = new Vector2(240, 240);
            pad.bobTarget = bob.transform;
            pad.iconImage = Img(bob.transform, "Icon", r.ui["build"], Color.white, new Vector2(0, 30), new Vector2(118, 118));
            pad.iconImage.preserveAspect = true;
            var coin = Img(bob.transform, "Coin", r.ui["coin"], Color.white, new Vector2(-62, -62), new Vector2(54, 54));
            coin.preserveAspect = true;
            pad.priceText = Txt(bob.transform, "Price", "10", font, worldMat, 58, Color.white, new Vector2(18, -62), new Vector2(170, 70));
            pad.priceText.alignment = TextAlignmentOptions.Left;
            // title floats above the pad, facing the camera tilt
            var titleCanvas = WorldCanvas(visual, "TitleCanvas", new Vector2(560, 90), 0.01f);
            titleCanvas.transform.localPosition = new Vector3(0f, 0.35f, 1.35f);
            titleCanvas.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
            pad.titleText = Txt(titleCanvas.transform, "Title", "Title", font, worldMat, 56, Color.white, Vector2.zero, new Vector2(560, 90));
            pad.titleText.enableAutoSizing = true;
            pad.titleText.fontSizeMax = 56;
            pad.titleText.fontSizeMin = 36;
            return SavePrefab(root, "PayPad");
        }

        static GameObject BuildZonePrefab(Result r)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "ZoneMarker";
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mr = root.GetComponent<MeshRenderer>();
            mr.sharedMaterial = r.zoneMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var zm = root.AddComponent<ZoneMarker>();
            zm.rend = mr;
            return SavePrefab(root, "ZoneMarker");
        }

        static GameObject BuildBubble(Transform parent, Result r, TMP_FontAsset font, Material worldMat)
        {
            var holder = new GameObject("Bubble");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            var canvas = WorldCanvas(holder.transform, "Canvas", new Vector2(160, 130), 0.0085f);
            var rootRt = (RectTransform)new GameObject("Root", typeof(RectTransform)).transform;
            rootRt.gameObject.layer = 5;
            rootRt.SetParent(canvas.transform, false);
            rootRt.sizeDelta = new Vector2(160, 130);
            Img(rootRt, "Shadow", r.ui["round"], new Color(0, 0, 0, 0.25f), new Vector2(0, -6), new Vector2(150, 110)).type = Image.Type.Sliced;
            var bubbleBg = Img(rootRt, "Bg", r.ui["round"], Color.white, Vector2.zero, new Vector2(150, 110));
            bubbleBg.type = Image.Type.Sliced;
            var tail = Img(rootRt, "Tail", r.ui["round_small"], Color.white, new Vector2(0, -56), new Vector2(30, 30));
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var bubble = holder.AddComponent<CustomerBubble>();
            bubble.root = rootRt;
            bubble.bg = bubbleBg;
            bubble.icon = Img(rootRt, "Icon", r.ui["coin"], Color.white, new Vector2(-24, 2), new Vector2(84, 84));
            bubble.icon.preserveAspect = true;
            bubble.count = Txt(rootRt, "Count", "2", font, null, 56, new Color32(70, 44, 30, 255), new Vector2(40, 0), new Vector2(70, 80));
            bubble.check = Img(rootRt, "Check", r.ui["check"], Color.white, new Vector2(40, 0), new Vector2(60, 60));
            bubble.check.gameObject.SetActive(false);
            rootRt.gameObject.SetActive(false);
            return holder;
        }

        static NavMeshAgent Agent(GameObject go, float speed)
        {
            var a = go.AddComponent<NavMeshAgent>();
            a.radius = 0.3f;
            a.height = 1.4f;
            a.speed = speed;
            a.angularSpeed = 720f;
            a.acceleration = 16f;
            a.stoppingDistance = 0.05f;
            a.autoBraking = true;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            return a;
        }

        static ItemStack Hands(GameObject go, float y, float z)
        {
            var hands = new GameObject("Hands").transform;
            hands.SetParent(go.transform, false);
            hands.localPosition = new Vector3(0f, y, z);
            var st = go.AddComponent<ItemStack>();
            st.anchor = hands;
            return st;
        }

        // ------------------------------------------------------------------ main
        public static Result BuildAll()
        {
            Dirs();
            var r = new Result();
            r.ui = SpriteFactory.BuildAll();
            var (font, outline, world) = ProjectSetup.BuildFont();

            // textures + materials
            var zoneP = new SpriteFactory.Painter(256);
            zoneP.Fill(q => Mathf.Abs(SpriteFactory.Box(q, new Vector2(0.5f, 0.5f), new Vector2(0.44f, 0.44f), 0.16f)) - 0.025f, Color.white);
            zoneP.Fill(q => SpriteFactory.Box(q, new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.43f), 0.15f), new Color(1, 1, 1, 0.16f));
            var zoneTex = SaveTex("zone", zoneP, TextureWrapMode.Clamp);
            r.zoneMat = UnlitTransparent("M_Zone", zoneTex, new Color(1, 1, 1, 0.55f));

            var dots = new SpriteFactory.Painter(64);
            dots.Fill(q => SpriteFactory.Circle(q, new Vector2(0.5f, 0.5f), 0.28f), Color.white);
            var dotTex = SaveTex("dots", dots, TextureWrapMode.Repeat);
            var lineMat = UnlitTransparent("M_GuideLine", dotTex, Color.white);

            var shadowTex = r.ui["shadow"].texture;
            r.shadowMat = UnlitTransparent("M_BlobShadow", shadowTex, new Color(0, 0, 0, 0.35f));
            r.arrowMat = SaveMat(new Material(Lit) { color = new Color32(255, 196, 40, 255) }, "M_GuideArrow");
            r.arrowMat.SetColor("_BaseColor", new Color32(255, 200, 40, 255));
            r.arrowMat.SetColor("_EmissionColor", new Color32(120, 80, 0, 255));
            r.arrowMat.EnableKeyword("_EMISSION");
            r.arrowMat.SetFloat("_Smoothness", 0.4f);

            r.groundGrass = LitMat("M_Ground_Grass", SyntyLib.Texture("Grass_01.png"), new Color(0.86f, 1.02f, 0.72f), 1f);
            r.groundPlaza = LitMat("M_Ground_Plaza", SyntyLib.Texture("Cobblestone_Texture_01.png"), new Color(1.08f, 1.02f, 0.94f), 1f);
            r.groundPath = LitMat("M_Ground_Path", SyntyLib.Texture("Path_01.png"), new Color(1f, 0.97f, 0.92f), 1f);
            r.groundDirt = LitMat("M_Ground_Dirt", SyntyLib.Texture("Village_Dirt_01.png"), new Color(0.95f, 0.88f, 0.8f), 1f);
            r.groundDeck = LitMat("M_Ground_Deck", SyntyLib.Texture("Path_02.png"), new Color(1.04f, 1.0f, 0.94f), 1f);

            // guide arrow prefab
            var arrowGo = new GameObject("GuideArrow");
            var am = new GameObject("Mesh");
            am.transform.SetParent(arrowGo.transform, false);
            am.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
            var amr = am.AddComponent<MeshRenderer>();
            amr.sharedMaterial = r.arrowMat;
            amr.shadowCastingMode = ShadowCastingMode.On;
            am.transform.localScale = Vector3.one * 1.1f;
            var arrowPrefab = SavePrefab(arrowGo, "GuideArrow");

            // items ----------------------------------------------------------
            var apple = ItemPrefab("SM_Prop_Food_Apple_01", 0.34f, "Item_Apple");
            var bread = ItemPrefab("SM_Prop_Food_Bread_Loaf_01", 0.46f, "Item_Bread");
            var pie = ItemPrefab("SM_Prop_Food_Pie_01", 0.44f, "Item_Pie");
            var plate = ItemPrefab("SM_Prop_Plate_02", 0.42f, "Item_Plate", w =>
            {
                var crumbs = (GameObject)PrefabUtility.InstantiatePrefab(SyntyLib.Find("SM_Prop_Food_Pie_Slice_01"), w.transform);
                PrefabUtility.UnpackPrefabInstance(crumbs, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var c in crumbs.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                crumbs.transform.localPosition = new Vector3(0.03f, 0.02f, -0.02f);
                crumbs.transform.localRotation = Quaternion.Euler(0, 35, 0);
                crumbs.transform.localScale = Vector3.one * 0.55f;
            });

            r.icons["apple"] = IconRenderer.Render(apple, "icon_item_apple", new Vector3(25, -30, 0));
            r.icons["bread"] = IconRenderer.Render(bread, "icon_item_bread", new Vector3(35, -40, 0));
            r.icons["pie"] = IconRenderer.Render(pie, "icon_item_pie", new Vector3(40, -30, 0));
            r.icons["plate"] = IconRenderer.Render(plate, "icon_item_plate", new Vector3(55, -30, 0));

            var appleT = ItemAsset("Apple", ItemKind.Apple, "item.apple", apple, 4, 0.3f, r.icons["apple"]);
            var breadT = ItemAsset("Bread", ItemKind.Bread, "item.bread", bread, 8, 0.2f, r.icons["bread"]);
            var pieT = ItemAsset("Pie", ItemKind.Pie, "item.pie", pie, 18, 0.17f, r.icons["pie"]);
            var plateT = ItemAsset("Plate", ItemKind.Plate, "item.plate", plate, 0, 0.075f, r.icons["plate"], false);

            // coins ---------------------------------------------------------------
            var coinFly = SyntyLib.Normalised("SM_Gen_Prop_Coin_01", 0.32f, "CoinFly");
            foreach (var rr in coinFly.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = ShadowCastingMode.Off;
            var coinFlyPf = SavePrefab(coinFly, "CoinFly");
            var stack = new GameObject("CoinStack");
            for (int i = 0; i < 3; i++)
            {
                var c = SyntyLib.Normalised("SM_Gen_Prop_Coin_01", 0.33f, "Coin" + i);
                c.transform.SetParent(stack.transform, false);
                c.transform.localPosition = new Vector3(Random.Range(-0.015f, 0.015f), i * 0.052f, Random.Range(-0.015f, 0.015f));
                c.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);
            }
            var coinStackPf = SavePrefab(stack, "CoinStack");

            // basket the customers carry home
            var basket = SyntyLib.Normalised("SM_Prop_Basket_02", 0.5f, "Basket");
            var bApple = SyntyLib.Normalised("SM_Prop_Food_Apple_01", 0.2f, "A");
            bApple.transform.SetParent(basket.transform, false);
            bApple.transform.localPosition = new Vector3(0.06f, 0.33f, 0.02f);
            var bBread = SyntyLib.Normalised("SM_Prop_Food_Bread_Loaf_01", 0.3f, "B");
            bBread.transform.SetParent(basket.transform, false);
            bBread.transform.localPosition = new Vector3(-0.05f, 0.31f, -0.02f);
            bBread.transform.localRotation = Quaternion.Euler(0, 0, 25);
            var basketPf = SavePrefab(basket, "Basket");

            // accessories
            var hat = SyntyLib.Normalised("SM_Chr_Attach_Hat_03", 0.42f, "HelperHat");
            var hatPf = SavePrefab(hat, "HelperHat");
            var crown = SyntyLib.Normalised("SM_Chr_Attach_Crown_01", 0.34f, "VipCrown");
            var crownPf = SavePrefab(crown, "VipCrown");

            // animation -------------------------------------------------------------
            r.animator = HumanoidClipFactory.BuildController();

            // character bases -----------------------------------------------------
            var cust = new GameObject("CustomerBase");
            var cAgent = Agent(cust, 2.7f);
            var cAnim = cust.AddComponent<CharacterAnim>();
            var cHands = Hands(cust, 0.66f, 0.36f);
            cAnim.stack = cHands;
            cAnim.runSpeed = 4.6f;
            var customer = cust.AddComponent<Customer>();
            customer.agent = cAgent;
            customer.anim = cAnim;
            customer.hands = cHands;
            customer.bubble = BuildBubble(cust.transform, r, font, world).GetComponent<CustomerBubble>();
            r.customerBase = SavePrefab(cust, "CustomerBase");

            var help = new GameObject("HelperBase");
            var hAgent = Agent(help, 3f);
            hAgent.avoidancePriority = 30;
            var hAnim = help.AddComponent<CharacterAnim>();
            var hStack = Hands(help, 0.66f, 0.38f);
            hAnim.stack = hStack;
            var hCarrier = help.AddComponent<Carrier>();
            hCarrier.kind = CarrierKind.Helper;
            hCarrier.stack = hStack;
            var helper = help.AddComponent<Helper>();
            helper.agent = hAgent;
            helper.carrier = hCarrier;
            helper.stack = hStack;
            helper.anim = hAnim;
            r.helperBase = SavePrefab(help, "HelperBase");

            r.padPrefab = BuildPadPrefab(r, font, world);
            r.zonePrefab = BuildZonePrefab(r);

            // config ------------------------------------------------------------------
            string cfgPath = "Assets/_Project/Resources/GameConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(cfgPath);
            if (cfg == null) { cfg = ScriptableObject.CreateInstance<GameConfig>(); AssetDatabase.CreateAsset(cfg, cfgPath); }
            cfg.balance = new Balance();   // code is the single source of truth for tuning
            cfg.apple = appleT; cfg.bread = breadT; cfg.pie = pieT; cfg.plate = plateT;
            cfg.coinFlyPrefab = coinFlyPf; cfg.coinStackPrefab = coinStackPf; cfg.basketPrefab = basketPf;
            cfg.playerModel = SyntyLib.Find(PlayerModel);
            cfg.customerModels = CustomerModels.Select(SyntyLib.Find).ToArray();
            cfg.helperModels = HelperModels.Select(SyntyLib.Find).ToArray();
            cfg.helperHat = hatPf;
            cfg.vipCrown = crownPf;
            cfg.animator = r.animator;
            cfg.fxDust = SyntyLib.Find("FX_Dust_Small_01");
            cfg.fxSparkles = SyntyLib.Find("FX_Sparkles_Small_01");
            cfg.fxConfetti = SyntyLib.Find("FX_Confetti_01");
            cfg.fxLevelUp = SyntyLib.Find("FX_LevelUp_01");
            cfg.fxHeart = SyntyLib.Find("FX_Pickup_Heart_01");
            cfg.fxCoinBurst = SyntyLib.Find("FX_Money_Coins_Burst_01");
            cfg.fxSmoke = SyntyLib.Find("FX_Smoke_White_Small_01");
            cfg.fxStep = SyntyLib.Find("FX_Cartoony_Footstep_01");
            cfg.fxFireworks = SyntyLib.Find("FX_Fireworks_Group_01");
            cfg.fxSplash = SyntyLib.Find("FX_Impact_Water_01");
            cfg.sfx = BuildSfx();
            cfg.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/music_main.ogg");
            cfg.font = font;
            cfg.fontOutline = outline;
            cfg.fontWorld = world;
            cfg.spriteRound = r.ui["round"]; cfg.spriteRoundSmall = r.ui["round_small"]; cfg.spriteCircle = r.ui["circle"];
            cfg.spriteRing = r.ui["ring"]; cfg.spriteRingThin = r.ui["ring_thin"]; cfg.spriteShadow = r.ui["shadow"];
            cfg.iconCoin = r.ui["coin"]; cfg.iconGear = r.ui["gear"]; cfg.iconArrow = r.ui["arrow"]; cfg.iconSpeed = r.ui["speed"];
            cfg.iconBag = r.ui["bag"]; cfg.iconProfit = r.ui["profit"]; cfg.iconHelper = r.ui["helper"]; cfg.iconCashier = r.ui["cashier"];
            cfg.iconHeart = r.ui["heart"]; cfg.iconLock = r.ui["lock"]; cfg.iconStar = r.ui["star"]; cfg.iconCheck = r.ui["check"];
            cfg.iconMusic = r.ui["music"]; cfg.iconSound = r.ui["sound"]; cfg.iconHand = r.ui["hand"]; cfg.iconBuild = r.ui["build"]; cfg.iconSad = r.ui["sad"]; cfg.iconTrash = r.ui["trash"];
            cfg.iconMedal = r.ui["medal"]; cfg.iconQuest = r.ui["quest"]; cfg.iconChest = r.ui["chest"];
            cfg.guideLineMaterial = lineMat;
            cfg.blobShadowMaterial = r.shadowMat;
            cfg.guideArrowPrefab = arrowPrefab;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            r.config = cfg;
            return r;
        }

        static SfxEntry[] BuildSfx()
        {
            AudioClip[] Clips(params string[] names) => names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/Sfx/{n}.ogg")).Where(c => c != null).ToArray();
            SfxEntry E(Sfx id, float vol, float jitter, float interval, params string[] names) => new SfxEntry { id = id, clips = Clips(names), volume = vol, pitchJitter = jitter, minInterval = interval };
            return new[]
            {
                E(Sfx.Pickup, 0.7f, 0.0f, 0.03f, "pickup_1", "pickup_2"),
                E(Sfx.Place, 0.7f, 0.05f, 0.03f, "place_1", "place_2", "place_3"),
                E(Sfx.Coin, 0.55f, 0.08f, 0.025f, "coin_1", "coin_2", "coin_3"),
                E(Sfx.CoinsCollect, 0.8f, 0.05f, 0.1f, "coins_collect_1", "coins_collect_2", "coins_handle_1", "coins_handle_2"),
                E(Sfx.Pay, 0.5f, 0.0f, 0.04f, "pay_1", "pay_2", "pay_3"),
                E(Sfx.Unlock, 0.8f, 0f, 0.2f, "unlock"),
                E(Sfx.LevelUp, 0.9f, 0f, 0.5f, "levelup"),
                E(Sfx.Hire, 0.8f, 0f, 0.3f, "hire"),
                E(Sfx.Upgrade, 0.8f, 0f, 0.15f, "upgrade"),
                E(Sfx.Checkout, 0.6f, 0.04f, 0.08f, "checkout"),
                E(Sfx.Happy, 0.45f, 0.1f, 0.1f, "happy"),
                E(Sfx.Click, 0.7f, 0.03f, 0.03f, "click"),
                E(Sfx.Open, 0.6f, 0.03f, 0.1f, "open"),
                E(Sfx.Close, 0.6f, 0.03f, 0.1f, "close"),
                E(Sfx.Error, 0.5f, 0.02f, 0.3f, "error"),
                E(Sfx.Tick, 0.5f, 0.05f, 0.05f, "tick"),
                E(Sfx.Max, 0.5f, 0.02f, 0.4f, "max"),
                E(Sfx.Step, 0.3f, 0.12f, 0.12f, "step_1", "step_2", "step_3", "step_4"),
                E(Sfx.Plate, 0.6f, 0.08f, 0.04f, "plate_1"),
                E(Sfx.Build, 0.6f, 0.03f, 0.1f, "build"),
                E(Sfx.Whoosh, 0.5f, 0.05f, 0.1f, "whoosh"),
                E(Sfx.Vip, 0.7f, 0f, 1f, "vip"),
                E(Sfx.Toggle, 0.6f, 0.03f, 0.05f, "toggle"),
            };
        }
    }
}
