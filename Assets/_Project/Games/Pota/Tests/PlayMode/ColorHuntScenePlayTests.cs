using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The Color Hunt scene as saved: Pota's camera, post, lights and surroundings around the board.</summary>
    public class ColorHuntScenePlayTests
    {
        [UnityTest]
        public IEnumerator Scene_IsPotasStage()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            var cam = Camera.main;
            Assert.AreEqual(38f, cam.fieldOfView, 1e-4f, "yapbozSahne.ts GORUS_ACISI");
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing, "post on: Pota's tonemap, bloom, vignette");
            Assert.AreSame(game.S.PuzzleProfile, Object.FindAnyObjectByType<Volume>().sharedProfile);
            Assert.AreEqual(PotaLook.Sky.r, cam.backgroundColor.r, 1e-3f);
            Assert.AreEqual(PotaLook.Sky.b, cam.backgroundColor.b, 1e-3f);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(30f, RenderSettings.fogStartDistance, 1e-4f);
            Assert.AreEqual(1.2f, game.BoardRoot.localScale.x, 1e-5f, "the grid fills the 8.8 backboard");
            var stage = Object.FindAnyObjectByType<PuzzleStage>();
            Assert.IsNotNull(stage.Hoop);
            Assert.Less(Vector3.Distance(stage.Hoop.RimCenter, game.transform.position + new Vector3(0f, -5.6f, -2.2f)), 1e-3f);
            Assert.IsNotNull(stage.Reflection, "baked room reflection over the board");
            float boardZ = game.transform.position.z;
            Assert.AreEqual(boardZ - 3f, stage.Court.GetComponent<Renderer>().bounds.max.z, 0.01f, "saha.ts: the court ends 3 m before the hoop");
            Assert.IsNotNull(stage.Roof, "the building under the court (saha.ts govde)");
            Assert.AreEqual(boardZ - 2f, stage.Roof.GetComponent<Renderer>().bounds.max.z, 0.01f, "the roof's edge 2 m before the hoop");
            Assert.AreEqual(game.transform.position.y - 8.5f, stage.Roof.GetComponent<Renderer>().bounds.max.y, 0.01f);
            Assert.IsTrue(stage.Reflection.bounds.Contains(game.CellWorld(0)));
            var fit = cam.GetComponent<ColorHuntCamera>();
            Assert.Less(Vector3.Distance(cam.transform.position, fit.FitFor(cam.fieldOfView, cam.aspect)), 0.6f, "shake aside");
        }
    }
}
