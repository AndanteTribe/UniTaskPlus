#nullable enable

using NUnit.Framework;

namespace UniTaskPlus.Tests.Editor
{
    public class UniTaskSemaphoreEditorTests
    {
        [Test]
        public void ReleaseAfterDisposeOutsidePlayModeUsesForceReleasePath()
        {
            Assert.That(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode, Is.False);

            var sem = new UniTaskSemaphore(0, 2);
            sem.Dispose();

            var previousCount = sem.Release();

            Assert.That(previousCount, Is.EqualTo(0u));
            Assert.That(sem.CurrentCount, Is.EqualTo(1u));
        }
    }
}
