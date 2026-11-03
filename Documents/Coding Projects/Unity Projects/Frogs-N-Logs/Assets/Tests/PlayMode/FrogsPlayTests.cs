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
                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                cam.targetTexture = previous;
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
            fly.SendMessage("OnTriggerEnter", frogCollider);
            yield return null;
            yield return null;

            Assert.AreEqual(1, (int)GetStatic("ScoreCounter", "score"), "Score should be exactly 1 after one fly");
            // Nov 2026: eaten flies are hidden and reused later instead of destroyed.
            Assert.IsTrue(fly == null || !fly.gameObject.activeInHierarchy, "Eaten fly should be removed");
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
            Behaviour filter = (Behaviour)Camera.main.GetComponent(GameType("ScreenFilter"));
            Assert.IsNotNull(filter, "Filter should be attached to the camera");
            Assert.IsTrue(filter.enabled, "Night filter should be on");
            Assert.AreEqual(Enum("GraphicsTier", "Low"), GetStaticProp("GraphicsManager", "ActiveTier"));
            yield return Screenshot("07-night-filter-low");

            // Changing a key binding sticks (and arrows still work as a backup).
            GameType("KeyBindings").GetMethod("Set").Invoke(null, new object[] { "up", KeyCode.I });
            Assert.AreEqual(KeyCode.I, GameType("KeyBindings").GetMethod("Get").Invoke(null, new object[] { "up" }));
        }
    }
}
