// FrogsPlayTests.cs (added October 2026)
//
// Automated play tests: Unity loads the real level and plays it by itself, checking that
// the frog, bird, flies, score and Game Over all behave. Runs in GitHub Actions on every
// change (.github/workflows/unity-upgrade.yml) and also in the editor:
// Window > General > Test Runner > PlayMode > Run All.
//
// Any error logged while the level runs (missing scripts, broken NavMesh, exceptions)
// automatically fails the test that was running - that's Unity's LogAssert at work.
//
// The game's scripts live in Unity's default assembly, which a test assembly can't
// reference directly, so they're looked up by name. A little reflection never hurt
// anyone - just ask the pond.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FrogsNLogs.Tests
{
    public class FrogsPlayTests
    {
        private const string ScenePath = "Assets/_Scenes.unity";

        // ---------- helpers ----------

        private static Type GameType(string name)
        {
            Type t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name, false))
                .FirstOrDefault(x => x != null);
            Assert.IsNotNull(t, "Game script '" + name + "' not found - did it fail to compile?");
            return t;
        }

        private static Component[] FindAll(string typeName)
        {
            return Object.FindObjectsByType(GameType(typeName), FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Cast<Component>().ToArray();
        }

        private static Component FindOne(string typeName)
        {
            Component[] all = FindAll(typeName);
            Assert.IsTrue(all.Length > 0, "No active " + typeName + " found in the level");
            return all[0];
        }

        private static object GetStatic(string typeName, string field)
        {
            return GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static object GetField(object obj, string field)
        {
            return obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        }

        private static void Call(object obj, string method)
        {
            obj.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(obj, null);
        }

        private static object GetStaticProp(string typeName, string prop)
        {
            return GameType(typeName).GetProperty(prop, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static void SetStaticProp(string typeName, string prop, object value)
        {
            GameType(typeName).GetProperty(prop, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
        }

        private static void SetStaticField(string typeName, string field, object value)
        {
            GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
        }

        private static object Enum(string typeName, string value)
        {
            return System.Enum.Parse(GameType(typeName), value);
        }

        // Fresh settings for every test, so one test's choices never leak into the next.
        [SetUp]
        public void ResetSettings()
        {
            PlayerPrefs.DeleteAll();
            Time.timeScale = 1f;
            SetStaticField("PlayerMovement", "TestMoveOverride", null);
            SetStaticField("PlayerMovement", "ClassicRootMotion", false);
        }

        [TearDown]
        public void CleanUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetStaticField("PlayerMovement", "TestMoveOverride", null);
            SetStaticField("PlayerMovement", "ClassicRootMotion", false);
            PlayerPrefs.DeleteAll();
        }

        private static Button FindButton(string label)
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Text t = b.GetComponentInChildren<Text>();
                if (t != null && t.text == label) return b;
            }
            Assert.Fail("No button labelled '" + label + "' on screen");
            return null;
        }

        private static string AllVisibleText()
        {
            return string.Join("\n", Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(t => t.text));
        }

        private static string ScoreText()
        {
            return FindOne("ScoreCounter").GetComponent<Text>().text;
        }

        private static string GameStateText()
        {
            Text text = (Text)GetField(FindOne("GameState"), "gameStateText");
            return text == null ? "" : text.text;
        }

        private static IEnumerator LoadLevel()
        {
            // Nov 2026 round 4: the helper buddy and daily challenge would change what the older
            // tests expect (the buddy chases the bird away!), so they start off; their own tests
            // turn them on.
            PlayerPrefs.SetInt("FNL.helperOn", 0);
            PlayerPrefs.SetInt("FNL.cheats", 0);
            PlayerPrefs.SetString("FNL.daily.done", DateTime.Now.ToString("yyyyMMdd"));
            PlayerPrefs.Save();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("_Scenes");
#endif
            // Give Start() methods and the fly spawner a few frames to run.
            for (int i = 0; i < 10; i++) yield return null;
        }

        private static IEnumerator StartGame()
        {
            // Same as pressing Space on the title text.
            Call(FindOne("GameState"), "StartGame");
            yield return null;
        }

        // Nov 2026: a phone held upright (720 x 1280), menus laid out for portrait.
        private static IEnumerator PortraitScreenshot(string name)
        {
            yield return null;
            Camera cam = Camera.main;
            GameObject uiGo = GameObject.Find("Frogs UI Canvas");
            if (cam == null || uiGo == null) yield break;
            Canvas ui = uiGo.GetComponent<Canvas>();
            var rt = new RenderTexture(720, 1280, 24);
            RenderTexture previous = cam.targetTexture;
            ui.renderMode = RenderMode.ScreenSpaceCamera;
            ui.worldCamera = cam;
            ui.planeDistance = cam.nearClipPlane + 0.5f;
            cam.targetTexture = rt;
            // Let the scaler, layout and fit-to-screen run at the new size.
            yield return null;
            yield return null;
            try
            {
                string dir = Environment.GetEnvironmentVariable("FROGS_SCREENSHOT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../PlaytestScreenshots");
                Directory.CreateDirectory(dir);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(720, 1280, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                Debug.Log("[Playtest] Saved screenshot " + name);
            }
            catch (Exception e)
            {
                Debug.Log("[Playtest] Screenshot " + name + " skipped: " + e.Message);
            }
            finally
            {
                cam.targetTexture = previous;
                ui.renderMode = RenderMode.ScreenSpaceOverlay;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        private static IEnumerator Screenshot(string name)
        {
            // Note: not WaitForEndOfFrame - in batch mode (no screen) it never fires and the
            // test would hang forever. The camera is rendered manually below instead.
            yield return null;
            try
            {
                string dir = Environment.GetEnvironmentVariable("FROGS_SCREENSHOT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../PlaytestScreenshots");
                Directory.CreateDirectory(dir);

                Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                if (cam == null) { Debug.Log("[Playtest] No camera to screenshot"); yield break; }

                const int w = 1280, h = 720;
                var rt = new RenderTexture(w, h, 24);
                RenderTexture previous = cam.targetTexture;

                // Nov 2026: the menus draw straight onto the screen, which a manual camera render
                // doesn't include. Briefly attach them to the camera so they show up in the shot.
                Canvas ui = null;
                GameObject uiGo = GameObject.Find("Frogs UI Canvas");
                if (uiGo != null) ui = uiGo.GetComponent<Canvas>();
                if (ui != null)
                {
                    ui.renderMode = RenderMode.ScreenSpaceCamera;
                    ui.worldCamera = cam;
                    ui.planeDistance = cam.nearClipPlane + 0.5f;
                }
                Canvas.ForceUpdateCanvases();
                // Nov 2026 (URP): ask the render pipeline for a frame into our texture.
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = previous;
                }
                if (ui != null) ui.renderMode = RenderMode.ScreenSpaceOverlay;

                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                rt.Release();
                Object.Destroy(rt);
                Debug.Log("[Playtest] Saved screenshot " + name);
            }
            catch (Exception e)
            {
                // Screenshots are a bonus; never fail a test because the server has no GPU.
                Debug.Log("[Playtest] Screenshot " + name + " skipped: " + e.Message);
            }
        }

        // ---------- tests (run in name order) ----------

        [Test]
        public void T0_LevelIsInTheBuildList()
        {
            // The Game Over restart reloads build-list entry #0, so the level must be first.
            // (Read from disk because the test runner swaps the live list while it runs.)
            string settings = File.ReadAllText(Path.Combine(Application.dataPath, "../ProjectSettings/EditorBuildSettings.asset"));
            int first = settings.IndexOf("path: ", StringComparison.Ordinal);
            Assert.GreaterOrEqual(first, 0, "No scenes in the build list");
            StringAssert.StartsWith("path: " + ScenePath, settings.Substring(first),
                "The level must be the first (index 0) scene in File > Build Profiles");
            StringAssert.Contains("enabled: 1", settings.Substring(Math.Max(0, first - 30), 30),
                "The level is in the build list but unchecked");
        }

        // Touch/mouse steering math (PointerInput.ScreenToGroundDirection). Real touches can't
        // be faked on the build server, so this checks the part that turns "where the finger
        // is" into "which way the frog hops".
        private static Vector3 Steer(Vector2 frog, Vector2 finger, Vector3 camRight, Vector3 camForward)
        {
            MethodInfo m = GameType("PointerInput").GetMethod("ScreenToGroundDirection", BindingFlags.Public | BindingFlags.Static);
            return (Vector3)m.Invoke(null, new object[] { frog, finger, 40f, camRight, camForward });
        }

        [Test]
        public void T0_TouchSteeringPointsTheFrogTheRightWay()
        {
            Vector3 right = Vector3.right;
            Vector3 tilted = new Vector3(0f, -0.7f, 0.7f);   // camera looking down at an angle
            Vector2 frog = new Vector2(500, 400);

            Assert.That(Vector3.Distance(Steer(frog, new Vector2(800, 400), right, tilted), Vector3.right), Is.LessThan(0.01f), "Finger to the right -> hop right");
            Assert.That(Vector3.Distance(Steer(frog, new Vector2(200, 400), right, tilted), Vector3.left), Is.LessThan(0.01f), "Finger to the left -> hop left");
            Assert.That(Vector3.Distance(Steer(frog, new Vector2(500, 700), right, tilted), Vector3.forward), Is.LessThan(0.01f), "Finger up the screen -> hop away from camera");
            Assert.That(Vector3.Distance(Steer(frog, new Vector2(500, 100), right, tilted), Vector3.back), Is.LessThan(0.01f), "Finger down the screen -> hop toward camera");
            Assert.AreEqual(Vector3.zero, Steer(frog, new Vector2(510, 410), right, tilted), "Finger on the frog -> stay put");
            Assert.That(Vector3.Distance(Steer(frog, new Vector2(500, 700), right, Vector3.down), Vector3.forward), Is.LessThan(0.01f), "Straight-down camera still works");

            Vector3 diag = Steer(frog, new Vector2(800, 700), right, tilted);
            Assert.That(diag.magnitude, Is.EqualTo(1f).Within(0.01f), "Direction is always full speed");
            Assert.That(diag.y, Is.EqualTo(0f).Within(0.0001f), "Frog never steers into the sky");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T1_LevelLoadsWithEverythingInIt()
        {
            yield return LoadLevel();

            Assert.AreEqual(1, FindAll("PlayerHealth").Length, "Expected exactly one frog");
            Assert.AreEqual(1, FindAll("BirdMovement").Length, "Expected exactly one bird");
            Assert.IsNotNull(GameObject.FindWithTag("Player"), "Frog isn't tagged Player");
            Assert.IsNotNull(GameObject.FindWithTag("Enemy"), "Bird isn't tagged Enemy");

            int flies = FindAll("FlyPickUp").Length;
            Assert.GreaterOrEqual(flies, 12, "Fly spawner should keep at least 12 flies");
            Assert.AreEqual(0, (int)GetStatic("ScoreCounter", "score"));
            StringAssert.Contains("0 Flies", ScoreText());

            // Before Space is pressed, nothing should be moving yet.
            Assert.IsFalse(((Behaviour)FindOne("BirdMovement")).enabled, "Bird should wait for the game to start");
            Assert.IsFalse(((Behaviour)FindOne("PlayerMovement")).enabled, "Frog should wait for the game to start");

            Debug.Log("[Playtest] Level loaded: frog, bird, " + flies + " flies, score '" + ScoreText()
                      + "', title text '" + GameStateText() + "'");
            yield return Screenshot("01-level-loaded");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T2_StartingTheGameWakesFrogAndBird()
        {
            yield return LoadLevel();
            var bird = (Behaviour)FindOne("BirdMovement");
            Vector3 birdStart = bird.transform.position;

            yield return StartGame();

            Assert.IsTrue(((Behaviour)FindOne("PlayerMovement")).enabled, "Frog controls didn't turn on");
            Assert.IsTrue(bird.enabled, "Bird didn't start moving");

            yield return new WaitForSeconds(2f);
            float moved = Vector3.Distance(birdStart, bird.transform.position);
            Debug.Log("[Playtest] Bird moved " + moved.ToString("0.00") + "m in 2s");
            Assert.Greater(moved, 0.5f, "Bird isn't chasing the frog (pathfinding/NavMesh problem?)");

            yield return Screenshot("02-game-running");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T3_EatingAFlyScoresOnePoint()
        {
            yield return LoadLevel();
            yield return StartGame();

            Component fly = FindOne("FlyPickUp");
            Collider frogCollider = GameObject.FindWithTag("Player").GetComponentInChildren<Collider>();
            Assert.IsNotNull(frogCollider, "Frog has no collider");

            // Deliver the "frog touched fly" event directly, the same message Unity physics sends.
            Vector3 flyWasAt = fly.transform.position;
            fly.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            yield return null;

            Assert.AreEqual(1, (int)GetStatic("ScoreCounter", "score"), "Score should be exactly 1 after one fly");
            // Nov 2026: eaten flies are reused - it either hides, or has already popped up
            // somewhere new as the replacement fly.
            bool gone = fly == null || !fly.gameObject.activeInHierarchy
                        || Vector3.Distance(flyWasAt, fly.transform.position) > 0.5f;
            Assert.IsTrue(gone, "Eaten fly should be removed from where it was");
            StringAssert.Contains("1 Flies", ScoreText());
            Assert.GreaterOrEqual(FindAll("FlyPickUp").Length, 12, "A new fly should spawn to replace it");

            yield return Screenshot("03-ate-a-fly");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator T4_BirdCatchesFrogThenLevelRestarts()
        {
            yield return LoadLevel();
            yield return StartGame();

            // Pretend we've already eaten 5 flies, so the restart has a score to reset.
            GameType("ScoreCounter").GetField("score", BindingFlags.Public | BindingFlags.Static).SetValue(null, 5);

            Component health = FindOne("PlayerHealth");
            float waited = 0f;
            // Stand still and let the bird come to us (tests the real chase + collision).
            while ((bool)GetField(health, "alive") && waited < 30f)
            {
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            bool caughtNaturally = !(bool)GetField(health, "alive");
            if (!caughtNaturally)
            {
                // Fallback so the rest of the flow still gets tested.
                Debug.LogWarning("[Playtest] Bird didn't reach the frog within 30s; simulating the catch");
                Collider birdCollider = GameObject.FindWithTag("Enemy").GetComponentInChildren<Collider>();
                health.SendMessage("OnTriggerEnter", birdCollider);
            }
            else
            {
                Debug.Log("[Playtest] Bird caught the frog after " + waited.ToString("0.0") + "s");
            }

            yield return null;
            yield return null;
            Assert.AreEqual("Game Over!", GameStateText(), "Game Over text should show");
            int best = (int)GameType("GameSettings").GetMethod("GetBestScore").Invoke(null, new[] { Enum("Difficulty", "Medium") });
            Assert.AreEqual(5, best, "The 5-fly score should be saved as the best on Medium");
            StringAssert.Contains("New best!", AllVisibleText(), "Game over should celebrate a new best");
            yield return Screenshot("04-game-over");

            // Restart happens after 3 seconds. GameState reloads the level by its build-list
            // number. Unity's test runner temporarily swaps out the build list while tests run,
            // so that reload can't work *inside a test* (the real game is fine - see
            // T0_LevelIsInTheBuildList). In that case, tolerate Unity's "Cannot load scene"
            // error and reload the level ourselves to check a fresh round starts cleanly.
            bool reloadPossibleHere = SceneManager.GetActiveScene().buildIndex >= 0;
            if (!reloadPossibleHere)
            {
                LogAssert.ignoreFailingMessages = true;
            }
            yield return new WaitForSeconds(4f);
            if (!reloadPossibleHere)
            {
                Debug.Log("[Playtest] Restart timer fired after Game Over; test runner hides the build list, so reloading the level manually");
                // Stop GameState retrying the reload every frame, let the last attempt's
                // error flush, and only then go back to failing on errors.
                ((Behaviour)FindOne("GameState")).enabled = false;
                yield return null;
                yield return null;
                LogAssert.ignoreFailingMessages = false;
                yield return LoadLevel();
            }
            for (int i = 0; i < 10; i++) yield return null;
            // Flies spawn at random spots, so one can occasionally bump into the frog right
            // after the restart (a free point!). The score just has to have reset from 5.
            int scoreAfter = (int)GetStatic("ScoreCounter", "score");
            Debug.Log("[Playtest] Score after restart: " + scoreAfter);
            Assert.Less(scoreAfter, 5, "Score should reset after restart");
            Assert.IsTrue((bool)GetField(FindOne("PlayerHealth"), "alive"), "New frog should be alive after restart");
            Assert.AreNotEqual("Game Over!", GameStateText(), "Game Over text should clear after restart");

            Assert.IsTrue(caughtNaturally, "Restart works, but the bird never caught the frog on its own within 30s");
        }

        // ---------- Nov 2026: menus, controls feel, pause, difficulty ----------

        [UnityTest, Timeout(60000)]
        public IEnumerator T5_TitleScreenExplainsBothControlStylesAndDifficulty()
        {
            yield return LoadLevel();
            GameObject title = GameObject.Find("Title Screen");
            Assert.IsNotNull(title, "Title screen should be showing when the level loads");

            string text = AllVisibleText();
            StringAssert.Contains("FROGS-N-LOGS", text);
            StringAssert.Contains("KEYBOARD", text, "Title should explain keyboard controls");
            StringAssert.Contains("TOUCH & MOUSE", text, "Title should explain touch/mouse controls");
            yield return Screenshot("05-title-screen");

            // Picking Easy on the title screen saves it.
            FindButton("Easy").onClick.Invoke();
            yield return null;
            Assert.AreEqual(Enum("Difficulty", "Easy"), GetStaticProp("GameSettings", "Difficulty"));

            // Play starts the round and hides the title.
            FindButton("PLAY").onClick.Invoke();
            yield return null;
            Assert.IsTrue(((Behaviour)FindOne("PlayerMovement")).enabled, "Play button should start the game");
            Assert.IsNull(GameObject.Find("Title Screen"), "Title should hide once the game starts");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T6_FrogRespondsInstantlyAndStopsWhenReleased()
        {
            yield return LoadLevel();
            yield return StartGame();
            Component frog = FindOne("PlayerMovement");
            Transform t = frog.transform;

            // Keep the bird out of this one so it can't catch the frog mid-measurement.
            Behaviour birdBrain = (Behaviour)FindOne("BirdMovement");
            birdBrain.enabled = false;
            birdBrain.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;

            // Hold "right" for a quarter of a second.
            Vector3 start = t.position;
            SetStaticField("PlayerMovement", "TestMoveOverride", (Vector3?)Vector3.right);
            yield return new WaitForSeconds(0.25f);
            float movedWhileHeld = Vector3.Distance(start, t.position);

            // Let go: the frog should stop almost straight away (no finishing the hop).
            SetStaticField("PlayerMovement", "TestMoveOverride", null);
            yield return new WaitForSeconds(0.15f);
            Vector3 released = t.position;
            yield return new WaitForSeconds(0.25f);
            float driftAfterStop = Vector3.Distance(released, t.position);

            Debug.Log("[Playtest] Frog moved " + movedWhileHeld.ToString("0.00") + "m in 0.25s held, then drifted "
                      + driftAfterStop.ToString("0.00") + "m after letting go");
            Assert.Greater(movedWhileHeld, 0.5f, "Frog should get moving within a quarter second");
            Assert.Less(driftAfterStop, 0.1f, "Frog should stop when the key is released");

            // For tuning: how fast was the 2017 hop-driven movement? (logged only)
            SetStaticField("PlayerMovement", "ClassicRootMotion", true);
            SetStaticField("PlayerMovement", "TestMoveOverride", (Vector3?)Vector3.left);
            Vector3 classicStart = t.position;
            yield return new WaitForSeconds(2f);
            float classicSpeed = Vector3.Distance(classicStart, t.position) / 2f;
            Debug.Log("[Playtest] 2017 hop speed for comparison: " + classicSpeed.ToString("0.00") + " m/s");
            Debug.Log("::notice::2017 hop speed " + classicSpeed.ToString("0.00") + " m/s; new steady speed held 0.25s moved " + movedWhileHeld.ToString("0.00") + " m");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T7_PauseFreezesEverythingAndResumes()
        {
            yield return LoadLevel();
            yield return StartGame();
            yield return new WaitForSeconds(0.5f);

            object ui = GetStaticProp("FrogsUI", "Instance");
            Assert.IsNotNull(ui, "Menus didn't load");
            ui.GetType().GetMethod("OpenMenu").Invoke(ui, new object[] { true });
            Assert.AreEqual(0f, Time.timeScale, "Pausing should freeze the game");
            StringAssert.Contains("Paused", AllVisibleText());

            Transform bird = FindOne("BirdMovement").transform;
            Vector3 frozen = bird.position;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Less(Vector3.Distance(frozen, bird.position), 0.01f, "Bird should not move while paused");

            // Every settings tab opens without errors.
            foreach (string tab in new[] { "Sound", "Graphics", "Controls", "Game" })
            {
                FindButton(tab).onClick.Invoke();
                yield return null;
            }
            FindButton("Graphics").onClick.Invoke();
            yield return null;
            yield return Screenshot("06-pause-menu");

            FindButton("Resume").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "Resume should unfreeze the game");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T8_DifficultyChangesTheBirdAndTheFlies()
        {
            SetStaticProp("GameSettings", "Difficulty", Enum("Difficulty", "Hard"));
            yield return LoadLevel();
            yield return StartGame();
            yield return new WaitForSeconds(0.3f);

            var agent = FindOne("BirdMovement").GetComponent<UnityEngine.AI.NavMeshAgent>();
            float hardSpeed = agent.speed;
            int hardFlies = FindAll("FlyPickUp").Length;

            SetStaticProp("GameSettings", "Difficulty", Enum("Difficulty", "Easy"));
            yield return null;
            yield return null;
            float easySpeed = agent.speed;
            Debug.Log("[Playtest] Bird speed Hard " + hardSpeed.ToString("0.00") + " vs Easy " + easySpeed.ToString("0.00")
                      + "; flies on Hard: " + hardFlies);
            Assert.Greater(hardSpeed, easySpeed * 1.5f, "Hard bird should be much faster than Easy");
            Assert.Less(hardFlies, 12, "Hard should have fewer flies than the classic 12");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T9_SettingsAreRememberedAndApplied()
        {
            yield return LoadLevel();
            SetStaticProp("GameSettings", "MusicOn", false);
            SetStaticProp("GameSettings", "EffectsVolume", 0.5f);
            SetStaticProp("GameSettings", "Filter", Enum("ScreenFilterMode", "Night"));
            SetStaticProp("GameSettings", "Graphics", Enum("GraphicsTier", "Low"));
            yield return null;

            // Saved...
            Assert.AreEqual(false, GetStaticProp("GameSettings", "MusicOn"));
            Assert.AreEqual(0.5f, (float)GetStaticProp("GameSettings", "EffectsVolume"), 0.001f);
            // ...and applied: music sources silent, filter on the camera, Low graphics active.
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                bool music = (bool)GameType("AudioManager").GetMethod("IsMusic").Invoke(null, new object[] { source });
                if (music) Assert.AreEqual(0f, source.volume, 0.001f, "Music should be muted: " + source.name);
            }
            // Nov 2026 (URP): filters are a renderer feature now, not a camera component.
            object mode = GetStaticProp("GameSettings", "Filter");
            int pass = (int)GameType("ScreenFilter").GetMethod("PassFor").Invoke(null, new[] { mode });
            Assert.AreNotEqual(-1, pass, "Night filter should pick a shader pass");
            Assert.IsNotNull(Resources.Load<Shader>("FrogsFilters"), "Filter shader missing");
            Debug.Log("[Playtest] Filter feature installed: " + GetStaticProp("FrogsFilterFeature", "IsInstalled"));
            Assert.AreEqual(Enum("GraphicsTier", "Low"), GetStaticProp("GraphicsManager", "ActiveTier"));
            yield return Screenshot("07-night-filter-low");

            // Changing a key binding sticks (and arrows still work as a backup).
            GameType("KeyBindings").GetMethod("Set").Invoke(null, new object[] { "up", KeyCode.I });
            Assert.AreEqual(KeyCode.I, GameType("KeyBindings").GetMethod("Get").Invoke(null, new object[] { "up" }));
        }

        // ---------- Nov 2026 round 2: levels, hazards, power-ups ----------

        [UnityTest, Timeout(90000)]
        public IEnumerator T10_FlyGoalsUnlockTheSwampAndNightWithTheirHazards()
        {
            yield return LoadLevel();
            yield return StartGame();
            // Keep the bird still so it can't end the run while we look around.
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            bird.enabled = false;
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;

            object levels = GetStaticProp("LevelManager", "Instance");
            Assert.IsNotNull(levels, "Level manager didn't load");
            Func<int> level = () => (int)levels.GetType().GetProperty("Level").GetValue(levels);
            Assert.AreEqual(1, level(), "Should start on Level 1 (The Pond)");
            StringAssert.Contains("Level 1", AllVisibleText(), "HUD should show the level");

            // Medium: 10 flies opens the Swamp.
            SetStaticField("ScoreCounter", "score", 10);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(2, level(), "10 flies should move us to the Swamp");
            Assert.IsTrue(RenderSettings.fog, "The Swamp should be foggy");
            Assert.IsNotNull(GameObject.Find("Snake"), "A snake should appear in the Swamp");
            Assert.IsNotNull(GameObject.Find("Drifting Log"), "Logs should drift through the Swamp");
            yield return Screenshot("08-the-swamp");

            // 24 flies: Night, and the bird is faster.
            SetStaticField("ScoreCounter", "score", 24);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(3, level(), "24 flies should bring on the Night");
            Assert.GreaterOrEqual((float)GetStatic("BirdMovement", "SpeedMultiplier"), 1.15f, "The bird should be faster at night");
            yield return Screenshot("09-night");
        }

        private static Component SpawnPowerUp(string type)
        {
            Component spawner = FindOne("PowerUpSpawner");
            MethodInfo spawn = spawner.GetType().GetMethod("Spawn");
            var go = (GameObject)spawn.Invoke(spawner, new[] { Enum("PowerUpType", type) });
            Assert.IsNotNull(go, "Couldn't place a " + type);
            Component pickup = go.GetComponentInChildren(GameType("PowerUpPickup"));
            Assert.IsNotNull(pickup, type + " has no pickup");
            return pickup;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T11_PowerUpsGoldenFlySpeedBoostAndShield()
        {
            yield return LoadLevel();
            yield return StartGame();
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            bird.enabled = false;
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
            Collider frogCollider = GameObject.FindWithTag("Player").GetComponentInChildren<Collider>();

            // Golden fly: +5.
            Component golden = SpawnPowerUp("GoldenFly");
            yield return null;
            yield return Screenshot("10-golden-fly");
            int before = (int)GetStatic("ScoreCounter", "score");
            golden.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            Assert.AreEqual(before + 5, (int)GetStatic("ScoreCounter", "score"), "Golden fly should be worth 5");

            // Speed boost: faster frog for a few seconds.
            Component orb = SpawnPowerUp("SpeedBoost");
            orb.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            yield return null;
            Assert.Greater((float)GetStatic("PlayerMovement", "SpeedMultiplier"), 1.5f, "Speed boost should speed the frog up");

            // Shield: the next catch pops the shield instead of ending the game.
            Component pad = SpawnPowerUp("Shield");
            pad.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            Assert.IsTrue((bool)GameType("FrogEffects").GetField("ShieldActive").GetValue(null), "Shield should be up");

            Component health = FindOne("PlayerHealth");
            Collider birdCollider = GameObject.FindWithTag("Enemy").GetComponentInChildren<Collider>();
            health.SendMessage("OnTriggerEnter", birdCollider);
            yield return null;
            Assert.IsTrue((bool)GetField(health, "alive"), "Shield should save the frog");
            Assert.IsFalse((bool)GameType("FrogEffects").GetField("ShieldActive").GetValue(null), "Shield should be used up");
            Assert.IsTrue((bool)GetStaticProp("BirdMovement", "IsStunned"), "The bird should be dizzy after hitting the shield");
        }

        // ---------- Nov 2026 round 3: shop, laser, looks, achievements ----------

        private static bool Buy(string id)
        {
            return (bool)GameType("Shop").GetMethod("TryBuy").Invoke(null, new object[] { id });
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T12_ShopUpgradesWork()
        {
            yield return LoadLevel();
            yield return StartGame();
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            Collider frogCollider = GameObject.FindWithTag("Player").GetComponentInChildren<Collider>();

            // Eating flies fills the wallet.
            Component fly = FindOne("FlyPickUp");
            fly.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            yield return null;
            Assert.GreaterOrEqual((int)GetStaticProp("Shop", "Wallet"), 1, "Eating a fly should add to the fly wallet");

            // Can't buy what you can't afford...
            Assert.IsFalse(Buy("double"), "Shouldn't be able to buy Double flies with 1 fly");
            // ...but with a full wallet everything works.
            PlayerPrefs.SetInt("FNL.wallet", 2000);
            Assert.IsTrue(Buy("speed"), "Buy Strong legs");
            Assert.IsTrue(Buy("magnet") && Buy("magnet") && Buy("magnet"), "Buy all 3 magnet levels");
            Assert.IsTrue(Buy("double"), "Buy Double flies");
            Assert.IsTrue(Buy("laser"), "Buy Laser eyes");
            Assert.IsTrue(Buy("skin.gold"), "Buy Golden frog");
            Assert.IsTrue(Buy("hat.crown"), "Buy Crown");
            yield return null;
            yield return null;

            Assert.Greater((float)GetStatic("PlayerMovement", "SpeedMultiplier"), 1.05f, "Strong legs should speed the frog up");
            Assert.IsNotNull(GameObject.Find("Frog Hat"), "The crown should be on the frog");

            // Golden frog really turns gold on the cartoon model (recolour, not just a tint).
            GameObject newFrog = GameObject.Find("Frog (new model)");
            if (newFrog != null)
            {
                var block = new MaterialPropertyBlock();
                newFrog.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block); // (not the hat)
                Color recolor = block.GetColor("_Recolor");
                Assert.AreEqual(1f, recolor.a, 0.01f, "The golden skin should recolour the frog");
                Assert.Greater(recolor.r, recolor.b + 0.3f, "...in gold");
            }

            // Double flies: next fly is worth 2.
            int before = (int)GetStatic("ScoreCounter", "score");
            Component another = FindOne("FlyPickUp");
            another.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            Assert.AreEqual(before + 2, (int)GetStatic("ScoreCounter", "score"), "Double flies should give 2 per fly");

            // Fly magnet: a fly 5 m away drifts closer.
            Component magnetFly = FindOne("FlyPickUp");
            Transform frog = GameObject.FindWithTag("Player").transform;
            magnetFly.transform.position = frog.position + new Vector3(5f, 0.8f, 0f);
            float startDistance = Vector3.Distance(Flat(magnetFly.transform.position), Flat(frog.position));
            yield return new WaitForSeconds(0.6f);
            float endDistance = magnetFly == null || !magnetFly.gameObject.activeInHierarchy ? 0f
                : Vector3.Distance(Flat(magnetFly.transform.position), Flat(frog.position));
            Assert.Less(endDistance, startDistance - 0.5f, "Fly magnet should pull nearby flies in");

            // Laser eyes: zap the bird when it's in range.
            bird.transform.position = frog.position + new Vector3(0f, 0f, 8f);
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(bird.transform.position);
            object director = GetStaticProp("GameDirector", "Instance");
            bool hit = (bool)director.GetType().GetMethod("FireLaser").Invoke(director, null);
            yield return null;
            Assert.IsTrue(hit, "Laser should hit a bird 8 m away");
            Assert.IsTrue((bool)GetStaticProp("BirdMovement", "IsStunned"), "Zapped bird should be dizzy");
            yield return Screenshot("11-shop-upgrades-zap");

            Assert.IsTrue((bool)GameType("Achievements").GetMethod("Has").Invoke(null, new object[] { "shopper" }), "Buying should unlock Shopper");
            Assert.IsTrue((bool)GameType("Achievements").GetMethod("Has").Invoke(null, new object[] { "first" }), "Eating a fly should unlock First bite");
        }

        private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        [UnityTest, Timeout(60000)]
        public IEnumerator T13_ShopTabShowsInThePauseMenu()
        {
            PlayerPrefs.SetInt("FNL.wallet", 75);
            yield return LoadLevel();
            yield return StartGame();
            object ui = GetStaticProp("FrogsUI", "Instance");
            ui.GetType().GetMethod("OpenMenu").Invoke(ui, new object[] { true });
            FindButton("Shop").onClick.Invoke();
            yield return null;
            string text = AllVisibleText();
            StringAssert.Contains("Fly wallet: 75", text);
            StringAssert.Contains("Laser eyes", text);
            StringAssert.Contains("Top hat", text);
            yield return Screenshot("12-shop");
            FindButton("Resume").onClick.Invoke();
        }

        // ---------- Nov 2026: real clicks (found in the live test) ----------

        // A pretend mouse that reports "no mouse and no touchscreen", like some browsers do,
        // and clicks wherever we tell it to.
        private class FakeBrowserMouse : BaseInput
        {
            public Vector2 position;
            public bool down, up, held;
            public override bool mousePresent { get { return false; } }
            public override bool touchSupported { get { return false; } }
            public override int touchCount { get { return 0; } }
            public override Vector2 mousePosition { get { return position; } }
            public override Vector2 mouseScrollDelta { get { return Vector2.zero; } }
            public override bool GetMouseButtonDown(int button) { return button == 0 && down; }
            public override bool GetMouseButtonUp(int button) { return button == 0 && up; }
            public override bool GetMouseButton(int button) { return button == 0 && held; }
            public override float GetAxisRaw(string axisName) { return 0f; }
            public override bool GetButtonDown(string buttonName) { return false; }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T14_ClickingPlayReallyWorks()
        {
            yield return LoadLevel();
            Button play = FindButton("PLAY");
            var module = Object.FindFirstObjectByType<StandaloneInputModule>();
            Assert.IsNotNull(module, "No input module for the menus");
            var mouse = module.gameObject.AddComponent<FakeBrowserMouse>();
            module.inputOverride = mouse;

            // Aim at the middle of the Play button (the menus draw straight onto the screen).
            Vector3[] corners = new Vector3[4];
            play.GetComponent<RectTransform>().GetWorldCorners(corners);
            mouse.position = (corners[0] + corners[2]) * 0.5f;
            yield return null;

            mouse.down = true; mouse.held = true;
            yield return null;
            mouse.down = false; mouse.up = true; mouse.held = false;
            yield return null;
            mouse.up = false;
            yield return null;

            module.inputOverride = null;
            Assert.IsTrue(((Behaviour)FindOne("PlayerMovement")).enabled, "A real click on Play should start the game");
        }

        // ---------- Nov 2026 round 3: URP, new models, music, credits ----------

        [UnityTest, Timeout(60000)]
        public IEnumerator T15_NewCartoonModelsReplaceTheOldOnes()
        {
            yield return LoadLevel();
            yield return null;
            GameObject frog = GameObject.Find("Frog (new model)");
            GameObject eagle = GameObject.Find("Eagle (new model)");
            Assert.IsNotNull(frog, "The new cartoon frog should be in the level");
            Assert.IsNotNull(eagle, "The new cartoon eagle should be in the level");
            Assert.IsNotNull(frog.GetComponentInChildren<Animation>(), "The frog should have its animations");
            Assert.IsNotNull(Object.FindFirstObjectByType(GameType("SceneDressing")), "Trees and plants should be added");
            Assert.IsNotNull(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline, "URP should be the active pipeline");
            yield return StartGame();
            yield return new WaitForSeconds(1f);
            yield return Screenshot("13-new-models");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T16_NewSoundsAndMusicAreThere()
        {
            yield return LoadLevel();
            foreach (string name in new[] { "Ribbit1", "Croak", "FlyEat", "PowerUp", "Click", "LevelUp", "GameOver", "Win", "Zap", "ShieldPop", "Crickets" })
            {
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/" + name), "Missing sound " + name);
            }
            foreach (string track in new[] { "FluffingaDuck", "Carefree", "SneakySnitch", "Investigations" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, "Music/" + track + ".mp3")), "Missing music " + track);
            }
            object director = GetStaticProp("AudioDirector", "Instance");
            Assert.IsNotNull(director, "The music player should start with the game");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator T17_BeatingTheGreatOwlRollsTheCredits()
        {
            yield return LoadLevel();
            yield return StartGame();
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            bird.enabled = false;
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;

            // Nov 2026 round 5: six levels; the last one ends with the Great Owl.
            object levels = GetStaticProp("LevelManager", "Instance");
            levels.GetType().GetMethod("GoToLevel").Invoke(levels, new object[] { 6 });
            yield return null;
            int start = (int)levels.GetType().GetProperty("LevelStartScore").GetValue(levels);
            int goal = (int)GameType("LevelManager").GetMethod("FinalGoal").Invoke(null, new[] { GetStaticProp("GameSettings", "Difficulty") });
            SetStaticField("ScoreCounter", "score", start + goal);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue((bool)levels.GetType().GetProperty("BossActive").GetValue(levels), "The Great Owl should arrive");
            object boss = GetStaticProp("BossOwl", "Active");
            Assert.IsNotNull(boss, "The Great Owl should be hunting");
            Assert.IsNotNull(GameObject.Find("The Great Owl"), "...and be in the sky");
            // Keep the frog safe while we watch the first dive.
            SetStaticField("FrogEffects", "InvulnerableUntil", Time.time + 30f);
            float until = Time.time + 5f;
            while (Time.time < until && GameObject.Find("Owl Dive") == null) yield return null;
            Assert.IsNotNull(GameObject.Find("Owl Dive"), "The owl should dive (a red ring warns you)");
            yield return Screenshot("20-great-owl");

            boss.GetType().GetMethod("Survive").Invoke(boss, null);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue((bool)levels.GetType().GetProperty("HasFinished").GetValue(levels), "Surviving the owl should finish the game");
            Component ui = FindOne("FrogsUI");
            Assert.IsTrue((bool)ui.GetType().GetProperty("CreditsShowing").GetValue(ui), "Credits should roll");
            string text = AllVisibleText();
            StringAssert.Contains("Kevin MacLeod", text, "Music should be credited");
            StringAssert.Contains("Quaternius", text, "Models should be credited");
            yield return Screenshot("14-credits");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T18_FrameRateSettingCapsTheGame()
        {
            yield return LoadLevel();
            SetStaticProp("GameSettings", "FrameRate", Enum("FrameRateCap", "Fps30"));
            yield return null;
            Assert.AreEqual(30, Application.targetFrameRate, "30 should cap the game at 30 FPS");
            SetStaticProp("GameSettings", "FrameRate", Enum("FrameRateCap", "Fps60"));
            yield return null;
            Assert.AreEqual(60, Application.targetFrameRate, "60 should cap the game at 60 FPS");
            SetStaticProp("GameSettings", "FrameRate", Enum("FrameRateCap", "MatchScreen"));
            yield return null;
            Assert.AreNotEqual(30, Application.targetFrameRate, "Match screen should lift the cap");
            Assert.AreNotEqual(60, Application.targetFrameRate, "Match screen should lift the cap");
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, ((Component)FindOne("PlayerMovement")).GetComponent<Rigidbody>().interpolation,
                "The frog should glide smoothly between physics steps");
            Assert.IsNotNull(Object.FindFirstObjectByType(GameType("SafeAreaFitter")), "Menus should stay clear of phone notches");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator T19_EverythingFitsOnAPhoneHeldUpright()
        {
            yield return LoadLevel();
            Type layout = GameType("ScreenLayout");
            FieldInfo testAspect = layout.GetField("TestAspect", BindingFlags.Public | BindingFlags.Static);
            float landscapeFov = Camera.main.fieldOfView;
            try
            {
                // An iPhone held upright is about 390 x 844 points.
                testAspect.SetValue(null, (float?)(390f / 844f));
                yield return null;
                yield return null;
                Assert.AreEqual(true, layout.GetProperty("IsPortrait").GetValue(null), "Should know it's portrait");
                var scaler = GameObject.Find("Frogs UI Canvas").GetComponent<CanvasScaler>();
                Assert.AreEqual(new Vector2(720f, 1280f), scaler.referenceResolution, "Menus should use the portrait layout");
                Assert.Greater(Camera.main.fieldOfView, landscapeFov + 5f, "The view should widen so you can see the sides");
                // Every menu card fits inside the screen once fitted (checked before the
                // screenshot, which briefly changes the canvas size).
                yield return null;
                foreach (Component fit in Object.FindObjectsByType(GameType("FitToParent"), FindObjectsInactive.Exclude, FindObjectsSortMode.None).Cast<Component>())
                {
                    var rt = (RectTransform)fit.transform;
                    var parent = (RectTransform)rt.parent;
                    float w = rt.rect.width * rt.localScale.x, h = rt.rect.height * rt.localScale.y;
                    Assert.LessOrEqual(w, parent.rect.width + 1f, fit.name + " is too wide for the screen");
                    Assert.LessOrEqual(h, parent.rect.height + 1f, fit.name + " is too tall for the screen");
                }
                yield return PortraitScreenshot("15-portrait-title");
                yield return null;
                yield return null;

                yield return StartGame();
                yield return new WaitForSeconds(1f);
                yield return PortraitScreenshot("16-portrait-playing");
            }
            finally
            {
                testAspect.SetValue(null, null);
            }
            yield return null;
            yield return null;
            Assert.AreEqual(landscapeFov, Camera.main.fieldOfView, 0.5f, "Back to the landscape view");
        }

        // ---------- Nov 2026 round 4: bigger pond, mini map, helper, cheats, combos ----------

        [UnityTest, Timeout(90000)]
        public IEnumerator T20_ThePondGrowsEachLevel()
        {
            yield return LoadLevel();
            yield return StartGame();
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            bird.enabled = false;
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;

            Type area = GameType("PlayArea");
            Func<float> half = () => (float)area.GetProperty("HalfSize").GetValue(null);
            Assert.AreEqual(30f, half(), 0.1f, "The Pond is the original 60 x 60 m");
            Assert.IsTrue((bool)area.GetProperty("BuildingDone").GetValue(null), "The bird's walking map should be built");

            object levels = GetStaticProp("LevelManager", "Instance");
            levels.GetType().GetMethod("GoToLevel").Invoke(levels, new object[] { 2 });
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(39f, half(), 0.2f, "The Swamp opens up to 78 x 78 m");
            UnityEngine.AI.NavMeshHit hit;
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(35f, 2f, 0f), out hit, 3f, UnityEngine.AI.NavMesh.AllAreas),
                "The bird should be able to walk into the new Swamp ring");
            GameObject wall = GameObject.Find("Wall 3");
            if (wall != null) Assert.AreEqual(39f, Mathf.Abs(wall.transform.position.x), 0.3f, "Walls should move out");
            Assert.Greater((int)GameType("FlySpawner").GetProperty("FlyMinimum").GetValue(FindOne("FlySpawner")), 12, "A bigger pond gets more flies");

            levels.GetType().GetMethod("GoToLevel").Invoke(levels, new object[] { 3 });
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(47f, half(), 0.2f, "Night opens up to 94 x 94 m");
            yield return Screenshot("17-big-night-pond");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T21_MiniMapShowsWhatsAroundYou()
        {
            yield return LoadLevel();
            yield return StartGame();
            yield return new WaitForSeconds(1f);
            Component map = FindOne("MiniMap");
            Assert.IsNotNull(map, "There should be a mini map");
            int dots = (int)GameType("MiniMap").GetProperty("VisibleDots").GetValue(null);
            int flies = (int)GameType("FlySpawner").GetField("totalFlies").GetValue(null);
            Assert.GreaterOrEqual(dots, flies, "Every fly (plus you and the bird) should be a dot");
            yield return Screenshot("18-mini-map");

            // Flies off: fewer dots. Map off: none.
            SetStaticProp("GameSettings", "MapFlies", false);
            yield return null; yield return null;
            Assert.Less((int)GameType("MiniMap").GetProperty("VisibleDots").GetValue(null), dots, "Hiding flies should hide their dots");
            SetStaticProp("GameSettings", "MiniMap", false);
            yield return null; yield return null;
            Assert.AreEqual(0, (int)GameType("MiniMap").GetProperty("VisibleDots").GetValue(null), "The map can be turned off");
            SetStaticProp("GameSettings", "MiniMap", true);
            SetStaticProp("GameSettings", "MapFlies", true);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator T22_HelperBuddyChasesTheBirdAway()
        {
            PlayerPrefs.SetInt("FNL.helperOn", 1);
            yield return LoadLevelKeepingHelper();
            yield return StartGame();
            yield return new WaitForSeconds(0.5f);
            Component helper = FindOne("HelperBuddy");
            Assert.IsNotNull(helper, "The helper buddy should come along");
            Assert.IsNotNull(helper.GetComponentInChildren<Animation>(), "The buddy should be animated");

            // Put the bird close to the frog: the buddy should dash at it and leave it dizzy.
            GameObject frog = GameObject.FindWithTag("Player");
            Component bird = FindOne("BirdMovement");
            var agent = bird.GetComponent<UnityEngine.AI.NavMeshAgent>();
            // Start it outside the guard zone so it flies in, like in a real round.
            agent.Warp(frog.transform.position + new Vector3(9f, 0f, 0f));
            float until = Time.time + 8f;
            while (Time.time < until && !(bool)GameType("BirdMovement").GetProperty("IsStunned").GetValue(null))
                yield return null;
            Assert.IsTrue((bool)GameType("BirdMovement").GetProperty("IsStunned").GetValue(null), "The buddy should chase the bird off");
            Assert.AreEqual(1, (int)GameType("HelperBuddy").GetProperty("ChasesThisRound").GetValue(null));
            Assert.IsTrue(frog.activeInHierarchy, "The frog should still be safe");
            yield return Screenshot("19-helper-buddy");

            // Turning it off sends the buddy home.
            SetStaticProp("GameSettings", "HelperOn", false);
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType(GameType("HelperBuddy"), FindObjectsSortMode.None).Length, "Helper off = no buddy");
            PlayerPrefs.SetInt("FNL.helperOn", 0);
        }

        private static IEnumerator LoadLevelKeepingHelper()
        {
            PlayerPrefs.SetInt("FNL.cheats", 0);
            PlayerPrefs.SetString("FNL.daily.done", DateTime.Now.ToString("yyyyMMdd"));
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("_Scenes");
#endif
            for (int i = 0; i < 10; i++) yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator T23_CheatsUnlockEverythingAndSuperFrogCantBeCaught()
        {
            yield return LoadLevel();
            Type cheats = GameType("Cheats");
            Type shop = GameType("Shop");
            int walletBefore = (int)shop.GetProperty("Wallet").GetValue(null);
            cheats.GetProperty("Enabled").SetValue(null, true);
            cheats.GetMethod("UnlockEverything").Invoke(null, null);
            cheats.GetMethod("AddFlies").Invoke(null, new object[] { 1000 });
            Assert.AreEqual(walletBefore + 1000, (int)shop.GetProperty("Wallet").GetValue(null), "+1000 flies");
            Assert.IsTrue((bool)shop.GetMethod("Owns").Invoke(null, new object[] { "laser" }), "Unlock everything gives the laser");
            Assert.IsTrue((bool)shop.GetMethod("Owns").Invoke(null, new object[] { "helper.wolf" }), "...and every helper");
            cheats.GetMethod("UnlockAllAchievements").Invoke(null, null);
            Type ach = GameType("Achievements");
            Assert.AreEqual(((string[][])ach.GetField("All").GetValue(null)).Length, (int)ach.GetProperty("Count").GetValue(null), "All achievements");

            cheats.GetProperty("SuperFrog").SetValue(null, true);
            yield return StartGame();
            yield return null;
            Assert.GreaterOrEqual((float)GetStatic("PlayerMovement", "SpeedMultiplier"), 1.6f, "Super frog is faster");

            // Let the bird reach the frog: it should bounce off.
            GameObject frog = GameObject.FindWithTag("Player");
            Component bird = FindOne("BirdMovement");
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(frog.transform.position + new Vector3(1.2f, 0f, 0f));
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(frog.activeInHierarchy, "Super frog can't be caught");

            // (Super frog's giant magnet may already have eaten its way to the next level.)
            object levels = GetStaticProp("LevelManager", "Instance");
            int levelBefore = (int)levels.GetType().GetProperty("Level").GetValue(levels);
            Assert.IsTrue((bool)cheats.GetMethod("SkipLevel").Invoke(null, null), "Skip level");
            Assert.AreEqual(levelBefore + 1, (int)levels.GetType().GetProperty("Level").GetValue(levels), "Skip level goes up one");
            Assert.IsTrue((bool)cheats.GetProperty("AffectsScore").GetValue(null), "Best scores pause while Super frog is on");

            cheats.GetProperty("Enabled").SetValue(null, false);
            Assert.IsFalse((bool)cheats.GetProperty("SuperFrog").GetValue(null), "Turning cheats off turns Super frog off");
            // Put the shop back how the other tests expect it.
            foreach (object item in (Array)shop.GetField("Items").GetValue(null))
                PlayerPrefs.DeleteKey("FNL.shop." + item.GetType().GetField("id").GetValue(item));
            PlayerPrefs.DeleteKey("FNL.wallet");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T24_QuickFliesMakeACombo()
        {
            yield return LoadLevel();
            yield return StartGame();
            yield return null;
            // Three flies in quick succession: x3 combo = 1 bonus fly.
            int start = (int)GetStatic("ScoreCounter", "score");
            for (int i = 1; i <= 3; i++)
            {
                SetStaticField("ScoreCounter", "score", (int)GetStatic("ScoreCounter", "score") + 1);
                yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            Assert.AreEqual(start + 4, (int)GetStatic("ScoreCounter", "score"), "x3 combo should add a bonus fly");
            Assert.IsNotNull(GameType("DailyChallenge").GetProperty("Goal").GetValue(null), "There's a daily challenge");
        }

        // ---------- Nov 2026 round 5: three new levels, tongue, dodge, stars, bees, hiding, rain ----------

        private static void KeepFrogSafe(float seconds)
        {
            SetStaticField("FrogEffects", "InvulnerableUntil", Time.time + seconds);
            Behaviour bird = (Behaviour)FindOne("BirdMovement");
            bird.enabled = false;
            bird.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator T25_NewLevelsTransformThePond()
        {
            yield return LoadLevel();
            yield return StartGame();
            KeepFrogSafe(120f);
            object levels = GetStaticProp("LevelManager", "Instance");
            MethodInfo go = levels.GetType().GetMethod("GoToLevel");
            Type biome = GameType("Biome");
            Terrain terrain = Terrain.activeTerrain;

            go.Invoke(levels, new object[] { 4 });
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(4, (int)biome.GetProperty("Current").GetValue(null), "Lily Pad Lake's world should be in");
            Assert.Greater(Object.FindObjectsByType(GameType("LilyPad"), FindObjectsSortMode.None).Length, 5, "There should be lily pads");
            if (terrain != null) StringAssert.Contains("Ground", terrain.materialTemplate.name, "The lake has its own high-res ground");
            yield return Screenshot("21-lily-pad-lake");

            go.Invoke(levels, new object[] { 5 });
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(5, (int)biome.GetProperty("Current").GetValue(null), "Autumn Woods' world should be in");
            Assert.Greater(Object.FindObjectsByType(GameType("LeafPile"), FindObjectsSortMode.None).Length, 5, "There should be leaf piles");
            Assert.Greater(Object.FindObjectsByType(GameType("BouncePad"), FindObjectsSortMode.None).Length, 2, "There should be giant mushrooms");
            Assert.Less(Object.FindObjectsByType(GameType("LilyPad"), FindObjectsSortMode.None).Length, 1, "The lily pads should be gone");
            float until = Time.time + 3f;
            while (Time.time < until && GetStaticProp("GroundHunter", "Active") == null) yield return null;
            Assert.IsNotNull(GetStaticProp("GroundHunter", "Active"), "A fox should be hunting in the woods");
            yield return Screenshot("22-autumn-woods");

            go.Invoke(levels, new object[] { 6 });
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(6, (int)biome.GetProperty("Current").GetValue(null), "The Winter Freeze should be in");
            Assert.Greater(Object.FindObjectsByType(GameType("ThinIce"), FindObjectsSortMode.None).Length, 3, "There should be thin ice");
            Assert.IsNotNull(GameObject.Find("Snowfall"), "It should be snowing");
            Assert.IsNull(GetStaticProp("GroundHunter", "Active"), "The fox stays in the woods");
            yield return Screenshot("23-winter-freeze");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T26_TongueFlickCatchesAFlyFromAfar()
        {
            yield return LoadLevel();
            yield return StartGame();
            KeepFrogSafe(60f);
            yield return null;
            GameObject frog = GameObject.FindWithTag("Player");
            // Move one fly 3 m away (out of reach of the frog's body, inside the tongue's reach).
            var flies = (System.Collections.IList)GameType("FlySpawner").GetField("ActiveFlies").GetValue(null);
            Assert.Greater(flies.Count, 0, "There should be flies");
            var fly = (GameObject)flies[0];
            fly.transform.position = frog.transform.position + new Vector3(3f, 1.2f, 0f);
            yield return null;
            int before = (int)GetStatic("ScoreCounter", "score");
            object tongue = GetStaticProp("FrogTongue", "Instance");
            Assert.IsNotNull(tongue, "The frog should have a tongue");
            Assert.IsTrue((bool)tongue.GetType().GetMethod("Flick").Invoke(tongue, null), "A fly 3 m away should be in reach");
            yield return new WaitForSeconds(0.3f);
            Assert.Greater((int)GetStatic("ScoreCounter", "score"), before, "The tongue should catch the fly");
            Assert.IsNotNull(GameObject.Find("Frog Tongue"), "...and you should see the tongue");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T27_DodgeHopLeapsOutOfTheWay()
        {
            yield return LoadLevel();
            yield return StartGame();
            KeepFrogSafe(0f);
            SetStaticField("FrogEffects", "InvulnerableUntil", 0f);
            yield return new WaitForSeconds(0.5f);
            GameObject frog = GameObject.FindWithTag("Player");
            Vector3 start = frog.transform.position;
            Component move = FindOne("PlayerMovement");
            Assert.IsTrue((bool)move.GetType().GetMethod("Dodge").Invoke(move, null), "Dodge hop should work");
            Assert.Greater((float)GetStatic("FrogEffects", "InvulnerableUntil"), Time.time, "You can't be caught mid-leap");
            yield return new WaitForSeconds(0.35f);
            float moved = Vector3.Distance(Flat(frog.transform.position), Flat(start));
            Assert.Greater(moved, 2.5f, "A dodge hop should leap a few metres (moved " + moved.ToString("0.0") + ")");
            Assert.IsFalse((bool)move.GetType().GetMethod("Dodge").Invoke(move, null), "...and then need to recharge");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T28_LevelSelectStartsAtAReachedLevelAndStarsAreSaved()
        {
            PlayerPrefs.SetInt("FNL.reached.Medium", 4);
            PlayerPrefs.SetInt("FNL.start.Medium", 4);
            yield return LoadLevel();
            PlayerPrefs.SetInt("FNL.reached.Medium", 4);
            PlayerPrefs.SetInt("FNL.start.Medium", 4);
            StringAssert.Contains("Lake", AllVisibleText(), "The title screen should offer the levels you've reached");
            yield return StartGame();
            KeepFrogSafe(30f);
            yield return new WaitForSeconds(0.5f);
            object levels = GetStaticProp("LevelManager", "Instance");
            Assert.AreEqual(4, (int)levels.GetType().GetProperty("Level").GetValue(levels), "The run should start at Lily Pad Lake");

            Type progress = GameType("LevelProgress");
            int stars = (int)progress.GetMethod("Complete").Invoke(null, new object[] { 2, 10f, 10 });
            Assert.AreEqual(2, stars, "A quick finish (under par) earns 2 stars");
            Assert.AreEqual(2, (int)progress.GetMethod("Stars").Invoke(null, new object[] { 2 }), "Stars are saved");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T29_DragonfliesAreWorthMoreAndBeesSting()
        {
            yield return LoadLevel();
            yield return StartGame();
            KeepFrogSafe(60f);
            yield return null;
            Collider frogCollider = GameObject.FindWithTag("Player").GetComponentInChildren<Collider>();
            Type kindType = GameType("FlyKind");
            Type kinds = kindType.GetNestedType("Type");

            Component fly = FindOne("FlyPickUp");
            Component kind = fly.GetComponentInParent(kindType);
            Assert.IsNotNull(kind, "Every fly should have a kind");
            kindType.GetMethod("Set").Invoke(kind, new[] { System.Enum.Parse(kinds, "Dragonfly") });
            // (Check before eating: an eaten fly is reused straight away as a new fly.)
            Assert.AreEqual(3, (int)kindType.GetProperty("Points").GetValue(kind), "A dragonfly is worth 3");
            int before = (int)GetStatic("ScoreCounter", "score");
            fly.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            // (A real fly can drift into the frog at the same moment, so allow for one extra.)
            Assert.GreaterOrEqual((int)GetStatic("ScoreCounter", "score"), before + 3, "Eating a dragonfly adds 3");

            SetStaticField("ScoreCounter", "score", 5);
            yield return null;
            int beforeBee = (int)GetStatic("ScoreCounter", "score");
            Component bee = FindOne("FlyPickUp");
            kindType.GetMethod("Set").Invoke(bee.GetComponentInParent(kindType), new[] { System.Enum.Parse(kinds, "Bee") });
            bee.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            Assert.Less((int)GetStatic("ScoreCounter", "score"), beforeBee, "A bee sting costs flies");
            Assert.Greater((float)GetStatic("FlyKind", "StungUntil"), Time.time, "...and slows you down");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator T30_HidingInTheReedsAndRainstorms()
        {
            yield return LoadLevel();
            yield return StartGame();
            KeepFrogSafe(60f);
            yield return null;
            // The reeds nearest the middle (inside the Pond's walls).
            Component spot = null;
            foreach (Component c in Object.FindObjectsByType(GameType("HidingSpot"), FindObjectsSortMode.None).Cast<Component>())
                if (spot == null || Flat(c.transform.position).magnitude < Flat(spot.transform.position).magnitude) spot = c;
            Assert.IsNotNull(spot, "There should be reeds to hide in");
            GameObject frog = GameObject.FindWithTag("Player");
            Vector3 p = spot.transform.position;
            frog.GetComponent<Rigidbody>().position = new Vector3(p.x, frog.transform.position.y, p.z);
            frog.transform.position = new Vector3(p.x, frog.transform.position.y, p.z);
            float until = Time.time + 2f;
            while (Time.time < until && !(bool)GetStaticProp("HidingSpot", "FrogHidden")) yield return null;
            Assert.IsTrue((bool)GetStaticProp("HidingSpot", "FrogHidden"), "Keeping still in the reeds should hide you");

            object weather = GetStaticProp("Weather", "Instance");
            weather.GetType().GetMethod("StartStorm").Invoke(weather, new object[] { 5f });
            yield return null;
            Assert.IsTrue((bool)GetStaticProp("Weather", "Raining"), "A storm should start");
            Assert.Greater((float)GetStaticProp("Weather", "FlyFactor"), 1f, "Rain brings more flies");
            Assert.Greater((float)GetStaticProp("Weather", "BirdFactor"), 1f, "...and a restless bird");
        }
    }
}
