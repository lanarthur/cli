// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigratePackageDependencies : TestBase
    {
        [Fact]
        public void It_migrates_basic_PackageReference()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""dependencies"": {
                        ""APackage"" : ""1.0.0-preview"",
                        ""BPackage"" : ""1.0.0""
                    }
                }");
            
            EmitsPackageReferences(mockProj, Tuple.Create("APackage", "1.0.0-preview", ""), Tuple.Create("BPackage", "1.0.0", ""));            
        }

        [Fact]
        public void It_migrates_type_build_to_PrivateAssets()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""dependencies"": {
                        ""APackage"" : {
                            ""version"": ""1.0.0-preview"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "APackage" && i.ItemType == "PackageReference");

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().Be("All");
        }

        [Fact]
        public void It_migrates_suppress_parent_to_PrivateAssets()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""dependencies"": {
                        ""APackage"" : {
                            ""version"": ""1.0.0-preview"",
                            ""suppressParent"":[ ""runtime"", ""native"" ]
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "APackage" && i.ItemType == "PackageReference");

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().Be("runtime;native");
        }

        [Fact]
        public void It_migrates_include_exclude_to_IncludeAssets()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""dependencies"": {
                        ""APackage"" : {
                            ""version"": ""1.0.0-preview"",
                            ""include"": [ ""compile"", ""runtime"", ""native"" ],
                            ""exclude"": [ ""native"" ]
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "APackage" && i.ItemType == "PackageReference");

            var privateAssetsMetadata = packageRef.GetMetadataWithName("IncludeAssets");
            privateAssetsMetadata.Value.Should().Be("compile;runtime");
        }

        [Fact]
        public void It_migrates_Tools()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"                
                {
                    ""tools"": {
                        ""APackage"" : ""1.0.0-preview"",
                        ""BPackage"" : ""1.0.0""
                    }
                }");
            
            EmitsToolReferences(mockProj, Tuple.Create("APackage", "1.0.0-preview"), Tuple.Create("BPackage", "1.0.0"));            
        }

        private void EmitsPackageReferences(ProjectRootElement mockProj, params Tuple<string, string, string>[] packageSpecs)
        {
            foreach (var packageSpec in packageSpecs)
            {
                var packageName = packageSpec.Item1;
                var packageVersion = packageSpec.Item2;
                var packageTFM = packageSpec.Item3;

                var items = mockProj.Items
                    .Where(i => i.ItemType == "PackageReference")
                    .Where(i => string.IsNullOrEmpty(packageTFM) || i.ConditionChain().Any(c => c.Contains(packageTFM)))
                    .Where(i => i.Include == packageName)
                    .Where(i => i.GetMetadataWithName("Version").Value == packageVersion);

                items.Should().HaveCount(1);
            }
        }

        private void EmitsToolReferences(ProjectRootElement mockProj, params Tuple<string, string>[] toolSpecs)
        {
            foreach (var toolSpec in toolSpecs)
            {
                var packageName = toolSpec.Item1;
                var packageVersion = toolSpec.Item2;

                var items = mockProj.Items
                    .Where(i => i.ItemType == "DotNetCliToolReference")
                    .Where(i => i.Include == packageName)
                    .Where(i => i.GetMetadataWithName("Version").Value == packageVersion);

                items.Should().HaveCount(1);
            }
        }

        private ProjectRootElement RunPackageDependenciesRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigratePackageDependenciesAndToolsRule()
            }, s, testDirectory);
        }
    }
}