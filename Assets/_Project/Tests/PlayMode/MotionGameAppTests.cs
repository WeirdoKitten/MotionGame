using System.Collections;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace FamilyAdventure.PlayTests
{
    public sealed class MotionGameAppTests
    {
        // Opening the MotionGame scene shows the mission menu with a working event system, and no game
        // (so no camera) starts until a mission is chosen.
        [UnityTest] public IEnumerator SceneOpensMissionMenu()
        {
            yield return SceneManager.LoadSceneAsync(MotionGameApp.SceneName);
            yield return null;
            var menu = Object.FindFirstObjectByType<MotionMissionMenu>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);
            Assert.That(menu.Current, Is.Null);
            Assert.That(Object.FindFirstObjectByType<MotionSensor>(), Is.Null);
            Object.Destroy(menu.gameObject);
            yield return null;
        }
    }
}
