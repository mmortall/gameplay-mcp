// Copyright (c) 2026 Koji Hasegawa.
// This software is released under the MIT License.

using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace GameplayMcp.Internals
{
    [TestFixture]
    public class GameObjectSerializerTest
    {
        [Test]
        [CreateScene]
        public void Serialize_WithName_ReturnsJsonContainingName()
        {
            var sut = new GameObject("TestObject");

            var actual = GameObjectSerializer.Serialize(sut);

            Assert.That(actual, Does.Contain("\"name\":\"TestObject\"").Or.Contain("\"name\": \"TestObject\""));
        }

        [Test]
        [CreateScene]
        public void Serialize_WithParent_ReturnsJsonContainingPath()
        {
            var parent = new GameObject("Parent");
            var sut = new GameObject("Child");
            sut.transform.SetParent(parent.transform);

            var actual = GameObjectSerializer.Serialize(sut);

            Assert.That(actual, Does.Contain("\"path\":\"/Parent/Child\"").Or.Contain("\"path\": \"/Parent/Child\""));
        }

        [Test]
        [CreateScene]
        public void Serialize_WithMultipleComponents_ReturnsAllComponentTypes()
        {
            var sut = new GameObject("MultiComp");
            sut.AddComponent<BoxCollider>();
            sut.AddComponent<Rigidbody>();

            var actual = GameObjectSerializer.Serialize(sut);

            Assert.That(actual, Does.Contain("BoxCollider").And.Contain("Rigidbody"));
        }

        [Test]
        [CreateScene]
        public void Serialize_PublicProperty_ReturnsPropertyValueAtComponentLevel()
        {
            var sut = new GameObject("WithCollider");
            var collider = sut.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            // Reading the getter here (assignment above only calls the setter) is a direct static
            // reference that keeps UnityLinker from stripping it under IL2CPP + Managed Stripping High
            // on a standalone player; GameObjectSerializer reaches it only via reflection, which the
            // linker cannot see through, so without this read the getter (and the JSON field) vanishes.
            Assume.That(collider.isTrigger, Is.True);

            var actual = GameObjectSerializer.Serialize(sut);

            Assert.That(actual, Does.Contain("isTrigger").And.Contain("True"));
        }

        [Test]
        [CreateScene]
        public void Serialize_PublicField_ReturnsFieldValueAtComponentLevel()
        {
            var sut = new GameObject("WithLight");
            var light = sut.AddComponent<Light>();
            light.intensity = 3.14f;

            var actual = GameObjectSerializer.Serialize(sut);

            // intensity is a public property of Light; verify it appears in the JSON
            Assert.That(actual, Does.Contain("intensity"));
        }
    }
}
