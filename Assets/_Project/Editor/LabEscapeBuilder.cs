using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LabEscape.EditorTools
{
    /// <summary>
    /// Сборщик игры «Мышиный побег».
    /// Создаёт текстуры, материалы, префабы (включая Prefab Variant), сцену с тремя зонами,
    /// врагов, спавнеры и интерфейс. Запуск: меню «Lab Escape → Собрать игру».
    /// При первом открытии проекта запускается автоматически (см. LabEscapeAutoSetup).
    /// После сборки всё можно свободно менять руками в редакторе.
    /// </summary>
    public static class LabEscapeBuilder
    {
        public const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/LabEscape.unity";
        private const string MatDir = Root + "/Materials";
        private const string TexDir = Root + "/Textures";
        private const string PrefabDir = Root + "/Prefabs";

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        private static Font uiFont;

        private class PrefabSet
        {
            public GameObject Mouse, Cat, FatCat, Cheese, Bandage, SyringeSpeed, SyringeJump, SyringeCoconut,
                Keycard, Crate, Crumb, Flask;
        }

        // =====================================================================
        //  МЕНЮ
        // =====================================================================

        [MenuItem("Lab Escape/Собрать игру (сцена, префабы, материалы)", priority = 0)]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Lab Escape",
                    "Сцена уже существует. Пересоздать сцену, префабы и материалы?\n" +
                    "Все ручные изменения в сцене LabEscape будут потеряны.",
                    "Пересоздать", "Отмена"))
                return;

            BuildAll();
        }

        [MenuItem("Lab Escape/Открыть сцену уровня", priority = 1)]
        public static void OpenLevel()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else EditorUtility.DisplayDialog("Lab Escape", "Сцена ещё не собрана. Выберите «Собрать игру».", "OK");
        }

        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Lab Escape] Выйдите из Play Mode перед сборкой.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Lab Escape", "Папки и текстуры...", 0.1f);
                EnsureFolders();
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Mats.Clear();
                var tex = CreateTextures();

                EditorUtility.DisplayProgressBar("Lab Escape", "Материалы...", 0.25f);
                CreateMaterials(tex);

                EditorUtility.DisplayProgressBar("Lab Escape", "Префабы...", 0.4f);
                PrefabSet prefabs = CreatePrefabs();

                EditorUtility.DisplayProgressBar("Lab Escape", "Сцена уровня...", 0.7f);
                BuildScene(prefabs);

                AssetDatabase.SaveAssets();
                Debug.Log("[Lab Escape] Готово! Сцена: " + ScenePath + ". Нажмите Play.");
            }
            catch (Exception e)
            {
                Debug.LogError("[Lab Escape] Ошибка сборки: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { Root + "/Scenes", MatDir, TexDir, PrefabDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        // =====================================================================
        //  ТЕКСТУРЫ (рисуются кодом, сохраняются в PNG)
        // =====================================================================

        private static Dictionary<string, Texture2D> CreateTextures()
        {
            var t = new Dictionary<string, Texture2D>();

            t["Tiles"] = MakeTexture("Tiles", 128, (x, y, s) =>
            {
                bool grout = x % 64 < 3 || y % 64 < 3;
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.05f;
                return grout ? new Color(0.62f, 0.64f, 0.66f) : new Color(0.93f - n, 0.94f - n, 0.95f - n);
            });

            t["MetalFloor"] = MakeTexture("MetalFloor", 128, (x, y, s) =>
            {
                bool ridge = (x + y) % 16 < 2 || (x - y + 256) % 16 < 2;
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.06f;
                float v = (ridge ? 0.5f : 0.38f) + n;
                return new Color(v, v * 1.02f, v * 1.05f);
            });

            t["Wood"] = MakeTexture("Wood", 128, (x, y, s) =>
            {
                bool frame = x < 10 || x > 117 || y < 10 || y > 117;
                bool brace = Mathf.Abs(x - y) < 6;
                bool gap = y % 32 < 2;
                float grain = Mathf.PerlinNoise(x * 0.02f, y * 0.3f) * 0.12f;
                Color baseCol = new Color(0.62f + grain, 0.42f + grain * 0.7f, 0.22f);
                if (frame || brace) baseCol *= 0.78f;
                if (gap && !frame) baseCol *= 0.6f;
                baseCol.a = 1f;
                return baseCol;
            });

            t["Hazard"] = MakeTexture("Hazard", 128, (x, y, s) =>
                ((x + y) / 16) % 2 == 0 ? new Color(0.98f, 0.8f, 0.1f) : new Color(0.12f, 0.12f, 0.12f));

            t["Cheese"] = MakeTexture("Cheese", 128, (x, y, s) =>
            {
                Vector2[] holes = { new Vector2(30, 40), new Vector2(90, 30), new Vector2(70, 90), new Vector2(20, 100), new Vector2(105, 80) };
                float[] radius = { 12, 9, 15, 7, 10 };
                for (int i = 0; i < holes.Length; i++)
                    if (Vector2.Distance(new Vector2(x, y), holes[i]) < radius[i])
                        return new Color(0.85f, 0.65f, 0.1f);
                return new Color(1f, 0.85f, 0.25f);
            });

            t["Acid"] = MakeTexture("Acid", 128, (x, y, s) =>
            {
                float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                float bubbles = Mathf.PerlinNoise(x * 0.25f + 10f, y * 0.25f) > 0.72f ? 0.25f : 0f;
                return new Color(0.2f + bubbles, 0.75f + n * 0.25f, 0.15f + bubbles);
            });

            t["WallPanel"] = MakeTexture("WallPanel", 128, (x, y, s) =>
            {
                bool seam = x % 64 < 2 || y % 128 < 2;
                bool rivet = (x % 64 > 6 && x % 64 < 10 || x % 64 > 54 && x % 64 < 58) && (y % 32 > 14 && y % 32 < 18);
                float v = seam ? 0.6f : rivet ? 0.7f : 0.82f;
                return new Color(v * 0.95f, v, v * 1.02f);
            });

            t["Fur"] = MakeTexture("Fur", 64, (x, y, s) =>
            {
                float n = Mathf.PerlinNoise(x * 0.4f, y * 0.1f) * 0.25f;
                return new Color(0.85f + n * 0.3f, 0.85f + n * 0.3f, 0.85f + n * 0.3f);
            });

            t["CatStripes"] = MakeTexture("CatStripes", 128, (x, y, s) =>
            {
                float wave = Mathf.Sin(x * 0.25f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 4f);
                float v = wave > 0.55f ? 0.72f : 1f;
                return new Color(v, v, v);
            });

            return t;
        }

        private static Texture2D MakeTexture(string name, int size, Func<int, int, int, Color> pixel)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = pixel(x, y, size);
            tex.SetPixels(px);
            tex.Apply();

            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================
        //  МАТЕРИАЛЫ
        // =====================================================================

        private static void CreateMaterials(Dictionary<string, Texture2D> t)
        {
            // Окружение
            Mat("Floor_Tiles", Color.white, t["Tiles"], new Vector2(12, 18), 0.5f);
            Mat("Floor_Metal", new Color(0.85f, 0.9f, 0.9f), t["MetalFloor"], new Vector2(10, 18), 0.6f, 0.4f);
            Mat("Floor_LabBlue", new Color(0.8f, 0.9f, 1f), t["Tiles"], new Vector2(15, 25), 0.55f);
            Mat("Wall_Lab", new Color(0.88f, 0.92f, 0.95f), t["WallPanel"], new Vector2(6, 1.5f), 0.3f);
            Mat("Wall_Storage", new Color(0.55f, 0.6f, 0.58f), t["WallPanel"], new Vector2(6, 1.5f), 0.3f);
            Mat("Wood_Crate", Color.white, t["Wood"], Vector2.one, 0.15f);
            Mat("Metal_Steel", new Color(0.7f, 0.72f, 0.75f), null, Vector2.one, 0.8f, 0.9f);
            Mat("Metal_Dark", new Color(0.22f, 0.24f, 0.27f), null, Vector2.one, 0.5f, 0.6f);
            Mat("Plastic_White", new Color(0.95f, 0.95f, 0.95f), null, Vector2.one, 0.6f);
            Mat("Plastic_Blue", new Color(0.25f, 0.45f, 0.8f), null, Vector2.one, 0.5f);
            Mat("Book_Red", new Color(0.7f, 0.15f, 0.12f), null, Vector2.one, 0.2f);
            Mat("Book_Green", new Color(0.15f, 0.5f, 0.25f), null, Vector2.one, 0.2f);
            Mat("Hazard", Color.white, t["Hazard"], new Vector2(2, 1), 0.3f);
            Mat("Acid", Color.white, t["Acid"], new Vector2(6, 4), 0.9f, 0f, new Color(0.1f, 0.6f, 0.05f));
            Mat("Glue", new Color(1f, 0.95f, 0.55f), null, Vector2.one, 0.95f);
            Mat("Glass_Green", new Color(0.4f, 0.9f, 0.6f), null, Vector2.one, 0.95f, 0f, new Color(0.05f, 0.3f, 0.1f));
            Mat("Glass_Purple", new Color(0.7f, 0.4f, 0.95f), null, Vector2.one, 0.95f, 0f, new Color(0.25f, 0.05f, 0.35f));
            Mat("Laser_Red", new Color(1f, 0.1f, 0.1f), null, Vector2.one, 0.5f, 0f, new Color(3f, 0.1f, 0.1f));
            Mat("Glow_Green", new Color(0.3f, 1f, 0.4f), null, Vector2.one, 0.5f, 0f, new Color(0.6f, 2.5f, 0.8f));
            Mat("Glow_White", Color.white, null, Vector2.one, 0.5f, 0f, new Color(2.5f, 2.5f, 2.2f));
            Mat("Glow_Cyan", new Color(0.3f, 0.9f, 1f), null, Vector2.one, 0.5f, 0f, new Color(0.3f, 1.5f, 2f));
            Mat("Plate_Indicator", new Color(0.9f, 0.15f, 0.1f), null, Vector2.one, 0.5f, 0f, new Color(0.5f, 0.05f, 0.05f));

            // Персонажи
            Mat("Mouse_Fur", new Color(0.78f, 0.78f, 0.8f), t["Fur"], Vector2.one, 0.2f);
            Mat("Mouse_Pink", new Color(1f, 0.65f, 0.72f), null, Vector2.one, 0.3f);
            Mat("Eye_Black", new Color(0.03f, 0.03f, 0.03f), null, Vector2.one, 0.95f);
            Mat("Cat_Ginger", new Color(1f, 0.55f, 0.18f), t["CatStripes"], new Vector2(2, 2), 0.2f);
            Mat("Cat_Grey", new Color(0.5f, 0.5f, 0.55f), t["CatStripes"], new Vector2(2, 2), 0.2f);
            Mat("Cat_Eye", new Color(0.4f, 1f, 0.3f), null, Vector2.one, 0.9f, 0f, new Color(0.3f, 1.2f, 0.2f));

            // Предметы
            Mat("Cheese", Color.white, t["Cheese"], Vector2.one, 0.4f, 0f, new Color(0.25f, 0.18f, 0f));
            Mat("Bandage_White", new Color(0.97f, 0.97f, 0.95f), null, Vector2.one, 0.3f);
            Mat("Bandage_Red", new Color(0.9f, 0.1f, 0.1f), null, Vector2.one, 0.3f, 0f, new Color(0.4f, 0f, 0f));
            Mat("Serum_Speed", new Color(0.3f, 1f, 0.3f), null, Vector2.one, 0.9f, 0f, new Color(0.3f, 1.4f, 0.3f));
            Mat("Serum_Jump", new Color(0.3f, 0.6f, 1f), null, Vector2.one, 0.9f, 0f, new Color(0.3f, 0.6f, 1.6f));
            Mat("Serum_Coconut", new Color(1f, 0.97f, 0.9f), null, Vector2.one, 0.9f, 0f, new Color(0.9f, 0.85f, 0.7f));
            Mat("Coconut_Brown", new Color(0.4f, 0.25f, 0.12f), null, Vector2.one, 0.2f);
            Mat("Keycard_Blue", new Color(0.15f, 0.55f, 1f), null, Vector2.one, 0.8f, 0.2f, new Color(0.1f, 0.5f, 1.2f));
        }

        private static Shader LitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        private static Material Mat(string name, Color color, Texture2D tex, Vector2 tiling,
            float smoothness = 0.3f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = LitShader();
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            foreach (string texProp in new[] { "_BaseMap", "_MainTex" })
            {
                if (!m.HasProperty(texProp)) continue;
                m.SetTexture(texProp, tex);
                m.SetTextureScale(texProp, tiling);
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");

            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        private static Material M(string name) => Mats[name];

        // =====================================================================
        //  ПРЕФАБЫ
        // =====================================================================

        private static PrefabSet CreatePrefabs()
        {
            var p = new PrefabSet();
            p.Crumb = SavePrefab(BuildCrumb(), "CheeseCrumb");
            p.Cheese = SavePrefab(BuildCheese(), "Cheese");
            p.Bandage = SavePrefab(BuildBandage(), "Bandage");
            p.Keycard = SavePrefab(BuildKeycard(), "Keycard");
            p.Crate = SavePrefab(BuildCrate(), "Crate");
            p.Flask = SavePrefab(BuildFlask(), "Flask");

            // Шприцы: базовый префаб + два Prefab Variant
            p.SyringeSpeed = SavePrefab(BuildSyringe(), "Syringe_Speed");
            p.SyringeJump = SaveSyringeVariant(p.SyringeSpeed, "Syringe_Jump", BoostType.Jump, 10f,
                M("Serum_Jump"), new Color(0.3f, 0.6f, 1f), false);
            p.SyringeCoconut = SaveSyringeVariant(p.SyringeSpeed, "Syringe_Coconut", BoostType.Coconut, 6f,
                M("Serum_Coconut"), new Color(1f, 0.95f, 0.8f), true);

            // Кот + Prefab Variant «Толстый кот»
            p.Cat = SavePrefab(BuildCat(p.Cheese), "Cat");
            p.FatCat = SaveFatCatVariant(p.Cat);

            p.Mouse = SavePrefab(BuildMouse(p.Crumb), "Mouse");
            return p;
        }

        private static GameObject SavePrefab(GameObject temp, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static GameObject BuildMouse(GameObject crumbPrefab)
        {
            var root = new GameObject("Mouse") { tag = "Player" };
            var cc = root.AddComponent<CharacterController>();
            cc.height = 0.7f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.37f, 0f);
            cc.stepOffset = 0.25f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;

            Transform model = Group("Model", root.transform).transform;
            Material fur = M("Mouse_Fur"), pink = M("Mouse_Pink"), eye = M("Eye_Black");
            Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.45f, 0.75f), fur, false);
            Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 0.43f, 0.38f), new Vector3(0.36f, 0.34f, 0.42f), fur, false);
            Prim(PrimitiveType.Sphere, "Ear_L", model, new Vector3(-0.16f, 0.63f, 0.33f), new Vector3(0.22f, 0.24f, 0.06f), pink, false);
            Prim(PrimitiveType.Sphere, "Ear_R", model, new Vector3(0.16f, 0.63f, 0.33f), new Vector3(0.22f, 0.24f, 0.06f), pink, false);
            Prim(PrimitiveType.Sphere, "Nose", model, new Vector3(0f, 0.42f, 0.6f), Vector3.one * 0.07f, pink, false);
            Prim(PrimitiveType.Sphere, "Eye_L", model, new Vector3(-0.09f, 0.5f, 0.53f), Vector3.one * 0.06f, eye, false);
            Prim(PrimitiveType.Sphere, "Eye_R", model, new Vector3(0.09f, 0.5f, 0.53f), Vector3.one * 0.06f, eye, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.15f : 0.15f;
                float z = i < 2 ? 0.2f : -0.2f;
                Prim(PrimitiveType.Sphere, "Paw_" + i, model, new Vector3(x, 0.06f, z), new Vector3(0.12f, 0.08f, 0.14f), pink, false);
            }
            Transform tail = Group("TailPivot", model, new Vector3(0f, 0.25f, -0.35f)).transform;
            Prim(PrimitiveType.Cylinder, "Tail", tail, new Vector3(0f, 0.02f, -0.3f), new Vector3(0.04f, 0.3f, 0.04f), pink, false,
                new Vector3(80f, 0f, 0f));

            Transform firePoint = Group("FirePoint", root.transform, new Vector3(0f, 0.5f, 0f)).transform;

            var stats = root.AddComponent<PlayerStats>();
            var controller = root.AddComponent<PlayerController>();
            var shooter = root.AddComponent<PlayerShooter>();
            var bob = root.AddComponent<BobAnimator>();

            Set(controller, "model", model);
            Set(stats, "blinkRenderers", model.GetComponentsInChildren<Renderer>());
            Set(shooter, "crumbPrefab", crumbPrefab.GetComponent<CheeseCrumb>());
            Set(shooter, "firePoint", firePoint);
            Set(bob, "model", model);
            Set(bob, "tail", tail);
            return root;
        }

        private static GameObject BuildCat(GameObject cheesePrefab)
        {
            var root = new GameObject("Cat");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.15f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 0.6f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;
            cc.minMoveDistance = 0f;

            Transform model = Group("Model", root.transform).transform;
            Material fur = M("Cat_Ginger"), eye = M("Cat_Eye"), pink = M("Mouse_Pink"), dark = M("Eye_Black");
            Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.65f, -0.05f), new Vector3(0.75f, 0.7f, 1.35f), fur, false);
            Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 1.05f, 0.65f), new Vector3(0.68f, 0.62f, 0.62f), fur, false);
            Prim(PrimitiveType.Cube, "Ear_L", model, new Vector3(-0.2f, 1.36f, 0.62f), new Vector3(0.2f, 0.2f, 0.07f), fur, false,
                new Vector3(0f, 0f, 45f));
            Prim(PrimitiveType.Cube, "Ear_R", model, new Vector3(0.2f, 1.36f, 0.62f), new Vector3(0.2f, 0.2f, 0.07f), fur, false,
                new Vector3(0f, 0f, 45f));
            Prim(PrimitiveType.Sphere, "Eye_L", model, new Vector3(-0.14f, 1.12f, 0.93f), new Vector3(0.13f, 0.13f, 0.06f), eye, false);
            Prim(PrimitiveType.Sphere, "Eye_R", model, new Vector3(0.14f, 1.12f, 0.93f), new Vector3(0.13f, 0.13f, 0.06f), eye, false);
            Prim(PrimitiveType.Sphere, "Pupil_L", model, new Vector3(-0.14f, 1.12f, 0.96f), new Vector3(0.04f, 0.11f, 0.02f), dark, false);
            Prim(PrimitiveType.Sphere, "Pupil_R", model, new Vector3(0.14f, 1.12f, 0.96f), new Vector3(0.04f, 0.11f, 0.02f), dark, false);
            Prim(PrimitiveType.Sphere, "Nose", model, new Vector3(0f, 1.0f, 0.97f), new Vector3(0.09f, 0.06f, 0.05f), pink, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.22f : 0.22f;
                float z = i < 2 ? 0.38f : -0.45f;
                Prim(PrimitiveType.Cylinder, "Leg_" + i, model, new Vector3(x, 0.22f, z), new Vector3(0.16f, 0.22f, 0.16f), fur, false);
            }
            Transform tail = Group("TailPivot", model, new Vector3(0f, 0.8f, -0.7f)).transform;
            Prim(PrimitiveType.Cylinder, "Tail", tail, new Vector3(0f, 0.3f, -0.2f), new Vector3(0.11f, 0.42f, 0.11f), fur, false,
                new Vector3(-25f, 0f, 0f));

            var cat = root.AddComponent<CatEnemy>();
            var bob = root.AddComponent<BobAnimator>();
            Set(cat, "model", model);
            Set(cat, "dropOnDeath", cheesePrefab);
            Set(cat, "dropChance", 0.6f);
            Set(bob, "model", model);
            Set(bob, "tail", tail);
            Set(bob, "bobHeight", 0.08f);
            Set(bob, "bobFrequency", 10f);
            return root;
        }

        /// <summary>Prefab Variant: толстый серый кот — медленнее, сильнее, больше жизней.</summary>
        private static GameObject SaveFatCatVariant(GameObject catPrefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab);
            inst.name = "FatCat";
            inst.transform.localScale = Vector3.one * 1.35f;

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial == M("Cat_Ginger")) r.sharedMaterial = M("Cat_Grey");

            var cat = inst.GetComponent<CatEnemy>();
            Set(cat, "displayName", "Толстый кот");
            Set(cat, "maxHealth", 4);
            Set(cat, "patrolSpeed", 1.3f);
            Set(cat, "chaseSpeed", 3.3f);
            Set(cat, "attackDamage", 25f);
            Set(cat, "dropChance", 1f);

            string path = $"{PrefabDir}/FatCat.prefab";
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return variant;
        }

        private static GameObject BuildCheese()
        {
            GameObject root = PickupRoot("Cheese", 0.45f, out Transform visual);
            Prim(PrimitiveType.Cube, "Wedge", visual, Vector3.zero, new Vector3(0.5f, 0.3f, 0.36f), M("Cheese"), false,
                new Vector3(0f, 30f, 0f));
            Prim(PrimitiveType.Cube, "Wedge_Tip", visual, new Vector3(0.22f, -0.02f, 0.13f), new Vector3(0.25f, 0.26f, 0.2f),
                M("Cheese"), false, new Vector3(0f, 30f, 0f));
            Set(root.AddComponent<CheesePickup>(), "visual", visual);
            return root;
        }

        private static GameObject BuildBandage()
        {
            GameObject root = PickupRoot("Bandage", 0.45f, out Transform visual);
            Prim(PrimitiveType.Cube, "Box", visual, Vector3.zero, new Vector3(0.5f, 0.22f, 0.4f), M("Bandage_White"), false);
            Prim(PrimitiveType.Cube, "Cross_H", visual, new Vector3(0f, 0.115f, 0f), new Vector3(0.3f, 0.02f, 0.09f), M("Bandage_Red"), false);
            Prim(PrimitiveType.Cube, "Cross_V", visual, new Vector3(0f, 0.115f, 0f), new Vector3(0.09f, 0.02f, 0.3f), M("Bandage_Red"), false);
            Set(root.AddComponent<HealthPickup>(), "visual", visual);
            return root;
        }

        private static GameObject BuildSyringe()
        {
            GameObject root = PickupRoot("Syringe_Speed", 0.5f, out Transform visual);
            Transform tilt = Group("Tilt", visual, Vector3.zero, new Vector3(0f, 0f, 55f)).transform;
            Prim(PrimitiveType.Cylinder, "Barrel", tilt, Vector3.zero, new Vector3(0.14f, 0.22f, 0.14f), M("Serum_Speed"), false);
            Prim(PrimitiveType.Cylinder, "Needle", tilt, new Vector3(0f, -0.33f, 0f), new Vector3(0.025f, 0.12f, 0.025f), M("Metal_Steel"), false);
            Prim(PrimitiveType.Cylinder, "Flange", tilt, new Vector3(0f, 0.23f, 0f), new Vector3(0.24f, 0.015f, 0.24f), M("Metal_Steel"), false);
            Prim(PrimitiveType.Cylinder, "Plunger", tilt, new Vector3(0f, 0.34f, 0f), new Vector3(0.05f, 0.11f, 0.05f), M("Metal_Steel"), false);
            Prim(PrimitiveType.Cylinder, "Thumb", tilt, new Vector3(0f, 0.46f, 0f), new Vector3(0.14f, 0.015f, 0.14f), M("Metal_Steel"), false);

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(visual, false);
            glow.type = LightType.Point;
            glow.range = 2.2f;
            glow.intensity = 1.6f;
            glow.color = new Color(0.3f, 1f, 0.3f);

            var syringe = root.AddComponent<SyringePickup>();
            Set(syringe, "visual", visual);
            Set(syringe, "boostType", BoostType.Speed);
            Set(syringe, "duration", 8f);
            return root;
        }

        /// <summary>Prefab Variant шприца: меняются тип буста, цвет и свечение.</summary>
        private static GameObject SaveSyringeVariant(GameObject basePrefab, string name, BoostType type, float duration,
            Material serum, Color glowColor, bool coconutFlakes)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            inst.name = name;
            Transform barrel = inst.transform.Find("Visual/Tilt/Barrel");
            if (barrel != null) barrel.GetComponent<Renderer>().sharedMaterial = serum;
            Light glow = inst.GetComponentInChildren<Light>();
            if (glow != null) glow.color = glowColor;

            if (coconutFlakes && barrel != null)
            {
                // Кусочки кокоса внутри сыворотки
                Transform tilt = barrel.parent;
                Prim(PrimitiveType.Sphere, "CoconutFlake_A", tilt, new Vector3(0.02f, 0.08f, 0.03f), Vector3.one * 0.07f, M("Coconut_Brown"), false);
                Prim(PrimitiveType.Sphere, "CoconutFlake_B", tilt, new Vector3(-0.03f, -0.07f, -0.02f), Vector3.one * 0.06f, M("Coconut_Brown"), false);
                Prim(PrimitiveType.Sphere, "Coconut_Top", inst.transform.Find("Visual"), new Vector3(0f, 0.5f, 0f), Vector3.one * 0.16f, M("Coconut_Brown"), false);
            }

            var syringe = inst.GetComponent<SyringePickup>();
            Set(syringe, "boostType", type);
            Set(syringe, "duration", duration);

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(inst);
            return variant;
        }

        private static GameObject BuildKeycard()
        {
            GameObject root = PickupRoot("Keycard", 0.5f, out Transform visual);
            Prim(PrimitiveType.Cube, "Card", visual, Vector3.zero, new Vector3(0.55f, 0.35f, 0.04f), M("Keycard_Blue"), false);
            Prim(PrimitiveType.Cube, "Stripe", visual, new Vector3(0f, -0.08f, 0f), new Vector3(0.56f, 0.07f, 0.045f), M("Eye_Black"), false);
            Prim(PrimitiveType.Cube, "Chip", visual, new Vector3(-0.15f, 0.07f, 0f), new Vector3(0.1f, 0.08f, 0.05f), M("Cheese"), false);
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(visual, false);
            glow.type = LightType.Point;
            glow.range = 3f;
            glow.intensity = 2f;
            glow.color = new Color(0.3f, 0.8f, 1f);
            Set(root.AddComponent<KeycardPickup>(), "visual", visual);
            return root;
        }

        private static GameObject BuildCrate()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Crate";
            root.GetComponent<Renderer>().sharedMaterial = M("Wood_Crate");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<PhysicsCrate>();
            return root;
        }

        private static GameObject BuildFlask()
        {
            var root = new GameObject("Flask");
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.3f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Prim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.6f, M("Glass_Purple"), false);
            Prim(PrimitiveType.Cylinder, "Neck", root.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.16f, 0.18f, 0.16f), M("Glass_Purple"), false);
            Prim(PrimitiveType.Cylinder, "Cork", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.14f, 0.05f, 0.14f), M("Wood_Crate"), false);
            return root;
        }

        private static GameObject BuildCrumb()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "CheeseCrumb";
            root.transform.localScale = Vector3.one * 0.2f;
            root.GetComponent<Renderer>().sharedMaterial = M("Cheese");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<CheeseCrumb>();
            return root;
        }

        private static GameObject PickupRoot(string name, float radius, out Transform visual)
        {
            var root = new GameObject(name);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            col.center = new Vector3(0f, 0.4f, 0f);
            visual = Group("Visual", root.transform, new Vector3(0f, 0.4f, 0f)).transform;
            return root;
        }

        // =====================================================================
        //  СЦЕНА
        // =====================================================================

        private static void BuildScene(PrefabSet P)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Освещение и атмосфера ---
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.4f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.18f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 110f;

            Transform management = Group("--- MANAGEMENT ---").transform;
            Transform lighting = Group("--- LIGHTING ---").transform;
            Transform level = Group("--- LEVEL ---").transform;
            Transform enemies = Group("--- ENEMIES ---").transform;
            Transform spawners = Group("--- SPAWNERS ---").transform;
            Transform ui = Group("--- UI ---").transform;

            var gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            gameManager.transform.SetParent(management, false);

            Light sun = AddLight("Directional Light (Ceiling Lamps)", lighting, LightType.Directional, Vector3.zero,
                new Color(1f, 0.97f, 0.92f), 0.85f, 0f);
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.shadows = LightShadows.Soft;

            // --- Камера ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(management, false);
            camGo.transform.position = new Vector3(0f, 2f, -3f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camGo.AddComponent<AudioListener>();
            var tpc = camGo.AddComponent<ThirdPersonCamera>();

            // --- Игрок ---
            GameObject player = Place(P.Mouse, null, new Vector3(0f, 0.05f, 0f), 0f, "Player_Mouse");
            Set(player.GetComponent<PlayerController>(), "cameraTransform", camGo.transform);
            Set(player.GetComponent<PlayerShooter>(), "aimCamera", camGo.transform);
            Set(tpc, "target", player.transform);

            // --- Уровень ---
            BuildZone1(level, lighting, enemies, P);
            Spawner[] zone3Spawners = CreateZone3Spawners(spawners, P);
            BuildZone2(level, lighting, P);
            BuildZone3(level, lighting, enemies, P, zone3Spawners);

            // --- Интерфейс ---
            HUD hud = BuildUI(ui);
            Set(gameManager, "player", player.GetComponent<PlayerStats>());
            Set(gameManager, "hud", hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ---------------------------------------------------------------
        //  ЗОНА 1 — ВИВАРИЙ (обучение)
        // ---------------------------------------------------------------
        private static void BuildZone1(Transform level, Transform lighting, Transform enemies, PrefabSet P)
        {
            Transform zone = Group("Zone1_Vivarium", level).transform;
            Transform structure = Group("Structure", zone).transform;
            Transform props = Group("Props", zone).transform;
            Transform cages = Group("Cages", zone).transform;
            Transform pickups = Group("Pickups", zone).transform;
            Transform triggers = Group("Triggers", zone).transform;

            Block("Floor", structure, new Vector3(0f, -0.5f, 12.5f), new Vector3(24f, 1f, 35f), M("Floor_Tiles"));
            Block("Wall_West", structure, new Vector3(-12.5f, 3f, 12.5f), new Vector3(1f, 6f, 35f), M("Wall_Lab"));
            Block("Wall_East", structure, new Vector3(12.5f, 3f, 12.5f), new Vector3(1f, 6f, 35f), M("Wall_Lab"));
            Block("Wall_South", structure, new Vector3(0f, 3f, -5.5f), new Vector3(26f, 6f, 1f), M("Wall_Lab"));
            DividerWall(structure, "Wall_To_Storage", 30f, 12.5f, M("Wall_Lab"));

            // Клетки: в средней стоит мышь (открыта спереди)
            Cage(cages, "Cage_Start (открыта)", new Vector3(0f, 0f, 0f), true);
            Cage(cages, "Cage_Left", new Vector3(-4f, 0f, 0f), false);
            Cage(cages, "Cage_Right", new Vector3(4f, 0f, 0f), false);
            Cage(cages, "Cage_Top", new Vector3(-4f, 1.9f, 0f), false);
            Cage(cages, "Cage_Right_Top", new Vector3(4f, 1.9f, 0f), false);
            Block("Cage_Shelf", cages, new Vector3(0f, 0.95f, -1.8f), new Vector3(12f, 3.9f, 0.6f), M("Metal_Dark"));

            // Обучение прыжку: стопка книг
            Block("Books_Low", props, new Vector3(0f, 0.3f, 10f), new Vector3(6f, 0.6f, 1.2f), M("Book_Red"));
            Block("Books_High", props, new Vector3(-2.5f, 0.85f, 10f), new Vector3(1.5f, 0.5f, 1.2f), M("Book_Green"));
            // Ступенька на платформу с сыром
            Block("Book_Step", props, new Vector3(-7f, 0.3f, 17.5f), new Vector3(1.6f, 0.6f, 1.6f), M("Book_Green"));
            Block("Box_Platform", props, new Vector3(-7f, 0.6f, 20f), new Vector3(2.2f, 1.2f, 2.2f), M("Plastic_Blue"));

            // Декор
            Table(props, "Lab_Table_East", new Vector3(8f, 0f, 14f), new Vector3(5f, 0.25f, 2.6f), 2.3f);
            Block("Shelf_West", props, new Vector3(-11.3f, 1.5f, 13f), new Vector3(1.2f, 3f, 6f), M("Metal_Dark"));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Jar_" + i, props, new Vector3(-11.3f, 3.35f, 10.8f + i * 1.4f),
                    new Vector3(0.6f, 0.35f, 0.6f), M("Glass_Green"), true).isStatic = true;
            Prim(PrimitiveType.Cylinder, "Trash_Bin", props, new Vector3(10.5f, 0.6f, 25f), new Vector3(1.2f, 0.6f, 1.2f),
                M("Metal_Steel"), true).isStatic = true;
            Block("Feed_Sack", props, new Vector3(9.5f, 0.5f, 4f), new Vector3(2f, 1f, 1.4f), M("Bandage_White"));

            // Предметы
            Place(P.Cheese, pickups, new Vector3(0f, 0f, 15.5f), 0f, "Cheese_First");
            Place(P.Cheese, pickups, new Vector3(-7f, 1.2f, 20f), 0f, "Cheese_OnBox");
            Place(P.Bandage, pickups, new Vector3(8f, 0f, 14f), 0f, "Bandage_UnderTable");

            // Тренировочный «сонный котёнок»
            GameObject kitten = Place(P.Cat, enemies, new Vector3(4f, 0f, 24f), 200f, "SleepyKitten (тренировка)");
            kitten.transform.localScale = Vector3.one * 0.7f;
            var k = kitten.GetComponent<CatEnemy>();
            Set(k, "displayName", "Сонный котёнок");
            Set(k, "maxHealth", 1);
            Set(k, "detectionRadius", 3.5f);
            Set(k, "loseRadius", 7f);
            Set(k, "chaseSpeed", 2.3f);
            Set(k, "patrolSpeed", 0.8f);
            Set(k, "attackDamage", 5f);
            Set(k, "dropChance", 1f);

            // Подсказки-триггеры
            Hint(triggers, "Hint_Run", new Vector3(0f, 1f, 5f), new Vector3(24f, 2f, 1f), "Виварий",
                "Удерживай Shift — бег. Но бег быстрее расходует сытость!");
            Hint(triggers, "Hint_Jump", new Vector3(0f, 1f, 8.5f), new Vector3(24f, 2f, 1f), null,
                "Space — прыжок. Запрыгни на книги!");
            Hint(triggers, "Hint_Cheese", new Vector3(0f, 1f, 13.5f), new Vector3(24f, 2f, 1f), null,
                "Сыр — это еда. Следи за полоской сытости: без еды мышь теряет здоровье.");
            Hint(triggers, "Hint_Kitten", new Vector3(0f, 1f, 21.5f), new Vector3(24f, 2f, 1f), null,
                "Котёнок! ЛКМ или F — бросить крошку сыра (тратит сытость). Ещё можно прыгнуть коту на голову.");

            AddLight("Light_Vivarium_A", lighting, LightType.Point, new Vector3(0f, 5f, 4f), new Color(1f, 0.9f, 0.75f), 2f, 16f);
            AddLight("Light_Vivarium_B", lighting, LightType.Point, new Vector3(0f, 5f, 20f), new Color(1f, 0.9f, 0.75f), 2f, 16f);
        }

        // ---------------------------------------------------------------
        //  ЗОНА 2 — СКЛАД РЕАКТИВОВ (преодоление уровня)
        // ---------------------------------------------------------------
        private static void BuildZone2(Transform level, Transform lighting, PrefabSet P)
        {
            Transform zone = Group("Zone2_Storage", level).transform;
            Transform structure = Group("Structure", zone).transform;
            Transform corridor = Group("NarrowCorridor", zone).transform;
            Transform acid = Group("AcidPool", zone).transform;
            Transform puzzle = Group("GatePuzzle", zone).transform;
            Transform physics = Group("PhysicsObjects", zone).transform;
            Transform props = Group("Props", zone).transform;
            Transform pickups = Group("Pickups", zone).transform;
            Transform triggers = Group("Triggers", zone).transform;

            Block("Floor", structure, new Vector3(0f, -0.5f, 52.5f), new Vector3(24f, 1f, 45f), M("Floor_Metal"));
            Block("Wall_West", structure, new Vector3(-12.5f, 3f, 52.5f), new Vector3(1f, 6f, 45f), M("Wall_Storage"));
            Block("Wall_East", structure, new Vector3(12.5f, 3f, 52.5f), new Vector3(1f, 6f, 45f), M("Wall_Storage"));

            // Узкий проход между стеллажами
            Block("Shelving_West", corridor, new Vector3(-6.5f, 2f, 35.25f), new Vector3(11f, 4f, 9.5f), M("Metal_Dark"));
            Block("Shelving_East", corridor, new Vector3(6.5f, 2f, 35.25f), new Vector3(11f, 4f, 9.5f), M("Metal_Dark"));
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Block("Shelf_Box_" + i, corridor, new Vector3(side * (3f + i), 4.4f, 32f + i * 1.3f), Vector3.one * 0.8f, M("Wood_Crate"));
            }

            // Ловушка: падающие ящики
            var falling = new List<Rigidbody>();
            Vector3[] crateSpots = { new Vector3(0f, 5f, 37.2f), new Vector3(0.2f, 6.3f, 38.1f), new Vector3(-0.2f, 7.6f, 39f) };
            for (int i = 0; i < crateSpots.Length; i++)
            {
                GameObject c = Place(P.Crate, corridor, crateSpots[i], i * 20f, "FallingCrate_" + i);
                c.transform.localScale = Vector3.one * 0.85f;
                var rb = c.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                falling.Add(rb);
            }
            GameObject trap = TriggerBox("Trap_FallingCrates", corridor, new Vector3(0f, 1f, 35f), new Vector3(2f, 2f, 1f));
            Set(trap.AddComponent<FallingObjectsTrap>(), "objects", falling.ToArray());

            // Кислотный бассейн с платформами
            GameObject pool = Prim(PrimitiveType.Cube, "Acid_Surface", acid, new Vector3(0f, 0.03f, 52f),
                new Vector3(24f, 0.06f, 16f), M("Acid"), false);
            pool.isStatic = true;
            GameObject acidZone = TriggerBox("Acid_DamageZone", acid, new Vector3(0f, 0.35f, 52f), new Vector3(24f, 0.7f, 16f));
            var acidDz = acidZone.AddComponent<DamageZone>();
            Set(acidDz, "mode", DamageZone.DamageMode.Continuous);
            Set(acidDz, "damage", 35f);
            Set(acidDz, "deathMessage", "Мышь упала в кислоту...");

            Block("Platform_1", acid, new Vector3(0f, 0.4f, 45.8f), new Vector3(2f, 0.8f, 2f), M("Hazard"));
            Block("Platform_2", acid, new Vector3(-2.2f, 0.7f, 48.9f), new Vector3(2f, 1.4f, 2f), M("Hazard"));
            Block("Platform_3", acid, new Vector3(0.5f, 0.7f, 52f), new Vector3(2f, 1.4f, 2f), M("Hazard"));
            GameObject mover = Prim(PrimitiveType.Cube, "MovingPlatform", acid, new Vector3(0.5f, 1.2f, 54.8f),
                new Vector3(2.2f, 0.4f, 2.2f), M("Metal_Steel"), true);
            var mp = mover.AddComponent<MovingPlatform>();
            Set(mp, "moveOffset", new Vector3(0f, 0f, 3.8f));
            Set(mp, "speed", 1.4f);
            Place(P.Cheese, pickups, new Vector3(-2.2f, 1.4f, 48.9f), 0f, "Cheese_Platform");

            // Головоломка с воротами
            Transform gateFrame = Group("Gate", puzzle).transform;
            DividerWall(structure, "Wall_To_MainLab", 75f, 15.5f, M("Wall_Storage"));
            GameObject gate = Block("Gate_Door (открывается плитой)", gateFrame, new Vector3(0f, 1.3f, 75f),
                new Vector3(3f, 2.6f, 0.6f), M("Hazard"), false);
            var gateDoor = gate.AddComponent<Door>();
            Set(gateDoor, "openOffset", new Vector3(0f, 2.7f, 0f));

            Block("PressurePlate_Base", puzzle, new Vector3(5f, 0.05f, 70f), new Vector3(2.2f, 0.1f, 2.2f), M("Hazard"));
            GameObject plateTop = Prim(PrimitiveType.Cube, "PressurePlate_Button", puzzle, new Vector3(5f, 0.13f, 70f),
                new Vector3(1.6f, 0.08f, 1.6f), M("Plate_Indicator"), false);
            GameObject plateTrigger = TriggerBox("PressurePlate_Trigger", puzzle, new Vector3(5f, 0.5f, 70f), new Vector3(1.6f, 1f, 1.6f));
            var plate = plateTrigger.AddComponent<PressurePlate>();
            Set(plate, "doors", new[] { gateDoor });
            Set(plate, "indicator", plateTop.GetComponent<Renderer>());

            // Физические объекты
            Place(P.Crate, physics, new Vector3(5f, 0.5f, 65.5f), 0f, "Crate_ForPlate");
            Place(P.Crate, physics, new Vector3(-3f, 0.5f, 68f), 15f, "Crate_B");
            Place(P.Crate, physics, new Vector3(-8f, 0.5f, 71f), 40f, "Crate_C");
            Place(P.Crate, physics, new Vector3(-8f, 1.5f, 71f), 10f, "Crate_C_Top");
            Place(P.Flask, physics, new Vector3(3f, 0f, 42f), 0f, "Flask_A");
            Place(P.Flask, physics, new Vector3(-4f, 0f, 41.5f), 0f, "Flask_B");
            Place(P.Flask, physics, new Vector3(8f, 0f, 62f), 0f, "Flask_C");

            // Лужа клея (замедление)
            Prim(PrimitiveType.Cube, "Glue_Puddle", puzzle, new Vector3(-4f, 0.02f, 63.5f), new Vector3(4.5f, 0.04f, 3f),
                M("Glue"), false).isStatic = true;
            GameObject glue = TriggerBox("Glue_SlowZone", puzzle, new Vector3(-4f, 0.4f, 63.5f), new Vector3(4.5f, 0.8f, 3f));
            Set(glue.AddComponent<SlowZone>(), "speedMultiplier", 0.4f);

            // Декор
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Barrel_" + i, props, new Vector3(10.5f, 0.8f, 61f + i * 1.6f),
                    new Vector3(1.2f, 0.8f, 1.2f), i % 2 == 0 ? M("Plastic_Blue") : M("Hazard"), true).isStatic = true;
            Prim(PrimitiveType.Cylinder, "Pipe_West", props, new Vector3(-11.6f, 4.5f, 52f), new Vector3(0.5f, 22f, 0.5f),
                M("Metal_Steel"), false, new Vector3(90f, 0f, 0f)).isStatic = true;
            Prim(PrimitiveType.Cylinder, "Pipe_East", props, new Vector3(11.6f, 3.8f, 52f), new Vector3(0.4f, 22f, 0.4f),
                M("Metal_Steel"), false, new Vector3(90f, 0f, 0f)).isStatic = true;

            Place(P.Bandage, pickups, new Vector3(-4f, 0f, 66.5f), 0f, "Bandage_BehindGlue");
            Place(P.SyringeSpeed, pickups, new Vector3(-8f, 0f, 73f), 0f, "Syringe_Speed_Storage");
            Place(P.Cheese, pickups, new Vector3(8f, 0f, 72f), 0f, "Cheese_Storage");

            Hint(triggers, "Hint_Storage", new Vector3(0f, 1f, 31f), new Vector3(3f, 2f, 1f), "Склад реактивов",
                "Узкий проход между стеллажами. Сверху что-то шатается — не мешкай!");
            Hint(triggers, "Hint_Acid", new Vector3(0f, 1f, 42.5f), new Vector3(24f, 2f, 1f), null,
                "Кислота! Прыгай по платформам и не касайся зелёной жижи.");
            Hint(triggers, "Hint_Gate", new Vector3(0f, 1f, 61f), new Vector3(24f, 2f, 1f), null,
                "Ворота закрыты. Кнопка слишком тугая для мыши — толкни на неё ящик!");

            AddLight("Light_Storage_A", lighting, LightType.Point, new Vector3(0f, 5f, 38f), new Color(0.85f, 1f, 0.85f), 1.8f, 15f);
            AddLight("Light_Acid_Glow", lighting, LightType.Point, new Vector3(0f, 1.5f, 52f), new Color(0.3f, 1f, 0.3f), 2.5f, 14f);
            AddLight("Light_Storage_B", lighting, LightType.Point, new Vector3(0f, 5f, 67f), new Color(0.95f, 1f, 0.9f), 2f, 16f);
        }

        // ---------------------------------------------------------------
        //  ЗОНА 3 — ГЛАВНАЯ ЛАБОРАТОРИЯ (испытание + финал)
        // ---------------------------------------------------------------
        private static Spawner[] CreateZone3Spawners(Transform spawners, PrefabSet P)
        {
            // Спавнер котов
            Transform catSp = Group("CatSpawner", spawners).transform;
            var catPoints = new[]
            {
                Group("SpawnPoint_VentWest_A", catSp, new Vector3(-13.5f, 0.05f, 95f)).transform,
                Group("SpawnPoint_VentEast_A", catSp, new Vector3(13.5f, 0.05f, 95f)).transform,
                Group("SpawnPoint_VentWest_B", catSp, new Vector3(-13.5f, 0.05f, 104f)).transform,
                Group("SpawnPoint_VentEast_B", catSp, new Vector3(13.5f, 0.05f, 104f)).transform,
            };
            Transform patrol = Group("SpawnedCats_Patrol", catSp).transform;
            var patrolPoints = new List<Transform>
            {
                Group("P1", patrol, new Vector3(-5f, 0f, 92f)).transform,
                Group("P2", patrol, new Vector3(5f, 0f, 92f)).transform,
                Group("P3", patrol, new Vector3(5f, 0f, 102f)).transform,
                Group("P4", patrol, new Vector3(-5f, 0f, 102f)).transform,
            };
            var catSpawner = catSp.gameObject.AddComponent<Spawner>();
            Set(catSpawner, "prefabs", new[] { P.Cat, P.Cat, P.FatCat });
            Set(catSpawner, "spawnPoints", catPoints);
            Set(catSpawner, "spawnInterval", 10f);
            Set(catSpawner, "firstSpawnDelay", 8f);
            Set(catSpawner, "maxAlive", 2);
            Set(catSpawner, "totalLimit", 5);
            Set(catSpawner, "activeOnStart", false);
            Set(catSpawner, "enemyPatrolPoints", patrolPoints);

            // Спавнер бонусов
            Transform bonusSp = Group("BonusSpawner", spawners).transform;
            var bonusPoints = new[]
            {
                Group("BonusPoint_A", bonusSp, new Vector3(-4f, 0f, 89f)).transform,
                Group("BonusPoint_B", bonusSp, new Vector3(4f, 0f, 89f)).transform,
                Group("BonusPoint_C", bonusSp, new Vector3(-6f, 0f, 107f)).transform,
                Group("BonusPoint_D", bonusSp, new Vector3(6f, 0f, 107f)).transform,
                Group("BonusPoint_E", bonusSp, new Vector3(0f, 0f, 112f)).transform,
            };
            var bonusSpawner = bonusSp.gameObject.AddComponent<Spawner>();
            Set(bonusSpawner, "prefabs", new[] { P.Cheese, P.Cheese, P.Cheese, P.Bandage, P.SyringeSpeed, P.SyringeJump, P.SyringeCoconut });
            Set(bonusSpawner, "spawnPoints", bonusPoints);
            Set(bonusSpawner, "spawnInterval", 9f);
            Set(bonusSpawner, "firstSpawnDelay", 2f);
            Set(bonusSpawner, "maxAlive", 3);
            Set(bonusSpawner, "totalLimit", 0);
            Set(bonusSpawner, "activeOnStart", false);

            return new[] { catSpawner, bonusSpawner };
        }

        private static void BuildZone3(Transform level, Transform lighting, Transform enemies, PrefabSet P, Spawner[] spawners)
        {
            Transform zone = Group("Zone3_MainLab", level).transform;
            Transform structure = Group("Structure", zone).transform;
            Transform lasers = Group("LaserGrid", zone).transform;
            Transform furniture = Group("Furniture", zone).transform;
            Transform physics = Group("PhysicsObjects", zone).transform;
            Transform pickups = Group("Pickups", zone).transform;
            Transform exit = Group("Exit_Vent", zone).transform;
            Transform triggers = Group("Triggers", zone).transform;
            Transform waypoints = Group("Waypoints", zone).transform;

            Block("Floor", structure, new Vector3(0f, -0.5f, 100f), new Vector3(30f, 1f, 50f), M("Floor_LabBlue"));
            Block("Wall_West", structure, new Vector3(-15.5f, 3f, 100f), new Vector3(1f, 6f, 50f), M("Wall_Lab"));
            Block("Wall_East", structure, new Vector3(15.5f, 3f, 100f), new Vector3(1f, 6f, 50f), M("Wall_Lab"));
            // Северная стена с проёмом под вентиляционную дверь
            DividerWall(structure, "Wall_North_Exit", 125.5f, 15.5f, M("Wall_Lab"));

            // Вентиляционные отверстия (визуально — откуда лезут коты)
            foreach (float z in new[] { 95f, 104f })
            {
                Block("Vent_West_" + z, structure, new Vector3(-14.95f, 0.7f, z), new Vector3(0.1f, 1.4f, 1.8f), M("Eye_Black"), true, false);
                Block("Vent_East_" + z, structure, new Vector3(14.95f, 0.7f, z), new Vector3(0.1f, 1.4f, 1.8f), M("Eye_Black"), true, false);
            }

            // Лазерная решётка
            float[] laserZ = { 80.5f, 83f, 85.5f };
            for (int i = 0; i < laserZ.Length; i++)
            {
                Transform laser = Group("Laser_" + (i + 1), lasers, new Vector3(0f, 0.35f, laserZ[i])).transform;
                GameObject beam = Prim(PrimitiveType.Cylinder, "Beam", laser, Vector3.zero, new Vector3(0.07f, 15f, 0.07f),
                    M("Laser_Red"), false, new Vector3(0f, 0f, 90f));
                Block("Emitter_West", laser, new Vector3(-14.7f, 0f, 0f), new Vector3(0.6f, 0.6f, 0.6f), M("Metal_Dark"), true, false);
                Block("Emitter_East", laser, new Vector3(14.7f, 0f, 0f), new Vector3(0.6f, 0.6f, 0.6f), M("Metal_Dark"), true, false);
                GameObject hit = TriggerBox("Laser_DamageZone", laser, Vector3.zero, new Vector3(30f, 0.35f, 0.3f));
                var dz = hit.AddComponent<DamageZone>();
                Set(dz, "mode", DamageZone.DamageMode.Hit);
                Set(dz, "damage", 20f);
                Set(dz, "deathMessage", "Мышь поджарилась на лазере...");
                Set(dz, "blinking", i != 0);
                Set(dz, "onTime", 1.6f);
                Set(dz, "offTime", 1.3f);
                Set(dz, "phaseOffset", i * 1.1f);
                Set(dz, "visuals", new[] { beam.GetComponent<Renderer>() });
            }

            // Лабораторные столы
            Table(furniture, "LabTable_West_A", new Vector3(-9f, 0f, 92f), new Vector3(5f, 0.25f, 2.6f), 1.2f);
            Table(furniture, "LabTable_East_A", new Vector3(9f, 0f, 92f), new Vector3(5f, 0.25f, 2.6f), 1.2f);
            Table(furniture, "LabTable_West_B", new Vector3(-9f, 0f, 104f), new Vector3(5f, 0.25f, 2.6f), 1.2f);
            Table(furniture, "LabTable_East_B", new Vector3(9f, 0f, 104f), new Vector3(5f, 0.25f, 2.6f), 1.2f);
            // Табуретки — ступеньки, чтобы запрыгнуть на столы
            Block("Stool_West_A", furniture, new Vector3(-9f, 0.35f, 89.8f), new Vector3(0.9f, 0.7f, 0.9f), M("Plastic_Blue"));
            Block("Stool_East_A", furniture, new Vector3(9f, 0.35f, 89.8f), new Vector3(0.9f, 0.7f, 0.9f), M("Plastic_Blue"));
            Block("Stool_West_B", furniture, new Vector3(-9f, 0.35f, 101.8f), new Vector3(0.9f, 0.7f, 0.9f), M("Plastic_Blue"));
            Block("Stool_East_B", furniture, new Vector3(9f, 0.35f, 101.8f), new Vector3(0.9f, 0.7f, 0.9f), M("Plastic_Blue"));

            // Центральная установка (укрытие)
            Prim(PrimitiveType.Cylinder, "Experiment_Machine", furniture, new Vector3(0f, 1.5f, 97f), new Vector3(3f, 1.5f, 3f),
                M("Metal_Steel"), true).isStatic = true;
            Prim(PrimitiveType.Cylinder, "Machine_GlowRing", furniture, new Vector3(0f, 2.2f, 97f), new Vector3(3.2f, 0.1f, 3.2f),
                M("Glow_Cyan"), false).isStatic = true;
            Prim(PrimitiveType.Sphere, "Machine_Dome", furniture, new Vector3(0f, 3f, 97f), new Vector3(2.4f, 1.2f, 2.4f),
                M("Glass_Green"), false).isStatic = true;
            Block("Cabinet_West", furniture, new Vector3(-14.4f, 1.5f, 115f), new Vector3(1.2f, 3f, 4f), M("Plastic_White"));
            Block("Cabinet_East", furniture, new Vector3(14.4f, 1.5f, 115f), new Vector3(1.2f, 3f, 4f), M("Plastic_White"));

            // Физика
            Place(P.Flask, physics, new Vector3(-9f, 1.45f, 92.5f), 0f, "Flask_Table_A");
            Place(P.Flask, physics, new Vector3(9f, 1.45f, 104.5f), 0f, "Flask_Table_B");
            Place(P.Crate, physics, new Vector3(-6f, 0.5f, 114f), 0f, "Crate_Arena_A");
            Place(P.Crate, physics, new Vector3(6f, 0.5f, 114f), 0f, "Crate_Arena_B");
            Place(P.Crate, physics, new Vector3(6f, 1.5f, 114f), 0f, "Crate_Arena_B_Top");

            // Бонусы на столах
            Place(P.SyringeJump, pickups, new Vector3(-9f, 1.45f, 91.5f), 0f, "Syringe_Jump_OnTable");
            Place(P.SyringeCoconut, pickups, new Vector3(9f, 1.45f, 103.5f), 0f, "Syringe_Coconut_OnTable");
            Place(P.Cheese, pickups, new Vector3(-9f, 1.45f, 104f), 0f, "Cheese_Table");
            Place(P.Cheese, pickups, new Vector3(9f, 1.45f, 92f), 0f, "Cheese_Table_B");
            Place(P.Bandage, pickups, new Vector3(0f, 0f, 108f), 0f, "Bandage_BeforeBoss");

            // Патрульные коты
            List<Transform> westRoute = Route(waypoints, "Route_West",
                new Vector3(-6f, 0f, 88f), new Vector3(-6f, 0f, 101f), new Vector3(-2.5f, 0f, 101f), new Vector3(-2.5f, 0f, 88f));
            List<Transform> eastRoute = Route(waypoints, "Route_East",
                new Vector3(6f, 0f, 101f), new Vector3(6f, 0f, 88f), new Vector3(2.5f, 0f, 88f), new Vector3(2.5f, 0f, 101f));
            List<Transform> bossRoute = Route(waypoints, "Route_Boss",
                new Vector3(-4f, 0f, 117f), new Vector3(4f, 0f, 117f), new Vector3(4f, 0f, 121f), new Vector3(-4f, 0f, 121f));

            GameObject catW = Place(P.Cat, enemies, new Vector3(-6f, 0f, 88f), 0f, "Cat_Guard_West");
            Set(catW.GetComponent<CatEnemy>(), "waypoints", westRoute);
            GameObject catE = Place(P.Cat, enemies, new Vector3(6f, 0f, 101f), 180f, "Cat_Guard_East");
            Set(catE.GetComponent<CatEnemy>(), "waypoints", eastRoute);

            // Босс — экземпляр Prefab Variant «FatCat» с переопределёнными параметрами
            GameObject boss = Place(P.FatCat, enemies, new Vector3(0f, 0f, 119f), 180f, "Boss_Barsik");
            boss.transform.localScale = Vector3.one * 1.9f;
            var bc = boss.GetComponent<CatEnemy>();
            Set(bc, "displayName", "Барсик");
            Set(bc, "isBoss", true);
            Set(bc, "maxHealth", 6);
            Set(bc, "detectionRadius", 9f);
            Set(bc, "loseRadius", 18f);
            Set(bc, "chaseSpeed", 3.8f);
            Set(bc, "attackDamage", 30f);
            Set(bc, "attackRange", 1.4f);
            Set(bc, "stunDuration", 2f);
            Set(bc, "dropOnDeath", P.Keycard);
            Set(bc, "dropChance", 1f);
            Set(bc, "waypoints", bossRoute);

            // Выход: вентиляционная дверь с ключ-картой
            GameObject vent = Block("VentDoor (нужна ключ-карта)", exit, new Vector3(0f, 1.3f, 125.5f),
                new Vector3(3f, 2.6f, 0.8f), M("Hazard"), false);
            var ventDoor = vent.AddComponent<Door>();
            Set(ventDoor, "requiresKeycard", true);
            Set(ventDoor, "openOffset", new Vector3(0f, 2.7f, 0f));
            Block("Exit_Sign", exit, new Vector3(0f, 3.3f, 124.95f), new Vector3(2f, 0.6f, 0.1f), M("Glow_Green"), true, false);
            Block("Tunnel_Floor", exit, new Vector3(0f, -0.5f, 129f), new Vector3(4f, 1f, 6f), M("Metal_Dark"));
            Block("Tunnel_Wall_W", exit, new Vector3(-2f, 1.5f, 129f), new Vector3(0.5f, 3f, 6f), M("Metal_Dark"));
            Block("Tunnel_Wall_E", exit, new Vector3(2f, 1.5f, 129f), new Vector3(0.5f, 3f, 6f), M("Metal_Dark"));
            Block("Tunnel_Roof", exit, new Vector3(0f, 3.1f, 129f), new Vector3(4.5f, 0.3f, 6f), M("Metal_Dark"));
            Block("Freedom_Light", exit, new Vector3(0f, 1.5f, 132f), new Vector3(3.5f, 3f, 0.3f), M("Glow_White"));
            GameObject exitZone = TriggerBox("ExitZone (финиш)", exit, new Vector3(0f, 1f, 129.5f), new Vector3(3f, 2f, 2f));
            exitZone.AddComponent<ExitZone>();

            // Подсказки
            GameObject entry = Hint(triggers, "Zone_MainLab_Entry", new Vector3(0f, 1f, 77f), new Vector3(3f, 2f, 1f),
                "Главная лаборатория",
                "Коты-охранники! Ключ-карту от выхода носит толстый кот Барсик в конце зала.");
            Set(entry.GetComponent<ZoneTrigger>(), "spawnersToActivate", spawners);
            Hint(triggers, "Hint_Lasers", new Vector3(0f, 1f, 79f), new Vector3(30f, 2f, 1f), null,
                "Лазеры! Перепрыгивай красные лучи или дождись, пока они погаснут.");
            Hint(triggers, "Hint_Boss", new Vector3(0f, 1f, 110f), new Vector3(30f, 2f, 1f), "Логово Барсика",
                "Барсик охраняет выход! Бросай сыр или прыгай ему на голову. Кокосовый щит отбрасывает котов.");

            AddLight("Light_Lasers", lighting, LightType.Point, new Vector3(0f, 1.5f, 83f), new Color(1f, 0.2f, 0.2f), 2f, 10f);
            AddLight("Light_Lab_A", lighting, LightType.Point, new Vector3(-7f, 5f, 92f), new Color(0.8f, 0.9f, 1f), 2f, 15f);
            AddLight("Light_Lab_B", lighting, LightType.Point, new Vector3(7f, 5f, 92f), new Color(0.8f, 0.9f, 1f), 2f, 15f);
            AddLight("Light_Lab_C", lighting, LightType.Point, new Vector3(-7f, 5f, 106f), new Color(0.8f, 0.9f, 1f), 2f, 15f);
            AddLight("Light_Lab_D", lighting, LightType.Point, new Vector3(7f, 5f, 106f), new Color(0.8f, 0.9f, 1f), 2f, 15f);
            AddLight("Light_Boss", lighting, LightType.Point, new Vector3(0f, 4f, 118f), new Color(1f, 0.6f, 0.3f), 2.5f, 14f);
            AddLight("Light_Exit", lighting, LightType.Point, new Vector3(0f, 2.5f, 128f), new Color(0.4f, 1f, 0.5f), 3f, 10f);
        }

        // =====================================================================
        //  ИНТЕРФЕЙС
        // =====================================================================

        private static HUD BuildUI(Transform parent)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<HUD>();

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(parent, false);

            Transform c = canvasGo.transform;
            Color yellow = new Color(1f, 0.82f, 0.25f);

            // ---------- Игровой HUD ----------
            var gameplay = Stretch("GameplayHUD", c, new Color(0f, 0f, 0f, 0f));
            gameplay.raycastTarget = false;
            Transform g = gameplay.transform;

            RectTransform healthFill = Bar("HealthBar", g, new Vector2(30f, -30f), new Color(0.9f, 0.2f, 0.25f), out Text healthText);
            RectTransform foodFill = Bar("FoodBar", g, new Vector2(30f, -76f), new Color(1f, 0.78f, 0.15f), out Text foodText);
            Text effects = Label("EffectsText", g, "", 22, new Vector2(0f, 1f), new Vector2(30f, -122f), new Vector2(520f, 200f),
                TextAnchor.UpperLeft, Color.white);

            Image infoBg = Img("InfoPanel", g, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(360f, 168f),
                new Color(0f, 0f, 0f, 0.45f));
            Transform info = infoBg.transform;
            Text cheese = Label("CheeseText", info, "Сыр: 0", 26, new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(330f, 36f),
                TextAnchor.MiddleLeft, yellow);
            Text cats = Label("CatsText", info, "Обезврежено котов: 0", 22, new Vector2(0f, 1f), new Vector2(18f, -48f), new Vector2(330f, 34f),
                TextAnchor.MiddleLeft, Color.white);
            Text time = Label("TimeText", info, "Время: 00:00", 22, new Vector2(0f, 1f), new Vector2(18f, -84f), new Vector2(330f, 34f),
                TextAnchor.MiddleLeft, Color.white);
            Text keycard = Label("KeycardText", info, "Ключ-карта: нет", 22, new Vector2(0f, 1f), new Vector2(18f, -120f), new Vector2(330f, 34f),
                TextAnchor.MiddleLeft, Color.white);

            Text zone = Label("ZoneText", g, "Виварий", 30, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 42f),
                TextAnchor.MiddleCenter, yellow, FontStyle.Bold);
            Text objective = Label("ObjectiveText", g, "", 22, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1000f, 60f),
                TextAnchor.UpperCenter, Color.white);
            Text message = Label("MessageText", g, "", 30, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1400f, 120f),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Label("Crosshair", g, "+", 34, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f),
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
            Label("ControlsReminder", g, "WASD — движение  •  Shift — бег  •  Space — прыжок  •  ЛКМ/F — бросок сыра  •  Esc — пауза",
                18, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 30f), TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.65f));

            // ---------- Стартовый экран ----------
            var start = Stretch("StartPanel", c, new Color(0.05f, 0.06f, 0.09f, 0.94f));
            Transform s = start.transform;
            Label("Title", s, "МЫШИНЫЙ ПОБЕГ", 96, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 120f),
                TextAnchor.MiddleCenter, yellow, FontStyle.Bold);
            Label("Story", s,
                "Лабораторная мышь №17 получила кокосовую сыворотку и стала слишком умной для клетки.\n" +
                "Помоги ей пробраться через виварий, склад реактивов и главную лабораторию — и сбежать на свободу!",
                28, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1500f, 100f), TextAnchor.MiddleCenter, Color.white);
            Label("Controls", s,
                "<b><color=#FFD140>УПРАВЛЕНИЕ</color></b>\n" +
                "WASD / стрелки — движение\n" +
                "Shift — бег (быстрее тратит сытость)\n" +
                "Space — прыжок\n" +
                "Мышь — камера, колёсико — приближение\n" +
                "ЛКМ или F — бросить крошку сыра\n" +
                "Прыжок на голову кота — оглушить\n" +
                "Esc — пауза,  R — начать заново",
                26, new Vector2(0.5f, 0.5f), new Vector2(-380f, -20f), new Vector2(680f, 420f), TextAnchor.UpperLeft, Color.white);
            Label("Rules", s,
                "<b><color=#FFD140>ЦЕЛЬ</color></b>\n" +
                "Победи кота-охранника Барсика, забери ключ-карту и выберись через вентиляцию.\n\n" +
                "<b><color=#FFD140>СТАТЫ</color></b>\n" +
                "Здоровье — коты, лазеры, кислота. Сытость падает со временем, сыр её восполняет и служит снарядами.\n\n" +
                "<b><color=#FFD140>ШПРИЦЫ</color></b>\n" +
                "<color=#6CFF6C>Зелёный</color> — скорость, <color=#6CC8FF>синий</color> — двойной прыжок, " +
                "<color=#FFF2C8>кокосовый</color> — лечение и щит.",
                24, new Vector2(0.5f, 0.5f), new Vector2(380f, -20f), new Vector2(680f, 420f), TextAnchor.UpperLeft, Color.white);
            Button startBtn = MakeButton("StartButton", s, "НАЧАТЬ ПОБЕГ  (Enter)", new Vector2(0.5f, 0f), new Vector2(0f, 110f),
                new Vector2(520f, 86f));

            // ---------- Пауза ----------
            var pause = Stretch("PausePanel", c, new Color(0f, 0f, 0f, 0.7f));
            Transform pz = pause.transform;
            Label("Title", pz, "ПАУЗА", 80, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(800f, 110f),
                TextAnchor.MiddleCenter, yellow, FontStyle.Bold);
            Button resumeBtn = MakeButton("ResumeButton", pz, "ПРОДОЛЖИТЬ  (Esc)", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(480f, 80f));
            Button pauseRestartBtn = MakeButton("RestartButton", pz, "НАЧАТЬ ЗАНОВО  (R)", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(480f, 80f));

            // ---------- Победа ----------
            var win = Stretch("WinPanel", c, new Color(0.04f, 0.18f, 0.08f, 0.92f));
            Transform w = win.transform;
            Label("Title", w, "ПОБЕГ УДАЛСЯ!", 90, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1400f, 120f),
                TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.5f), FontStyle.Bold);
            Label("Subtitle", w, "Мышь выбралась на свободу и уже нюхает ветер с сырного завода по соседству.", 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 60f), TextAnchor.MiddleCenter, Color.white);
            Text winStats = Label("Stats", w, "", 32, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 160f),
                TextAnchor.MiddleCenter, yellow);
            Button winBtn = MakeButton("RestartButton", w, "ИГРАТЬ СНОВА  (R)", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(480f, 84f));

            // ---------- Поражение ----------
            var lose = Stretch("LosePanel", c, new Color(0.22f, 0.04f, 0.04f, 0.92f));
            Transform l = lose.transform;
            Label("Title", l, "МЫШЬ ПОЙМАНА", 90, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1400f, 120f),
                TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.4f), FontStyle.Bold);
            Text loseReason = Label("Reason", l, "", 34, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 60f),
                TextAnchor.MiddleCenter, Color.white);
            Text loseStats = Label("Stats", l, "", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 160f),
                TextAnchor.MiddleCenter, yellow);
            Button loseBtn = MakeButton("RestartButton", l, "ПОПРОБОВАТЬ СНОВА  (R)", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(520f, 84f));

            // Стартовое состояние: видно только меню
            gameplay.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);
            win.gameObject.SetActive(false);
            lose.gameObject.SetActive(false);

            Set(hud, "startPanel", start.gameObject);
            Set(hud, "gameplayPanel", gameplay.gameObject);
            Set(hud, "pausePanel", pause.gameObject);
            Set(hud, "winPanel", win.gameObject);
            Set(hud, "losePanel", lose.gameObject);
            Set(hud, "healthFill", healthFill);
            Set(hud, "foodFill", foodFill);
            Set(hud, "healthText", healthText);
            Set(hud, "foodText", foodText);
            Set(hud, "cheeseText", cheese);
            Set(hud, "catsText", cats);
            Set(hud, "timeText", time);
            Set(hud, "keycardText", keycard);
            Set(hud, "effectsText", effects);
            Set(hud, "zoneText", zone);
            Set(hud, "objectiveText", objective);
            Set(hud, "messageText", message);
            Set(hud, "winStatsText", winStats);
            Set(hud, "loseReasonText", loseReason);
            Set(hud, "loseStatsText", loseStats);
            Set(hud, "startButton", startBtn);
            Set(hud, "resumeButton", resumeBtn);
            Set(hud, "pauseRestartButton", pauseRestartBtn);
            Set(hud, "winRestartButton", winBtn);
            Set(hud, "loseRestartButton", loseBtn);
            return hud;
        }

        private static Image Stretch(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Label(string name, Transform parent, string text, int fontSize, Vector2 anchor, Vector2 pos, Vector2 size,
            TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = uiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            go.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static RectTransform Bar(string name, Transform parent, Vector2 pos, Color fillColor, out Text valueText)
        {
            Image bg = Img(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(440f, 38f), new Color(0f, 0f, 0f, 0.55f));
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = (RectTransform)fillGo.transform;
            fill.SetParent(bg.transform, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            var img = fillGo.GetComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;

            valueText = Label("Value", bg.transform, "", 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 36f),
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            return fill;
        }

        private static Button MakeButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f);
            var btn = go.GetComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.92f, 0.6f);
            colors.pressedColor = new Color(0.85f, 0.65f, 0.1f);
            btn.colors = colors;

            Text t = Label("Label", go.transform, label, 30, new Vector2(0.5f, 0.5f), Vector2.zero, size,
                TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.05f), FontStyle.Bold);
            Object.DestroyImmediate(t.GetComponent<Shadow>());
            return btn;
        }

        // =====================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ДЛЯ ГЕОМЕТРИИ
        // =====================================================================

        private static GameObject Group(string name, Transform parent = null, Vector3 localPos = default, Vector3 euler = default)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            return go;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            bool keepCollider, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Статичный блок геометрии уровня (стена, пол, платформа).</summary>
        private static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            bool isStatic = true, bool collider = true)
        {
            GameObject go = Prim(PrimitiveType.Cube, name, parent, center, size, mat, collider);
            go.isStatic = isStatic;
            return go;
        }

        /// <summary>Поперечная стена с проходом шириной 3 м и высотой 2.6 м посередине.</summary>
        private static void DividerWall(Transform parent, string name, float z, float halfWidth, Material mat)
        {
            Transform wall = Group(name, parent).transform;
            float sideWidth = halfWidth - 1.5f;
            float sideCenter = 1.5f + sideWidth / 2f;
            Block("Left", wall, new Vector3(-sideCenter, 3f, z), new Vector3(sideWidth, 6f, 1f), mat);
            Block("Right", wall, new Vector3(sideCenter, 3f, z), new Vector3(sideWidth, 6f, 1f), mat);
            Block("Lintel", wall, new Vector3(0f, 4.3f, z), new Vector3(3f, 3.4f, 1f), mat);
            Block("Frame_L", wall, new Vector3(-1.6f, 1.3f, z), new Vector3(0.2f, 2.6f, 1.1f), M("Hazard"));
            Block("Frame_R", wall, new Vector3(1.6f, 1.3f, z), new Vector3(0.2f, 2.6f, 1.1f), M("Hazard"));
        }

        private static void Cage(Transform parent, string name, Vector3 basePos, bool openFront)
        {
            Transform cage = Group(name, parent, basePos).transform;
            const float w = 2.6f, h = 1.8f, d = 2.6f;
            Block("Tray", cage, new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d), M("Plastic_Blue"));
            Block("Roof", cage, new Vector3(0f, h, 0f), new Vector3(w, 0.06f, d), M("Metal_Steel"));

            const int barsPerSide = 8;
            for (int side = 0; side < 4; side++)
            {
                if (openFront && side == 0) continue; // передняя сторона (+Z) открыта
                for (int i = 0; i <= barsPerSide; i++)
                {
                    float t = -0.5f + i / (float)barsPerSide;
                    Vector3 pos;
                    switch (side)
                    {
                        case 0: pos = new Vector3(t * w, h / 2f, d / 2f); break;
                        case 1: pos = new Vector3(t * w, h / 2f, -d / 2f); break;
                        case 2: pos = new Vector3(-w / 2f, h / 2f, t * d); break;
                        default: pos = new Vector3(w / 2f, h / 2f, t * d); break;
                    }
                    Prim(PrimitiveType.Cylinder, $"Bar_{side}_{i}", cage, pos, new Vector3(0.04f, h / 2f, 0.04f),
                        M("Metal_Steel"), true).isStatic = true;
                }
            }
            if (openFront)
            {
                // Открытая дверца, откинутая вниз — как пандус
                Block("Door_Open", cage, new Vector3(0f, 0.03f, d / 2f + 0.45f), new Vector3(w * 0.8f, 0.04f, 0.9f), M("Metal_Steel"));
            }
        }

        private static void Table(Transform parent, string name, Vector3 basePos, Vector3 topSize, float height)
        {
            Transform table = Group(name, parent, basePos).transform;
            Block("Top", table, new Vector3(0f, height, 0f), topSize, M("Plastic_White"));
            float lx = topSize.x / 2f - 0.2f, lz = topSize.z / 2f - 0.2f;
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -lx : lx;
                float z = i < 2 ? -lz : lz;
                Block("Leg_" + i, table, new Vector3(x, height / 2f, z), new Vector3(0.15f, height, 0.15f), M("Metal_Steel"));
            }
        }

        private static GameObject TriggerBox(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            GameObject go = Group(name, parent, pos);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            return go;
        }

        private static GameObject Hint(Transform parent, string name, Vector3 pos, Vector3 size, string zoneName, string hint)
        {
            GameObject go = TriggerBox(name, parent, pos, size);
            var zt = go.AddComponent<ZoneTrigger>();
            Set(zt, "zoneName", zoneName ?? "");
            Set(zt, "hint", hint);
            return go;
        }

        private static List<Transform> Route(Transform parent, string name, params Vector3[] points)
        {
            Transform route = Group(name, parent).transform;
            var list = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
                list.Add(Group("Point_" + (i + 1), route, points[i]).transform);
            return list;
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yRotation, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            if (!string.IsNullOrEmpty(name)) go.name = name;
            return go;
        }

        private static Light AddLight(string name, Transform parent, LightType type, Vector3 pos, Color color, float intensity, float range)
        {
            var go = Group(name, parent, pos);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            if (type != LightType.Directional) light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Записать значение в приватное [SerializeField] поле компонента (как будто через Inspector).</summary>
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[Lab Escape] Поле '{field}' не найдено в {target.GetType().Name}");
                return;
            }

            switch (value)
            {
                case null: p.objectReferenceValue = null; break;
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i;
                    else p.intValue = i;
                    break;
                case bool b: p.boolValue = b; break;
                case string str: p.stringValue = str; break;
                case Vector3 v: p.vector3Value = v; break;
                case Color col: p.colorValue = col; break;
                case Enum e: p.intValue = Convert.ToInt32(e); break;
                case IList list:
                    p.arraySize = list.Count;
                    for (int i = 0; i < list.Count; i++)
                        p.GetArrayElementAtIndex(i).objectReferenceValue = list[i] as Object;
                    break;
                default:
                    Debug.LogError($"[Lab Escape] Неподдерживаемый тип для поля '{field}': {value.GetType()}");
                    return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
