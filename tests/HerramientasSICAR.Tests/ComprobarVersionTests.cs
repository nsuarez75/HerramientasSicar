using HerramientasSICAR.Logica;
using System;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class ComprobarVersionTests
    {
        [Theory]
        [InlineData("v1.2.0", "1.2.0")]
        [InlineData("1.2", "1.2.0")]
        [InlineData("V2.0.1-beta", "2.0.1")]
        public void Lee_la_version_del_tag(string tag, string esperada)
        {
            Assert.Equal(Version.Parse(esperada), ComprobarVersion.LeerVersion(tag));
        }

        [Theory]
        [InlineData("")]
        [InlineData("version-final")]
        [InlineData("1.2.3.4.5")]
        public void Un_tag_sin_version_no_vale(string tag)
        {
            Assert.Null(ComprobarVersion.LeerVersion(tag));
        }

        [Fact]
        public void Lee_la_respuesta_de_GitHub()
        {
            var release = ComprobarVersion.Leer("{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://example.com/r\",\"draft\":false}");

            Assert.Equal("v1.3.0", release.Tag);
            Assert.Equal(new Version(1, 3, 0), release.Version);
            Assert.Equal("https://example.com/r", release.Url);
        }

        [Fact]
        public void Una_respuesta_que_no_se_entiende_no_avisa()
        {
            Assert.Null(ComprobarVersion.Leer("no es json"));
            Assert.Null(ComprobarVersion.Leer("{\"message\":\"Not Found\"}"));
        }

        [Theory]
        [InlineData("v1.2.0", "1.1.0.0", true)]
        [InlineData("v1.1.0", "1.1.0.0", false)]
        [InlineData("v1.0.9", "1.1.0.0", false)]
        public void Solo_avisa_si_la_release_es_mas_nueva(string tag, string actual, bool esperado)
        {
            var release = ComprobarVersion.Leer("{\"tag_name\":\"" + tag + "\"}");
            Assert.Equal(esperado, ComprobarVersion.EsMasNueva(release, Version.Parse(actual)));
        }
    }
}
