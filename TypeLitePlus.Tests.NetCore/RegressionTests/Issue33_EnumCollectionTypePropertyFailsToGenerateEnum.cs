﻿using System.Collections.Generic;
using System.Linq;
using TypeLite;
using Xunit;

namespace TypeLitePlus.Tests.NetCore.RegressionTests
{
    public class Issue33_EnumCollectionTypePropertyFailsToGenerateEnum
    {
        [Fact]
        public void WhenEnumTypeAppearsOnlyInCollectionTypeProperty_EnumIsGenerated()
        {
            var builder = new TsModelBuilder();
            builder.Add<AuthenticationResult>();

            var generator = new TsGenerator();
            var model = builder.Build();
            var result = generator.Generate(model);

            Assert.True(model.Enums.Count == 1);
            Assert.True(model.Enums.Single().Type == typeof(AuthenticationError));
        }

        [TsClass]
        public class AuthenticationResult
        {
            public IList<AuthenticationError> Errors { get; set; }
        }

        public enum AuthenticationError
        {
            One,
            Two
        }
    }
}
