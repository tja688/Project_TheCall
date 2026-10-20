using NUnit.Framework;
using UnityEditor;

namespace TheCall
{
    public sealed class UiSfxCatalogTests
    {
        [Test]
        public void 界面音效目录绑定本批六条资源()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/Resources/GameAudioCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.uiClicks, Has.Length.EqualTo(3));
            Assert.That(catalog.uiClicks[0].name, Is.EqualTo("S_UI_通用UI01"));
            Assert.That(catalog.uiClicks[1].name, Is.EqualTo("S_UI_通用UI02"));
            Assert.That(catalog.uiClicks[2].name, Is.EqualTo("S_UI_通用UI03"));
            Assert.That(catalog.purchase.name, Is.EqualTo("S_UI_成功购买"));
            Assert.That(catalog.error.name, Is.EqualTo("S_UI_错误提示"));
            Assert.That(catalog.clockOut.name, Is.EqualTo("S_UI_下班"));
        }
    }
}
