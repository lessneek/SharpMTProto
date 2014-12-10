/*
 * Copyright (c) Alexander Logger. All rights reserved.
 */

namespace SharpMTProto.Tests.SetUp
{
    using System;
    using Autofac;

    public class TestLifetime
    {
        private bool _canOverride;
        private ILifetimeScope _testScope;
        private readonly IContainer _mainContainer;

        public TestLifetime(IContainer mainContainer)
        {
            _mainContainer = mainContainer;
        }

        public void SetUp()
        {
            _testScope = _mainContainer.BeginLifetimeScope();
            _canOverride = true;
        }

        public void TearDown()
        {
            _testScope.Dispose();
            _testScope = null;
        }

        public TService Resolve<TService>()
        {
            _canOverride = false;
            return _testScope.Resolve<TService>();
        }

        public void Override(Action<ContainerBuilder> configurationAction)
        {
            _testScope.Dispose();

            if (!_canOverride)
            {
                throw new InvalidOperationException("Override can only be called once per test and must be before any calls to Resolve.");
            }

            _canOverride = false;
            _testScope = _mainContainer.BeginLifetimeScope(configurationAction);
        }
    }
}
