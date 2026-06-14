using System;
using System.Collections.Generic;
using nadena.dev.ndmf.localization;

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// NDMF のエラー報告（<see cref="nadena.dev.ndmf.ErrorReport"/>）で使用するローカライザ。
    /// エディタ言語に応じて en-us / ja-jp のメッセージを出し分ける。
    /// </summary>
    internal static class SpoLocalizer
    {
        public const string NotApplied = "spo:warn.notApplied";

        private static readonly Dictionary<string, string> EnUs = new Dictionary<string, string>
        {
            [NotApplied] =
                "Shader property '{0}' (type {1}) was not applied to any material. " +
                "The name may be misspelled, or no material has a property of that type.",
        };

        private static readonly Dictionary<string, string> JaJp = new Dictionary<string, string>
        {
            [NotApplied] =
                "シェーダープロパティ '{0}' (型 {1}) はどのマテリアルにも適用されませんでした。" +
                "名前の誤りか、その型のプロパティを持つマテリアルが存在しない可能性があります。",
        };

        public static readonly Localizer Instance = new Localizer(
            "en-us",
            () => new List<(string, Func<string, string>)>
            {
                ("en-us", key => EnUs.TryGetValue(key, out var v) ? v : key),
                ("ja-jp", key => JaJp.TryGetValue(key, out var v) ? v : key),
            });
    }
}
