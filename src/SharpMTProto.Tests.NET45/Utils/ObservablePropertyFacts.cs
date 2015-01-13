//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Utils
{
    using System;
    using System.Collections.Concurrent;
    using FluentAssertions;
    using NUnit.Framework;
    using Ploeh.AutoFixture;
    using SharpMTProto.Utils;

    [TestFixture]
    public class ObservablePropertyFacts
    {
        private Fixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture();
        }

        [Test]
        public void Should_return_initial_value()
        {
            var sender = _fixture.Create<object>();
            var initialValue = _fixture.Create<int>();

            var observableProperty = new ObservableProperty<object, int>(sender, initialValue);

            observableProperty.Value.Should().Be(initialValue);
        }

        [Test]
        public void Should_receive_property_change()
        {
            var changes = new ConcurrentQueue<PropertyChange<object, int>>();
            var sender = _fixture.Create<object>();
            var initialValue = _fixture.Create<int>();
            var newValue = _fixture.Create<int>();

            var observableProperty = new ObservableProperty<object, int>(sender, initialValue);

            observableProperty.Subscribe(changes.Enqueue);

            changes.Should().HaveCount(0);

            observableProperty.Value = newValue;
            observableProperty.Value.Should().Be(newValue);

            changes.Should().HaveCount(1);

            PropertyChange<object, int> change;
            changes.TryDequeue(out change).Should().BeTrue();
            change.Sender.Should().Be(sender);
            change.NewValue.Should().Be(newValue);
            change.OldValue.Should().Be(initialValue);
        }

        [Test]
        public void Should_unsubscribe()
        {
            var changes = new ConcurrentQueue<PropertyChange<object, int>>();
            var sender = _fixture.Create<object>();

            var observableProperty = new ObservableProperty<object, int>(sender);

            IDisposable subscription = observableProperty.Subscribe(changes.Enqueue);
            subscription.Dispose();

            changes.Should().HaveCount(0);

            observableProperty.Value = _fixture.Create<int>();

            changes.Should().HaveCount(0);
        }
    }
}
