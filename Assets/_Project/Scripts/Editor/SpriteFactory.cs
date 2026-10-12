using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleArcade.EditorTools
{
    /// <summary>
    /// Paints the UI kit (rounded panels, rings, icons) with signed-distance shapes so every sprite
    /// is crisp, anti-aliased and in one consistent "chunky outlined" casual style.
    /// </summary>
    public static class SpriteFactory
    {
        public const string Dir = "Assets/_Project/Generated/UI";

        // ------------------------------------------------------------------ painter
        public class Painter
        {
            public readonly int size;
            public readonly Color[] px;
            public Painter(int s) { size = s; px = new Color[s * s]; }

            /// <summary>Composite a shape given by a signed distance in normalised units (0..1 canvas).</summary>
            public void Fill(Func<Vector2, float> sdf, Color c)
            {
                float aa = 1.2f / size;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float d = sdf(p);
                    float a = Mathf.Clamp01(0.5f - d / aa) * c.a;
                    if (a <= 0f) continue;
                    int i = y * size + x;
                    var dst = px[i];
                    float outA = a + dst.a * (1f - a);
                    if (outA <= 0f) continue;
                    var rgb = (new Color(c.r, c.g, c.b) * a + new Color(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / outA;
                    px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
            }

            /// <summary>Shape with outline: outline colour expanded by 'stroke', then fill on top.</summary>
            public void Shape(Func<Vector2, float> sdf, Color fill, Color outline, float stroke = 0.035f)
            {
                if (stroke > 0f) Fill(p => sdf(p) - stroke, outline);
                Fill(sdf, fill);
            }

            /// <summary>Soft vertical shading inside a shape: lighter top, darker bottom.</summary>
            public void Shade(Func<Vector2, float> sdf, float top, float bottom, float y0, float y1)
            {
                float aa = 1.2f / size;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float m = Mathf.Clamp01(0.5f - sdf(p) / aa);
                    if (m <= 0f) continue;
                    int i = y * size + x;
                    float k = Mathf.InverseLerp(y0, y1, p.y);
                    float f = Mathf.Lerp(bottom, top, k);
                    var c = px[i];
                    var shaded = f >= 0 ? Color.Lerp(c, Color.white, f) : Color.Lerp(c, Color.black, -f);
                    shaded.a = c.a;
                    px[i] = Color.Lerp(c, shaded, m);
                }
            }

            public Texture2D ToTexture()
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                t.SetPixels(px);
                t.Apply();
                return t;
            }
        }

        // ------------------------------------------------------------------ sdf primitives
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        public static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
            }
            return s * Mathf.Sqrt(d);
        }

        public static Vector2[] Star(Vector2 c, float r1, float r2, int points, float rot = 90f)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float a = (rot + i * 180f / points) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? r1 : r2;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ------------------------------------------------------------------ output
        static Sprite Save(string name, Painter p, Vector4 border = default, int ppu = 100)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            var tex = p.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = ppu;
            imp.spriteBorder = border;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static readonly Color Ink = new Color32(70, 44, 30, 255);

        public static Dictionary<string, Sprite> BuildAll()
        {
            var s = new Dictionary<string, Sprite>();

            // panels -----------------------------------------------------------
            var p = new Painter(128);
            p.Fill(q => Box(q, V(0.5f, 0.5f), V(0.5f, 0.5f), 0.32f), Color.white);
            s["round"] = Save("round", p, new Vector4(46, 46, 46, 46));

            p = new Painter(64);
            p.Fill(q => Box(q, V(0.5f, 0.5f), V(0.5f, 0.5f), 0.25f), Color.white);
            s["round_small"] = Save("round_small", p, new Vector4(18, 18, 18, 18));

            p = new Painter(256);
            p.Fill(q => Circle(q, V(0.5f, 0.5f), 0.49f), Color.white);
            s["circle"] = Save("circle", p);

            p = new Painter(256);
            p.Fill(q => Mathf.Abs(Circle(q, V(0.5f, 0.5f), 0.45f)) - 0.035f, Color.white);
            s["ring"] = Save("ring", p);

            p = new Painter(256);
            p.Fill(q => Mathf.Abs(Circle(q, V(0.5f, 0.5f), 0.47f)) - 0.015f, Color.white);
            s["ring_thin"] = Save("ring_thin", p);

            p = new Painter(128);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = new Vector2((x + 0.5f) / 128f - 0.5f, (y + 0.5f) / 128f - 0.5f).magnitude * 2f;
                float a = Mathf.Clamp01(1f - d);
                p.px[y * 128 + x] = new Color(0, 0, 0, a * a * (3 - 2 * a));
            }
            s["shadow"] = Save("shadow", p);

            // icons ------------------------------------------------------------
            s["coin"] = Save("icon_coin", Coin());
            s["gear"] = Save("icon_gear", Gear());
            s["arrow"] = Save("icon_arrow", Arrow());
            s["speed"] = Save("icon_speed", Bolt());
            s["bag"] = Save("icon_bag", Bag());
            s["profit"] = Save("icon_profit", Profit());
            s["helper"] = Save("icon_helper", Person(new Color32(80, 160, 255, 255)));
            s["cashier"] = Save("icon_cashier", Register());
            s["heart"] = Save("icon_heart", Heart());
            s["lock"] = Save("icon_lock", Lock());
            s["star"] = Save("icon_star", StarIcon());
            s["check"] = Save("icon_check", Check());
            s["music"] = Save("icon_music", Music());
            s["sound"] = Save("icon_sound", Speaker());
            s["build"] = Save("icon_build", Hammer());
            s["hand"] = Save("icon_hand", Person(new Color32(255, 170, 60, 255)));
            s["upgrade_board"] = Save("icon_upgrades", UpArrow());
            s["sad"] = Save("icon_sad", Sad());
            s["trash"] = Save("icon_trash", Trash());
            s["medal"] = Save("icon_medal", Medal());
            s["quest"] = Save("icon_quest", Scroll());
            s["chest"] = Save("icon_chest", Chest());
            return s;
        }

        static Painter Medal()
        {
            var p = new Painter(256);
            // two ribbon tails behind the disc
            p.Shape(q => Polygon(q, new[] { V(0.3f, 0.95f), V(0.46f, 0.95f), V(0.56f, 0.55f), V(0.4f, 0.55f) }), new Color32(70, 140, 235, 255), Ink, 0.03f);
            p.Shape(q => Polygon(q, new[] { V(0.54f, 0.95f), V(0.7f, 0.95f), V(0.6f, 0.55f), V(0.44f, 0.55f) }), new Color32(235, 80, 80, 255), Ink, 0.03f);
            Vector2 c = V(0.5f, 0.38f);
            p.Shape(q => Circle(q, c, 0.3f), new Color32(255, 196, 50, 255), Ink, 0.035f);
            p.Shade(q => Circle(q, c, 0.3f), 0.3f, -0.15f, 0.08f, 0.68f);
            var star = Star(c, 0.17f, 0.08f, 5);
            p.Fill(q => Polygon(q, star), new Color32(225, 140, 20, 255));
            return p;
        }

        static Painter Scroll()
        {
            var p = new Painter(256);
            var paper = new Color32(255, 240, 200, 255);
            Func<Vector2, float> sheet = q => Box(q, V(0.5f, 0.5f), V(0.3f, 0.36f), 0.04f);
            p.Shape(sheet, paper, Ink, 0.035f);
            p.Shade(sheet, 0.15f, -0.12f, 0.14f, 0.86f);
            p.Shape(q => Box(q, V(0.5f, 0.86f), V(0.36f, 0.06f), 0.06f), new Color32(214, 150, 80, 255), Ink, 0.03f);
            p.Shape(q => Box(q, V(0.5f, 0.14f), V(0.36f, 0.06f), 0.06f), new Color32(214, 150, 80, 255), Ink, 0.03f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.64f - i * 0.13f;
                float w = i == 2 ? 0.12f : 0.18f;
                p.Fill(q => Segment(q, V(0.32f, y), V(0.32f + w * 2f, y), 0.022f), new Color32(170, 120, 80, 255));
            }
            p.Shape(q => Polygon(q, new[] { V(0.62f, 0.28f), V(0.7f, 0.2f), V(0.86f, 0.4f) }) - 0.02f, new Color32(100, 200, 80, 255), Ink, 0.025f);
            return p;
        }

        static Painter Chest()
        {
            var p = new Painter(256);
            var wood = new Color32(200, 120, 60, 255);
            var gold = new Color32(255, 200, 50, 255);
            Func<Vector2, float> body = q => Box(q, V(0.5f, 0.33f), V(0.38f, 0.21f), 0.05f);
            Func<Vector2, float> lid = q => Mathf.Max(Circle(q, V(0.5f, 0.42f), 0.38f), -(q.y - 0.54f));
            p.Shape(lid, wood, Ink, 0.035f);
            p.Shape(body, wood, Ink, 0.035f);
            p.Shade(body, 0.15f, -0.2f, 0.12f, 0.54f);
            p.Fill(q => Box(q, V(0.5f, 0.54f), V(0.38f, 0.025f), 0.01f), Ink);
            p.Shape(q => Box(q, V(0.24f, 0.45f), V(0.04f, 0.33f), 0.02f), gold, Ink, 0.025f);
            p.Shape(q => Box(q, V(0.76f, 0.45f), V(0.04f, 0.33f), 0.02f), gold, Ink, 0.025f);
            p.Shape(q => Box(q, V(0.5f, 0.5f), V(0.07f, 0.09f), 0.03f), gold, Ink, 0.025f);
            return p;
        }

        static Painter Sad()
        {
            var p = new Painter(256);
            Vector2 c = V(0.5f, 0.5f);
            p.Shape(q => Circle(q, c, 0.42f), new Color32(255, 196, 60, 255), Ink, 0.035f);
            p.Shade(q => Circle(q, c, 0.42f), 0.25f, -0.15f, 0.1f, 0.9f);
            p.Fill(q => Mathf.Min(Circle(q, V(0.37f, 0.56f), 0.045f), Circle(q, V(0.63f, 0.56f), 0.045f)), Ink);
            // frown + eyebrows
            p.Fill(q => Mathf.Max(Mathf.Abs(Circle(q, V(0.5f, 0.2f), 0.2f)) - 0.025f, -(q.y - 0.3f)), Ink);
            p.Fill(q => Mathf.Min(Segment(q, V(0.29f, 0.7f), V(0.43f, 0.66f), 0.02f), Segment(q, V(0.57f, 0.66f), V(0.71f, 0.7f), 0.02f)), Ink);
            p.Fill(q => Circle(q, V(0.76f, 0.62f), 0.05f), new Color32(120, 200, 255, 255));
            return p;
        }

        static Painter Trash()
        {
            var p = new Painter(256);
            var col = new Color32(110, 190, 90, 255);
            p.Shape(q => Polygon(q, new[] { V(0.24f, 0.72f), V(0.76f, 0.72f), V(0.69f, 0.1f), V(0.31f, 0.1f) }), col, Ink, 0.035f);
            p.Shape(q => Box(q, V(0.5f, 0.78f), V(0.32f, 0.05f), 0.03f), col, Ink, 0.03f);
            p.Shape(q => Box(q, V(0.5f, 0.87f), V(0.08f, 0.04f), 0.02f), col, Ink, 0.025f);
            for (int i = -1; i <= 1; i++) { float x = 0.5f + i * 0.11f; p.Fill(q => Segment(q, V(x, 0.2f), V(x, 0.6f), 0.022f), new Color32(70, 130, 60, 255)); }
            return p;
        }

        static Painter Coin()
        {
            var p = new Painter(256);
            Vector2 c = V(0.5f, 0.5f);
            p.Fill(q => Circle(q, c + V(0, -0.035f), 0.44f), new Color32(196, 120, 20, 255));
            p.Shape(q => Circle(q, c, 0.42f), new Color32(255, 200, 50, 255), Ink, 0.035f);
            p.Fill(q => Mathf.Abs(Circle(q, c, 0.31f)) - 0.018f, new Color32(226, 150, 24, 255));
            var star = Star(c, 0.19f, 0.085f, 5);
            p.Fill(q => Polygon(q, star), new Color32(226, 150, 24, 255));
            p.Fill(q => Mathf.Max(Mathf.Abs(Circle(q, c, 0.36f)) - 0.02f, -(q.y - 0.62f)), new Color(1, 1, 1, 0.55f));
            return p;
        }

        static Painter Gear()
        {
            var p = new Painter(256);
            Vector2 c = V(0.5f, 0.5f);
            Func<Vector2, float> gear = q =>
            {
                Vector2 d = q - c;
                float a = Mathf.Atan2(d.y, d.x);
                float teeth = Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f) * 0.5f + 0.5f;
                float r = 0.34f + teeth * 0.1f;
                return Mathf.Max(d.magnitude - r, -(d.magnitude - 0.14f));
            };
            p.Fill(gear, Color.white);
            return p;
        }

        static Painter Arrow()
        {
            var p = new Painter(256);
            var head = new[] { V(0.5f, 0.92f), V(0.86f, 0.52f), V(0.64f, 0.52f), V(0.64f, 0.1f), V(0.36f, 0.1f), V(0.36f, 0.52f), V(0.14f, 0.52f) };
            p.Shape(q => Polygon(q, head) + 0.0f, Color.white, Ink, 0.04f);
            return p;
        }

        static Painter UpArrow()
        {
            var p = new Painter(256);
            var head = new[] { V(0.5f, 0.92f), V(0.86f, 0.52f), V(0.64f, 0.52f), V(0.64f, 0.1f), V(0.36f, 0.1f), V(0.36f, 0.52f), V(0.14f, 0.52f) };
            p.Shape(q => Polygon(q, head), new Color32(110, 210, 80, 255), Ink, 0.04f);
            p.Shade(q => Polygon(q, head), 0.25f, -0.15f, 0.1f, 0.92f);
            return p;
        }

        static Painter Bolt()
        {
            var p = new Painter(256);
            var v = new[] { V(0.6f, 0.95f), V(0.2f, 0.45f), V(0.47f, 0.45f), V(0.38f, 0.05f), V(0.8f, 0.58f), V(0.53f, 0.58f), V(0.66f, 0.95f) };
            p.Shape(q => Polygon(q, v), new Color32(255, 214, 40, 255), Ink, 0.035f);
            p.Shade(q => Polygon(q, v), 0.3f, -0.1f, 0.05f, 0.95f);
            return p;
        }

        static Painter Bag()
        {
            var p = new Painter(256);
            Func<Vector2, float> body = q => Box(q, V(0.5f, 0.38f), V(0.32f, 0.28f), 0.14f);
            Func<Vector2, float> neck = q => Box(q, V(0.5f, 0.7f), V(0.14f, 0.07f), 0.05f);
            Func<Vector2, float> handle = q => Mathf.Max(Mathf.Abs(Circle(q, V(0.5f, 0.74f), 0.16f)) - 0.035f, -(q.y - 0.74f));
            p.Shape(handle, new Color32(150, 90, 50, 255), Ink, 0.03f);
            p.Shape(q => Mathf.Min(body(q), neck(q)), new Color32(214, 150, 80, 255), Ink, 0.035f);
            p.Fill(q => Box(q, V(0.5f, 0.64f), V(0.2f, 0.035f), 0.03f), new Color32(170, 100, 50, 255));
            p.Shade(body, 0.2f, -0.15f, 0.1f, 0.66f);
            return p;
        }

        static Painter Profit()
        {
            var p = new Painter(256);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.18f + i * 0.11f;
                p.Shape(q => Box(q, V(0.4f, y), V(0.26f, 0.06f), 0.06f), new Color32(255, 200, 50, 255), Ink, 0.03f);
            }
            var arrow = new[] { V(0.78f, 0.95f), V(0.95f, 0.72f), V(0.84f, 0.72f), V(0.84f, 0.45f), V(0.72f, 0.45f), V(0.72f, 0.72f), V(0.61f, 0.72f) };
            p.Shape(q => Polygon(q, arrow), new Color32(110, 210, 80, 255), Ink, 0.03f);
            return p;
        }

        static Painter Person(Color body)
        {
            var p = new Painter(256);
            Func<Vector2, float> torso = q => Mathf.Max(Box(q, V(0.5f, 0.2f), V(0.32f, 0.26f), 0.2f), -(q.y - 0.08f));
            p.Shape(torso, body, Ink, 0.035f);
            p.Shape(q => Circle(q, V(0.5f, 0.68f), 0.19f), new Color32(255, 214, 170, 255), Ink, 0.035f);
            p.Shade(torso, 0.2f, -0.2f, 0.08f, 0.45f);
            return p;
        }

        static Painter Register()
        {
            var p = new Painter(256);
            Func<Vector2, float> body = q => Box(q, V(0.5f, 0.36f), V(0.38f, 0.24f), 0.07f);
            p.Shape(q => Box(q, V(0.5f, 0.76f), V(0.2f, 0.1f), 0.04f), new Color32(80, 80, 90, 255), Ink, 0.03f);
            p.Fill(q => Box(q, V(0.5f, 0.76f), V(0.15f, 0.055f), 0.02f), new Color32(130, 240, 140, 255));
            p.Shape(body, new Color32(240, 90, 80, 255), Ink, 0.035f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    int ii = i, jj = j;
                    p.Fill(q => Box(q, V(0.32f + ii * 0.18f, 0.42f + jj * 0.1f), V(0.06f, 0.03f), 0.02f), new Color(1, 1, 1, 0.85f));
                }
            p.Fill(q => Box(q, V(0.5f, 0.2f), V(0.3f, 0.035f), 0.02f), new Color32(160, 50, 45, 255));
            return p;
        }

        static Painter Heart()
        {
            var p = new Painter(256);
            Func<Vector2, float> heart = q =>
            {
                float a = Circle(q, V(0.35f, 0.62f), 0.2f);
                float b = Circle(q, V(0.65f, 0.62f), 0.2f);
                float t = Polygon(q, new[] { V(0.17f, 0.56f), V(0.5f, 0.14f), V(0.83f, 0.56f), V(0.5f, 0.66f) });
                return Mathf.Min(Mathf.Min(a, b), t);
            };
            p.Shape(heart, new Color32(255, 80, 100, 255), Ink, 0.035f);
            p.Fill(q => Circle(q, V(0.34f, 0.66f), 0.06f), new Color(1, 1, 1, 0.7f));
            return p;
        }

        static Painter Lock()
        {
            var p = new Painter(256);
            p.Shape(q => Mathf.Max(Mathf.Abs(Circle(q, V(0.5f, 0.56f), 0.2f)) - 0.05f, -(q.y - 0.56f)), new Color32(180, 180, 190, 255), Ink, 0.03f);
            p.Shape(q => Box(q, V(0.5f, 0.34f), V(0.3f, 0.22f), 0.06f), new Color32(255, 190, 40, 255), Ink, 0.035f);
            p.Fill(q => Mathf.Min(Circle(q, V(0.5f, 0.38f), 0.05f), Box(q, V(0.5f, 0.28f), V(0.022f, 0.07f), 0.02f)), Ink);
            return p;
        }

        static Painter StarIcon()
        {
            var p = new Painter(256);
            var v = Star(V(0.5f, 0.48f), 0.46f, 0.22f, 5);
            p.Shape(q => Polygon(q, v) - 0.02f, new Color32(255, 204, 40, 255), Ink, 0.035f);
            p.Shade(q => Polygon(q, v), 0.35f, -0.1f, 0.1f, 0.9f);
            return p;
        }

        static Painter Check()
        {
            var p = new Painter(256);
            p.Shape(q => Circle(q, V(0.5f, 0.5f), 0.42f), new Color32(100, 200, 80, 255), Ink, 0.035f);
            p.Fill(q => Mathf.Min(Segment(q, V(0.3f, 0.5f), V(0.45f, 0.34f), 0.055f), Segment(q, V(0.45f, 0.34f), V(0.72f, 0.66f), 0.055f)), Color.white);
            return p;
        }

        static Painter Music()
        {
            var p = new Painter(256);
            var col = new Color32(160, 110, 255, 255);
            Func<Vector2, float> note = q => Mathf.Min(Mathf.Min(Circle(q, V(0.32f, 0.26f), 0.13f), Circle(q, V(0.72f, 0.34f), 0.13f)),
                Mathf.Min(Mathf.Min(Box(q, V(0.42f, 0.55f), V(0.035f, 0.3f), 0.01f), Box(q, V(0.82f, 0.62f), V(0.035f, 0.3f), 0.01f)),
                    Polygon(q, new[] { V(0.39f, 0.86f), V(0.85f, 0.94f), V(0.85f, 0.8f), V(0.39f, 0.72f) })));
            p.Shape(note, col, Ink, 0.035f);
            return p;
        }

        static Painter Speaker()
        {
            var p = new Painter(256);
            var col = new Color32(90, 170, 255, 255);
            var cone = new[] { V(0.12f, 0.38f), V(0.3f, 0.38f), V(0.52f, 0.18f), V(0.52f, 0.82f), V(0.3f, 0.62f), V(0.12f, 0.62f) };
            p.Shape(q => Polygon(q, cone), col, Ink, 0.035f);
            p.Shape(q => Mathf.Max(Mathf.Abs(Circle(q, V(0.5f, 0.5f), 0.2f)) - 0.035f, -(q.x - 0.6f)), col, Ink, 0.025f);
            p.Shape(q => Mathf.Max(Mathf.Abs(Circle(q, V(0.5f, 0.5f), 0.34f)) - 0.035f, -(q.x - 0.66f)), col, Ink, 0.025f);
            return p;
        }

        static Painter Hammer()
        {
            var p = new Painter(256);
            p.Shape(q => Segment(q, V(0.3f, 0.12f), V(0.62f, 0.6f), 0.06f), new Color32(190, 120, 60, 255), Ink, 0.03f);
            var head = new[] { V(0.42f, 0.7f), V(0.66f, 0.92f), V(0.9f, 0.68f), V(0.66f, 0.46f) };
            p.Shape(q => Polygon(q, head) - 0.02f, new Color32(150, 160, 175, 255), Ink, 0.035f);
            p.Shade(q => Polygon(q, head), 0.3f, -0.15f, 0.46f, 0.92f);
            return p;
        }
    }
}
