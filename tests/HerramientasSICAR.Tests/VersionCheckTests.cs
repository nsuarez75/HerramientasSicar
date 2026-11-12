using HerramientasSICAR.Logic;
using System;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class VersionCheckTests
    {
        [Theory]
        [InlineData("v1.2.0", "1.2.0")]
        [InlineData("1.2", "1.2.0")]
        [InlineData("V2.0.1-beta", "2.0.1")]
        public void ParseVersion_Tag_ReturnsVersion(string tag, string expected)
        {
            Assert.Equal(Version.Parse(expected), VersionCheck.ParseVersion(tag));
        }

        [Theory]
        [InlineData("")]
        [InlineData("version-final")]
        [InlineData("1.2.3.4.5")]
        public void ParseVersion_NoVersion_ReturnsNull(string tag)
        {
            Assert.Null(VersionCheck.ParseVersion(tag));
        }

        [Fact]
        public void Read_GitHubResponse_ReturnsRelease()
        {
            var release = VersionCheck.Read("{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://example.com/r\",\"draft\":false}");

            Assert.Equal("v1.3.0", release.Tag);
            Assert.Equal(new Version(1, 3, 0), release.Version);
            Assert.Equal("https://example.com/r", release.Url);
        }

        [Fact]
        public void Read_InvalidResponse_ReturnsNull()
        {
            Assert.Null(VersionCheck.Read("no es json"));
            Assert.Null(VersionCheck.Read("{\"message\":\"Not Found\"}"));
        }

        [Theory]
        [InlineData("v1.2.0", "1.1.0.0", true)]
        [InlineData("v1.1.0", "1.1.0.0", false)]
        [InlineData("v1.0.9", "1.1.0.0", false)]
        public void IsNewer_ReleaseAndAppVersion_Compares(string tag, string current, bool expected)
        {
            var release = VersionCheck.Read("{\"tag_name\":\"" + tag + "\"}");
            Assert.Equal(expected, VersionCheck.IsNewer(release, Version.Parse(current)));
        }
    }
}
