/*
 * Copyright (c) Alexander Logger. All rights reserved.
 */

namespace SharpMTProto.Tests.SetUp
{
    using System;
    using Autofac;
    using NUnit.Framework;

    public abstract class TestFixtureBase
    {
        private IContainer _mainContainer;
        private TestLifetime _testLifetime;

        [SetUp]
        public void BaseSetUp()
        {
            var builder = new ContainerBuilder();

            ConfigureBuilder(builder);

            _mainContainer = builder.Build();

            _testLifetime = new TestLifetime(_mainContainer);
            _testLifetime.SetUp();
        }

        protected abstract void ConfigureBuilder(ContainerBuilder builder);

        [TearDown]
        public void BaseTearDown()
        {
            _testLifetime.TearDown();
        }

        protected TService Resolve<TService>()
        {
            return _testLifetime.Resolve<TService>();
        }

        protected void Override(Action<ContainerBuilder> configurationAction)
        {
            _testLifetime.Override(configurationAction);
        }
    }
}
