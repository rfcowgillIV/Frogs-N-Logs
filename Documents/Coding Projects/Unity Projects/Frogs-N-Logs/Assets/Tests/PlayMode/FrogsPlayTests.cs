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
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previous;

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
            Assert.IsTrue(fly == null, "Eaten fly should be removed");
            StringAssert.Contains("1 Flies", ScoreText());
            Assert.GreaterOrEqual(FindAll("FlyPickUp").Length, 12, "A new fly should spawn to replace it");

            yield return Screenshot("03-ate-a-fly");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator T4_BirdCatchesFrogThenLevelRestarts()
        {
            yield return LoadLevel();
            yield return StartGame();

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
            Assert.AreEqual(0, (int)GetStatic("ScoreCounter", "score"), "Score should reset after restart");
            Assert.IsTrue((bool)GetField(FindOne("PlayerHealth"), "alive"), "New frog should be alive after restart");
            Assert.AreNotEqual("Game Over!", GameStateText(), "Game Over text should clear after restart");

            Assert.IsTrue(caughtNaturally, "Restart works, but the bird never caught the frog on its own within 30s");
        }
    }
}
